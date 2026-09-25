using PgmStudio.Analysis.Playability;
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

        var zoned = WorldWalk.OfBuilt(ground, crown, [(3, 0, 5, 0)]);
        var path = Walk.Between(zoned.Stand((0, 0))!.Value, zoned.Stand((8, 0))!.Value, zoned);
        await Assert.That(path).IsNotNull();
        await Assert.That(path!.Cost.Blocks).IsEqualTo(3);
    }
}
