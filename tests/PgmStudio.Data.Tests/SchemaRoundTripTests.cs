using System.Text.Json;
using LinqToDB.Data;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;
using PgmStudio.Minecraft;

namespace PgmStudio.Data.Tests;

/// <summary>
/// M1 integration tests: the FluentMigrator schema applies against a real MariaDB, and the
/// linq2db DAL round-trips an entity graph (FK links, JSON leaf columns, cascade delete).
/// Runs serially — the tests share one test schema and reset it at the start of each.
/// </summary>
[NotInParallel]
public sealed class SchemaRoundTripTests
{
    [Test]
    public async Task Migrations_create_the_expected_tables()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();

        var tables = db.Query<string>(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE()")
            .ToHashSet();

        foreach (var expected in new[]
                 {
                     "map", "team", "region", "filter", "wool", "monument", "spawn", "kit",
                     "kit_item", "kit_armor", "map_spawner", "renewable", "block_drop_rule",
                     "apply_rule", "author", "wool_block", "resource_block", "chest_item",
                     "spawner_block", "monument_candidate", "layer_segment", "map_artifact",
                 })
        {
            await Assert.That(tables).Contains(expected);
        }
    }

    [Test]
    public async Task Map_graph_round_trips_with_json_and_cascade()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var repo = new MapRepository(db);

        // ── insert a small map graph ────────────────────────────────────────────────
        var mapId = await repo.InsertAsync(new MapRow
        {
            Slug = "test-map", Name = "Test Map", Version = "1.4.0",
            Gamemode = "ctw", Objective = "Capture the wool", MaxBuildHeight = 128,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });

        await repo.InsertAsync(new TeamRow
        {
            MapId = mapId, TeamKey = "red", Name = "Red", Color = "red",
            DyeColor = "red", MaxPlayers = 10, MinPlayers = 1,
        });

        await repo.InsertAsync(new RegionRow
        {
            MapId = mapId, RegionKey = "spawn-zone", Type = "cuboid",
            CoordsJson = """{"radius":5,"height":3}""",
            ChildRefIdsJson = """["child-a","child-b"]""",
            SourceId = null,
        });

        var woolId = await repo.InsertAsync(new WoolRow
        {
            MapId = mapId, WoolKey = "red", Color = "red", Team = "blue",
            LocationJson = """{"x":10,"y":64,"z":-20}""",
        });

        await repo.InsertAsync(new MonumentRow
        {
            WoolId = woolId, MonumentKey = "red-blue", Team = "blue",
            LocationJson = """{"x":1,"y":64,"z":2}""",
        });

        // ── read back via the repository ────────────────────────────────────────────
        var map = await repo.GetBySlugAsync("test-map");
        await Assert.That(map).IsNotNull();
        await Assert.That(map!.Name).IsEqualTo("Test Map");
        await Assert.That(map.Version).IsEqualTo("1.4.0");
        await Assert.That(map.MaxBuildHeight).IsEqualTo(128d);

        var teams = await repo.TeamsForMapAsync(mapId);
        await Assert.That(teams.Count).IsEqualTo(1);
        await Assert.That(teams[0].TeamKey).IsEqualTo("red");
        await Assert.That(teams[0].MaxPlayers).IsEqualTo(10);

        var regions = await repo.RegionsForMapAsync(mapId);
        await Assert.That(regions.Count).IsEqualTo(1);
        await Assert.That(regions[0].Type).IsEqualTo("cuboid");
        // JSON survives the round-trip (MariaDB may reformat, so parse rather than string-compare)
        using (var coords = JsonDocument.Parse(regions[0].CoordsJson!))
            await Assert.That(coords.RootElement.GetProperty("radius").GetInt32()).IsEqualTo(5);
        using (var children = JsonDocument.Parse(regions[0].ChildRefIdsJson!))
            await Assert.That(children.RootElement.GetArrayLength()).IsEqualTo(2);

        var wools = await repo.WoolsForMapAsync(mapId);
        await Assert.That(wools.Count).IsEqualTo(1);
        await Assert.That(wools[0].Team).IsEqualTo("blue");

        var monuments = await repo.MonumentsForWoolAsync(wools[0].Id);
        await Assert.That(monuments.Count).IsEqualTo(1);
        await Assert.That(monuments[0].MonumentKey).IsEqualTo("red-blue");
        using (var loc = JsonDocument.Parse(monuments[0].LocationJson!))
            await Assert.That(loc.RootElement.GetProperty("y").GetInt32()).IsEqualTo(64);

        // ── cascade delete: removing the map removes every child row ────────────────
        await repo.DeleteMapAsync(mapId);
        await Assert.That(await repo.GetBySlugAsync("test-map")).IsNull();
        await Assert.That((await repo.TeamsForMapAsync(mapId)).Count).IsEqualTo(0);
        await Assert.That((await repo.RegionsForMapAsync(mapId)).Count).IsEqualTo(0);
        await Assert.That((await repo.WoolsForMapAsync(mapId)).Count).IsEqualTo(0);
        await Assert.That((await repo.MonumentsForWoolAsync(woolId)).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Monument_candidates_round_trip_and_cascade()   // F9
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var repo = new MapRepository(db);

        var mapId = await repo.InsertAsync(new MapRow
        {
            Slug = "mon-map", Name = "Mon", Version = "1.0.0", Gamemode = "ctw",
            Objective = "ctw", MaxBuildHeight = 128, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });

        var gathered = new List<MonumentCandidate>
        {
            new(5, 8, 5, "sign", 7, 0, 0, 0, "green", 5, 7, 6, 3, "Green Wool", null, null, "Green Wool"),
            new(20, 9, 20, "armorstand", 159, 1, 95, 2, "blue", null, null, null, null, null, "blue", "Blue", "Blue"),
        };

        await Assert.That(await MonumentCandidateStore.WriteAsync(db, mapId, gathered)).IsEqualTo(2);

        var read = (await MonumentCandidateStore.ReadAsync(db, mapId)).OrderBy(c => c.X).ToList();
        await Assert.That(read.Count).IsEqualTo(2);
        await Assert.That((read[0].X, read[0].Y, read[0].Z, read[0].Source, read[0].PedestalId, read[0].ColorHint, read[0].SignFacing, read[0].SignText))
            .IsEqualTo((5, 8, 5, "sign", 7, "green", (int?)3, "Green Wool"));
        await Assert.That((read[1].Source, read[1].CapId, read[1].CapData, read[1].StandHeadColor, read[1].StandName))
            .IsEqualTo(("armorstand", 95, 2, "blue", "Blue"));

        // re-gather is idempotent (delete-then-insert per map).
        await Assert.That(await MonumentCandidateStore.WriteAsync(db, mapId, gathered.Take(1).ToList())).IsEqualTo(1);
        await Assert.That((await MonumentCandidateStore.ReadAsync(db, mapId)).Count).IsEqualTo(1);

        // cascade-delete with the map.
        await repo.DeleteMapAsync(mapId);
        await Assert.That((await MonumentCandidateStore.ReadAsync(db, mapId)).Count).IsEqualTo(0);
    }
}
