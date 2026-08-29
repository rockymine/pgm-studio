using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// B116: the export-time refusals <c>MapExportComposer</c> asks against a sketch-originated map's built
/// world, over the ground the rasterizer actually produced — <c>OB17</c> (objective placement) and
/// <c>OB19</c> (a tree, boulder or building standing inside a goal's clearance) — each answering <b>409</b>
/// from the one composer, with the rule id a caller can act on. Every scenario here is one the compile gate
/// never sees: none of these maps was authored through a plan, so <c>PlanValidator</c> never runs over them —
/// proving the export gate is the only place that catches a destroy goal authored straight into Sketch.
/// <para>B116 also wired a third gate here, <c>OB18</c> — a kit/material mismatch, refusing an obsidian goal
/// against an iron pickaxe as unwinnable. <c>B134</c> found the premise false (an iron pickaxe breaks
/// obsidian, it just does not drop it, so the goal was winnable and merely slow) and removed it; the test
/// below now asserts the export succeeds where it used to 409.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class MapExportComposerTests
{
    // A single 40×40 island, unmirrored — every scenario below plants its own intent on this same ground.
    private const string IslandLayout = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":40,"min_z":0,"max_z":40}],
                   "groups":[{"id":"i1","name":"Island","mirrors":false,"shapeIds":["a"]}]} }]}
        """;

    // The same island, plus one tree drawn at (10,10) — well inside the clearance of a goal anchored there.
    private const string IslandLayoutWithTree = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":40,"min_z":0,"max_z":40}],
                   "groups":[{"id":"i1","name":"Island","mirrors":false,"shapeIds":["a"]}]} }],
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

    // BuildAndCompose is the one chain every export runs, so what it
    // refuses and where the decoration lands are contract, not plumbing.
    [Test]
    public async Task BuildAndCompose_decorates_the_document_between_projection_and_judgement()
    {
        var intent = new PgmStudio.Pgm.Authoring.MapIntent
        {
            Teams = [new PgmStudio.Pgm.Authoring.TeamDef { Id = "red-team", Name = "Red", Color = "dark red" }],
            Spawns = [new PgmStudio.Pgm.Authoring.SpawnIntent { Team = "red-team", Point = new PgmStudio.Pgm.Authoring.Pt(10, 0, 10) }],
            Destroyables =
            [
                new PgmStudio.Pgm.Authoring.DestroyableIntent
                {
                    Owner = "red-team", Name = "Farmon", Style = "pillar-1", Materials = "obsidian",
                    Anchor = new PgmStudio.Pgm.Authoring.Pt(20, 0, 20), Float = 4,
                },
            ],
        };
        var result = PgmStudio.Export.MapExportComposer.BuildAndCompose(
            [], IslandLayout, intent, doc =>
            {
                doc["name"] = "Chainproof";
                doc["version"] = "1.0.0";
                doc["objective"] = "Prove the chain.";
            });

        await Assert.That(result.Refusal).IsNull();
        await Assert.That(result.World).IsNotNull();
        await Assert.That(result.Xml!).Contains("Chainproof");
        await Assert.That(result.Xml!).Contains("Prove the chain.");
    }

    [Test]
    public async Task BuildAndCompose_refuses_a_goal_over_the_void_with_the_export_gate_finding()
    {
        var intent = new PgmStudio.Pgm.Authoring.MapIntent
        {
            Teams = [new PgmStudio.Pgm.Authoring.TeamDef { Id = "red-team", Name = "Red", Color = "dark red" }],
            Spawns = [new PgmStudio.Pgm.Authoring.SpawnIntent { Team = "red-team", Point = new PgmStudio.Pgm.Authoring.Pt(10, 0, 10) }],
            Destroyables =
            [
                new PgmStudio.Pgm.Authoring.DestroyableIntent
                {
                    Owner = "red-team", Name = "Farmon", Style = "pillar-1", Materials = "obsidian",
                    Anchor = new PgmStudio.Pgm.Authoring.Pt(500, 0, 500), Float = 4,
                },
            ],
        };
        var result = PgmStudio.Export.MapExportComposer.BuildAndCompose([], IslandLayout, intent);

        await Assert.That(result.Refusal).IsNotNull();
        await Assert.That(result.Refusal!.Status).IsEqualTo(409);
        await Assert.That(result.Refusal!.Findings.Select(finding => finding.Rule)).Contains("OB17");
    }

    [Test]
    public async Task OB24_refuses_a_destroyable_and_a_core_built_into_the_same_blocks()
    {
        // The shape a rot_180 board produces by itself: a destroyable drawn at one position and a core at the
        // position the orbit maps it onto, so each lands inside the other's image. The plan reads two
        // placements at two coordinates and has nothing to object to; the boxes are what disagree, and they
        // exist only once the stampers have run.
        var shared = new PgmStudio.Pgm.Authoring.Pt(20, 0, 20);   // mid-island, so nothing else refuses first
        var intent = new PgmStudio.Pgm.Authoring.MapIntent
        {
            Teams = [new PgmStudio.Pgm.Authoring.TeamDef { Id = "red-team", Name = "Red", Color = "dark red" }],
            Spawns = [new PgmStudio.Pgm.Authoring.SpawnIntent { Team = "red-team", Point = new PgmStudio.Pgm.Authoring.Pt(10, 0, 10) }],
            Destroyables =
            [
                new PgmStudio.Pgm.Authoring.DestroyableIntent
                {
                    Owner = "red-team", Name = "Cairn", Style = "cube-3", Materials = "end stone",
                    Anchor = shared, Float = 2,
                },
            ],
                        // Size/Height/Shell default to 0 on a hand-built intent — the compiler fills 5/5/1 — and a core
            // of no size stamps nothing, so it has to be said here or the gate has one box to compare.
            Cores =
            [
                new PgmStudio.Pgm.Authoring.CoreIntent
                {
                    Owner = "red-team", Name = "Heart", Anchor = shared, Float = 2,
                    Size = 5, Height = 5, Shell = 1,
                },
            ],
        };

        var result = PgmStudio.Export.MapExportComposer.BuildAndCompose([], IslandLayout, intent);

        await Assert.That(result.Refusal).IsNotNull()
            .Because("a destroyable stamped inside a core is one structure serving two objectives");
        await Assert.That(result.Refusal!.Status).IsEqualTo(409);
        var shares = result.Refusal!.Findings.SingleOrDefault(finding => finding.Rule == "OB24");
        await Assert.That(shares).IsNotNull()
            .Because($"refused with: {string.Join(" | ", result.Refusal!.Findings.Select(f => f.Rule + " " + f.Message))}");
        await Assert.That(shares!.Subjects!).Contains("Cairn");
        await Assert.That(shares!.Subjects!).Contains("Heart");
    }

    [Test]
    public async Task Two_goals_that_stand_apart_are_not_refused_for_sharing_ground()
    {
        // The other half, so the gate is not passing by refusing everything: the same two goals, moved apart.
        var intent = new PgmStudio.Pgm.Authoring.MapIntent
        {
            Teams = [new PgmStudio.Pgm.Authoring.TeamDef { Id = "red-team", Name = "Red", Color = "dark red" }],
            Spawns = [new PgmStudio.Pgm.Authoring.SpawnIntent { Team = "red-team", Point = new PgmStudio.Pgm.Authoring.Pt(10, 0, 10) }],
            Destroyables =
            [
                new PgmStudio.Pgm.Authoring.DestroyableIntent
                {
                    Owner = "red-team", Name = "Cairn", Style = "cube-3", Materials = "end stone",
                    Anchor = new PgmStudio.Pgm.Authoring.Pt(10, 0, 10), Float = 2,
                },
            ],
                        Cores =
            [
                new PgmStudio.Pgm.Authoring.CoreIntent
                {
                    Owner = "red-team", Name = "Heart", Anchor = new PgmStudio.Pgm.Authoring.Pt(30, 0, 30),
                    Float = 2, Size = 5, Height = 5, Shell = 1,
                },
            ],
        };

        var result = PgmStudio.Export.MapExportComposer.BuildAndCompose([], IslandLayout, intent);

        await Assert.That(result.Refusal?.Findings.Any(finding => finding.Rule == "OB24") ?? false).IsFalse()
            .Because($"goals twenty blocks apart share no blocks: {result.Refusal?.Message}");
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
    public async Task A_kitless_obsidian_goal_is_not_refused_for_its_kit_OB18_was_retired()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await CreateFinishedSketchAsync(client, IslandLayout);

        // No spawns at all: TeamsGenerator only derives a kit when the map has one (GenerateKits gates on
        // Spawns.Count > 0), so the exported document's kits carry no pickaxe whatsoever. Before B134 this
        // was the kitless destroy map MG18/OB18 refused as "unwinnable"; an iron pickaxe (or no pickaxe at
        // all) still breaks obsidian, it just does not drop it, so the goal is winnable and the map exports.
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

        // The map is refused, and which rule refuses it is what this test is about. With no spawn and no
        // observer there is nobody to put in it, which EX2 (B140) now says outright — but nothing here refuses
        // on kit grounds, and that retirement is what is being pinned. Before B134 the same document came back
        // as an unwinnable destroy map because its kits carried no diamond pickaxe.
        var resp = await client.GetAsync($"/api/map/{slug}/xml");
        var body = await resp.Content.ReadAsStringAsync();
        await Assert.That(body).DoesNotContain("unwinnable");
        await Assert.That(body).DoesNotContain("pickaxe");

        var refused = JsonSerializer.Deserialize<JsonElement>(body);
        await Assert.That(refused.GetProperty("findings").EnumerateArray()
            .Select(finding => finding.GetProperty("rule").GetString())).IsEquivalentTo(new[] { "EX2" });
    }

    /// <summary>
    /// <b><c>OB19</c> drops the tree and exports the map.</b> A goal is what the map is for, so nothing may
    /// stand on the ground it is read against — and a prop is removable, which is what makes this a decline
    /// rather than a refusal: the pass leaves the tree out, the finding names it, and the world ships. The
    /// author hears it in <c>Pgm-Warnings</c> on the way out and in <c>warnings</c> from the preview that
    /// runs the same pass, instead of a 409 at the door with nothing built.
    /// </summary>
    [Test]
    public async Task OB19_declines_a_tree_standing_inside_a_goals_clearance()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await CreateFinishedSketchAsync(client, IslandLayoutWithTree);

        // Well clear of the tree/goal cluster at (10,10) — this map is testing OB19 alone.
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

        var export = await client.GetAsync($"/api/map/{slug}/export");
        await Assert.That(export.IsSuccessStatusCode).IsTrue().Because(await export.Content.ReadAsStringAsync());

        // One line on the way out: the count, then the rule an author looks up. It stays OB19 rather than
        // becoming a DR-* — the rule that names the goal, not the one that names the ground.
        await Assert.That(export.Headers.TryGetValues("Pgm-Warnings", out var warnings)).IsTrue();
        await Assert.That(warnings!.Single()).IsEqualTo("1 OB19");

        // And in full from the preview that runs the same pass, with the prop's own id as the subject the
        // canvas highlights.
        var columns = await client.PostAsync($"/api/map/{slug}/sketch/columns",
            new StringContent(IslandLayoutWithTree, Encoding.UTF8, "application/json"));
        await Assert.That(columns.IsSuccessStatusCode).IsTrue();
        var declined = (await columns.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("warnings");
        var ob19 = declined.EnumerateArray().Single(f => f.GetProperty("rule").GetString() == "OB19");
        await Assert.That(ob19.GetProperty("severity").GetString()).IsEqualTo("decline");
        await Assert.That(ob19.GetProperty("message").GetString()).Contains("tree 't1'");
        await Assert.That(ob19.GetProperty("subjects").EnumerateArray().Select(s => s.GetString())).Contains("t1");
    }

    /// <summary>The control: the same shape of map as the refusals above, minus the fault each one looks
    /// for, still exports clean — proving the gate does not reject a map that was never wrong.</summary>
    [Test]
    public async Task A_map_with_none_of_the_gated_faults_still_exports()
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
