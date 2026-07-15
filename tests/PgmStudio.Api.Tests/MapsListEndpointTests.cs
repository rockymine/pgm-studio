using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MySqlConnector;
using LinqToDB;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Migrations;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The staged map collection: GET /api/maps?stage=… filters by lifecycle stage and GET
/// /api/maps/stage-counts tallies it, while the originating/finishing endpoints set the stage
/// (sketch-create → sketch, sketch-finish → configure). Runs against the <c>pgm_studio_test</c>
/// schema (override with <c>PGM_STUDIO_TEST_DB</c>); each test resets the schema, so they run serially.
/// </summary>
[NotInParallel("api-db")]
public sealed class MapsListEndpointTests
{
    [Test]
    public async Task A_created_sketch_is_stage_sketch_and_filters_and_counts()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Draft One" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        // Listed under sketch, with its stage; absent from edit.
        var sketches = await client.GetFromJsonAsync<JsonElement[]>("/api/maps?stage=sketch");
        await Assert.That(sketches!.Length).IsEqualTo(1);
        await Assert.That(sketches[0].GetProperty("slug").GetString()).IsEqualTo(slug);
        await Assert.That(sketches[0].GetProperty("stage").GetString()).IsEqualTo("sketch");

        var edits = await client.GetFromJsonAsync<JsonElement[]>("/api/maps?stage=edit");
        await Assert.That(edits!.Length).IsEqualTo(0);

        var counts = await client.GetFromJsonAsync<JsonElement>("/api/maps/stage-counts");
        await Assert.That(counts.GetProperty("sketch").GetInt32()).IsEqualTo(1);
        await Assert.That(counts.GetProperty("configure").GetInt32()).IsEqualTo(0);
        await Assert.That(counts.GetProperty("edit").GetInt32()).IsEqualTo(0);
    }

    [Test]
    public async Task Finishing_a_sketch_advances_it_to_configure()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Two Sides" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        // A shape fully on one side of the mirror axis → it plus its mirror are two disjoint islands,
        // which is the minimum a CTW map needs for finish to succeed.
        var put = await client.PutAsJsonAsync($"/api/map/{slug}/sketch", new
        {
            setup = new { mirror_mode = "mirror_x", center = new { cx = 0, cz = 0 } },
            layout = new
            {
                shapes = new object[] { new { id = "s1", type = "rectangle", operation = "add", @override = false, min_x = 10, max_x = 40, min_z = -20, max_z = 20 } },
                islands = new object[] { new { id = "i1", name = "East", mirrors = true, shapeIds = new[] { "s1" } } },
            },
        });
        await Assert.That(put.IsSuccessStatusCode).IsTrue();

        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue();

        var sketches = await client.GetFromJsonAsync<JsonElement[]>("/api/maps?stage=sketch");
        await Assert.That(sketches!.Length).IsEqualTo(0);
        var configuring = await client.GetFromJsonAsync<JsonElement[]>("/api/maps?stage=configure");
        await Assert.That(configuring!.Length).IsEqualTo(1);
        await Assert.That(configuring[0].GetProperty("slug").GetString()).IsEqualTo(slug);
    }

    [Test]
    public async Task Listed_gamemodes_come_from_the_objective_rows_not_the_declared_label()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using (var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString)))
        {
            var repo = new MapRepository(db);

            // Every map below declares the same label, so nothing the endpoint returns can have come from it.
            async Task<long> Map(string slug) => await repo.InsertAsync(new MapRow
            {
                Slug = slug, Name = slug, Version = "1.0.0", Gamemode = "ad",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });

            var wooled = await Map("a-wools");
            await db.InsertAsync(new WoolRow { MapId = wooled, WoolKey = "red", Color = "red", Team = "red-team" });

            var both = await Map("b-wools-and-destroyable");
            await db.InsertAsync(new WoolRow { MapId = both, WoolKey = "blue", Color = "blue", Team = "blue-team" });
            await db.InsertAsync(new DestroyableRow
            {
                MapId = both, DestroyableKey = "blue-obsidian", Name = "Blue Obsidian",
                Owner = "blue-team", Materials = "obsidian", Show = true,
            });
            // A phantom alongside the real one must not double-count DTM.
            await db.InsertAsync(new DestroyableRow
            {
                MapId = both, DestroyableKey = "build-floor", Name = "Build Floor",
                Owner = "blue-team", Materials = "glass", Show = false, ModeChanges = true,
            });

            // Phantoms only: a scripted block-swap is not a goal, so this map is not DTM — it is nothing.
            var phantomOnly = await Map("c-phantom-only");
            await db.InsertAsync(new DestroyableRow
            {
                MapId = phantomOnly, DestroyableKey = "build-floor", Name = "Build Floor",
                Owner = "red-team", Materials = "glass", Show = false, ModeChanges = true,
            });

            var cored = await Map("d-core");
            await db.InsertAsync(new CoreRow
            {
                MapId = cored, CoreKey = "red-core", Owner = "red-team", Material = "obsidian",
            });
        }

        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var maps = (await client.GetFromJsonAsync<JsonElement[]>("/api/maps"))!;
        var bySlug = maps.ToDictionary(
            m => m.GetProperty("slug").GetString()!,
            m => m.GetProperty("gamemodes").EnumerateArray().Select(g => g.GetString()!).ToArray());

        await Assert.That(bySlug["a-wools"]).IsEquivalentTo(new[] { "ctw" });
        await Assert.That(bySlug["b-wools-and-destroyable"]).IsEquivalentTo(new[] { "ctw", "dtm" });
        await Assert.That(bySlug["c-phantom-only"]).IsEmpty();
        await Assert.That(bySlug["d-core"]).IsEquivalentTo(new[] { "dtc" });
    }

    // ── harness (self-contained, mirrors SketchEndpointTests) ───────────────────────
}
