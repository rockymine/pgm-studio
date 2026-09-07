using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>PATCH /map/{slug}/intent/rooms/{reference}</c> — the route a drag on the sketch canvas arrives at
/// (S25b). The sketch draws a spawn, a wool room and the building inside one out of the stored intent, so a
/// move has to be written back there or the picture and the world build disagree about where a room is.
///
/// <para>What the route owns rather than <c>RoomPieceMove</c> is the round trip: the stored intent is read,
/// the moved one is stored and projected the way an ordinary intent PUT is, and a refusal is answered at the
/// status that says whose fault it is — <b>404</b> where the reference names no room, <b>400</b> where the
/// move itself cannot be made.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class RoomMoveRouteTests
{
    /// <summary>One team, one spawn on a ten-block region with a marker in the middle of it and a building
    /// on it — the smallest board that can answer every rule the move has.</summary>
    private const string Board = """
        {"meta":{"name":"Weirgate"},
         "teams":[{"id":"red","name":"Red","color":"red","size":8}],
         "spawns":[{"team":"red","point":{"x":5,"y":12,"z":5},
                    "protection":[{"minX":0,"minZ":0,"maxX":10,"maxZ":10}],
                    "footprint":{"minX":2,"minZ":2,"maxX":8,"maxZ":8}}]}
        """;

    private static async Task<(HttpClient Client, string Slug)> BoardAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();

        var create = await client.PostAsJsonAsync("/api/sketch", new { name = "Weirgate" });
        await Assert.That(create.IsSuccessStatusCode).IsTrue().Because(await create.Content.ReadAsStringAsync());
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var stored = await client.PutAsync($"/api/map/{slug}/intent",
            new StringContent(Board, Encoding.UTF8, "application/json"));
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());
        return (client, slug);
    }

    private static Task<HttpResponseMessage> MoveAsync(
        HttpClient client, string slug, string reference, object body) =>
        client.PatchAsJsonAsync($"/api/map/{slug}/intent/rooms/{reference}", body);

    /// <summary>The bounds the map's own <c>red-spawn</c> protection region carries, read off the region tree
    /// — the projected document rather than the intent artifact, which is what makes it a projection test.</summary>
    private static async Task<(double MinX, double MinZ, double MaxX, double MaxZ)> ProtectionAsync(
        HttpClient client, string slug)
    {
        var tree = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/regions/tree");
        // The tree nests, so the walk does: a protection region hangs under whatever contains it.
        static IEnumerable<JsonElement> Walk(JsonElement region)
        {
            yield return region;
            if (region.TryGetProperty("children", out var kids))
                foreach (var kid in kids.EnumerateArray())
                    foreach (var found in Walk(kid)) yield return found;
        }
        var region = tree.GetProperty("groups").EnumerateArray()
            .SelectMany(group => group.GetProperty("regions").EnumerateArray())
            .SelectMany(Walk)
            .First(r => r.GetProperty("id").GetString() == "red-spawn");
        var bounds = region.GetProperty("bounds");
        return (bounds.GetProperty("min_x").GetDouble(), bounds.GetProperty("min_z").GetDouble(),
                bounds.GetProperty("max_x").GetDouble(), bounds.GetProperty("max_z").GetDouble());
    }

    private static async Task<JsonElement> SpawnAsync(HttpClient client, string slug) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/intent"))
            .GetProperty("spawns").EnumerateArray().First();

    [Test]
    public async Task A_moved_region_is_stored_with_the_room_seated_on_it()
    {
        var (client, slug) = await BoardAsync();
        using var _ = client;

        var moved = await MoveAsync(client, slug, "red",
            new { part = "spawn", minX = 100, minZ = 50, maxX = 110, maxZ = 60 });
        await Assert.That(moved.IsSuccessStatusCode).IsTrue().Because(await moved.Content.ReadAsStringAsync());

        var spawn = await SpawnAsync(client, slug);
        var region = spawn.GetProperty("protection").EnumerateArray().Single();
        await Assert.That(region.GetProperty("minX").GetDouble()).IsEqualTo(100);
        await Assert.That(region.GetProperty("minZ").GetDouble()).IsEqualTo(50);
        // The marker and the building travelled the same hundred and fifty blocks.
        await Assert.That(spawn.GetProperty("point").GetProperty("x").GetDouble()).IsEqualTo(105);
        await Assert.That(spawn.GetProperty("point").GetProperty("z").GetDouble()).IsEqualTo(55);
        await Assert.That(spawn.GetProperty("footprint").GetProperty("minX").GetDouble()).IsEqualTo(102);
    }

    /// <summary>A reference naming no room is a subject the studio does not hold, which is a different fault
    /// from a move it cannot make: only one of them is something the caller can fix by asking differently.</summary>
    [Test]
    public async Task A_reference_naming_no_room_answers_404()
    {
        var (client, slug) = await BoardAsync();
        using var _ = client;

        var missing = await MoveAsync(client, slug, "green",
            new { part = "spawn", minX = 0, minZ = 0, maxX = 10, maxZ = 10 });

        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(await missing.Content.ReadAsStringAsync()).Contains("RQ4");
    }

    [Test]
    public async Task A_move_that_resizes_answers_400_and_stores_nothing()
    {
        var (client, slug) = await BoardAsync();
        using var _ = client;

        var refused = await MoveAsync(client, slug, "red",
            new { part = "spawn", minX = 0, minZ = 0, maxX = 20, maxZ = 10 });

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await refused.Content.ReadAsStringAsync()).Contains("does not resize");

        // And the board is exactly as it was — a refused move is not a partial one.
        var region = (await SpawnAsync(client, slug)).GetProperty("protection").EnumerateArray().Single();
        await Assert.That(region.GetProperty("maxX").GetDouble()).IsEqualTo(10);
    }

    /// <summary>The building is the house on the region's ground, so one carried off it stands over whatever
    /// the neighbour happens to be. <c>WX12</c> is the rule that already says so.</summary>
    [Test]
    public async Task A_building_carried_off_its_region_answers_400_under_WX12()
    {
        var (client, slug) = await BoardAsync();
        using var _ = client;

        var refused = await MoveAsync(client, slug, "red",
            new { part = "building", minX = 6, minZ = 6, maxX = 12, maxZ = 12 });

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await refused.Content.ReadAsStringAsync()).Contains("WX12");
    }

    /// <summary>The move projects, which is the whole point of routing it through the intent write rather
    /// than storing the artifact alone: the map's own regions are rewritten in the same call, so the document
    /// a server would load has the spawn region where the drag put it rather than where the plan did.</summary>
    [Test]
    public async Task A_moved_region_reaches_the_map_the_intent_projects_into()
    {
        var (client, slug) = await BoardAsync();
        using var _ = client;

        await Assert.That(await ProtectionAsync(client, slug))
            .IsEqualTo((0, 0, 10, 10));

        var moved = await MoveAsync(client, slug, "red",
            new { part = "spawn", minX = 100, minZ = 50, maxX = 110, maxZ = 60 });
        await Assert.That(moved.IsSuccessStatusCode).IsTrue().Because(await moved.Content.ReadAsStringAsync());

        await Assert.That(await ProtectionAsync(client, slug))
            .IsEqualTo((100, 50, 110, 60));
    }
}
