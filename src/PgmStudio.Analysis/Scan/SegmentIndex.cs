using PgmStudio.Geom;

namespace PgmStudio.Analysis.Scan;

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
    /// column solid to the sky, or roofed everywhere at less than <see cref="Walk.Headroom"/>, is not one.
    /// The storey is discarded here and only here; a caller that needs it takes the tops.</summary>
    public HashSet<(int, int)> StandingColumns() => StandingTops().Select(row => (row.x, row.z)).ToHashSet();

    /// <summary>Lowest solid block per column (x, z, y) — the bottom-up base scan that feeds
    /// floating-mass pruning (a build floating over void reads at its own high Y, the ground below it
    /// at the terrain Y).</summary>
    public IEnumerable<(int x, int z, int y)> BaseColumns()
        => _byCol.Select(kv => (kv.Key.x, kv.Key.z, kv.Value.Min(s => s.ys)));

    /// <summary>Every place a player can stand, with how much room is over it: the first air above each
    /// surface that carries <see cref="Walk.Headroom"/> clear blocks, and the number of clear blocks there
    /// are before the next solid one. A column offering no such surface is not returned at all.
    ///
    /// <para>A stacked column answers more than once — a gallery under a deck is two places, and they are
    /// different somewhere to be. The clearance is what keeps them apart: a player builds up through open air
    /// and falls down through it, so a roof sixteen blocks over a floor is what says the deck above is not a
    /// step from it, while the same floor where the roof is cut away is open to the sky.</para>
    ///
    /// <para>The headroom test is load-bearing on its own: the surface under a building is the course its
    /// floor sits on, and a walk that took it would cross the walls as if they were not there.</para></summary>
    public IEnumerable<(int x, int z, int top, int clear)> StandingTops()
    {
        foreach (var (cell, segments) in _byCol)
            foreach (var (top, clear) in Walk.Standing([.. segments.Select(s => (s.ys, s.ye))]))
                yield return (cell.x, cell.z, top, clear);
    }

    public bool IsSolid(int x, int y, int z)
        => _byCol.TryGetValue((x, z), out var segs) && segs.Any(s => s.ys <= y && y <= s.ye);

    public bool IsAir(int x, int y, int z) => !IsSolid(x, y, z);
}
