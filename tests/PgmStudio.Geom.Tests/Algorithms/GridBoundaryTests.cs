using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// Perimeter tracing: a filled block numbers exactly its outer ring and leaves the interior out, numbering
/// starts at the top-left cell, a single cell is arc 0, and the empty set traces nothing.
/// </summary>
public sealed class GridBoundaryTests
{
    [Test]
    public async Task A_filled_block_numbers_its_ring_and_skips_the_interior()
    {
        var cells = new List<(int X, int Z)>();
        for (var x = 0; x < 3; x++)
        for (var z = 0; z < 3; z++)
            cells.Add((x, z));

        var arc = GridBoundary.TracePerimeter(cells);
        await Assert.That(arc.Count).IsEqualTo(8);           // the 8 ring cells, not the centre
        await Assert.That(arc.ContainsKey((1, 1))).IsFalse();
        await Assert.That(arc[(0, 0)]).IsEqualTo(0);          // start = top-left, entered from the west
        // Contiguous 0..7 around the loop.
        await Assert.That(arc.Values.OrderBy(v => v).ToList()).IsEquivalentTo(Enumerable.Range(0, 8).ToList());
    }

    [Test]
    public async Task A_single_cell_is_arc_zero()
    {
        var arc = GridBoundary.TracePerimeter([(4, 7)]);
        await Assert.That(arc.Count).IsEqualTo(1);
        await Assert.That(arc[(4, 7)]).IsEqualTo(0);
    }

    [Test]
    public async Task The_empty_set_traces_nothing()
    {
        await Assert.That(GridBoundary.TracePerimeter([]).Count).IsEqualTo(0);
    }

    /// <summary>
    /// The walk must stop when its loop closes, not run on to a backstop. This is a cost test, not an output
    /// one: the trace returned the correct ring even while every call ran to an iteration cap, so no assertion
    /// about the result could see it — only the flat ~110 ms per call it charged whatever the input size, which
    /// was most of the terrain painter's time. Two hundred small squares finish in milliseconds when the walk
    /// terminates and take over twenty seconds when it does not, so the bound is loose and still decisive.
    /// </summary>
    [Test]
    public async Task A_trace_stops_when_its_loop_closes()
    {
        var square = new List<(int X, int Z)>();
        for (var x = 0; x < 20; x++)
        for (var z = 0; z < 20; z++)
            square.Add((x, z));

        var started = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 200; i++)
            await Assert.That(GridBoundary.TracePerimeter(square).Count).IsEqualTo(76);   // 20*4 - 4
        await Assert.That(started.Elapsed.TotalSeconds).IsLessThan(1);
    }

    // ── steps inward ────────────────────────────────────────────────────────────────────────────────
    /// <summary>The walk this lift replaced, written out here as it stood inside the building plan: seed every
    /// cell with an eight-neighbour outside the set, then flood four-connected. Kept as a test oracle rather
    /// than deleted with the original, because a shared walk is only safe to share if it answers what both of
    /// its callers were already answering — and a rectangle alone cannot tell the two apart.</summary>
    private static Dictionary<(int X, int Z), int> Original(HashSet<(int X, int Z)> region)
    {
        bool Holds(int x, int z) => region.Contains((x, z));
        bool OnPerimeter(int x, int z)
        {
            if (!Holds(x, z)) return false;
            for (var dx = -1; dx <= 1; dx++)
                for (var dz = -1; dz <= 1; dz++)
                    if ((dx != 0 || dz != 0) && !Holds(x + dx, z + dz)) return true;
            return false;
        }

        var steps = new Dictionary<(int X, int Z), int>();
        var queue = new Queue<(int X, int Z)>();
        foreach (var (x, z) in region)
            if (OnPerimeter(x, z)) { steps[(x, z)] = 0; queue.Enqueue((x, z)); }
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            var next = steps[(x, z)] + 1;
            foreach (var (nx, nz) in new[] { (x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1) })
                if (Holds(nx, nz) && !steps.ContainsKey((nx, nz)))
                {
                    steps[(nx, nz)] = next;
                    queue.Enqueue((nx, nz));
                }
        }
        return steps;
    }

    private static HashSet<(int X, int Z)> Rect(int minX, int minZ, int maxX, int maxZ)
    {
        var cells = new HashSet<(int X, int Z)>();
        for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++) cells.Add((x, z));
        return cells;
    }

    [Test]
    [Arguments("rectangle")]
    [Arguments("ell")]
    [Arguments("tee")]
    [Arguments("holed")]
    [Arguments("split")]
    [Arguments("crook")]
    public async Task The_lifted_walk_answers_what_the_building_plans_own_walk_answered(string shape)
    {
        var cells = shape switch
        {
            "rectangle" => Rect(0, 0, 9, 6),
            // Two arms meeting, so the cell in the crook has four orthogonal neighbours inside the set and is
            // still on the outline — the case an orthogonal seed test gets wrong.
            "ell" => [.. Rect(0, 0, 10, 6), .. Rect(0, 7, 5, 13)],
            "tee" => [.. Rect(0, 0, 12, 4), .. Rect(4, 5, 8, 14)],
            "holed" => [.. Rect(0, 0, 10, 10).Except(Rect(4, 4, 6, 6))],
            "split" => [.. Rect(0, 0, 4, 4), .. Rect(20, 20, 26, 24)],
            // Two blocks touching only at a corner: nothing steps diagonally, so the flood cannot cross.
            "crook" => [.. Rect(0, 0, 3, 3), .. Rect(4, 4, 7, 7)],
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

        var lifted = GridBoundary.StepsInward(cells);
        var original = Original(cells);

        await Assert.That((shape, lifted.Count)).IsEqualTo((shape, original.Count));
        foreach (var (cell, step) in original)
            await Assert.That((shape, cell, lifted.GetValueOrDefault(cell, -1))).IsEqualTo((shape, cell, step));
    }

    [Test]
    public async Task The_edge_is_nought_and_the_count_grows_inward()
    {
        var steps = GridBoundary.StepsInward(Rect(0, 0, 8, 8));

        await Assert.That(steps[(0, 0)]).IsEqualTo(0);      // a corner of the edge
        await Assert.That(steps[(4, 0)]).IsEqualTo(0);      // mid-edge
        await Assert.That(steps[(1, 1)]).IsEqualTo(1);
        await Assert.That(steps[(4, 4)]).IsEqualTo(4);      // the middle of a 9x9
        await Assert.That(steps.ContainsKey((9, 9))).IsFalse();   // outside the region
    }

    [Test]
    public async Task A_cell_counts_to_the_edge_actually_nearest_it()
    {
        // Multi-source, not one seed at a time: the narrow arm of an L is two wide, so nothing in it is more
        // than one step from a wall however far it runs from the hall.
        HashSet<(int X, int Z)> ell = [.. Rect(0, 0, 20, 20), .. Rect(21, 0, 24, 3)];
        var steps = GridBoundary.StepsInward(ell);

        await Assert.That(steps[(23, 1)]).IsEqualTo(1);
        await Assert.That(steps[(10, 10)]).IsEqualTo(10);
    }

    [Test]
    public async Task The_answer_does_not_depend_on_the_order_the_cells_arrive_in()
    {
        HashSet<(int X, int Z)> shape = [.. Rect(0, 0, 10, 6), .. Rect(0, 7, 5, 13)];
        var forward = GridBoundary.StepsInward(shape.OrderBy(c => c.X).ThenBy(c => c.Z).ToList());
        var backward = GridBoundary.StepsInward(shape.OrderByDescending(c => c.Z).ThenByDescending(c => c.X).ToList());

        await Assert.That(forward.Count).IsEqualTo(backward.Count);
        foreach (var (cell, step) in forward)
            await Assert.That((cell, backward.GetValueOrDefault(cell, -1))).IsEqualTo((cell, step));
    }
}
