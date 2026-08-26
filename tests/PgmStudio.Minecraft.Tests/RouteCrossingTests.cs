using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Whether a building stands across a road or at the end of one (DR-CROSS). The whole rule is the difference
/// between those two, so every case here is a road and a footprint on it: what is asserted is which of the
/// two the pair reads as, and never the geometry that produced them.
/// </summary>
public sealed class RouteCrossingTests
{
    /// <summary>A straight road along z = 0, from x = <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static List<(int X, int Z)> Road(int from, int to)
    {
        var cells = new List<(int X, int Z)>();
        for (var x = from; x <= to; x++)
        for (var z = -1; z <= 1; z++)
            cells.Add((x, z));
        return cells;
    }

    private static HashSet<(int X, int Z)> Box(int minX, int minZ, int maxX, int maxZ)
    {
        var cells = new HashSet<(int X, int Z)>();
        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
            cells.Add((x, z));
        return cells;
    }

    [Test]
    public async Task A_building_the_road_runs_past_stands_across_it()
    {
        await Assert.That(RouteCrossing.Crosses(Road(0, 40), Box(15, -4, 25, 4))).IsTrue();
    }

    [Test]
    public async Task A_building_at_the_end_of_the_road_is_a_porch()
    {
        // The road stops at its wall and everything left of it is one run — which is what a road running to a
        // door looks like, and the author's ruling that it stands.
        await Assert.That(RouteCrossing.Crosses(Road(0, 40), Box(30, -4, 45, 4))).IsFalse();
    }

    [Test]
    public async Task A_building_the_road_does_not_reach_is_not_this_faults_to_report()
    {
        await Assert.That(RouteCrossing.Crosses(Road(0, 40), Box(60, -4, 70, 4))).IsFalse();
    }

    [Test]
    public async Task A_road_that_was_already_in_two_runs_is_not_broken_by_a_building_at_its_end()
    {
        // A worn road is holes by design, so the count is taken before and after: what fires is the building
        // adding a break, never the road having one.
        var broken = new List<(int X, int Z)>([.. Road(0, 10), .. Road(30, 40)]);
        await Assert.That(RouteCrossing.Crosses(broken, Box(5, -4, 12, 4))).IsFalse();
    }

    [Test]
    public async Task A_road_that_was_already_in_two_runs_is_still_reported_where_a_building_makes_three()
    {
        var broken = new List<(int X, int Z)>([.. Road(0, 10), .. Road(30, 40)]);
        await Assert.That(RouteCrossing.Crosses(broken, Box(33, -4, 36, 4))).IsTrue();
    }

    [Test]
    public async Task A_gap_of_two_blocks_is_one_run_rather_than_two()
    {
        // The stroke's own coverage leaves cells out, and a two-block hole in the paving is not a road that
        // stops. A building beside such a hole is not standing across anything.
        var worn = new List<(int X, int Z)>([.. Road(0, 18), .. Road(21, 40)]);
        await Assert.That(RouteCrossing.Crosses(worn, Box(30, -4, 45, 4))).IsFalse();
    }

    [Test]
    public async Task A_building_that_takes_the_whole_road_is_an_end_rather_than_a_crossing()
    {
        await Assert.That(RouteCrossing.Crosses(Road(10, 20), Box(0, -10, 30, 10))).IsFalse();
    }
}
