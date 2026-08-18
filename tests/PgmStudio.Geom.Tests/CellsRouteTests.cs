using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// The walk's three derived reads: the distance field, the corridor ribbon and the ways round a hole. Every
/// case is a shape whose answer is known by construction, because the thing being tested is whether a
/// measurement distinguishes two boards a reachability check cannot — a ring and a ring with one arm cut are
/// both "connected", and only these three say which is which.
/// </summary>
public sealed class CellsRouteTests
{
    // '#' is walkable, a space is not — a picture of the case, so a reader can see what is being asserted.
    private static HashSet<(int X, int Z)> Grid(params string[] rows)
    {
        var set = new HashSet<(int X, int Z)>();
        for (var z = 0; z < rows.Length; z++)
            for (var x = 0; x < rows[z].Length; x++)
                if (rows[z][x] != ' ') set.Add((x, z));
        return set;
    }

    // A ring: two arms of equal length round a hole three cells wide.
    private static HashSet<(int X, int Z)> Ring() => Grid(
        "#########",
        "#########",
        "###   ###",
        "###   ###",
        "###   ###",
        "#########",
        "#########");

    private static HashSet<(int X, int Z)> Hole(IReadOnlySet<(int X, int Z)> within)
    {
        var bounds = Cells.BoundingBox([.. within]);
        var outside = Cells.Flood(
            [(bounds.X - 1, bounds.Z - 1)],
            AllEmpty(within, bounds));
        var hole = new HashSet<(int X, int Z)>();
        for (var x = bounds.X; x < bounds.MaxX; x++)
            for (var z = bounds.Z; z < bounds.MaxZ; z++)
                if (!within.Contains((x, z)) && !outside.Contains((x, z))) hole.Add((x, z));
        return hole;

        static HashSet<(int X, int Z)> AllEmpty(IReadOnlySet<(int X, int Z)> filled, CellRect bounds)
        {
            var empty = new HashSet<(int X, int Z)>();
            for (var x = bounds.X - 1; x <= bounds.MaxX; x++)
                for (var z = bounds.Z - 1; z <= bounds.MaxZ; z++)
                    if (!filled.Contains((x, z))) empty.Add((x, z));
            return empty;
        }
    }

    [Test]
    public async Task DistanceField_agrees_with_the_walk_on_every_cell_it_reaches()
    {
        var ring = Ring();
        var field = Cells.DistanceField([(0, 3)], ring);

        await Assert.That(field.Count).IsEqualTo(ring.Count).Because("a ring is one component");
        foreach (var (cell, steps) in field)
            await Assert.That(Cells.PathLength((0, 3), cell, ring)).IsEqualTo(steps);
    }

    [Test]
    public async Task DistanceField_omits_what_the_targets_cannot_reach()
    {
        var split = Grid(
            "### ###",
            "### ###");
        var field = Cells.DistanceField([(0, 0)], split);

        await Assert.That(field.ContainsKey((2, 1))).IsTrue();
        await Assert.That(field.ContainsKey((4, 0))).IsFalse().Because("the far block is a separate component");
    }

    [Test]
    public async Task DistanceField_takes_the_nearest_of_several_targets()
    {
        var slab = Grid("#########");
        var field = Cells.DistanceField([(0, 0), (8, 0)], slab);

        await Assert.That(field[(1, 0)]).IsEqualTo(1);
        await Assert.That(field[(7, 0)]).IsEqualTo(1).Because("the east target is the nearer one there");
        await Assert.That(field[(4, 0)]).IsEqualTo(4);
    }

    [Test]
    public async Task Corridor_carries_both_arms_where_one_geodesic_commits_to_one()
    {
        var ring = Ring();
        var north = ring.Where(c => c.Z <= 1).ToHashSet();
        var south = ring.Where(c => c.Z >= 5).ToHashSet();

        var ribbon = Cells.Corridor((0, 3), (8, 3), ring);
        await Assert.That(ribbon.Overlaps(north)).IsTrue();
        await Assert.That(ribbon.Overlaps(south)).IsTrue();

        // This is the behaviour the ribbon replaces: a dilated single path can only ever carry one side, so
        // the other reads unused however many players walk it.
        var geodesic = Cells.ShortestPath((0, 3), (8, 3), ring)!.ToHashSet();
        var arms = (geodesic.Overlaps(north) ? 1 : 0) + (geodesic.Overlaps(south) ? 1 : 0);
        await Assert.That(arms).IsEqualTo(1);
    }

    [Test]
    public async Task Corridor_stops_at_the_slack_budget()
    {
        // The north way is 12 cells and the south way 16 — 1.33x, just past a 30% budget.
        var unequal = Grid(
            "#########",
            "#########",
            "###   ###",
            "###   ###",
            "#       #",
            "#       #",
            "#       #",
            "#########");
        await Assert.That(Cells.PathLength((0, 3), (8, 3), unequal)).IsEqualTo(12);

        await Assert.That(Cells.Corridor((0, 3), (8, 3), unequal, slack: 0.30).Contains((4, 7))).IsFalse();
        await Assert.That(Cells.Corridor((0, 3), (8, 3), unequal, slack: 3.0).Contains((4, 7))).IsTrue();
    }

    [Test]
    public async Task Corridor_is_empty_when_the_ends_do_not_connect()
    {
        var split = Grid(
            "### ###",
            "### ###");
        await Assert.That(Cells.Corridor((0, 0), (6, 0), split)).IsEmpty();
    }

    [Test]
    public async Task WaysRound_reports_two_when_a_journey_may_pass_either_side()
    {
        var ring = Ring();
        await Assert.That(Cells.WaysRound((0, 3), (8, 3), [.. Hole(ring)], ring, horizontal: false)).IsEqualTo(2);
    }

    [Test]
    public async Task WaysRound_reports_one_when_the_hole_stands_but_an_arm_is_severed()
    {
        // The notch at x 2 sits OUTSIDE the ring, so the hole is untouched and only the north arm is walkable.
        // Cutting the arm itself is not this test: removing it opens the hole, and a ring without an arm is a
        // cup with nothing to go round.
        var severed = Grid(
            "#########",
            "#########",
            "###   ###",
            "###   ###",
            "###   ###",
            "## ######",
            "## ######");

        await Assert.That(Hole(severed).Count).IsEqualTo(9).Because("the hole is still enclosed");
        await Assert.That(Cells.WaysRound((0, 3), (8, 3), [.. Hole(severed)], severed, horizontal: false)).IsEqualTo(1);
    }

    [Test]
    public async Task An_open_bay_is_not_a_hole_and_has_no_way_round()
    {
        var cup = Grid(
            "#########",
            "#########",
            "###   ###",
            "###   ###",
            "###   ###",
            "###   ###");

        await Assert.That(Hole(cup)).IsEmpty();
        await Assert.That(Cells.WaysRound((0, 3), (8, 3), [], cup)).IsEqualTo(0);
    }

    [Test]
    public async Task RayCut_runs_from_the_hole_to_the_bound_on_the_side_asked_for()
    {
        var ring = Ring();
        var bounds = Cells.BoundingBox([.. ring]);
        var hole = Hole(ring);

        // Vertical rays leave the hole's median column and reach the board's edge, one each way.
        var north = Cells.RayCut([.. hole], bounds, horizontal: false, forward: false);
        var south = Cells.RayCut([.. hole], bounds, horizontal: false, forward: true);

        await Assert.That(north).IsEquivalentTo(new HashSet<(int, int)> { (4, 1), (4, 0) });
        await Assert.That(south).IsEquivalentTo(new HashSet<(int, int)> { (4, 5), (4, 6) });
        await Assert.That(north.Overlaps(south)).IsFalse().Because("the two rays are opposite halves of one line");
    }
}
