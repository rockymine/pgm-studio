using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The house-style gate, asked on every road a layout can be stored through.
///
/// <para><b>The invariant is the road, not the rule.</b> <c>HouseStyleValidation</c> has its own tests; what
/// this holds is that a board cannot reach the world without passing them, whichever way it was authored — the
/// plain PUT a person uses, the compile path an editor rebuilds through, and the one-call load a headless
/// author stores through. A rule wired to one of the three is a rule two thirds of the maps never meet.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class SketchRoomStyleGateTests
{
    /// <summary>A board whose wool cage wears a log verge — which <c>HS3</c> refuses, since a log is never a
    /// roof or a verge material.</summary>
    private const string LogVerge = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
           "islands":[{"id":"i","name":"I","shapeIds":["s1"]}]}}],
         "roomStyles":{"cage":{"roof":{"form":"gable","pitch":1,"slab":-1,
                                       "body":{"kind":"solid","id":5,"data":1},
                                       "verge":{"kind":"solid","id":17,"data":1}}}}}
        """;

    /// <summary>The same fault on a placed building instead of a bound shell. A prop carries its shell as a
    /// snapshot, so this style is the one the export would stamp.</summary>
    private const string LogVergeProp = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
           "islands":[{"id":"i","name":"I","shapeIds":["s1"]}]}}],
         "dressing":{"props":[{"kind":"house","id":"byre","wings":[{"corners":[[0,0],[6,6]]}],
                               "style":{"roof":{"form":"gable","pitch":1,"slab":-1,
                                                "body":{"kind":"solid","id":5,"data":1},
                                                "verge":{"kind":"solid","id":17,"data":1}}}}]}}
        """;

    private const string Clean = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
           "islands":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]}
        """;

    private static StringContent Body(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<string> RefusedAsync(HttpResponseMessage resp)
    {
        await Assert.That((int)resp.StatusCode).IsEqualTo(400).Because(await resp.Content.ReadAsStringAsync());
        var refusal = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return refusal.GetProperty("findings")[0].GetProperty("rule").GetString()!;
    }

    [Test]
    public async Task Every_road_to_a_stored_layout_asks_the_same_gate()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        await client.PostAsJsonAsync("/api/sketch", new { name = "Gate" });

        // 1 · the plain PUT — the road a person takes
        await Assert.That(await RefusedAsync(
            await client.PutAsync("/api/map/gate/sketch", Body(LogVerge)))).IsEqualTo("HS3");

        // 2 · the compile path — the road an editor rebuilds through, and an agent drives
        await Assert.That(await RefusedAsync(
            await client.PutAsync("/api/map/gate/sketch/from-plan", Body(LogVerge)))).IsEqualTo("HS3");

        // 3 · the one-call load — the road a headless author stores a whole map through
        var loaded = await client.PostAsJsonAsync("/api/map/from-documents", new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
            layout = JsonDocument.Parse(LogVerge).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Gate Two"}}""").RootElement,
            name = "Gate Two",
        });
        await Assert.That(await RefusedAsync(loaded)).IsEqualTo("HS3");

        // and the map it would have made is not there
        var maps = await client.GetFromJsonAsync<JsonElement>("/api/maps");
        await Assert.That(maps.EnumerateArray().Any(m => m.GetProperty("slug").GetString() == "gate-two")).IsFalse();
    }

    /// <summary>A placed building's shell is a house style and is read as one. Nothing else opens it:
    /// <c>HouseProp.Check</c> reads wings and joints and never the style it carries.</summary>
    [Test]
    public async Task A_placed_buildings_own_shell_is_checked_too()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        await client.PostAsJsonAsync("/api/sketch", new { name = "Dressed" });

        var resp = await client.PutAsync("/api/map/dressed/sketch", Body(LogVergeProp));
        await Assert.That(await RefusedAsync(resp)).IsEqualTo("HS3");

        var refusal = await (await client.PutAsync("/api/map/dressed/sketch", Body(LogVergeProp)))
            .Content.ReadFromJsonAsync<JsonElement>();
        var finding = refusal.GetProperty("findings")[0];
        // the prop is named, since a board carries many and "verge" alone does not say which
        await Assert.That(finding.GetProperty("field").GetString()).Contains("dressing.props[0].style");
        await Assert.That(finding.GetProperty("subjects")[0].GetString()).IsEqualTo("byre");
    }

    /// <summary>A board with no styles at all passes every road — the gate reports what is there and never
    /// asks for a house.</summary>
    [Test]
    public async Task A_board_carrying_no_house_passes()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        await client.PostAsJsonAsync("/api/sketch", new { name = "Bare" });

        var put = await client.PutAsync("/api/map/bare/sketch", Body(Clean));
        await Assert.That(put.IsSuccessStatusCode).IsTrue().Because(await put.Content.ReadAsStringAsync());
        var fromPlan = await client.PutAsync("/api/map/bare/sketch/from-plan", Body(Clean));
        await Assert.That(fromPlan.IsSuccessStatusCode).IsTrue().Because(await fromPlan.Content.ReadAsStringAsync());
    }
}
