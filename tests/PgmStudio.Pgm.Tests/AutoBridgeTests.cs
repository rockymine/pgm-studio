using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// AutoBridge infers the build-area rectangles that connect a sketch's separate islands. One fewer bridge
/// than islands (a spanning tree), and overlaying a bridge's cells onto the islands collapses the gap it
/// spans — so the bridged footprint is one connected component.
/// </summary>
public sealed class AutoBridgeTests
{
    private static HashSet<(int, int)> Square(int x0, int z0, int side)
    {
        var s = new HashSet<(int, int)>();
        for (var x = x0; x < x0 + side; x++)
            for (var z = z0; z < z0 + side; z++)
                s.Add((x, z));
        return s;
    }

    private static HashSet<(int, int)> RectCells(Rect r)
    {
        var s = new HashSet<(int, int)>();
        for (var x = (int)Math.Floor(r.MinX); x <= (int)Math.Ceiling(r.MaxX); x++)
            for (var z = (int)Math.Floor(r.MinZ); z <= (int)Math.Ceiling(r.MaxZ); z++)
                s.Add((x, z));
        return s;
    }

    private static int ComponentCount(HashSet<(int, int)> cells)
    {
        var seen = new HashSet<(int, int)>();
        var count = 0;
        foreach (var start in cells)
        {
            if (!seen.Add(start)) continue;
            count++;
            var stack = new Stack<(int, int)>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var (x, z) = stack.Pop();
                for (var dx = -1; dx <= 1; dx++)
                    for (var dz = -1; dz <= 1; dz++)
                        if (cells.Contains((x + dx, z + dz)) && seen.Add((x + dx, z + dz)))
                            stack.Push((x + dx, z + dz));
            }
        }
        return count;
    }

    [Test]
    public async Task A_single_island_needs_no_bridge()
    {
        await Assert.That(AutoBridge.Infer(Square(0, 0, 5), 4).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Two_islands_get_one_bridge_that_connects_them()
    {
        var cells = new HashSet<(int, int)>(Square(0, 0, 5));
        cells.UnionWith(Square(0, 10, 5));                 // gap at z 5..9
        await Assert.That(ComponentCount(cells)).IsEqualTo(2);

        var bridges = AutoBridge.Infer(cells, 4);
        await Assert.That(bridges.Count).IsEqualTo(1);

        foreach (var b in bridges) cells.UnionWith(RectCells(b));
        await Assert.That(ComponentCount(cells)).IsEqualTo(1);
    }

    [Test]
    public async Task Three_islands_get_two_bridges_into_one_plane()
    {
        var cells = new HashSet<(int, int)>(Square(0, 0, 5));
        cells.UnionWith(Square(0, 10, 5));
        cells.UnionWith(Square(0, 20, 5));
        var bridges = AutoBridge.Infer(cells, 4);
        await Assert.That(bridges.Count).IsEqualTo(2);

        foreach (var b in bridges) cells.UnionWith(RectCells(b));
        await Assert.That(ComponentCount(cells)).IsEqualTo(1);
    }
}
