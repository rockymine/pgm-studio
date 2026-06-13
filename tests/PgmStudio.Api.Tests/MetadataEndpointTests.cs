using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MySqlConnector;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;
using PgmStudio.Migrations;

namespace PgmStudio.Api.Tests;

/// <summary>
/// B6 integration tests: PATCH /api/map/{slug}/metadata persists authors/contributors to the
/// <c>author</c> table (the bug was that the endpoint dropped them), and GET reads them back.
/// Runs against the <c>pgm_studio_test</c> schema (override with <c>PGM_STUDIO_TEST_DB</c>); each
/// test resets the schema and seeds one map, so they run serially.
/// </summary>
[NotInParallel]
public sealed class MetadataEndpointTests
{
    [Test]
    public async Task Patch_round_trips_authors_and_contributors()
    {
        await using var factory = await SeedAsync("amap");
        using var client = factory.CreateClient();

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
    public async Task Patch_skips_authors_without_a_uuid()
    {
        await using var factory = await SeedAsync("bmap");
        using var client = factory.CreateClient();

        await client.PatchAsJsonAsync("/api/map/bmap/metadata", new
        {
            authors = new object[]
            {
                new { uuid = "", name = "typed-but-unresolved", role = "author" },
                new { uuid = "069a79f4-44e9-4726-a5be-fca90e38aaf5", name = "Notch", role = "author" },
            },
        });

        var authors = await GetAuthorsAsync(client, "bmap");
        await Assert.That(authors.Count).IsEqualTo(1);
        await Assert.That(Field(authors[0], "name")).IsEqualTo("Notch");
    }

    [Test]
    public async Task Patch_replaces_authors_rather_than_appending()
    {
        await using var factory = await SeedAsync("cmap");
        using var client = factory.CreateClient();

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
        await using var factory = await SeedAsync("dmap");
        using var client = factory.CreateClient();

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

    /// <summary>Reset the test schema, seed one empty map, and return a factory bound to that DB.</summary>
    private static async Task<TestApiFactory> SeedAsync(string slug)
    {
        await ResetSchemaAsync();
        await using (var db = new PgmDb(PgmDataOptions.ForConnectionString(TestConnectionString)))
        {
            await new MapRepository(db).InsertAsync(new MapRow
            {
                Slug = slug, Name = "Seed Map", Version = "1.0.0", Gamemode = "ctw",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        }
        return new TestApiFactory();
    }

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

    /// <summary>Boots the real app but points the connection string at the test schema.</summary>
    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:PgmStudio", TestConnectionString);
    }
}
