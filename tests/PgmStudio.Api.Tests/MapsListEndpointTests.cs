using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MySqlConnector;
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

    // ── harness (self-contained, mirrors SketchEndpointTests) ───────────────────────
}
