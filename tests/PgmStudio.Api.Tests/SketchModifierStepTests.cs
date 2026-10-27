using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PgmStudio.Contracts;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The modifier stack read a step at a time: <c>?step=N</c> on the contour and columns reads, and the per-modifier
/// count of columns moved. The three agree with each other because they read one stack.
/// </summary>
[NotInParallel("api-db")]
public sealed class SketchModifierStepTests
{
    private const string Ground = """
        "setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
        "layers":[{"base_y":0,"layout":{
          "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                     "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
          "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]
        """;

    private const string Hill = """
        {"id":"hill","op":"mound","rise":30,
         "mask":{"areas":[{"kind":"ellipse","at":[0,0],"rx":18,"rz":18}],"mirror":false}}
        """;

    private const string Idle = """
        {"id":"idle","op":"mound","rise":10,"off":true,
         "mask":{"areas":[{"kind":"ellipse","at":[5,5],"rx":6,"rz":6}],"mirror":false}}
        """;

    private const string Flat = """
        {"id":"flat","op":"level","top":40,
         "mask":{"areas":[{"kind":"board"}],"mirror":false}}
        """;

    private static string Layout(params string[] modifiers) =>
        "{" + Ground + (modifiers.Length == 0 ? "" : ",\"modifiers\":[" + string.Join(",", modifiers) + "]") + "}";

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string route, string layout) =>
        await client.PostAsync($"/api/map/{SketchBoard.Slug}/sketch/{route}",
            new StringContent(layout, Encoding.UTF8, "application/json"));

    private static async Task<string> ColumnsAsync(HttpClient client, string layout, string query = "")
    {
        using var response = await PostAsync(client, "columns" + query, layout);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cols").GetRawText();
    }

    private static async Task<ReliefGroupContoursDto> SurfaceAsync(HttpClient client, string layout, string query = "")
    {
        using var response = await PostAsync(client, "relief?heights=true" + query, layout);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ReliefContoursDto>())!.Groups.Single();
    }

    [Test]
    public async Task The_columns_read_steps_through_the_stack()
    {
        using var client = await SketchBoard.FreshAsync();
        var stack = Layout(Hill, Flat);

        var shapesAlone = await ColumnsAsync(client, Layout());
        var whole = await ColumnsAsync(client, stack);

        await Assert.That(await ColumnsAsync(client, stack, "?step=0")).IsEqualTo(shapesAlone);
        await Assert.That(await ColumnsAsync(client, stack, "?step=1")).IsEqualTo(await ColumnsAsync(client, Layout(Hill)));
        await Assert.That(await ColumnsAsync(client, stack, "?step=2")).IsEqualTo(whole);
        await Assert.That(await ColumnsAsync(client, stack, "?step=1")).IsNotEqualTo(whole);
        await Assert.That(whole).IsNotEqualTo(shapesAlone);
    }

    [Test]
    public async Task The_contour_read_steps_through_the_stack()
    {
        using var client = await SketchBoard.FreshAsync();
        var stack = Layout(Hill, Flat);

        var alone = await SurfaceAsync(client, stack, "&step=0");
        var hilled = await SurfaceAsync(client, stack, "&step=1");
        var whole = await SurfaceAsync(client, stack);

        await Assert.That(alone.Max).IsEqualTo(20);
        await Assert.That(hilled.Max).IsGreaterThan(alone.Max);
        await Assert.That(whole.Heights!.SequenceEqual((await SurfaceAsync(client, stack, "&step=2")).Heights!)).IsTrue();
        await Assert.That(whole.Heights!.SequenceEqual(hilled.Heights!)).IsFalse();
    }

    [Test]
    public async Task A_step_past_the_end_is_the_whole_stack_and_a_negative_one_is_none()
    {
        using var client = await SketchBoard.FreshAsync();
        var stack = Layout(Hill);

        await Assert.That(await ColumnsAsync(client, stack, "?step=9")).IsEqualTo(await ColumnsAsync(client, stack));
        await Assert.That(await ColumnsAsync(client, stack, "?step=-3")).IsEqualTo(await ColumnsAsync(client, Layout()));
    }

    [Test]
    public async Task The_changes_read_counts_the_columns_each_modifier_moved_in_run_order()
    {
        using var client = await SketchBoard.FreshAsync();

        using var response = await PostAsync(client, "modifiers/changes", Layout(Hill, Idle, Flat));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var changes = (await response.Content.ReadFromJsonAsync<ModifierChangesDto>())!.Modifiers;

        await Assert.That(changes.Select(change => change.Id).SequenceEqual(new[] { "hill", "idle", "flat" })).IsTrue();
        await Assert.That(changes[0].Columns).IsGreaterThan(0);
        await Assert.That(changes[1].Columns).IsEqualTo(0);
        await Assert.That(changes[2].Columns).IsGreaterThan(0);

        var raised = await SurfaceAsync(client, Layout(Hill));
        await Assert.That(changes[0].Columns).IsEqualTo(raised.Heights!.Count(height => height is { } top && top != 20));
    }

    [Test]
    public async Task The_changes_read_answers_an_empty_list_for_a_layout_with_no_modifier()
    {
        using var client = await SketchBoard.FreshAsync();

        using var response = await PostAsync(client, "modifiers/changes", Layout());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await response.Content.ReadFromJsonAsync<ModifierChangesDto>())!.Modifiers).IsEmpty();
    }

    [Test]
    public async Task The_changes_read_refuses_a_body_that_is_not_a_layout()
    {
        using var client = await SketchBoard.FreshAsync();

        using var response = await PostAsync(client, "modifiers/changes", "{ not json");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task The_schema_names_the_step_word_on_the_two_reads()
    {
        using var client = await SketchBoard.FreshAsync();
        using var schema = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));
        var paths = schema.RootElement.GetProperty("paths");

        foreach (var route in new[] { "/api/map/{slug}/sketch/relief", "/api/map/{slug}/sketch/columns" })
        {
            var parameters = paths.GetProperty(route).GetProperty("post").GetProperty("parameters").EnumerateArray()
                                  .Select(parameter => parameter.GetProperty("name").GetString()).ToList();
            await Assert.That(parameters).Contains("step");
        }
        await Assert.That(paths.TryGetProperty("/api/map/{slug}/sketch/modifiers/changes", out _)).IsTrue();
    }
}
