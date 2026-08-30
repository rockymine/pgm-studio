using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MySqlConnector;
using PgmStudio.Contracts;
using PgmStudio.Migrations;

namespace PgmStudio.Api.Tests;

/// <summary>
/// S2d: the sketch persistence endpoints. POST /api/sketch creates a draft map (slugified, deduped) + its
/// layout artifact — empty {} for a frameless body, or seeded with a working frame's setup when one is
/// posted; PUT/GET /api/map/{slug}/sketch round-trips the JS-origin layout blob.
/// Runs against the <c>pgm_studio_test</c> schema (override with <c>PGM_STUDIO_TEST_DB</c>); each test
/// resets the schema, so they run serially.
/// </summary>
[NotInParallel("api-db")]
public sealed class SketchEndpointTests
{
    [Test]
    public async Task Create_returns_a_slug_and_seeds_an_empty_layout()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/sketch", new { name = "My Sketch" });
        await Assert.That(resp.IsSuccessStatusCode).IsTrue();
        var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(created.GetProperty("slug").GetString()).IsEqualTo("my-sketch");

        // Freshly created with no frame → the layout artifact is an empty object (the editor falls back to
        // its landscape default on load).
        var layout = await client.GetFromJsonAsync<JsonElement>("/api/map/my-sketch/sketch");
        await Assert.That(layout.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(layout.EnumerateObject().Any()).IsFalse();
    }

    /// <summary><b>A drawing in progress is stored whatever its geometry says, and the preview draws it.</b>
    /// An edit is not atomic: putting a floor under a hole and then removing the hole is an ordinary order to
    /// work in, and so is the reverse. A store that refuses the intermediate state does not prevent the board,
    /// it deletes the shapes the author drew to get there — and a refused PUT is a completed round-trip that
    /// throws nothing, so the tool cannot even see that it happened. <c>finish</c> is where the same check
    /// becomes fatal, and this pins all three ends of that.</summary>
    [Test]
    public async Task A_board_its_own_gate_refuses_is_stored_and_drawn_and_refused_only_at_finish()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Mid edit" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        // A mass with a hole in it, and a second layer putting the ground back under the hole — SK13, the
        // fault an author meets halfway through carving a room.
        const string refused = """
            {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
             "layers":[
               {"id":"rock","base_y":0,"layout":{"shapes":[
                  {"id":"mass","type":"rectangle","operation":"add","min_x":-15,"min_z":-10,"max_x":15,"max_z":10,"floor":0,"base_height":30},
                  {"id":"hole","type":"rectangle","operation":"subtract","min_x":-2,"min_z":-6,"max_x":10,"max_z":6,"floor":0}],
                "groups":[{"id":"g","mirrors":false,"shapeIds":["mass","hole"]}]}},
               {"id":"floor","base_y":0,"layout":{"shapes":[
                  {"id":"f","type":"rectangle","operation":"add","override":true,"min_x":-2,"min_z":-6,"max_x":10,"max_z":6,"floor":0,"base_height":4}],
                "groups":[{"id":"h","mirrors":false,"shapeIds":["f"]}]}}]}
            """;
        var body = new StringContent(refused, Encoding.UTF8, "application/json");

        // Stored, with the finding carried rather than thrown away …
        var put = await client.PutAsync($"/api/map/{slug}/sketch", body);
        await Assert.That(put.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(put.Headers.TryGetValues("Pgm-Warnings", out var warned)).IsTrue();
        await Assert.That(string.Join(" ", warned!)).Contains("SK13");

        // … and it is really there: both layers survive the round-trip, which is the whole point.
        var back = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/sketch");
        await Assert.That(back.GetProperty("layers").GetArrayLength()).IsEqualTo(2);

        // The 3-D read draws it rather than going dark — the algebra never fails, only the gate did.
        var columns = await client.PostAsync($"/api/map/{slug}/sketch/columns",
            new StringContent(refused, Encoding.UTF8, "application/json"));
        await Assert.That(columns.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var drawn = await columns.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(drawn.GetProperty("cols").GetArrayLength()).IsGreaterThan(0);

        // And finish is where it stops.
        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
        var refusal = await finish.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(refusal.GetProperty("message").GetString()).Contains("takes away");
    }

    [Test]
    public async Task The_3D_preview_answers_a_clean_board_with_no_warnings_key()
    {
        // The regression this exists for: the declines the preview carries are null when nothing was
        // declined, and spreading that null threw straight into the endpoint's catch-all — so every board
        // with nothing wrong answered 400 "could not build layout" and the 3-D preview went blank. Nothing
        // in this suite posted to /sketch/columns, which is why it shipped.
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Clean board" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        const string layout = """
            {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
             "layers":[{"base_y":0,"layout":{
               "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                          "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
               "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]}
            """;
        var body = new StringContent(layout, Encoding.UTF8, "application/json");

        var columns = await client.PostAsync($"/api/map/{slug}/sketch/columns", body);
        await Assert.That(columns.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var payload = await columns.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(payload.GetProperty("cols").GetArrayLength()).IsGreaterThan(0);
        // A clean board says nothing, so the key is absent: `warnings` appears when there is something in it
        // and never otherwise, which is what makes an absent one readable as "nothing was complained about".
        await Assert.That(payload.TryGetProperty("warnings", out _)).IsFalse();
    }

    [Test]
    public async Task Create_with_a_frame_seeds_the_working_setup()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        // A portrait footprint (80×120) off-centre, mirror-Z — the new-sketch page's blank-create body.
        var resp = await client.PostAsJsonAsync("/api/sketch",
            new { name = "Framed", width = 80, depth = 120, mode = "mirror_z", centerX = 4, centerZ = -2 });
        var slug = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        // GET returns a setup whose origin-centred bbox is width×depth and whose centre/mode round-trip.
        var setup = (await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/sketch")).GetProperty("setup");
        await Assert.That(setup.GetProperty("mirror_mode").GetString()).IsEqualTo("mirror_z");
        var bbox = setup.GetProperty("bbox");
        await Assert.That(bbox.GetProperty("min_x").GetDouble()).IsEqualTo(-40);
        await Assert.That(bbox.GetProperty("max_x").GetDouble()).IsEqualTo(40);
        await Assert.That(bbox.GetProperty("min_z").GetDouble()).IsEqualTo(-60);
        await Assert.That(bbox.GetProperty("max_z").GetDouble()).IsEqualTo(60);
        var center = setup.GetProperty("center");
        await Assert.That(center.GetProperty("cx").GetDouble()).IsEqualTo(4);
        await Assert.That(center.GetProperty("cz").GetDouble()).IsEqualTo(-2);
    }

    [Test]
    public async Task Layout_round_trips_through_put_then_get()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Round Trip" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var put = await client.PutAsJsonAsync($"/api/map/{slug}/sketch", new
        {
            setup = new { mirror_mode = "mirror_x", center = new { cx = 0, cz = 0 } },
            layers = new object[] { new { id = "ground", base_y = 0, layout = new
            {
                shapes = new object[] { new { id = "s1", type = "rectangle", operation = "add", @override = false, min_x = -20, max_x = 20, min_z = -20, max_z = 20 } },
                groups = new object[] { new { id = "i1", name = "North", mirrors = true, shapeIds = new[] { "s1" } } },
            } } },
        });
        await Assert.That(put.IsSuccessStatusCode).IsTrue();

        var got = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/sketch");
        await Assert.That(got.GetProperty("setup").GetProperty("mirror_mode").GetString()).IsEqualTo("mirror_x");
        var ground = got.GetProperty("layers")[0].GetProperty("layout");
        var shapes = ground.GetProperty("shapes");
        await Assert.That(shapes.GetArrayLength()).IsEqualTo(1);
        await Assert.That(shapes[0].GetProperty("id").GetString()).IsEqualTo("s1");
        await Assert.That(ground.GetProperty("groups")[0].GetProperty("name").GetString()).IsEqualTo("North");
    }

    [Test]
    public async Task Create_dedupes_the_slug()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var first = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Dup" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();
        var second = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Dup" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();

        await Assert.That(first).IsEqualTo("dup");
        await Assert.That(second).IsEqualTo("dup-2");
    }

    [Test]
    public async Task Put_rejects_non_json()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Bad" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var resp = await client.PutAsync($"/api/map/{slug}/sketch", new StringContent("not json", Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Finish_rasterizes_the_layout_and_advances_the_map_to_configure()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Finish Me" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        // Two disjoint rectangles (x-gap from -20 to 20) → two islands, the CTW minimum. mirrors:false so
        // the layout stands on its own without a mirror copy.
        var put = await client.PutAsJsonAsync($"/api/map/{slug}/sketch", new
        {
            setup = new { mirror_mode = "mirror_x", center = new { cx = 1000, cz = 0 } },
            layers = new object[] { new { id = "ground", base_y = 0, layout = new
            {
                shapes = new object[]
                {
                    new { id = "a", type = "rectangle", operation = "add", @override = false, min_x = -40, max_x = -20, min_z = -10, max_z = 10 },
                    new { id = "b", type = "rectangle", operation = "add", @override = false, min_x = 20, max_x = 40, min_z = -10, max_z = 10 },
                },
                groups = new object[]
                {
                    new { id = "i1", name = "West", mirrors = false, shapeIds = new[] { "a" } },
                    new { id = "i2", name = "East", mirrors = false, shapeIds = new[] { "b" } },
                },
            } } },
        });
        await Assert.That(put.IsSuccessStatusCode).IsTrue();

        // Before finishing the map sits in the Sketch stage.
        var sketchStaged = await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=sketch");
        await Assert.That(sketchStaged!.Any(m => m.Slug == slug)).IsTrue();

        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue();
        var body = await finish.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("slug").GetString()).IsEqualTo(slug);
        await Assert.That(body.GetProperty("configureUrl").GetString()).IsEqualTo($"/maps/{slug}/configure");

        // Rasterize → geometry written → the draft has advanced into the Configure wizard.
        var configureStaged = await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=configure");
        await Assert.That(configureStaged!.Any(m => m.Slug == slug)).IsTrue();

        // And it stays in the Sketches list, because it still holds the sketch it was drawn in. Finishing
        // moves where the map stands, not what it is made of — dropping it out of Sketches is what used to
        // leave the drawing unreachable from the tool that made it.
        var stillSketch = await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=sketch");
        var entry = stillSketch!.SingleOrDefault(m => m.Slug == slug);
        await Assert.That(entry).IsNotNull();
        await Assert.That(entry!.Stage).IsEqualTo("configure");
        await Assert.That(entry.HasSketch).IsTrue();
        await Assert.That(entry.HasSurface).IsTrue();   // the world the wizard configures
        await Assert.That(entry.HasPlan).IsFalse();     // this one was drawn, not planned
    }

    /// <summary>One connected landmass is a map. An island is not a side: 17% of the destroy-the-monument
    /// corpus is a single island and 26% carries a single major one, and the layout generator's own boards
    /// compile to exactly one — so a two-island floor refused the commonest shape in the category, and the
    /// generator's own output with it. Symmetry says whether a board has two sides, and it is stated in the
    /// setup rather than counted in the ground.</summary>
    [Test]
    public async Task Finish_accepts_a_single_island()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "One Island" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        await client.PutAsJsonAsync($"/api/map/{slug}/sketch", new
        {
            setup = new { mirror_mode = "mirror_x", center = new { cx = 1000, cz = 0 } },
            layers = new object[] { new { id = "ground", base_y = 0, layout = new
            {
                shapes = new object[] { new { id = "a", type = "rectangle", operation = "add", @override = false, min_x = 0, max_x = 20, min_z = 0, max_z = 20 } },
                groups = new object[] { new { id = "i1", name = "Solo", mirrors = false, shapeIds = new[] { "a" } } },
            } } },
        });

        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue()
            .Because("a board both teams stand on is one island, which is a map and not a half-drawn one");

        // It advanced: the draft now carries geometry, so it is what the wizard configures.
        var configureStaged = await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=configure");
        await Assert.That(configureStaged!.Any(m => m.Slug == slug)).IsTrue();
    }

    /// <summary>Nothing drawn is the one thing finish still refuses — there is no ground to rasterize.</summary>
    [Test]
    public async Task Finish_rejects_a_layout_with_no_ground()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Nothing Drawn" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        await client.PutAsJsonAsync($"/api/map/{slug}/sketch", new
        {
            setup = new { mirror_mode = "mirror_x", center = new { cx = 1000, cz = 0 } },
            layers = new object[] { new { id = "ground", base_y = 0,
                layout = new { shapes = Array.Empty<object>(), groups = Array.Empty<object>() } } },
        });

        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That((int)finish.StatusCode).IsEqualTo(422);

        // It stays in Sketch — a rejected finish must not advance the stage.
        var sketchStaged = await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=sketch");
        await Assert.That(sketchStaged!.Any(m => m.Slug == slug)).IsTrue();
    }

    [Test]
    public async Task Compiled_plan_drives_the_full_create_layout_finish_intent_loop()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        // Compile a seed plan into the pair the pipeline consumes (the editor's Compile step).
        var compile = await client.PostAsync("/api/plan/compile",
            new StringContent(ReadSeed("base-2wool.plan.json"), Encoding.UTF8, "application/json"));
        await Assert.That(compile.IsSuccessStatusCode).IsTrue();
        var compiled = await compile.Content.ReadFromJsonAsync<JsonElement>();
        var layoutJson = compiled.GetProperty("layout").GetRawText();
        var intentJson = compiled.GetProperty("intent").GetRawText();

        // Drive the walk-test chain the client runs, asserting 2xx at each step.
        var create = await client.PostAsJsonAsync("/api/sketch", new { name = "Compiled Loop" });
        await Assert.That(create.IsSuccessStatusCode).IsTrue();
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var layout = await client.PutAsync($"/api/map/{slug}/sketch", new StringContent(layoutJson, Encoding.UTF8, "application/json"));
        await Assert.That(layout.IsSuccessStatusCode).IsTrue();

        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue();

        var intent = await client.PutAsync($"/api/map/{slug}/intent", new StringContent(intentJson, Encoding.UTF8, "application/json"));
        await Assert.That(intent.IsSuccessStatusCode).IsTrue();

        // The draft now exports a sketch-origin world ZIP.
        var export = await client.GetAsync($"/api/map/{slug}/export");
        await Assert.That(export.IsSuccessStatusCode).IsTrue();
        await Assert.That(export.Content.Headers.ContentType?.MediaType).IsEqualTo("application/zip");
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
        throw new FileNotFoundException($"seed {file} not found above the test binary");
    }

    // ── harness (self-contained, mirrors MetadataEndpointTests) ─────────────────────
}
