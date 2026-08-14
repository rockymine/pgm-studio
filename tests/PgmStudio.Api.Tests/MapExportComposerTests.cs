using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// B116: the three export-time refusals <c>MapExportComposer</c> asks against a sketch-originated map's
/// built world, over the ground the rasterizer actually produced — <c>OB17</c> (objective placement),
/// <c>OB18</c> (a destroy kit that cannot break its own goal) and <c>OB19</c> (a tree, boulder or building
/// standing inside a goal's clearance) — each answering <b>409</b> from the one composer, with the rule id
/// a caller can act on. Every scenario here is one the compile gate never sees: none of these maps was
/// authored through a plan, so <c>PlanValidator</c> never runs over them — proving the export gate is the
/// only place that catches a destroy goal authored straight into Sketch.
/// </summary>
[NotInParallel("api-db")]
public sealed class MapExportComposerTests
{
    // A single 40×40 island, unmirrored — every scenario below plants its own intent on this same ground.
    private const string IslandLayout = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":40,"min_z":0,"max_z":40}],
                   "islands":[{"id":"i1","name":"Island","mirrors":false,"shapeIds":["a"]}]}}
        """;

    // The same island, plus one tree drawn at (10,10) — well inside the clearance of a goal anchored there.
    private const string IslandLayoutWithTree = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":40,"min_z":0,"max_z":40}],
                   "islands":[{"id":"i1","name":"Island","mirrors":false,"shapeIds":["a"]}]},
         "dressing":{"props":[{"kind":"tree","id":"t1","seed":1,"x":10,"z":10}]}}
        """;

    private static async Task<string> CreateFinishedSketchAsync(HttpClient client, string layoutJson)
    {
        var create = await client.PostAsJsonAsync("/api/sketch", new { name = $"B116 {Guid.NewGuid():N}" });
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var put = await client.PutAsync($"/api/map/{slug}/sketch",
            new StringContent(layoutJson, Encoding.UTF8, "application/json"));
        await Assert.That(put.IsSuccessStatusCode).IsTrue();

        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue();
        return slug;
    }

    private static async Task<JsonElement> Refuse409Async(HttpClient client, string slug)
    {
        var resp = await client.GetAsync($"/api/map/{slug}/xml");
        await Assert.That((int)resp.StatusCode).IsEqualTo(409);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Test]
    public async Task OB17_refuses_a_destroyable_that_overhangs_the_void()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await CreateFinishedSketchAsync(client, IslandLayout);

        var intent = new
        {
            teams = new[] { new { id = "red", name = "Red", color = "red" } },
            spawns = new[] { new { team = "red", point = new { x = 10, y = 0, z = 10 }, yaw = 0 } },
            destroyables = new[]
            {
                new
                {
                    owner = "red", name = "farmon", style = "pillar-1", materials = "obsidian",
                    anchor = new { x = 500, y = 0, z = 500 }, @float = 4,
                },
            },
        };
        await Assert.That((await client.PutAsJsonAsync($"/api/map/{slug}/intent", intent)).IsSuccessStatusCode).IsTrue();

        var body = await Refuse409Async(client, slug);
        await Assert.That(body.GetProperty("error").GetString()).IsEqualTo("objective placement");
        var findings = body.GetProperty("findings");
        await Assert.That(findings.GetArrayLength()).IsGreaterThan(0);
        await Assert.That(findings[0].GetProperty("rule").GetString()).IsEqualTo("OB17");
        await Assert.That(findings[0].GetProperty("message").GetString()).Contains("void");
    }

    [Test]
    public async Task OB18_refuses_a_goal_nothing_in_the_kit_can_break()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await CreateFinishedSketchAsync(client, IslandLayout);

        // No spawns at all: TeamsGenerator only derives a kit when the map has one (GenerateKits gates on
        // Spawns.Count > 0), so the exported document's kits carry no pickaxe whatsoever — the kitless
        // destroy map MG18 records, reached here through a document the derivation never touched.
        var intent = new
        {
            teams = new[] { new { id = "red", name = "Red", color = "red" } },
            spawns = Array.Empty<object>(),
            destroyables = new[]
            {
                new
                {
                    owner = "red", name = "farmon", style = "pillar-1", materials = "obsidian",
                    anchor = new { x = 10, y = 0, z = 10 }, @float = 4,
                },
            },
        };
        await Assert.That((await client.PutAsJsonAsync($"/api/map/{slug}/intent", intent)).IsSuccessStatusCode).IsTrue();

        var body = await Refuse409Async(client, slug);
        await Assert.That(body.GetProperty("error").GetString()).IsEqualTo("unwinnable goal");
        await Assert.That(body.GetProperty("rule").GetString()).IsEqualTo("OB18");
        var goals = body.GetProperty("goals").EnumerateArray().Select(g => g.GetString()).ToList();
        await Assert.That(goals).Contains("farmon");
    }

    [Test]
    public async Task OB19_refuses_a_tree_standing_inside_a_goals_clearance()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await CreateFinishedSketchAsync(client, IslandLayoutWithTree);

        // Well clear of the tree/goal cluster at (10,10), and materials left unset so OB18 cannot also fire —
        // this map is testing OB19 alone.
        var intent = new
        {
            teams = new[] { new { id = "red", name = "Red", color = "red" } },
            spawns = new[] { new { team = "red", point = new { x = 5, y = 0, z = 35 }, yaw = 0 } },
            destroyables = new[]
            {
                new
                {
                    owner = "red", name = "farmon", style = "pillar-1", materials = "",
                    anchor = new { x = 10, y = 0, z = 10 }, @float = 4,
                },
            },
        };
        await Assert.That((await client.PutAsJsonAsync($"/api/map/{slug}/intent", intent)).IsSuccessStatusCode).IsTrue();

        var body = await Refuse409Async(client, slug);
        await Assert.That(body.GetProperty("error").GetString()).IsEqualTo("prop in goal clearance");
        await Assert.That(body.GetProperty("rule").GetString()).IsEqualTo("OB19");
        var props = body.GetProperty("props");
        await Assert.That(props.GetArrayLength()).IsEqualTo(1);
        await Assert.That(props[0].GetProperty("kind").GetString()).IsEqualTo("tree");
        await Assert.That(props[0].GetProperty("id").GetString()).IsEqualTo("t1");
    }

    /// <summary>The control: the same shape of map as the three refusals above, minus the fault each one
    /// looks for, still exports clean — proving the new gate does not reject a map that was never wrong.</summary>
    [Test]
    public async Task A_map_with_none_of_the_three_faults_still_exports()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await CreateFinishedSketchAsync(client, IslandLayout);

        var intent = new
        {
            teams = new[] { new { id = "red", name = "Red", color = "red" } },
            spawns = new[] { new { team = "red", point = new { x = 5, y = 0, z = 35 }, yaw = 0 } },
            destroyables = new[]
            {
                new
                {
                    owner = "red", name = "farmon", style = "pillar-1", materials = "obsidian",
                    anchor = new { x = 10, y = 0, z = 10 }, @float = 4,
                },
            },
        };
        await Assert.That((await client.PutAsJsonAsync($"/api/map/{slug}/intent", intent)).IsSuccessStatusCode).IsTrue();

        var resp = await client.GetAsync($"/api/map/{slug}/xml");
        await Assert.That(resp.IsSuccessStatusCode).IsTrue();
        var xml = await resp.Content.ReadAsStringAsync();
        await Assert.That(xml).Contains("farmon");
    }
}
