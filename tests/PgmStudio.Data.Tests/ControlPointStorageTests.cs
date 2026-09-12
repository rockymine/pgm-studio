using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;

namespace PgmStudio.Data.Tests;

/// <summary>
/// A control point and the score module it pays into survive storage. The invariant is the one the columns
/// are nullable for: a knob the map never stated comes back unstated, because the value PGM would apply in
/// its place depends on which element the point was written as, and storing that value would store a
/// different map.
/// </summary>
[NotInParallel]
public sealed class ControlPointStorageTests
{
    private static MapXml Document() => new()
    {
        Name = "Hill Map",
        Version = "1.0.0",
        ControlPoints =
        [
            new ControlPoint
            {
                Id = "north", Name = "North", Element = ControlPointElement.King,
                CaptureRegionId = "north-cap", ProgressRegionId = "north-cap", OwnerRegionId = "north-signal",
                CaptureTime = "5s", Points = 1, TimeMultiplier = 0,
                NeutralState = true, Incremental = true, ShowProgress = true, Required = false,
            },
            new ControlPoint
            {
                // States almost nothing, which is the case the nullable columns exist for.
                Id = "middle", Element = ControlPointElement.ControlPoints, CaptureRegionId = "mid-cap",
            },
        ],
        Score = new ScoreConfig { Limit = 750, King = true },
    };

    private static async Task<long> InsertMapAsync(PgmDb db) =>
        await new MapRepository(db).InsertAsync(new MapRow
        {
            Slug = "hill-map", Name = "Hill Map", Version = "1.0.0",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });

    [Test]
    public async Task A_control_point_and_its_score_module_round_trip_through_storage()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await InsertMapAsync(db);

        await new MapWriter(db).WriteEntitiesAsync(mapId, Document());
        var read = await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId));

        await Assert.That(read.ControlPoints.Count).IsEqualTo(2);

        var north = read.ControlPoints.Single(p => p.Id == "north");
        await Assert.That(north.Element).IsEqualTo(ControlPointElement.King);
        await Assert.That(north.Name).IsEqualTo("North");
        await Assert.That(north.CaptureRegionId).IsEqualTo("north-cap");
        await Assert.That(north.OwnerRegionId).IsEqualTo("north-signal");
        await Assert.That(north.CaptureTime).IsEqualTo("5s");
        await Assert.That(north.Points).IsEqualTo(1.0);
        await Assert.That(north.Required).IsFalse();
        await Assert.That(north.NeutralState).IsTrue();

        await Assert.That(read.Score!.Limit).IsEqualTo(750);
        await Assert.That(read.Score.King).IsTrue();
        await Assert.That(read.Score.Kills).IsNull();
    }

    [Test]
    public async Task A_knob_the_map_never_stated_comes_back_unstated()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await InsertMapAsync(db);

        await new MapWriter(db).WriteEntitiesAsync(mapId, Document());
        var middle = (await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId)))
            .ControlPoints.Single(p => p.Id == "middle");

        await Assert.That(middle.Element).IsEqualTo(ControlPointElement.ControlPoints);
        await Assert.That(middle.Name).IsEqualTo("");
        await Assert.That(middle.Required).IsNull();
        await Assert.That(middle.NeutralState).IsNull();
        await Assert.That(middle.Incremental).IsNull();
        await Assert.That(middle.Points).IsNull();
        await Assert.That(middle.TimeMultiplier).IsNull();
        await Assert.That(middle.CaptureTime).IsEqualTo("");
        await Assert.That(middle.ProgressRegionId).IsEqualTo("");
    }

    // No row is not an empty configuration: PGM builds no score module for a map with no <score>, and on
    // that map a point's points rate pays nothing at all.
    [Test]
    public async Task A_map_with_no_score_element_stores_no_score_row()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await InsertMapAsync(db);

        var doc = Document();
        doc.Score = null;
        await new MapWriter(db).WriteEntitiesAsync(mapId, doc);

        await Assert.That(await db.Scores.CountAsync(s => s.MapId == mapId)).IsEqualTo(0);
        await Assert.That((await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId))).Score).IsNull();
    }

    // The gamemode is derived from the objective rows, never from the <gamemode> label — and a score limit
    // is how a capture map ends rather than a mode of its own.
    [Test]
    public async Task The_stored_rows_derive_koth_and_not_deathmatch()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await InsertMapAsync(db);

        var doc = Document();
        doc.ControlPoints.RemoveAll(p => p.Element == ControlPointElement.ControlPoints);
        await new MapWriter(db).WriteEntitiesAsync(mapId, doc);

        var modes = await new MapRepository(db).GamemodesAsync();
        await Assert.That(modes[mapId]).IsEquivalentTo(new[] { "koth" });
    }
}
