using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Authoring a capture board over HTTP — the surface an agent actually drives, end to end. There is no new
/// endpoint for it: <c>PUT /map/{slug}/intent</c> takes the whole intent, and a KotH board is that intent
/// carrying <c>controlPoints</c>.
///
/// <para>The flow is the one the <c>pgm-board</c> skill runs — compile a plan, store its layout, finish it,
/// then state the intent — with the hills stated on that last call. Two things are proved here that no unit
/// test reaches: the wire shape an agent posts is the one the record deserializes from, and the map that
/// comes back out of <c>GET …/xml</c> is one PGM would load and read as KotH.</para>
///
/// <para>The board's boxes are resolved by the <b>world build</b>, which runs at export rather than at the
/// intent write — the same as a destroyable's or a core's. So a point is a point in the exported map, not in
/// the document a bare intent PUT projects, and the assertions are made where the answer is.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class ControlPointIntentTests
{
    // What an agent adds to a compiled board: one point dead centre and one to each side, which is the
    // author's rule (docs/gameplay/approaches.md).
    //
    // Every point is stated here rather than one side being left to the orbit, because a <b>compiled</b>
    // intent carries no symmetry — the plan compiler has already fanned the board, and the field is left
    // unset on purpose (IntentCarry). An intent that does carry one fans its points like any other unit
    // (ControlPointSymmetryTests); a compiled one states them. Making the plan itself place them is TC8.
    private const string Points = """
        [
          { "name": "North",  "anchor": { "x": 0, "y": 0, "z": -24 }, "size": 7, "points": 1 },
          { "name": "Middle", "anchor": { "x": 0, "y": 0, "z": 0 } },
          { "name": "South",  "anchor": { "x": 0, "y": 0, "z": 24 }, "size": 7, "points": 1 }
        ]
        """;

    private static async Task<(HttpClient Client, string Slug)> HillBoardAsync(string? points = Points)
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();

        var compile = await client.PostAsync("/api/plan/compile",
            new StringContent(ReadSeed("base-2wool.plan.json"), Encoding.UTF8, "application/json"));
        await Assert.That(compile.IsSuccessStatusCode).IsTrue().Because(await compile.Content.ReadAsStringAsync());
        var compiled = await compile.Content.ReadFromJsonAsync<JsonElement>();

        var create = await client.PostAsJsonAsync("/api/sketch", new { name = "Hill Board" });
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var layout = await client.PutAsync($"/api/map/{slug}/sketch",
            new StringContent(compiled.GetProperty("layout").GetRawText(), Encoding.UTF8, "application/json"));
        await Assert.That(layout.IsSuccessStatusCode).IsTrue().Because(await layout.Content.ReadAsStringAsync());

        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue().Because(await finish.Content.ReadAsStringAsync());

        var stored = await client.PutAsync($"/api/map/{slug}/intent",
            new StringContent(WithPoints(compiled.GetProperty("intent"), points), Encoding.UTF8, "application/json"));
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        return (client, slug);
    }

    /// <summary>The compiled intent with the hills stated on it, and an author so the header board is not
    /// blank (`EX6`). This is the one call an agent makes differently to author a capture board.</summary>
    private static string WithPoints(JsonElement intent, string? points)
    {
        var node = JsonNode.Parse(intent.GetRawText())!.AsObject();
        var meta = node["meta"]?.AsObject() ?? [];
        meta["authors"] = new JsonArray(new JsonObject { ["name"] = "rockymine" });
        node["meta"] = meta;
        if (points is not null) node["controlPoints"] = JsonNode.Parse(points);
        return node.ToJsonString();
    }

    private static async Task<string> XmlAsync(HttpClient client, string slug)
    {
        var response = await client.GetAsync($"/api/map/{slug}/xml");
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(response.IsSuccessStatusCode).IsTrue().Because(body);
        return body;
    }

    /// <summary>The intent an agent posted is the intent the studio stores, the points included.</summary>
    [Test]
    public async Task The_posted_points_are_stored_on_the_intent()
    {
        var (client, slug) = await HillBoardAsync();
        using var _ = client;

        var intent = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/intent");
        var points = intent.GetProperty("controlPoints").EnumerateArray().ToList();
        await Assert.That(points.Count).IsEqualTo(3);
        await Assert.That(points[1].GetProperty("name").GetString()).IsEqualTo("Middle");
        await Assert.That(points[0].GetProperty("size").GetInt32()).IsEqualTo(7);
    }

    /// <summary>
    /// The whole claim: a board an agent stated as three points exports as a three-hill KotH map, and every
    /// part of it PGM needs is there.
    /// </summary>
    [Test]
    public async Task The_board_exports_as_a_three_hill_koth_map()
    {
        var (client, slug) = await HillBoardAsync();
        using var _ = client;

        var map = MapParser.ParseXmlString(await XmlAsync(client, slug));

        await Assert.That(map.ControlPoints.Count).IsEqualTo(3);
        await Assert.That(map.Gamemodes).Contains("koth");
        // Without a <score> element PGM builds no score module and every point pays nothing, all match.
        await Assert.That(map.Score!.Limit).IsEqualTo(ObjectiveDefaults.ControlPointScoreLimit);

        foreach (var point in map.ControlPoints)
        {
            // PGM defaults `required` to true, and a point that keeps it ends the match on first capture.
            await Assert.That(point.Required).IsFalse();
            await Assert.That(point.Element).IsEqualTo(ControlPointElement.King);
            // All three regions resolve, or the point is played and displayed through nothing. The owner
            // region is the sky marker: white until somebody holds the point, the holder's dye after.
            await Assert.That(map.Regions.ContainsKey(point.CaptureRegionId)).IsTrue();
            await Assert.That(map.Regions.ContainsKey(point.ProgressRegionId)).IsTrue();
            await Assert.That(map.Regions.ContainsKey(point.OwnerRegionId)).IsTrue();
            await Assert.That(map.Regions[point.OwnerRegionId].MinY!.Value)
                .IsGreaterThan(map.Regions[point.CaptureRegionId].MaxY!.Value);
        }
        await Assert.That(map.ControlPoints.Select(p => p.Name))
            .IsEquivalentTo(new[] { "North", "Middle", "South" });
    }

    /// <summary>A board stating no points is the board it was before — the slice is opt-in, and a wool map
    /// does not grow a score module by having the field exist.</summary>
    [Test]
    public async Task A_board_with_no_points_exports_unchanged()
    {
        var (client, slug) = await HillBoardAsync(points: null);
        using var _ = client;

        var map = MapParser.ParseXmlString(await XmlAsync(client, slug));
        await Assert.That(map.ControlPoints).IsEmpty();
        await Assert.That(map.Score).IsNull();
        await Assert.That(map.Gamemodes).DoesNotContain("koth");
    }

    private static string ReadSeed(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tools", "seeds", file);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException($"seed not found: {file}");
    }
}
