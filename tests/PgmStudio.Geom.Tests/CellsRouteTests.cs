using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// The topology reads over a cell set: the ways round a hole, the ray a cut is made with, and the cheapest
/// walk under a per-cell or per-step cost. Every case is a shape whose answer is known by construction,
/// because the thing being tested is whether a measurement distinguishes two boards a reachability check
/// cannot — a ring and a ring with one arm cut are both "connected", and only these say which is which.
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
    public async Task A_step_cost_can_charge_a_direction_a_cell_cost_cannot()
    {
        // Two parallel lanes; crossing between them costs one way and not the other.
        var pair = Grid(
            "#########",
            "#########");
        int Step((int X, int Z) from, (int X, int Z) to) => from.Z == 0 && to.Z == 1 ? 20 : 1;

        // going down into the lower lane is dear, so a walk along it starts by staying up
        var down = Cells.CheapestPath((0, 0), (8, 1), pair, Step)!;
        await Assert.That(down.Count(c => c.Z == 1)).IsEqualTo(1).Because("it drops at the last moment");

        var up = Cells.CheapestPath((0, 1), (8, 0), pair, Step)!;
        await Assert.That(up.Count(c => c.Z == 1)).IsGreaterThan(1).Because("rising costs nothing here");
    }

    [Test]
    public async Task The_cell_cost_forms_are_the_step_forms_with_the_direction_ignored()
    {
        var ring = Ring();
        int Cost((int X, int Z) c) => c.Z == 0 ? 5 : 1;

        var byCell = Cells.CheapestPath((0, 3), (8, 3), ring, Cost)!;
        var byStep = Cells.CheapestPath((0, 3), (8, 3), ring, (_, to) => Cost(to))!;
        await Assert.That(byStep.SequenceEqual(byCell)).IsTrue();

        var fieldCell = Cells.CostField([(0, 3)], ring, Cost);
        var fieldStep = Cells.CostField([(0, 3)], ring, (_, to) => Cost(to));
        await Assert.That(fieldStep.Count).IsEqualTo(fieldCell.Count);
        foreach (var (cell, spent) in fieldCell) await Assert.That(fieldStep[cell]).IsEqualTo(spent);

        await Assert.That(Cells.CostCorridor((0, 3), (8, 3), ring, (_, to) => Cost(to)))
            .IsEquivalentTo(Cells.CostCorridor((0, 3), (8, 3), ring, Cost));
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
