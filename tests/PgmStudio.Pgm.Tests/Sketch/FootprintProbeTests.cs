using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// What a ring stands on, against a footprint held directly — the rasterised one, which is the only coast
/// that decides anything. Every case is a shape whose answer is known by construction.
/// </summary>
public sealed class FootprintProbeTests
{
    private static HashSet<(int X, int Z)> Rect(int x0, int z0, int width, int depth)
    {
        var cells = new HashSet<(int X, int Z)>();
        for (var x = x0; x < x0 + width; x++)
            for (var z = z0; z < z0 + depth; z++)
                cells.Add((x, z));
        return cells;
    }

    private static double[][] Ring(int x0, int z0, int x1, int z1) =>
        [[x0, z0], [x1, z0], [x1, z1], [x0, z1]];

    [Test]
    public async Task A_ring_wholly_on_land_reads_land_and_nothing_else()
    {
        var probe = FootprintProbe.Of(Rect(0, 0, 40, 40), Ring(10, 10, 20, 20));

        await Assert.That(probe.Land).IsEqualTo(probe.Cells);
        await Assert.That(probe.Void).IsEqualTo(0);
        await Assert.That(probe.Hole).IsEqualTo(0);
    }

    [Test]
    public async Task A_ring_overhanging_the_coast_names_the_cells_past_it()
    {
        // Two columns past the east coast — the case a rebuilt model of the coast rounds away and the
        // rasterised one does not.
        var probe = FootprintProbe.Of(Rect(0, 0, 20, 20), Ring(16, 5, 22, 10));

        await Assert.That(probe.Void).IsEqualTo(10);      // x 20..21 over z 5..9
        await Assert.That(probe.Hole).IsEqualTo(0);
        await Assert.That(probe.Land + probe.Void + probe.Hole).IsEqualTo(probe.Cells);
        await Assert.That(probe.VoidCells).Contains((20, 5));
        await Assert.That(probe.VoidCells.All(cell => cell.X >= 20)).IsTrue();
    }

    [Test]
    public async Task A_slot_the_arrangement_made_is_a_hole_and_not_open_void()
    {
        // A ring of land around a gap nothing declares: no region marks it, and a shape dropped on it fills
        // in the layout that was asked for. It is void by membership and a hole by enclosure, and the two
        // want telling apart because only one of them is outside the map.
        var footprint = Rect(0, 0, 30, 30);
        foreach (var cell in Rect(12, 12, 6, 6)) footprint.Remove(cell);

        var probe = FootprintProbe.Of(footprint, Ring(13, 13, 16, 16));

        await Assert.That(probe.Hole).IsEqualTo(probe.Cells);
        await Assert.That(probe.Void).IsEqualTo(0);
        await Assert.That(probe.HoleCells).Contains((13, 13));
    }

    [Test]
    public async Task A_ring_straddling_a_slot_and_the_coast_separates_the_two()
    {
        var footprint = Rect(0, 0, 30, 30);
        foreach (var cell in Rect(12, 12, 6, 6)) footprint.Remove(cell);

        var inside = FootprintProbe.Of(footprint, Ring(10, 10, 20, 20));
        await Assert.That(inside.Hole).IsEqualTo(36);
        await Assert.That(inside.Void).IsEqualTo(0);

        var past = FootprintProbe.Of(footprint, Ring(28, 10, 34, 14));
        await Assert.That(past.Void).IsGreaterThan(0);
        await Assert.That(past.Hole).IsEqualTo(0);
    }

    [Test]
    public async Task A_ring_of_two_points_covers_nothing_and_says_so()
    {
        var probe = FootprintProbe.Of(Rect(0, 0, 20, 20), [[0, 0], [10, 10]]);

        await Assert.That(probe.Cells).IsEqualTo(0);
        await Assert.That(probe.Land).IsEqualTo(0);
    }
}
