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
[NotInParallel]
public sealed class SketchEndpointTests
{
    [Test]
    public async Task Create_returns_a_slug_and_seeds_an_empty_layout()
    {
        await ResetSchemaAsync();
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

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

    [Test]
    public async Task Create_with_a_frame_seeds_the_working_setup()
    {
        await ResetSchemaAsync();
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

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
        await ResetSchemaAsync();
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Round Trip" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var put = await client.PutAsJsonAsync($"/api/map/{slug}/sketch", new
        {
            setup = new { mirror_mode = "mirror_x", center = new { cx = 0, cz = 0 } },
            layout = new
            {
                shapes = new object[] { new { id = "s1", type = "rectangle", operation = "add", @override = false, min_x = -20, max_x = 20, min_z = -20, max_z = 20 } },
                islands = new object[] { new { id = "i1", name = "North", mirrors = true, shapeIds = new[] { "s1" } } },
            },
        });
        await Assert.That(put.IsSuccessStatusCode).IsTrue();

        var got = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/sketch");
        await Assert.That(got.GetProperty("setup").GetProperty("mirror_mode").GetString()).IsEqualTo("mirror_x");
        var shapes = got.GetProperty("layout").GetProperty("shapes");
        await Assert.That(shapes.GetArrayLength()).IsEqualTo(1);
        await Assert.That(shapes[0].GetProperty("id").GetString()).IsEqualTo("s1");
        await Assert.That(got.GetProperty("layout").GetProperty("islands")[0].GetProperty("name").GetString()).IsEqualTo("North");
    }

    [Test]
    public async Task Create_dedupes_the_slug()
    {
        await ResetSchemaAsync();
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var first = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Dup" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();
        var second = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Dup" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();

        await Assert.That(first).IsEqualTo("dup");
        await Assert.That(second).IsEqualTo("dup-2");
    }

    [Test]
    public async Task Generate_creates_a_draft_with_a_framed_origin_centred_layout()
    {
        await ResetSchemaAsync();
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/sketch/generate", new { name = "Gen H", archetype = "H", seed = 1 });
        await Assert.That(resp.IsSuccessStatusCode).IsTrue();
        var slug = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        await Assert.That(slug).IsEqualTo("gen-h");

        // GET returns a real generated layout: framed, origin-centred, with shapes + islands.
        var layout = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/sketch");
        var setup = layout.GetProperty("setup");
        await Assert.That(setup.GetProperty("center").GetProperty("cx").GetDouble()).IsEqualTo(0);
        await Assert.That(setup.TryGetProperty("bbox", out _)).IsTrue();
        await Assert.That(layout.GetProperty("layout").GetProperty("shapes").GetArrayLength()).IsGreaterThan(0);
        await Assert.That(layout.GetProperty("layout").GetProperty("islands").GetArrayLength()).IsGreaterThan(0);
    }

    [Test]
    public async Task Generate_stages_emits_the_pipeline_intermediates_deterministically()
    {
        await ResetSchemaAsync();
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/sketch/generate/stages", new { seed = 42, wools = 3 });
        await Assert.That(resp.IsSuccessStatusCode).IsTrue();
        var s = await resp.Content.ReadFromJsonAsync<JsonElement>();

        await Assert.That(s.GetProperty("seed").GetInt32()).IsEqualTo(42);
        await Assert.That(s.GetProperty("noise").GetProperty("values").GetArrayLength()).IsGreaterThan(0);
        await Assert.That(s.GetProperty("hub").GetProperty("r").GetDouble()).IsGreaterThan(0);
        await Assert.That(s.GetProperty("woolTips").GetArrayLength()).IsEqualTo(3);
        await Assert.That(s.GetProperty("spines").GetArrayLength()).IsGreaterThan(0);
        await Assert.That(s.GetProperty("shapes").GetArrayLength()).IsGreaterThan(0);
        await Assert.That(s.GetProperty("mirrorMode").GetString()).IsEqualTo("mirror_z");

        // same seed → identical payload (the generator is deterministic; no map is created)
        var again = await (await client.PostAsJsonAsync("/api/sketch/generate/stages", new { seed = 42, wools = 3 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(again.GetRawText()).IsEqualTo(s.GetRawText());
    }

    [Test]
    public async Task Put_rejects_non_json()
    {
        await ResetSchemaAsync();
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Bad" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var resp = await client.PutAsync($"/api/map/{slug}/sketch", new StringContent("not json", Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Finish_rasterizes_the_layout_and_advances_the_map_to_configure()
    {
        await ResetSchemaAsync();
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Finish Me" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        // Two disjoint rectangles (x-gap from -20 to 20) → two islands, the CTW minimum. mirrors:false so
        // the layout stands on its own without a mirror copy.
        var put = await client.PutAsJsonAsync($"/api/map/{slug}/sketch", new
        {
            setup = new { mirror_mode = "mirror_x", center = new { cx = 1000, cz = 0 } },
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
        var stillSketch = await client.GetFromJsonAsync<List<MapSummary>>("/api/maps?stage=sketch");
        await Assert.That(stillSketch!.Any(m => m.Slug == slug)).IsFalse();
    }

    [Test]
    public async Task Finish_rejects_a_layout_with_fewer_than_two_islands()
    {
        await ResetSchemaAsync();
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "One Island" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        // A single rectangle → one island; a CTW needs both sides, so finish must refuse.
        await client.PutAsJsonAsync($"/api/map/{slug}/sketch", new
        {
            setup = new { mirror_mode = "mirror_x", center = new { cx = 1000, cz = 0 } },
            layout = new
            {
                shapes = new object[] { new { id = "a", type = "rectangle", operation = "add", @override = false, min_x = 0, max_x = 20, min_z = 0, max_z = 20 } },
                islands = new object[] { new { id = "i1", name = "Solo", mirrors = false, shapeIds = new[] { "a" } } },
            },
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
        await ResetSchemaAsync();
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

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

    private static string TestConnectionString =>
        Environment.GetEnvironmentVariable("PGM_STUDIO_TEST_DB")
        ?? "Server=localhost;Database=pgm_studio_test;User ID=pgm;Password=pgm_dev_pw;";

    private static async Task ResetSchemaAsync()
    {
        await using (var conn = new MySqlConnection(TestConnectionString))
        {
            await conn.OpenAsync();
            var tables = new List<string>();
            await using (var cmd = new MySqlCommand(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE()", conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    tables.Add(reader.GetString(0));
            if (tables.Count > 0)
            {
                await Exec(conn, "SET FOREIGN_KEY_CHECKS=0");
                foreach (var t in tables) await Exec(conn, $"DROP TABLE IF EXISTS `{t}`");
                await Exec(conn, "SET FOREIGN_KEY_CHECKS=1");
            }
        }
        SchemaMigrator.MigrateUp(TestConnectionString);
    }

    private static async Task Exec(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:PgmStudio", TestConnectionString);
        }
    }
}
