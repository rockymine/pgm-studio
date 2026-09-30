using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A map's changes over HTTP: the list of them, the documents at one, the edits and the columns between two, and
/// a change written back. What is asserted is what a round leans on — every change is listed with what it
/// wrote and why, two changes compare by the path each edit names, and a restore lands as a change whose
/// documents are the ones it restored.
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class MapChangeEndpointsTests
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"id":"ground","base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":0,"base_height":12}],
           "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]}
        """;

    private static readonly string Narrower = Layout.Replace("\"min_x\":-20,\"max_x\":20", "\"min_x\":-10,\"max_x\":10");

    private static object Body() => new
    {
        plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
        layout = JsonDocument.Parse(Layout).RootElement,
        intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate","authors":[],"contributors":[]}}""").RootElement,
        name = "Weirgate",
        origin = new { repo = "rockymine/pgm-studio-mapgen", commit = "5daa56f", path = "specs/weirgate", dirty = false },
        note = "pass 1",
    };

    [Test]
    public async Task The_list_says_who_wrote_each_change_what_it_wrote_and_why()
    {
        using var client = await NarrowedAsync();

        var changes = (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/changes")).GetProperty("changes");
        await Assert.That(changes.GetArrayLength()).IsEqualTo(2);
        var load = changes[0];
        await Assert.That(Words(load.GetProperty("documents"))).IsEquivalentTo(["plan", "refinement", "layout", "intent"]);
        await Assert.That(load.GetProperty("note").GetString()).IsEqualTo("pass 1");
        await Assert.That(load.GetProperty("origin").GetProperty("commit").GetString()).IsEqualTo("5daa56f");
        await Assert.That(Words(changes[1].GetProperty("documents"))).IsEquivalentTo(["layout"]);

        var first = load.GetProperty("number").GetInt64();
        var since = (await client.GetFromJsonAsync<JsonElement>($"/api/map/weirgate/changes?since={first}"))
            .GetProperty("changes");
        await Assert.That(since.GetArrayLength()).IsEqualTo(1);

        var text = await client.GetStringAsync("/api/map/weirgate/changes?format=text");
        await Assert.That(text).Contains($"#{first}");
        await Assert.That(text).Contains("pass 1");
    }

    [Test]
    public async Task A_change_answers_the_documents_as_they_stood_at_it()
    {
        using var client = await NarrowedAsync();
        var (first, second) = await NumbersAsync(client);

        var then = await client.GetFromJsonAsync<JsonElement>($"/api/map/weirgate/changes/{first}");
        var now = await client.GetFromJsonAsync<JsonElement>($"/api/map/weirgate/changes/{second}");

        await Assert.That(MaxX(then.GetProperty("layout"))).IsEqualTo(20d);
        await Assert.That(MaxX(now.GetProperty("layout"))).IsEqualTo(10d);
        await Assert.That(now.GetProperty("intent").GetProperty("meta").GetProperty("name").GetString())
            .IsEqualTo("Weirgate").Because("a document the change did not write stands as the latest change before it wrote it");

        using var missing = await client.GetAsync($"/api/map/weirgate/changes/{second + 10}");
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Two_changes_compare_by_the_path_each_edit_names()
    {
        using var client = await NarrowedAsync();
        var (first, second) = await NumbersAsync(client);

        var diff = await client.GetFromJsonAsync<JsonElement>($"/api/map/weirgate/diff?from={first}&to={second}");
        var edits = diff.GetProperty("edits").EnumerateArray().ToList();
        var maxX = edits.Single(edit => edit.GetProperty("path").GetString() == "layers[ground].layout.shapes[s1].max_x");
        await Assert.That(maxX.GetProperty("document").GetString()).IsEqualTo("layout");
        await Assert.That(maxX.GetProperty("op").GetString()).IsEqualTo("set");
        await Assert.That(maxX.GetProperty("before").GetDouble()).IsEqualTo(20d);
        await Assert.That(maxX.GetProperty("value").GetDouble()).IsEqualTo(10d);
        await Assert.That(diff.TryGetProperty("world", out _)).IsFalse().Because("the world is built only when asked for");

        var latest = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/diff");
        await Assert.That(latest.GetProperty("from").GetInt64()).IsEqualTo(first)
            .Because("unasked, the diff is what the latest change did");
        await Assert.That(latest.GetProperty("edits").GetArrayLength()).IsEqualTo(edits.Count);
    }

    [Test]
    public async Task The_world_diff_counts_the_columns_an_edit_moved_and_draws_them()
    {
        using var client = await NarrowedAsync();
        var (first, second) = await NumbersAsync(client);
        var asked = $"/api/map/weirgate/diff?from={first}&to={second}&world=true";

        var world = (await client.GetFromJsonAsync<JsonElement>(asked)).GetProperty("world");
        var ground = world.GetProperty("ground");
        await Assert.That(ground.GetProperty("columns").GetInt32()).IsGreaterThanOrEqualTo(2 * 10 * 40)
            .Because("narrowing a 40-wide plate to 20 takes away two strips of 10 by 40");
        await Assert.That(ground.GetProperty("runs").EnumerateArray()
            .All(run => run.GetProperty("minX").GetInt32() >= 10 || run.GetProperty("maxX").GetInt32() < -10)).IsTrue();

        var text = await client.GetStringAsync(asked + "&format=text");
        await Assert.That(text).Contains("ground");
        using var png = await client.GetAsync(asked.Replace("&world=true", "&format=png"));
        await Assert.That(png.Content.Headers.ContentType?.MediaType).IsEqualTo("image/png");
    }

    [Test]
    public async Task A_restore_lands_as_a_change_holding_the_documents_it_restored()
    {
        using var client = await NarrowedAsync();
        var (first, second) = await NumbersAsync(client);

        using var restoring = await client.PostAsJsonAsync($"/api/map/weirgate/changes/{first}/restore", new { });
        await Assert.That(restoring.IsSuccessStatusCode).IsTrue().Because(await restoring.Content.ReadAsStringAsync());
        var restored = await restoring.Content.ReadFromJsonAsync<JsonElement>();
        var landed = restored.GetProperty("change").GetInt64();
        await Assert.That(landed).IsGreaterThan(second);
        await Assert.That(Words(restored.GetProperty("documents"))).IsEquivalentTo(["layout"]);

        var diff = await client.GetFromJsonAsync<JsonElement>($"/api/map/weirgate/diff?from={first}&to={landed}");
        await Assert.That(diff.GetProperty("edits").GetArrayLength()).IsEqualTo(0);
        var sketch = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/sketch");
        await Assert.That(MaxX(sketch)).IsEqualTo(20d);

        var changes = (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/changes")).GetProperty("changes");
        await Assert.That(changes[changes.GetArrayLength() - 1].GetProperty("note").GetString())
            .IsEqualTo($"restores change {first}");
    }

    [Test]
    public async Task A_restore_to_what_the_map_already_holds_writes_nothing()
    {
        using var client = await NarrowedAsync();
        var (_, second) = await NumbersAsync(client);

        using var restoring = await client.PostAsJsonAsync($"/api/map/weirgate/changes/{second}/restore", new { });
        var restored = await restoring.Content.ReadFromJsonAsync<JsonElement>();

        await Assert.That(restored.TryGetProperty("change", out var change) && change.ValueKind != JsonValueKind.Null)
            .IsFalse();
        await Assert.That(restored.GetProperty("documents").GetArrayLength()).IsEqualTo(0);
        var changes = (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/changes")).GetProperty("changes");
        await Assert.That(changes.GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task A_change_the_map_does_not_have_is_answered_404()
    {
        using var client = await NarrowedAsync();
        var (_, second) = await NumbersAsync(client);

        using var diff = await client.GetAsync($"/api/map/weirgate/diff?from=1&to={second + 5}");
        using var restore = await client.PostAsJsonAsync($"/api/map/weirgate/changes/{second + 5}/restore", new { });

        await Assert.That(diff.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(restore.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    /// <summary>Weirgate loaded whole, then its plate narrowed from 40 blocks wide to 20: two changes.</summary>
    private static async Task<HttpClient> NarrowedAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();
        using var loaded = await client.PutAsJsonAsync("/api/map/weirgate/source", Body());
        await Assert.That(loaded.IsSuccessStatusCode).IsTrue().Because(await loaded.Content.ReadAsStringAsync());
        using var narrowed = await client.PutAsync("/api/map/weirgate/sketch",
            new StringContent(Narrower, Encoding.UTF8, "application/json"));
        await Assert.That(narrowed.IsSuccessStatusCode).IsTrue();
        return client;
    }

    private static async Task<(long First, long Second)> NumbersAsync(HttpClient client)
    {
        var changes = (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/changes")).GetProperty("changes");
        return (changes[0].GetProperty("number").GetInt64(), changes[1].GetProperty("number").GetInt64());
    }

    private static List<string> Words(JsonElement list) => [.. list.EnumerateArray().Select(word => word.GetString()!)];

    private static double MaxX(JsonElement layout) =>
        layout.GetProperty("layers")[0].GetProperty("layout").GetProperty("shapes")[0].GetProperty("max_x").GetDouble();
}
