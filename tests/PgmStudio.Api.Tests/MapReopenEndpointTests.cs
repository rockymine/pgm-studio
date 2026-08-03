using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinqToDB;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Tests;

/// <summary>
/// POST /api/map/{slug}/reopen — sending a map back to the tool it was drawn in. The stage pointer only
/// moves forward on its own (sketch-finish advances it to configure), so reopening is what makes a
/// finished draft selectable in the Sketch tool again. Eligibility is the authoring artifact, not the
/// stage: an imported world has no drawn source and is refused. Runs against the
/// <c>pgm_studio_test</c> schema (override with <c>PGM_STUDIO_TEST_DB</c>); each test resets the schema,
/// so they run serially.
/// </summary>
[NotInParallel("api-db")]
public sealed class MapReopenEndpointTests
{
    [Test]
    public async Task A_finished_sketch_reopens_into_the_sketch_stage()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var slug = await FinishedSketchAsync(client);

        // Finished → it lists under configure, carrying the stage it can go back to.
        var configuring = (await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=configure"))!;
        await Assert.That(configuring.Single(m => m.Slug == slug).ReopenStage).IsEqualTo(MapStage.Sketch);

        var reopen = await client.PostAsync($"/api/map/{slug}/reopen", null);
        await Assert.That(reopen.IsSuccessStatusCode).IsTrue();
        var body = await reopen.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("stage").GetString()).IsEqualTo(MapStage.Sketch);
        await Assert.That(body.GetProperty("url").GetString()).IsEqualTo($"/maps/{slug}/sketch");

        // It is selectable in the Sketch tool again, and gone from Configuring.
        var sketches = (await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=sketch"))!;
        await Assert.That(sketches.Any(m => m.Slug == slug)).IsTrue();
        var stillConfiguring = (await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=configure"))!;
        await Assert.That(stillConfiguring.Any(m => m.Slug == slug)).IsFalse();

        // The drawn layout survives the round trip — reopening moves the stage pointer, nothing else.
        var layout = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/sketch");
        await Assert.That(layout.GetProperty("layout").GetProperty("shapes").GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task An_imported_map_has_nothing_to_reopen_into()
    {
        await ApiTestFactory.ResetSchemaAsync();
        // An import writes geometry (and a derived island outline) but no authored sketch or plan.
        await using (var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString)))
        {
            var mapId = await new MapRepository(db).InsertAsync(new MapRow
            {
                Slug = "imported", Name = "imported", Gamemode = "ctw", Stage = MapStage.Configure,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.InsertAsync(new MapArtifactRow { MapId = mapId, Kind = ArtifactKind.LayerParquet, Data = [1] });
            await db.InsertAsync(new MapArtifactRow { MapId = mapId, Kind = ArtifactKind.IslandSketchJson, Data = "{}"u8.ToArray() });
        }

        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        // A derived island outline is not an authoring source, so the row offers no reopen …
        var configuring = (await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=configure"))!;
        await Assert.That(configuring.Single(m => m.Slug == "imported").ReopenStage).IsNull();

        // … and the endpoint refuses rather than staging a map with nothing to draw.
        var reopen = await client.PostAsync("/api/map/imported/reopen", null);
        await Assert.That((int)reopen.StatusCode).IsEqualTo(422);

        var sketches = (await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=sketch"))!;
        await Assert.That(sketches.Any(m => m.Slug == "imported")).IsFalse();
    }

    [Test]
    public async Task An_authored_plan_reopens_into_the_plan_stage()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/plan", new { name = "Authored" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        // Push it forward the way a later stage advance would, leaving only the plan blob behind.
        await ApiTestFactory.ExecuteAsync($"UPDATE map SET stage = '{MapStage.Configure}' WHERE slug = '{slug}'");

        var reopen = await client.PostAsync($"/api/map/{slug}/reopen", null);
        await Assert.That(reopen.IsSuccessStatusCode).IsTrue();
        var body = await reopen.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("stage").GetString()).IsEqualTo(MapStage.Plan);

        var plans = (await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=plan"))!;
        await Assert.That(plans.Any(m => m.Slug == slug)).IsTrue();
    }

    [Test]
    public async Task A_map_in_its_only_authoring_stage_has_nowhere_to_reopen_into()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Still Drawing" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        // It is already open in the tool that owns its one source, so the list offers no action …
        var sketches = (await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=sketch"))!;
        await Assert.That(sketches.Single(m => m.Slug == slug).ReopenStage).IsNull();
        // … and the endpoint says the same.
        var reopen = await client.PostAsync($"/api/map/{slug}/reopen", null);
        await Assert.That((int)reopen.StatusCode).IsEqualTo(422);
    }

    [Test]
    public async Task A_plan_built_onto_its_own_map_reaches_both_of_its_tools()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        // A plan map that was built in place: it keeps its plan blob and gains the layout it compiled
        // into, which is what the plan editor's map-backed build leaves behind.
        var slug = (await (await client.PostAsJsonAsync("/api/plan", new { name = "Built In Place" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        await SketchLayoutAsync(client, slug);
        await Assert.That((await client.PostAsync($"/api/map/{slug}/sketch/finish", null)).IsSuccessStatusCode).IsTrue();

        // From Configuring the sketch is the way back — the later of its two sources.
        var configuring = (await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=configure"))!;
        await Assert.That(configuring.Single(m => m.Slug == slug).ReopenStage).IsEqualTo(MapStage.Sketch);
        var toSketch = await client.PostAsync($"/api/map/{slug}/reopen", null);
        await Assert.That((await toSketch.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("stage").GetString()).IsEqualTo(MapStage.Sketch);

        // Standing in the sketch, the plan it was compiled from is the one left to go back to.
        var sketches = (await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=sketch"))!;
        await Assert.That(sketches.Single(m => m.Slug == slug).ReopenStage).IsEqualTo(MapStage.Plan);
        var toPlan = await client.PostAsync($"/api/map/{slug}/reopen", null);
        await Assert.That((await toPlan.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("stage").GetString()).IsEqualTo(MapStage.Plan);

        var plans = (await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=plan"))!;
        await Assert.That(plans.Any(m => m.Slug == slug)).IsTrue();
    }

    [Test]
    public async Task Reopening_an_unknown_map_is_a_404()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var reopen = await client.PostAsync("/api/map/no-such-map/reopen", null);
        await Assert.That(reopen.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    /// <summary>A drawn sketch carried through finish: two disjoint rectangles (the CTW two-island
    /// minimum), rasterized, so the map sits at stage configure with its layout kept.</summary>
    private static async Task<string> FinishedSketchAsync(HttpClient client)
    {
        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Drawn" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        await SketchLayoutAsync(client, slug);
        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue();
        return slug;
    }

    /// <summary>Write a two-island layout onto a map, whatever stage it is in.</summary>
    private static async Task SketchLayoutAsync(HttpClient client, string slug)
    {
        await client.PutAsJsonAsync($"/api/map/{slug}/sketch", new
        {
            setup = new { mirror_mode = "mirror_x", center = new { cx = 0, cz = 0 } },
            layout = new
            {
                shapes = new object[]
                {
                    new { id = "a", type = "rectangle", operation = "add", @override = false, min_x = -40, max_x = -20, min_z = -10, max_z = 10 },
                    new { id = "b", type = "rectangle", operation = "add", @override = false, min_x = 20, max_x = 40, min_z = -10, max_z = 10 },
                },
                islands = new object[]
                {
                    new { id = "i1", name = "West", mirrors = false, shapeIds = new[] { "a" } },
                    new { id = "i2", name = "East", mirrors = false, shapeIds = new[] { "b" } },
                },
            },
        });
    }
}
