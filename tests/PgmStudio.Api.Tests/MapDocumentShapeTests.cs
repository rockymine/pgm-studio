using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>MapDocumentDto</c> says what <c>GET /map/{slug}</c> answers, and the document it describes is built by
/// the codec in <c>Pgm</c> and the wool grouping in <c>Data</c>, neither of which the record is mapped
/// through. Nothing in the compiler connects the two, so this does: a map is filled through the write
/// routes, fetched, and deserialized into the record with unmapped members <b>disallowed</b>, which fails
/// the moment a key arrives that the record — and so the published schema — has no field for.
///
/// <para>The map is filled rather than seeded empty because the conditional halves are the ones that drift:
/// a team, a region, the spawn that names it, and a wool carrying a monument.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class MapDocumentShapeTests
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    [Test]
    public async Task The_record_carries_every_key_the_document_answers()
    {
        using var client = await SeedAsync("shapemap");

        await Ok(client.PostAsJsonAsync("/api/map/shapemap/teams",
            new { id = "blue", name = "Blue", color = "blue", max_players = 12 }));
        await Ok(client.PostAsJsonAsync("/api/map/shapemap/regions",
            new { id = "blue-spawn", type = "rectangle", min_x = 0, min_z = 0, max_x = 8, max_z = 8 }));
        await Ok(client.PostAsJsonAsync("/api/map/shapemap/spawns",
            new { region_id = "blue-spawn", team = "blue", kit = "spawn", yaw = 90.0 }));
        await Ok(client.PostAsJsonAsync("/api/map/shapemap/wools", new { color = "red" }));
        await Ok(client.PostAsJsonAsync("/api/map/shapemap/wools/red/monuments",
            new { team = "blue", monument_region = "blue-spawn" }));

        var body = await client.GetStringAsync("/api/map/shapemap");
        var doc = JsonSerializer.Deserialize<MapDocumentDto>(body, Strict);

        await Assert.That(doc).IsNotNull();
        await Assert.That(doc!.Teams.Single().Id).IsEqualTo("blue");
        await Assert.That(doc.Teams.Single().MaxPlayers).IsEqualTo(12);
        await Assert.That(doc.Regions.Keys).Contains("blue-spawn");
        // A spawn names its region by id where the author has one, which is why the field stays open.
        await Assert.That(doc.Spawns.Single().Region.GetString()).IsEqualTo("blue-spawn");
        await Assert.That(doc.Spawns.Single().Yaw).IsEqualTo(90.0);

        var wool = doc.Wools.Single();
        await Assert.That(wool.Color).IsEqualTo("red");
        await Assert.That(wool.Monuments.Single().Team).IsEqualTo("blue");
        await Assert.That(wool.Monuments.Single().MonumentRegion).IsEqualTo("blue-spawn");
    }

    /// <summary>And an empty map, where every conditional key is absent: the record must not require one.</summary>
    [Test]
    public async Task An_empty_map_reads_back_as_the_record_describes_it()
    {
        using var client = await SeedAsync("baremap");

        var doc = JsonSerializer.Deserialize<MapDocumentDto>(
            await client.GetStringAsync("/api/map/baremap"), Strict);

        await Assert.That(doc).IsNotNull();
        await Assert.That(doc!.Version).IsEqualTo("1.0.0");
        await Assert.That(doc.Wools).IsEmpty();
        await Assert.That(doc.Destroyables).IsNull();
    }

    private static async Task Ok(Task<HttpResponseMessage> call)
    {
        var resp = await call;
        await Assert.That(resp.IsSuccessStatusCode)
            .IsTrue().Because(await resp.Content.ReadAsStringAsync());
    }

    /// <summary>Reset the test schema, seed one empty map, and return a client onto it.</summary>
    private static async Task<HttpClient> SeedAsync(string slug)
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using (var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString)))
        {
            await new MapRepository(db).InsertAsync(new MapRow
            {
                Slug = slug, Name = "Shape Map", Version = "1.0.0", Gamemode = "ctw",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        }
        return ApiTestFactory.Shared.CreateClient();
    }
}
