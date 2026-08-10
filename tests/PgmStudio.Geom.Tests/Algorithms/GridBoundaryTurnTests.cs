using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// Bend measurement over a traced perimeter: the windowed turn separates a corner from a curve on shapes that
/// exist only as squares. The cases are the ones that decide it — a shallow edge, which cell-to-cell is a
/// quarter turn at every cell and must measure straight over a window; a disc, which must stay well under any
/// corner threshold and read tighter as it shrinks; and a rectangle, whose four vertices must read their
/// exterior angle exactly. The 45° edge is pinned separately because a Moore trace draws it straight to begin
/// with, so it would pass a windowed test without exercising the window at all.
/// </summary>
public sealed class GridBoundaryTurnTests
{
    private static (int X, int Z)[] LoopOf(IEnumerable<(int X, int Z)> cells) =>
        GridBoundary.Loop(GridBoundary.TracePerimeter(cells));

    private static List<(int X, int Z)> Disc(int radius)
    {
        var cells = new List<(int X, int Z)>();
        for (var z = -radius; z <= radius; z++)
            for (var x = -radius; x <= radius; x++)
                if (x * x + z * z <= radius * radius) cells.Add((x, z));
        return cells;
    }

    private static List<(int X, int Z)> Rectangle(int width, int depth)
    {
        var cells = new List<(int X, int Z)>();
        for (var z = 0; z < depth; z++)
            for (var x = 0; x < width; x++) cells.Add((x, z));
        return cells;
    }

    // ── the staircase, which is the reason a window exists ───────────────────────────────────────────────

    /// <summary>A wedge whose sloped edge climbs one cell every <paramref name="run"/> across.</summary>
    private static List<(int X, int Z)> Wedge(int run, int length)
    {
        var cells = new List<(int X, int Z)>();
        for (var z = 0; z < length; z++)
            for (var x = 0; x <= z / run; x++) cells.Add((x, z));
        return cells;
    }

    private static List<int> SlopedEdge((int X, int Z)[] loop, int run) =>
        Enumerable.Range(0, loop.Length)
            .Where(index => loop[index].X == loop[index].Z / run && loop[index].Z is > 15 and < 45)
            .ToList();

    [Test]
    public async Task A_shallow_edge_reads_as_a_corner_at_every_cell_and_straight_over_a_window()
    {
        // A 1-in-2 edge alternates a diagonal step with an axis step, and each change of kind is a quarter
        // turn — so cell to cell a dead straight wall is 45° everywhere. This is what the window is for.
        var loop = LoopOf(Wedge(2, 60));
        var edge = SlopedEdge(loop, 2);
        await Assert.That(edge).IsNotEmpty();

        await Assert.That(edge.Max(index => GridBoundary.TurnAt(loop, index, 1))).IsEqualTo(45).Within(0.001);
        await Assert.That(edge.Max(index => GridBoundary.TurnAt(loop, index, 4))).IsEqualTo(0);
    }

    [Test]
    public async Task A_45_degree_edge_is_drawn_straight_in_the_first_place()
    {
        // The one slope a Moore trace renders without a staircase, since it may step diagonally. Worth
        // pinning precisely because it is the case that would pass a windowed test for the wrong reason.
        var loop = LoopOf(Wedge(1, 60));
        var edge = SlopedEdge(loop, 1);
        await Assert.That(edge).IsNotEmpty();

        await Assert.That(edge.Max(index => GridBoundary.TurnAt(loop, index, 1))).IsEqualTo(0);
        await Assert.That(edge.Max(index => GridBoundary.TurnAt(loop, index, 4))).IsEqualTo(0);
    }

    [Test]
    public async Task The_staircases_residue_falls_off_as_the_window_widens()
    {
        // Off the exact repeat a residue survives, and shrinking it is what buys a wider window. A 1-in-5
        // edge is the awkward case: no small window is a whole number of its repeat.
        var loop = LoopOf(Wedge(5, 60));
        var edge = SlopedEdge(loop, 5);
        double Worst(int window) => edge.Max(index => GridBoundary.TurnAt(loop, index, window));

        await Assert.That(Worst(1)).IsGreaterThan(Worst(4));
        await Assert.That(Worst(4)).IsGreaterThan(Worst(8));
        await Assert.That(Worst(4)).IsLessThan(20);     // already far below any corner threshold
    }

    // ── a curve reads as a curve, and reads tighter as it tightens ───────────────────────────────────────

    [Test]
    public async Task A_wide_disc_never_reaches_a_corner_threshold()
    {
        var loop = LoopOf(Disc(20));
        var sharpest = 0.0;
        for (var index = 0; index < loop.Length; index++)
            sharpest = Math.Max(sharpest, GridBoundary.TurnAt(loop, index, 6));

        // k/R is about 17° at this radius; the raster's own jitter lifts it, but nowhere near a 90° vertex.
        await Assert.That(sharpest).IsLessThan(45);
    }

    [Test]
    public async Task A_disc_reads_sharper_as_its_radius_falls()
    {
        static double Median(int radius)
        {
            var loop = LoopOf(Disc(radius));
            var turns = new List<double>();
            for (var index = 0; index < loop.Length; index++)
                turns.Add(GridBoundary.TurnAt(loop, index, 4));
            turns.Sort();
            return turns[turns.Count / 2];
        }

        // The turn on a circle goes as window / radius, so halving the radius roughly doubles the bend.
        await Assert.That(Median(24)).IsLessThan(Median(12));
        await Assert.That(Median(12)).IsLessThan(Median(6));
    }

    // ── a vertex reads its exterior angle ────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_rectangles_four_vertices_are_the_sharpest_cells_and_measure_a_right_angle()
    {
        var loop = LoopOf(Rectangle(20, 14));
        var corners = new HashSet<(int X, int Z)> { (0, 0), (19, 0), (0, 13), (19, 13) };

        var sharpest = Enumerable.Range(0, loop.Length)
            .Select(index => (Cell: loop[index], Turn: GridBoundary.TurnAt(loop, index, 5)))
            .OrderByDescending(entry => entry.Turn).Take(4).ToList();

        await Assert.That(sharpest.Select(entry => entry.Cell).ToHashSet()).IsEquivalentTo(corners);
        foreach (var entry in sharpest)
            await Assert.That(Math.Abs(entry.Turn - 90)).IsLessThan(1);
    }

    [Test]
    public async Task A_rectangles_edges_measure_straight_away_from_its_vertices()
    {
        var loop = LoopOf(Rectangle(20, 14));
        for (var index = 0; index < loop.Length; index++)
        {
            var cell = loop[index];
            var fromEnds = Math.Min(Math.Min(cell.X, 19 - cell.X), Math.Min(cell.Z, 13 - cell.Z));
            if (fromEnds == 0 && cell.X is > 5 and < 14)                    // mid-run of a long edge
                await Assert.That(GridBoundary.TurnAt(loop, index, 5)).IsEqualTo(0);
        }
    }

    // ── the ramp, which is what gives a caller a band around each corner ─────────────────────────────────

    [Test]
    public async Task The_turn_ramps_to_a_vertex_rather_than_switching_on_at_it()
    {
        var loop = LoopOf(Rectangle(20, 14));
        var corner = Array.FindIndex(loop, cell => cell == (19, 0));
        await Assert.That(corner).IsGreaterThanOrEqualTo(0);

        // Walking in from a window away, the measured bend rises without ever falling back.
        var climbing = new List<double>();
        for (var step = 5; step >= 0; step--) climbing.Add(GridBoundary.TurnAt(loop, corner - step, 5));

        for (var i = 1; i < climbing.Count; i++)
            await Assert.That(climbing[i]).IsGreaterThanOrEqualTo(climbing[i - 1]);
        await Assert.That(climbing[0]).IsLessThan(45);      // a window out, barely bent
        await Assert.That(climbing[^1]).IsGreaterThan(85);  // at the vertex, a right angle
    }

    // ── degenerate loops ─────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_loop_too_small_to_bend_reports_nothing()
    {
        await Assert.That(GridBoundary.TurnAt([(0, 0)], 0, 4)).IsEqualTo(0);
        await Assert.That(GridBoundary.TurnAt([(0, 0), (1, 0)], 0, 4)).IsEqualTo(0);
    }

    [Test]
    public async Task A_window_wider_than_the_loop_shrinks_to_fit_instead_of_reading_across_it()
    {
        var loop = LoopOf(Rectangle(3, 3));                 // an 8-cell ring
        for (var index = 0; index < loop.Length; index++)
        {
            var wide = GridBoundary.TurnAt(loop, index, 50);
            await Assert.That(double.IsFinite(wide)).IsTrue();
            await Assert.That(wide).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Turns_measures_the_whole_ring_and_agrees_with_the_single_reading()
    {
        var perimeter = GridBoundary.TracePerimeter(Rectangle(12, 9));
        var loop = GridBoundary.Loop(perimeter);
        var all = GridBoundary.Turns(perimeter, 5);

        await Assert.That(all.Count).IsEqualTo(loop.Length);
        for (var index = 0; index < loop.Length; index++)
            await Assert.That(all[loop[index]]).IsEqualTo(GridBoundary.TurnAt(loop, index, 5));
    }
}
