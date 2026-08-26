using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Whether the board still joins up with a prop standing on it (DR-WAY). Every case is a board with a known
/// way through it and a footprint placed either on that way or off it, so what is asserted is the verdict
/// rather than a distance: the local tests a building already passes cannot see any of this, which is the
/// whole reason the walk is taken.
/// </summary>
public sealed class WayThroughTests
{
    /// <summary>Flat ground over a rectangle, minus whatever is carved out of it.</summary>
    private static Dictionary<(int X, int Z), int> Board(int wide, int deep,
        Func<int, int, bool>? carved = null)
    {
        var ground = new Dictionary<(int X, int Z), int>();
        for (var z = 0; z < deep; z++)
        for (var x = 0; x < wide; x++)
            if (carved is null || !carved(x, z)) ground[(x, z)] = 8;
        return ground;
    }

    private static List<(int X, int Z)> Rect(int minX, int minZ, int maxX, int maxZ)
    {
        var cells = new List<(int X, int Z)>();
        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
            cells.Add((x, z));
        return cells;
    }

    [Test]
    public async Task A_building_across_the_only_neck_closes_the_way()
    {
        // Two open halves joined by a three-cell neck. Nothing local can see that the neck is the only way:
        // the ground beside the building is wide open on both faces.
        var ground = Board(41, 21, (x, z) => x is >= 19 and <= 21 && z is < 9 or > 11);
        var ways = WayThrough.Of(ground, [(2, 10), (38, 10)]);
        await Assert.That(ways.HasRoutes).IsTrue();

        var closed = ways.Admit("hall", Rect(19, 9, 21, 11));

        await Assert.That(closed).IsNotNull();
        await Assert.That(closed!.Rule).IsEqualTo(DressingRules.WayThrough);
        await Assert.That(closed.Message).Contains("closes the only way");
    }

    [Test]
    public async Task The_same_building_beside_the_neck_stands()
    {
        var ground = Board(41, 21, (x, z) => x is >= 19 and <= 21 && z is < 9 or > 11);
        var ways = WayThrough.Of(ground, [(2, 10), (38, 10)]);

        await Assert.That(ways.Admit("hall", Rect(4, 2, 6, 4))).IsNull();
    }

    [Test]
    public async Task A_wall_that_sends_the_route_the_long_way_round_is_declined()
    {
        // The way survives and is worth nothing: the route has to run fifty blocks down the board and back.
        var ground = Board(41, 61);
        var ways = WayThrough.Of(ground, [(2, 5), (38, 5)]);

        var closed = ways.Admit("range", Rect(20, 0, 20, 55));

        await Assert.That(closed).IsNotNull();
        await Assert.That(closed!.Message).Contains("further round");
    }

    [Test]
    public async Task A_wall_the_route_steps_round_stands()
    {
        // The other side of the same rule: a few blocks out of the way is what a player does anyway, so a
        // building that costs the route less than the detour allowance is not closing anything.
        var ground = Board(41, 61);
        var ways = WayThrough.Of(ground, [(2, 0), (38, 0)]);

        await Assert.That(ways.Admit("shed", Rect(20, 0, 20, 3))).IsNull();
    }

    [Test]
    public async Task Two_buildings_that_each_leave_a_way_and_together_leave_none_are_caught_at_the_second()
    {
        // A board with two necks. Either one alone is a detour the route absorbs; both blocked is a board in
        // two halves, and only a check that remembers what it already admitted can say so.
        var ground = Board(41, 21, (x, z) => x is >= 19 and <= 21 && z is not (>= 4 and <= 6) and not (>= 14 and <= 16));
        var ways = WayThrough.Of(ground, [(2, 5), (38, 15)]);
        await Assert.That(ways.HasRoutes).IsTrue();

        await Assert.That(ways.Admit("north", Rect(19, 4, 21, 6))).IsNull();

        var closed = ways.Admit("south", Rect(19, 14, 21, 16));

        await Assert.That(closed).IsNotNull();
        await Assert.That(closed!.Message).Contains("closes the only way");
    }

    [Test]
    public async Task A_board_with_one_waypoint_has_no_way_to_close()
    {
        var ways = WayThrough.Of(Board(21, 21), [(5, 5)]);

        await Assert.That(ways.HasRoutes).IsFalse();
    }
}
