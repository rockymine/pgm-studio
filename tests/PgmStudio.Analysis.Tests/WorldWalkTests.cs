using PgmStudio.Analysis.Playability;
using PgmStudio.Analysis.Scan;
using PgmStudio.Geom;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// The built fidelity's ground: a world's own solid runs, its build zones and its water, read into the places
/// one walk runs over.
/// </summary>
public sealed class WorldWalkTests
{
    [Test]
    public async Task A_crown_over_the_void_is_void_to_the_walk_and_bridged_where_building_is_allowed()
    {
        // Ground x 0–2 whose top course is 10, then a crown level with it over x 3–5, then ground again x 6–8.
        // Outside every build zone the crown's cells hold nothing and the two fields do not join; inside one
        // they are void a player bridges, one block a cell, rather than ground crossed for nothing.
        var ground = new List<(int X, int Z, int YFloor, int YTop)>();
        var crown = new List<(int X, int Z, int YFloor, int YTop)>();
        for (var x = 0; x < 9; x++)
            if (x is < 3 or > 5) ground.Add((x, 0, 0, 10));
            else crown.Add((x, 0, 7, 10));

        var closed = WorldWalk.OfBuilt(ground, crown, []);
        await Assert.That(closed.Ground.Any(place => place.X is >= 3 and <= 5)).IsFalse();
        await Assert.That(Walk.Between(closed.Stand((0, 0))!.Value, closed.Stand((8, 0))!.Value, closed)).IsNull();

        var zoned = WorldWalk.OfBuilt(ground, crown, [(3, 0), (4, 0), (5, 0)]);
        var path = Walk.Between(zoned.Stand((0, 0))!.Value, zoned.Stand((8, 0))!.Value, zoned);
        await Assert.That(path).IsNotNull();
        await Assert.That(path!.Cost.Blocks).IsEqualTo(3);
    }

    [Test]
    public async Task A_scanned_ladder_lifts_a_player_up_the_wall_it_stands_against()
    {
        // A floor whose top course is 0 at x 0, a wall to 10 at x 1, and a ladder in x 0 from 1 to 10.
        var segments = new SegmentIndex([(0, 0, 0, 0), (1, 0, 0, 10)], liftRuns: [(0, 0, 1, 10)]);
        var doc = new Dictionary<string, object?>();

        var walk = WorldWalk.Ground(doc, segments);
        var path = Walk.Between(walk.Stand((0, 0))!.Value, walk.Stand((1, 0))!.Value, walk);

        await Assert.That(path).IsNotNull();
        await Assert.That(path!.Cost.Blocks).IsEqualTo(0);
    }

    [Test]
    public async Task A_beam_built_out_over_the_void_from_stairs_is_walked_and_a_lobby_in_the_sky_is_not()
    {
        // A field whose top course is 10 over x 0–29, z 0–29; a stair along z 5 rising a block a column from 11 at
        // x 10 to 23 at x 22; a beam three wide over the void past the field, x 30–40, z 4–6, standing on 21–23;
        // and a lobby over the void at x 30–34, z 20–24, standing on 60–62 beside the field at 10.
        var rows = new List<(int X, int Z, int YStart, int YEnd)>();
        for (var x = 0; x < 30; x++)
            for (var z = 0; z < 30; z++)
                rows.Add((x, z, 0, z == 5 && x is >= 10 and <= 22 ? x + 1 : 10));
        for (var x = 23; x < 30; x++) rows[x * 30 + 5] = (x, 5, 0, 23);
        for (var x = 30; x <= 40; x++)
            for (var z = 4; z <= 6; z++) rows.Add((x, z, 21, 23));
        for (var x = 30; x <= 34; x++)
            for (var z = 20; z <= 24; z++) rows.Add((x, z, 60, 62));

        var walk = WorldWalk.Ground(new Dictionary<string, object?>(), new SegmentIndex(rows));

        var path = Walk.Between(walk.Stand((0, 0))!.Value, walk.Stand((40, 5))!.Value, walk);
        await Assert.That(path).IsNotNull().Because("the beam is ground once its stairs reach it");
        await Assert.That(walk.Ground.Any(place => place.X >= 30 && place.Z >= 20)).IsFalse()
            .Because("a lobby whose floor no step reaches does not pose as ground");
    }
}
