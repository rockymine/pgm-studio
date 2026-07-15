using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MySqlConnector;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Migrations;

namespace PgmStudio.Api.Tests;

/// <summary>
/// PATCH /api/map/{slug}/symmetry confirm/reject (B7). The detector emits diagonal mirrors
/// (<c>mirror_d1</c>/<c>mirror_d2</c>) and the GET endpoint surfaces one as the primary, so confirming
/// that detected primary must be accepted — the whitelist used to omit the diagonals and rejected them
/// with 400. Runs against the <c>pgm_studio_test</c> schema (override with <c>PGM_STUDIO_TEST_DB</c>);
/// each test resets the schema and seeds one map, so they run serially.
/// </summary>
[NotInParallel("api-db")]
public sealed class SymmetryEndpointTests
{
    [Test]
    [Arguments("mirror_d2")]
    [Arguments("mirror_d1")]
    public async Task Patch_confirms_a_diagonal_mirror_primary(string type)
    {
        await using var factory = await SeedAsync("symmap");
        using var client = factory.CreateClient();

        var resp = await client.PatchAsJsonAsync("/api/map/symmap/symmetry",
            new { status = "confirmed", confirmed_type = type, cx = -36.5, cz = -303.5 });
        await Assert.That(resp.IsSuccessStatusCode).IsTrue();

        // The confirmed diagonal round-trips through GET as the user-overridden primary.
        var got = await client.GetFromJsonAsync<JsonElement>("/api/map/symmap/symmetry");
        await Assert.That(got.GetProperty("status").GetString()).IsEqualTo("confirmed");
        await Assert.That(got.GetProperty("primary").GetProperty("type").GetString()).IsEqualTo(type);
        await Assert.That(got.GetProperty("primary").GetProperty("user_override").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task Patch_rejects_an_unknown_symmetry_type()
    {
        await using var factory = await SeedAsync("symmap");
        using var client = factory.CreateClient();

        var resp = await client.PatchAsJsonAsync("/api/map/symmap/symmetry",
            new { status = "confirmed", confirmed_type = "rot_270" });
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ── harness (self-contained, mirrors MetadataEndpointTests) ─────────────────────

    /// <summary>Reset the test schema, seed one empty map, and return a factory bound to that DB.</summary>
    private static async Task<ApiTestFactory> SeedAsync(string slug)
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using (var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString)))
        {
            await new MapRepository(db).InsertAsync(new MapRow
            {
                Slug = slug, Name = "Seed Map", Version = "1.0.0", Gamemode = "ctw",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        }
        return new ApiTestFactory();
    }
}
