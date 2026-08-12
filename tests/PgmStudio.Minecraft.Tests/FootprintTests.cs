using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The plan a house stands on, once it may be more than one rectangle. Two things are asked of it. A single
/// rectangle must answer exactly what the arithmetic it replaced answered, because every shipped building is
/// one and none of them may move; and a plan that turns a corner must read as <b>one</b> building — one
/// outline, one closed ring, corners of both kinds in the right cells.
/// </summary>
public sealed class FootprintTests
{
    /// <summary>An L: a hall along −z with a wing running north off its west end.</summary>
    private static Footprint Ell() => new([new Wing(0, 0, 9, 4), new Wing(0, 5, 4, 9)]);

    /// <summary>A T: the same hall with the wing centred on it instead.</summary>
    private static Footprint Tee() => new([new Wing(0, 0, 10, 4), new Wing(4, 5, 6, 9)]);

    [Test]
    [Arguments(11, 9)]
    [Arguments(11, 5)]
    [Arguments(3, 3)]
    [Arguments(21, 9)]
    public async Task A_rectangle_answers_what_its_arithmetic_answered(int width, int depth)
    {
        var plan = new Footprint(0, 0, width - 1, depth - 1);

        for (var x = -1; x <= width; x++)
            for (var z = -1; z <= depth; z++)
            {
                var inside = x >= 0 && x < width && z >= 0 && z < depth;
                await Assert.That(plan.Holds(x, z)).IsEqualTo(inside);
                await Assert.That(plan.OnPerimeter(x, z))
                    .IsEqualTo(inside && (x == 0 || x == width - 1 || z == 0 || z == depth - 1));
                await Assert.That(plan.OnCorner(x, z))
                    .IsEqualTo(inside && (x == 0 || x == width - 1) && (z == 0 || z == depth - 1));
                await Assert.That(plan.Ring(x, z))
                    .IsEqualTo(inside ? Math.Min(Math.Min(x, width - 1 - x), Math.Min(z, depth - 1 - z)) : -1);
            }
    }

    /// <summary>A rectangle turns away from itself at four cells and never back into itself, so the kind that
    /// exists to mark where two wings meet may not appear on a building that has only one.</summary>
    [Test]
    public async Task A_rectangle_has_four_corners_and_no_inner_one()
    {
        var plan = new Footprint(0, 0, 10, 6);
        await Assert.That(plan.Cells().Count(cell => plan.OnCorner(cell.X, cell.Z))).IsEqualTo(4);
        await Assert.That(plan.Cells().Any(cell => plan.OnInnerCorner(cell.X, cell.Z))).IsFalse();
    }

    /// <summary>The notch is outside the building. A bounding box would hold it, which is why every question is
    /// asked of the cells.</summary>
    [Test]
    public async Task An_ell_holds_its_wings_and_not_the_notch()
    {
        var plan = Ell();
        await Assert.That(plan.Holds(9, 4)).IsTrue();          // far end of the hall
        await Assert.That(plan.Holds(2, 9)).IsTrue();          // top of the wing
        await Assert.That(plan.Holds(7, 7)).IsFalse();         // the notch, inside the box
        await Assert.That((plan.MinX, plan.MinZ, plan.MaxX, plan.MaxZ)).IsEqualTo((0, 0, 9, 9));
    }

    /// <summary>Where the two wings meet, the wall of one runs into the wall of the other. Neither cell of that
    /// turn is a corner the building turns <em>away</em> at, so no post stands in a notch — and both are inner
    /// corners, which is the margin an opening keeps.</summary>
    [Test]
    public async Task An_ell_turns_back_into_itself_where_its_wings_meet()
    {
        var plan = Ell();

        await Assert.That(plan.OnInnerCorner(5, 4)).IsTrue();   // the hall's wall, running out of the notch
        await Assert.That(plan.OnInnerCorner(4, 5)).IsTrue();   // the wing's wall, meeting it corner to corner
        await Assert.That(plan.OnCorner(5, 4)).IsFalse();
        await Assert.That(plan.OnCorner(4, 5)).IsFalse();

        // An L turns away from itself five times and back into itself once, which is the count that says it is
        // one building rather than two rectangles standing next to each other — those would answer eight.
        var outer = plan.Cells().Where(cell => plan.OnCorner(cell.X, cell.Z))
            .OrderBy(cell => cell.X).ThenBy(cell => cell.Z).ToList();
        await Assert.That(outer).IsEquivalentTo(
            new[] { (0, 0), (0, 9), (4, 9), (9, 0), (9, 4) }
                .OrderBy(cell => cell.Item1).ThenBy(cell => cell.Item2)
                .Select(cell => (X: cell.Item1, Z: cell.Item2)));

        var inner = plan.Cells().Where(cell => plan.OnInnerCorner(cell.X, cell.Z))
            .OrderBy(cell => cell.X).ThenBy(cell => cell.Z).ToList();
        await Assert.That(inner).IsEquivalentTo(new[] { (X: 4, Z: 5), (X: 5, Z: 4) });
    }

    /// <summary>Every wall cell of an L is on one closed ring, and no cell is on it twice. A wall pattern reads
    /// the arc round the whole building, so a ring that stopped at a wing would stripe half a house.</summary>
    [Test]
    [MethodDataSource(nameof(Plans))]
    public async Task A_plan_that_turns_a_corner_has_one_closed_ring(Footprint plan)
    {
        var walls = plan.Cells().Where(cell => plan.OnPerimeter(cell.X, cell.Z)).ToList();
        var arcs = walls.Select(cell => plan.Arc(cell.X, cell.Z)).ToList();

        await Assert.That(arcs.Any(arc => arc < 0)).IsFalse();
        await Assert.That(arcs.Distinct().Count()).IsEqualTo(walls.Count);
        await Assert.That(arcs.Order()).IsEquivalentTo(Enumerable.Range(0, walls.Count));

        // Off the ring is off the ring: an interior cell has no arc, and answers no bend.
        foreach (var (x, z) in plan.Cells().Where(cell => !plan.OnPerimeter(cell.X, cell.Z)))
        {
            await Assert.That(plan.Arc(x, z)).IsEqualTo(-1);
            await Assert.That(plan.Turn(plan.Arc(x, z))).IsEqualTo(0);
        }
    }

    /// <summary>The walk measures the building's own outline, so the bend a wall reads is the bend the terrain
    /// beside it would read off the same cells.</summary>
    [Test]
    [MethodDataSource(nameof(Plans))]
    public async Task A_plan_bends_where_the_walked_ring_does(Footprint plan)
    {
        var ring = GridBoundary.TracePerimeter(plan.Cells());
        var walked = GridBoundary.Turns(ring, GridBoundary.CornerWindow);

        foreach (var (cell, turn) in walked)
            await Assert.That(plan.Turn(plan.Arc(cell.X, cell.Z))).IsEqualTo((int)Math.Round(turn));
    }

    /// <summary>Steps in from the nearest wall, not in from the nearest edge of a box: a cell in the crook of
    /// two wings has walls on two sides of it and is one step from both.</summary>
    [Test]
    public async Task Inset_counts_from_the_nearest_wall_rather_than_the_box()
    {
        var plan = Ell();

        await Assert.That(plan.Ring(0, 0)).IsEqualTo(0);        // on the wall
        await Assert.That(plan.Ring(4, 4)).IsEqualTo(1);        // in the crook, a step from the wall at (5,4)
        await Assert.That(plan.Ring(2, 2)).IsEqualTo(2);        // deep in the hall
        await Assert.That(plan.Ring(7, 7)).IsEqualTo(-1);       // the notch

        // The deepest cell of this L stands 3 in — more than either wing offers alone, because where they meet
        // there is no wall between them and the two five-deep halls read as one ten-deep room. A box measured
        // from its own corners answers 4, and a wing measured alone answers 2; neither is the building.
        await Assert.That(plan.Cells().Max(cell => plan.Ring(cell.X, cell.Z))).IsEqualTo(3);
    }

    /// <summary>A wall lies along the axis it runs on, and a corner stands upright because it faces two ways at
    /// once. Both wings' walls answer, whichever way they run.</summary>
    [Test]
    public async Task A_wall_runs_along_its_own_axis_in_either_wing()
    {
        var plan = Ell();

        await Assert.That(plan.Run(5, 0)).IsEqualTo(GridBoundary.RunAlongX);   // the hall's long wall
        await Assert.That(plan.Run(0, 7)).IsEqualTo(GridBoundary.RunAlongZ);   // the wing's west wall
        await Assert.That(plan.Run(0, 0)).IsEqualTo(GridBoundary.RunsBothWays);
        await Assert.That(plan.Run(3, 3)).IsEqualTo(0);                        // inside, no wall to run along
    }

    public static IEnumerable<Func<Footprint>> Plans() => [Ell, Tee];
}
