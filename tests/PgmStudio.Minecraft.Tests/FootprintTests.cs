using PgmStudio.Domain;
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

        // The turn is the cell the two walls run into, and the building surrounds it: no run passes through it,
        // and without a block on it the two walls touch along a vertical edge and nothing else.
        await Assert.That(plan.OnInnerCorner(4, 4)).IsTrue();
        await Assert.That(plan.OnPerimeter(4, 4)).IsTrue();      // so a wall stands there
        await Assert.That(plan.OnCorner(4, 4)).IsFalse();        // and the building does not turn away at it
        await Assert.That(plan.OnInnerCorner(5, 4)).IsFalse();   // the wall running into it, not the turn
        await Assert.That(plan.OnInnerCorner(4, 5)).IsFalse();

        // An L turns away from itself five times and back into itself once — six corners, six posts, which is
        // the count that says it is one building rather than two rectangles standing next to each other.
        var outer = plan.Cells().Where(cell => plan.OnCorner(cell.X, cell.Z))
            .OrderBy(cell => cell.X).ThenBy(cell => cell.Z).ToList();
        await Assert.That(outer).IsEquivalentTo(
            new[] { (0, 0), (0, 9), (4, 9), (9, 0), (9, 4) }
                .OrderBy(cell => cell.Item1).ThenBy(cell => cell.Item2)
                .Select(cell => (X: cell.Item1, Z: cell.Item2)));

        var inner = plan.Cells().Where(cell => plan.OnInnerCorner(cell.X, cell.Z))
            .OrderBy(cell => cell.X).ThenBy(cell => cell.Z).ToList();
        await Assert.That(inner).IsEquivalentTo(new[] { (X: 4, Z: 4) });
    }

    /// <summary>Every wall the walk passes is on one closed ring, and no cell is on it twice. A wall pattern
    /// reads the arc round the whole building, so a ring that stopped at a wing would stripe half a house.
    ///
    /// <para>The turn where two wings meet is the one wall cell the walk does not pass: it steps from one wall
    /// to the other diagonally, across the corner rather than through it, so that cell has no arc — it takes a
    /// post, and a post is not something an arc places.</para></summary>
    [Test]
    [MethodDataSource(nameof(Plans))]
    public async Task A_plan_that_turns_a_corner_has_one_closed_ring(Footprint plan)
    {
        var walked = plan.Cells()
            .Where(cell => plan.OnPerimeter(cell.X, cell.Z) && !plan.OnInnerCorner(cell.X, cell.Z)).ToList();
        var arcs = walked.Select(cell => plan.Arc(cell.X, cell.Z)).ToList();

        await Assert.That(arcs.Any(arc => arc < 0)).IsFalse();
        await Assert.That(arcs.Distinct().Count()).IsEqualTo(walked.Count);
        await Assert.That(arcs.Order()).IsEquivalentTo(Enumerable.Range(0, walked.Count));

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
    /// two wings counts to the wall actually nearest it, the one at the turn included.</summary>
    [Test]
    public async Task Inset_counts_from_the_nearest_wall_rather_than_the_box()
    {
        var plan = Ell();

        await Assert.That(plan.Ring(0, 0)).IsEqualTo(0);        // on the wall
        await Assert.That(plan.Ring(4, 4)).IsEqualTo(0);        // the turn itself carries a wall
        await Assert.That(plan.Ring(2, 2)).IsEqualTo(2);        // deep in the hall
        await Assert.That(plan.Ring(7, 7)).IsEqualTo(-1);       // the notch

        // Nowhere in this L stands more than 2 in, which a box drawn round it would put at 4. The wall at the
        // turn is what holds it there: without a block on that cell the crook has no wall near it, and the two
        // five-deep wings read as one room deeper than either of them is.
        await Assert.That(plan.Cells().Max(cell => plan.Ring(cell.X, cell.Z))).IsEqualTo(2);
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

// ── the runs of wall ────────────────────────────────────────────────────────────────────────────────

    /// <summary>A wall ends wherever the building turns, so the count of runs is the count of the plan's sides.
    /// A rectangle answers four — one per facing, which is what lets a caller that used to name a wall by its
    /// direction keep every answer it had — and the shapes that turn a corner answer their own sides.</summary>
    [Test]
    public async Task A_plan_stands_in_as_many_runs_of_wall_as_it_has_sides()
    {
        await Assert.That(new Footprint(0, 0, 14, 10).Segments.Count).IsEqualTo(4);
        await Assert.That(Ell().Segments.Count).IsEqualTo(6);
        await Assert.That(Tee().Segments.Count).IsEqualTo(8);
    }

    /// <summary>A rectangle's four runs are its four sides, each running corner to corner — the numbers a caller
    /// used to read straight off a min and a max.</summary>
    [Test]
    public async Task A_rectangle_stands_in_its_own_four_sides()
    {
        var walls = new Footprint(0, 0, 14, 10).Segments;
        await Assert.That(walls).IsEquivalentTo(new[]
        {
            new WallSegment(RoomEdge.NegZ, 0, 0, 14),
            new WallSegment(RoomEdge.PosZ, 10, 0, 14),
            new WallSegment(RoomEdge.NegX, 0, 0, 10),
            new WallSegment(RoomEdge.PosX, 14, 0, 10),
        });
    }

    /// <summary>Two of an L's six walls look the same way, at different lines and over different stretches —
    /// which is the whole reason a run is carried rather than a facing. Named by direction alone, a window in
    /// one of them and a window in the other are indistinguishable.</summary>
    [Test]
    public async Task An_ell_has_two_walls_looking_the_same_way()
    {
        var walls = Ell().Segments;

        var south = walls.Where(wall => wall.Facing == RoomEdge.PosZ).ToList();
        await Assert.That(south).IsEquivalentTo(new[]
        {
            new WallSegment(RoomEdge.PosZ, 4, 5, 9),      // the hall's south side, east of the wing
            new WallSegment(RoomEdge.PosZ, 9, 0, 4),      // the wing's own far end
        });

        // The west side is one wall over both wings, because nothing interrupts it: two rectangles standing
        // beside each other would report two, and a building reports the wall a player walks along.
        await Assert.That(walls.Where(wall => wall.Facing == RoomEdge.NegX))
            .IsEquivalentTo(new[] { new WallSegment(RoomEdge.NegX, 0, 0, 9) });
    }

    /// <summary>Every run's cells are on the outline and face the way the run says, and every wall cell of the
    /// plan is either in a run or the turn where two wings meet — so nothing a wall pass would build is left
    /// unaccounted for, and nothing a run claims is off the outline.</summary>
    [Test]
    [MethodDataSource(nameof(Plans))]
    public async Task Every_wall_cell_belongs_to_a_run_that_faces_open_ground(Footprint plan)
    {
        var seated = new HashSet<(int X, int Z)>();
        foreach (var wall in plan.Segments)
        {
            var (outX, outZ) = (-wall.Inward.X, -wall.Inward.Z);
            for (var along = wall.Lo; along <= wall.Hi; along++)
            {
                var (x, z) = wall.Cell(along);
                await Assert.That(plan.Holds(x, z)).IsTrue();
                await Assert.That(plan.Holds(x + outX, z + outZ)).IsFalse();
                seated.Add((x, z));
            }
        }

        // The turn belongs to no run: the building surrounds it, so it faces open ground on no axis at all.
        foreach (var (x, z) in plan.Cells().Where(cell => plan.OnInnerCorner(cell.X, cell.Z)))
            await Assert.That(seated.Contains((x, z))).IsFalse();

        var built = plan.Cells().Where(cell => plan.OnPerimeter(cell.X, cell.Z)).ToHashSet();
        var accounted = seated.Concat(plan.Cells().Where(cell => plan.OnInnerCorner(cell.X, cell.Z))).ToHashSet();
        await Assert.That(accounted.OrderBy(c => c.X).ThenBy(c => c.Z))
            .IsEquivalentTo(built.OrderBy(c => c.X).ThenBy(c => c.Z));
    }

    /// <summary>An opening keeps two blocks off each end of the wall it is cut in, and it is the wall's own end
    /// that counts — so the short return beside a turn offers a short seat rather than the whole side's.</summary>
    [Test]
    public async Task A_run_seats_an_opening_two_blocks_clear_of_both_its_ends()
    {
        var hall = Ell().Segments.Single(wall => wall.Facing == RoomEdge.NegZ);
        await Assert.That(hall.Seat).IsEqualTo((2, 7));
        await Assert.That(hall.BetweenCorners).IsEqualTo((1, 8));

        // The wing's far end is five long, so its seat is the one cell in the middle: a wall that narrow says
        // it is narrow rather than pushing an opening against the turn.
        var end = Ell().Segments.Single(wall => wall is { Facing: RoomEdge.PosZ, Fixed: 9 });
        await Assert.That(end.Seat).IsEqualTo((2, 2));
    }

    /// <summary>Which wall a door handed in belongs to. On a rectangle the answer is the side it names; where a
    /// plan looks the same way twice, the one whose stretch reaches the place asked for wins, and length breaks
    /// a tie because a building is entered by its face rather than by its return.</summary>
    [Test]
    public async Task A_side_named_by_direction_resolves_to_the_run_that_reaches_the_place()
    {
        var plan = Ell();

        await Assert.That(plan.WallFacing(RoomEdge.PosZ, 7))
            .IsEqualTo(new WallSegment(RoomEdge.PosZ, 4, 5, 9));
        await Assert.That(plan.WallFacing(RoomEdge.PosZ, 2))
            .IsEqualTo(new WallSegment(RoomEdge.PosZ, 9, 0, 4));

        // A rectangle answers its one run whatever is asked of it.
        var box = new Footprint(0, 0, 14, 10);
        await Assert.That(box.WallFacing(RoomEdge.NegZ, 7))
            .IsEqualTo(new WallSegment(RoomEdge.NegZ, 0, 0, 14));
    }

    // ── a storey of its own ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A hall of two storeys with a wing of one running off it.</summary>
    private static Footprint Unequal()
        => new([new Wing(0, 0, 10, 6, Storeys: 2), new Wing(0, 7, 6, 12, Storeys: 1)]);

    /// <summary>A plan loses wings on the way up and never gains one, so a storey is a plan in its own right —
    /// the ground over both wings, the storey above over the hall alone, and nothing at all above that.</summary>
    [Test]
    public async Task A_storey_is_the_plan_of_the_wings_that_reach_it()
    {
        var plan = Unequal();

        await Assert.That(plan.At(0)).IsSameReferenceAs(plan);
        await Assert.That(plan.At(0)!.Holds(3, 10)).IsTrue();       // the wing, on the ground

        var upper = plan.At(1);
        await Assert.That(upper).IsNotNull();
        await Assert.That(upper!.Holds(3, 10)).IsFalse();           // the wing has stopped
        await Assert.That(upper.Holds(3, 3)).IsTrue();              // the hall carries on
        await Assert.That((upper.MinX, upper.MinZ, upper.MaxX, upper.MaxZ)).IsEqualTo((0, 0, 10, 6));

        await Assert.That(plan.At(2)).IsNull();                     // nothing stands three storeys up
    }

    /// <summary>The wall a taller wing needs against its neighbour's roof is no new rule: at that height the
    /// neighbour is not there, so the boundary between them is simply the storey's own outline.</summary>
    [Test]
    public async Task A_taller_wing_walls_itself_where_its_neighbour_has_stopped()
    {
        var plan = Unequal();

        // On the ground the two wings are one room, so the line between them carries no wall at all.
        await Assert.That(plan.OnPerimeter(3, 6)).IsFalse();

        // A storey up it is the hall's own north wall, and both ends of it are corners the hall turns away at.
        var upper = plan.At(1)!;
        await Assert.That(upper.OnPerimeter(3, 6)).IsTrue();
        await Assert.That(upper.Segments.Any(wall => wall == new WallSegment(RoomEdge.PosZ, 6, 0, 10))).IsTrue();
        await Assert.That(upper.Cells().Count(cell => upper.OnCorner(cell.X, cell.Z))).IsEqualTo(4);
        await Assert.That(upper.Cells().Any(cell => upper.OnInnerCorner(cell.X, cell.Z))).IsFalse();
    }

    /// <summary>A plan whose wings all stand the whole way up is the same plan at every storey, so the walk and
    /// the runs measured once serve all of them.</summary>
    [Test]
    public async Task A_plan_of_equal_wings_is_itself_at_every_storey()
    {
        var plan = Ell();
        await Assert.That(plan.At(1)).IsSameReferenceAs(plan);
        await Assert.That(plan.At(7)).IsSameReferenceAs(plan);
    }

    public static IEnumerable<Func<Footprint>> Plans() => [Ell, Tee];
}