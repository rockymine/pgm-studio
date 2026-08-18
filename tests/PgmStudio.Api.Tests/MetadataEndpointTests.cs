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
/// PATCH /api/map/{slug}/metadata persists authors/contributors to the <c>author</c> table and GET
/// reads them back. A person is an account (a <c>uuid</c>) or a pseudonym (a name, carried as the
/// element's own text), and either alone is a whole author.
/// Runs against the <c>pgm_studio_test</c> schema (override with <c>PGM_STUDIO_TEST_DB</c>); each
/// test resets the schema and seeds one map, so they run serially.
/// </summary>
[NotInParallel("api-db")]
public sealed class MetadataEndpointTests
{
    [Test]
    public async Task Patch_round_trips_authors_and_contributors()
    {
        using var client = await SeedAsync("amap");

        var resp = await client.PatchAsJsonAsync("/api/map/amap/metadata", new
        {
            authors = new object[]
            {
                new { uuid = "069a79f4-44e9-4726-a5be-fca90e38aaf5", name = "Notch", role = "author", contribution = "design" },
                new { uuid = "61699b2e-d327-4a01-9f1e-0ea8c3f06bc6", name = "Dinnerbone", role = "contributor" },
            },
        });
        await Assert.That(resp.IsSuccessStatusCode).IsTrue();

        var authors = await GetAuthorsAsync(client, "amap");
        await Assert.That(authors.Count).IsEqualTo(2);

        var notch = authors.Single(a => Field(a, "uuid") == "069a79f4-44e9-4726-a5be-fca90e38aaf5");
        await Assert.That(Field(notch, "role")).IsEqualTo("author");
        await Assert.That(Field(notch, "name")).IsEqualTo("Notch");
        await Assert.That(Field(notch, "contribution")).IsEqualTo("design");

        var dinner = authors.Single(a => Field(a, "role") == "contributor");
        await Assert.That(Field(dinner, "uuid")).IsEqualTo("61699b2e-d327-4a01-9f1e-0ea8c3f06bc6");
        await Assert.That(Field(dinner, "name")).IsEqualTo("Dinnerbone");
    }

    [Test]
    public async Task Patch_keeps_a_pseudonym_and_skips_an_empty_entry()
    {
        using var client = await SeedAsync("bmap");

        await client.PatchAsJsonAsync("/api/map/bmap/metadata", new
        {
            authors = new object[]
            {
                new { uuid = "", name = "Opus 5", role = "author" },
                "a bare string",
                new { uuid = "", name = "", role = "author" },
                new { uuid = "069a79f4-44e9-4726-a5be-fca90e38aaf5", name = "Notch", role = "author" },
            },
        });

        var authors = await GetAuthorsAsync(client, "bmap");
        await Assert.That(authors.Count).IsEqualTo(3);

        var pseudonym = authors.Single(a => Field(a, "name") == "Opus 5");
        await Assert.That(Field(pseudonym, "uuid")).IsEqualTo("");
        await Assert.That(authors.Any(a => Field(a, "name") == "a bare string")).IsTrue();
        await Assert.That(authors.Any(a => Field(a, "uuid") == "069a79f4-44e9-4726-a5be-fca90e38aaf5")).IsTrue();
    }

    [Test]
    public async Task Patch_replaces_authors_rather_than_appending()
    {
        using var client = await SeedAsync("cmap");

        await client.PatchAsJsonAsync("/api/map/cmap/metadata", new
        {
            authors = new object[]
            {
                new { uuid = "069a79f4-44e9-4726-a5be-fca90e38aaf5", name = "Notch", role = "author" },
                new { uuid = "61699b2e-d327-4a01-9f1e-0ea8c3f06bc6", name = "Dinnerbone", role = "author" },
            },
        });
        await client.PatchAsJsonAsync("/api/map/cmap/metadata", new
        {
            authors = new object[] { new { uuid = "853c80ef-3c37-49fd-aa49-938b674adae6", name = "jeb_", role = "author" } },
        });

        var authors = await GetAuthorsAsync(client, "cmap");
        await Assert.That(authors.Count).IsEqualTo(1);
        await Assert.That(Field(authors[0], "name")).IsEqualTo("jeb_");
    }

    [Test]
    public async Task Patch_without_authors_key_leaves_existing_authors_intact()
    {
        using var client = await SeedAsync("dmap");

        await client.PatchAsJsonAsync("/api/map/dmap/metadata", new
        {
            authors = new object[] { new { uuid = "069a79f4-44e9-4726-a5be-fca90e38aaf5", name = "Notch", role = "author" } },
        });
        // A metadata-only patch (no authors key) must not wipe the author table.
        await client.PatchAsJsonAsync("/api/map/dmap/metadata", new { version = "2.0.0" });

        var doc = await client.GetFromJsonAsync<JsonElement>("/api/map/dmap");
        await Assert.That(doc.GetProperty("version").GetString()).IsEqualTo("2.0.0");
        var authors = doc.GetProperty("authors").EnumerateArray().ToList();
        await Assert.That(authors.Count).IsEqualTo(1);
        await Assert.That(Field(authors[0], "name")).IsEqualTo("Notch");
    }

    // ── helpers ───────────────────────────────────────────────────────────────────
    private static async Task<List<JsonElement>> GetAuthorsAsync(HttpClient client, string slug)
    {
        var doc = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}");
        return doc.GetProperty("authors").EnumerateArray().ToList();
    }

    private static string? Field(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Reset the test schema, seed one empty map, and return a client onto it.</summary>
    private static async Task<HttpClient> SeedAsync(string slug)
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
        return ApiTestFactory.Shared.CreateClient();
    }
}
