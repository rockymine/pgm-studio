using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A field the reader had nowhere to keep is named on every write that takes one of the three documents an
/// author or an agent actually posts — the plan, the sketch layout and the intent — and a document read whole
/// says nothing.
///
/// <para><b>Both halves are the test.</b> A misspelled property is dropped by the deserializer and the request
/// answers 200, so the loss is invisible from the response: fourteen rectangles keyed <c>x</c>/<c>z</c>/
/// <c>w</c>/<c>h</c> covered no ground under an <c>{"ok": true}</c>, and a <c>relief</c> written one level too
/// deep dropped without a word. But a check that fires on a correct document is worse than none, because an
/// author learns to ignore the warnings and then misses the real one — so the silence is asserted at least as
/// hard as the complaint.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class UnreadFieldTests
{
    /// <summary>A board of one rectangle, in the shape the sketch bridge writes: renamed fields
    /// (<c>min_x</c>), a raw-JSON dressing and a theme registry whose entries are the author's own words.
    /// None of those is a name the layout record carries, and none of them is unread.</summary>
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0},
                  "bbox":{"min_x":-60,"max_x":60,"min_z":-40,"max_z":40}},
         "themes":{"meadow":{"buckets":{"surface":{"kind":"solid","block":"grass_block"}}}},
         "mapTheme":"meadow",
         "dressing":{"props":[{"kind":"tree","at":[3,4]}]},
         "layers":[{"id":"L1","name":"base","base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
           "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]}
        """;

    private const string Intent = """
        {"maxPlayers":12,"spawns":[],"islandTeams":{"i":"red"},
         "meta":{"name":"read whole","authors":["rockymine"]}}
        """;

    private const string Plan = """
        {"plan":2,"meta":{"name":"read whole"},
         "globals":{"cell":5,"symmetry":"rot_180","maxPlayers":12,"surface":9},
         "pieces":[{"id":"a","role":"spawn","rect":[0,0,4,4]}],
         "zones":[],
         "placements":{"spawns":[{"id":"sp","piece":"a","at":[10,10],"facing":"front"}],
                       "wools":[],"iron":[],"destroyables":[],"cores":[]},
         "walls":[],"boxes":[]}
        """;

    [Test]
    public async Task A_document_read_whole_carries_no_unread_complaint()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await OriginateAsync(client);

        await Silent(await client.PutAsync($"/api/map/{slug}/sketch", Json(Layout)), "PUT sketch");
        await Silent(await client.PutAsync($"/api/map/{slug}/intent", Json(Intent)), "PUT intent");
        await Silent(await client.PutAsync($"/api/map/{slug}/plan", Json(Plan)), "PUT plan");
        await Silent(await client.PostAsync("/api/plan/compile", Json(Plan)), "POST plan/compile");
    }

    /// <summary>The board that was actually lost: a rectangle stating the corner-and-extent keys the studio
    /// does not have, which rasterizes to nothing.</summary>
    [Test]
    public async Task A_rectangle_keyed_by_extent_is_named_field_by_field()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await OriginateAsync(client);

        var layout = Layout.Replace(
            """ "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,""", """ "x":-20,"z":-20,"w":40,"h":40,""");

        var fields = await Unread(await client.PutAsync($"/api/map/{slug}/sketch", Json(layout)), "PUT sketch");
        await Assert.That(fields).IsEquivalentTo([
            "layers[0].layout.shapes[0].x", "layers[0].layout.shapes[0].z",
            "layers[0].layout.shapes[0].w", "layers[0].layout.shapes[0].h",
        ]);
    }

    /// <summary>And a part of the document written one level too deep, which is the same loss with no
    /// misspelling in it — the name is right and the place is not.</summary>
    [Test]
    public async Task A_part_written_one_level_too_deep_is_named_by_its_path()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await OriginateAsync(client);

        var layout = Layout.Replace("\"groups\":[", "\"relief\":{\"i\":{\"kind\":\"ridge\"}},\"groups\":[");

        await Assert.That(await Unread(await client.PutAsync($"/api/map/{slug}/sketch", Json(layout)), "PUT sketch"))
            .IsEquivalentTo(["layers[0].layout.relief"]);
    }

    [Test]
    public async Task A_misspelled_intent_slice_is_named()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await OriginateAsync(client);

        await Assert.That(await Unread(
                await client.PutAsync($"/api/map/{slug}/intent", Json(Intent.Replace("\"spawns\"", "\"spawnz\""))),
                "PUT intent"))
            .IsEquivalentTo(["spawnz"]);
    }

    [Test]
    public async Task A_misspelled_plan_global_is_named_at_the_compile_gate()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        await Assert.That(await Unread(
                await client.PostAsync("/api/plan/compile", Json(Plan.Replace("\"surface\"", "\"surfce\""))),
                "POST plan/compile"))
            .IsEquivalentTo(["globals.surfce"]);
    }

    /// <summary>The <c>RQ3</c> paths on a success, or an empty list where the response carries no complaints
    /// at all — a success with nothing to say answers no <c>warnings</c> key.</summary>
    private static async Task<IReadOnlyList<string>> Unread(HttpResponseMessage response, string route)
    {
        await Assert.That(response.IsSuccessStatusCode).IsTrue()
            .Because($"{route} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!body.TryGetProperty("warnings", out var warnings)) return [];
        return [.. warnings.EnumerateArray()
            .Where(finding => finding.GetProperty("rule").GetString() == "RQ3")
            .Select(finding => finding.GetProperty("field").GetString()!)];
    }

    private static async Task Silent(HttpResponseMessage response, string route) =>
        await Assert.That(await Unread(response, route)).IsEmpty()
            .Because($"{route} was posted a document every part of which the reader keeps");

    private static async Task<string> OriginateAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/sketch", new { name = "unread fields" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
