using PgmStudio.Geom;

namespace PgmStudio.Analysis.Layer;

/// <summary>
/// Vertical-segment terrain index: solid Y-ranges per (x,z) column. The single source for Y=0 presence
/// (buildability void), where a player stands in a column and whether they can stand there at all
/// (traversability, and every walk over a scanned board), and air-at-a-point (monument obstruction).
/// </summary>
public sealed class SegmentIndex
{
    private readonly Dictionary<(int x, int z), List<(int ys, int ye)>> _byCol = new();

    public SegmentIndex(IEnumerable<(int x, int z, int ys, int ye)> rows)
    {
        foreach (var (x, z, ys, ye) in rows)
        {
            if (!_byCol.TryGetValue((x, z), out var list)) { list = []; _byCol[(x, z)] = list; }
            list.Add((ys, ye));
        }
    }

    /// <summary>Columns with a solid block at Y=0 (≡ layer_y0).</summary>
    public HashSet<(int, int)> Y0Columns()
        => _byCol.Where(kv => kv.Value.Any(s => s.ys <= 0 && 0 <= s.ye)).Select(kv => kv.Key).ToHashSet();

    /// <summary>Columns a player can stand in — those <see cref="StandingTops"/> finds a surface for. A
    /// column solid to the sky, or roofed everywhere at less than <see cref="Walk.Headroom"/>, is not one.</summary>
    public HashSet<(int, int)> StandingColumns() => StandingTops().Select(row => (row.x, row.z)).ToHashSet();

    /// <summary>Lowest solid block per column (x, z, y) — the bottom-up base scan that feeds
    /// floating-mass pruning (a build floating over void reads at its own high Y, the ground below it
    /// at the terrain Y).</summary>
    public IEnumerable<(int x, int z, int y)> BaseColumns()
        => _byCol.Select(kv => (kv.Key.x, kv.Key.z, kv.Value.Min(s => s.ys)));

    /// <summary>Where a player stands in each column: the first air over the <b>lowest</b> surface that
    /// carries <see cref="Walk.Headroom"/> clear blocks above it, which is where someone walking in at
    /// terrain level ends up. A column offering no such surface is not returned at all.
    ///
    /// <para>Both halves of that rule are load-bearing. Lowest, because the highest surface of a wooded cell
    /// is its canopy and of a roofed cell its ridge, and a route that had to climb either would avoid every
    /// tree on the board. With headroom, because the lowest surface under a building is the course its floor
    /// sits on, and a walk reading that crosses the walls as if they were not there.</para></summary>
    public IEnumerable<(int x, int z, int top)> StandingTops()
    {
        foreach (var (cell, segments) in _byCol)
            foreach (var (_, ye) in segments.OrderBy(segment => segment.ye))
            {
                var top = ye + 1;
                if (top + Walk.Headroom > Walk.WorldHeight) continue;
                if (Enumerable.Range(top, Walk.Headroom).Any(y => IsSolid(cell.x, y, cell.z))) continue;
                yield return (cell.x, cell.z, top);
                break;
            }
    }

    public bool IsSolid(int x, int y, int z)
        => _byCol.TryGetValue((x, z), out var segs) && segs.Any(s => s.ys <= y && y <= s.ye);

    public bool IsAir(int x, int y, int z) => !IsSolid(x, y, z);
}
