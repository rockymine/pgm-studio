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
}
