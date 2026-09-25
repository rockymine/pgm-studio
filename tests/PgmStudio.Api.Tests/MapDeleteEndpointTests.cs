using System.Net;
using System.Net.Http.Json;
using System.Text;
using MySqlConnector;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>DELETE /api/map/{slug}</c> removes the map and everything stored under it, and nothing stored under
/// another map. The child rows are the invariant: a map row gone while its teams, regions, authors or
/// artifacts stay behind is the half-deletion the cascade exists to prevent.
/// </summary>
[NotInParallel("api-db")]
public sealed class MapDeleteEndpointTests
{
    /// <summary>The child tables asserted on, written through the document editors, the metadata route and
    /// the artifact store.</summary>
    private static readonly string[] ChildTables = ["team", "region", "author", "map_artifact"];

    [Test]
    public async Task Deleting_a_map_removes_it_and_its_children_and_leaves_another_alone()
    {
        await ApiTestFactory.ResetSchemaAsync();
        var doomed = await SeedAsync("doomed");
        var kept = await SeedAsync("kept");
        using var client = ApiTestFactory.Shared.CreateClient();
        await FurnishAsync(client, "doomed");
        await FurnishAsync(client, "kept");

        foreach (var table in ChildTables)
            await Assert.That(await CountAsync(table, doomed)).IsGreaterThan(0)
                .Because($"the arrangement wrote no {table} row, so its removal would prove nothing");

        using var deleted = await client.DeleteAsync("/api/map/doomed");
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        await Assert.That(await CountAsync("map", doomed, column: "id")).IsEqualTo(0);
        foreach (var table in ChildTables)
        {
            await Assert.That(await CountAsync(table, doomed)).IsEqualTo(0).Because($"{table} rows outlived their map");
            await Assert.That(await CountAsync(table, kept)).IsGreaterThan(0).Because($"another map lost its {table} rows");
        }

        using var after = await client.GetAsync("/api/map/doomed");
        await Assert.That(after.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        using var neighbour = await client.GetAsync("/api/map/kept");
        await Assert.That(neighbour.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Deleting_an_unknown_map_is_a_404_in_the_refusal_envelope()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        using var resp = await client.DeleteAsync("/api/map/no-such-map");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        var refusal = await resp.Content.ReadFromJsonAsync<RefusalDto>();
        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!.Findings.Select(finding => finding.Rule)).Contains("RQ4");
    }

    /// <summary>The same map deleted twice: the second call names a map that is gone.</summary>
    [Test]
    public async Task A_deleted_map_is_unknown_to_a_second_delete()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await SeedAsync("once");
        using var client = ApiTestFactory.Shared.CreateClient();

        using var first = await client.DeleteAsync("/api/map/once");
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        using var second = await client.DeleteAsync("/api/map/once");
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    /// <summary>A bare map row carrying one stored artifact.</summary>
    private static async Task<long> SeedAsync(string slug)
    {
        await using var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString));
        var mapId = await new MapRepository(db).InsertAsync(new MapRow
        {
            Slug = slug, Name = "Seed Map", Version = "1.0.0", Gamemode = "ctw",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await new MapArtifactStore(db).SaveAsync(mapId, "probe", Encoding.UTF8.GetBytes("{}"));
        return mapId;
    }

    /// <summary>A team, a region and an author, each through the route an author would use.</summary>
    private static async Task FurnishAsync(HttpClient client, string slug)
    {
        await Succeeds(await client.PostAsJsonAsync($"/api/map/{slug}/teams",
            new TeamCreateRequest("red-team", "Red", "red")));
        await Succeeds(await client.PostAsJsonAsync($"/api/map/{slug}/regions",
            new RegionCreateRequest(Type: "rectangle", Id: "pad",
                Coords: new RegionCoordsDto(MinX: 0, MinZ: 0, MaxX: 4, MaxZ: 4))));
        await Succeeds(await client.PatchAsJsonAsync($"/api/map/{slug}/metadata",
            new { authors = new object[] { new { uuid = "", name = "Opus 5", role = "author" } } }));
    }

    private static async Task Succeeds(HttpResponseMessage resp)
    {
        using (resp)
            await Assert.That(resp.IsSuccessStatusCode).IsTrue()
                .Because($"{resp.RequestMessage?.Method} {resp.RequestMessage?.RequestUri} answered "
                         + $"{(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
    }

    private static async Task<long> CountAsync(string table, long mapId, string column = "map_id")
    {
        await using var connection = new MySqlConnection(ApiTestFactory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand($"SELECT COUNT(*) FROM `{table}` WHERE `{column}` = @id", connection);
        command.Parameters.AddWithValue("@id", mapId);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
