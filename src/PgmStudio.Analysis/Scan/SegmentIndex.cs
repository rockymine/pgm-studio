using PgmStudio.Geom;

namespace PgmStudio.Analysis.Scan;

/// <summary>
/// Vertical-segment terrain index: solid Y-ranges per (x,z) column, and the floor marks — y=0 blocks no
/// segment holds. The single source for Y=0 presence (buildability void), where a player stands in a column
/// and whether they can stand there at all (traversability, and every walk over a scanned board), and
/// air-at-a-point (monument obstruction).
/// </summary>
public sealed class SegmentIndex
{
    private readonly Dictionary<(int x, int z), List<(int ys, int ye)>> _byCol = new();
    private readonly HashSet<(int, int)> _floorMarks;

    private readonly Dictionary<(int x, int z), List<(int ys, int ye)>> _doors = new();

    public SegmentIndex(IEnumerable<(int x, int z, int ys, int ye)> rows, IEnumerable<(int x, int z)>? floorMarks = null,
        IEnumerable<(int x, int z, int ys, int ye)>? doorRuns = null)
    {
        foreach (var (x, z, ys, ye) in rows)
        {
            if (!_byCol.TryGetValue((x, z), out var list)) { list = []; _byCol[(x, z)] = list; }
            list.Add((ys, ye));
        }
        _floorMarks = floorMarks is null ? [] : [.. floorMarks];
        foreach (var (x, z, ys, ye) in doorRuns ?? [])
        {
            if (!_doors.TryGetValue((x, z), out var list)) { list = []; _doors[(x, z)] = list; }
            list.Add((ys, ye));
        }
    }

    /// <summary>Columns whose y=0 block is a floor mark — not void to PGM, and no ground to stand on.</summary>
    public IReadOnlySet<(int, int)> FloorMarks => _floorMarks;

    /// <summary>Columns PGM's void filter reads as not void: a block at Y=0, whether a segment's or a floor
    /// mark's. A block-36 marker counts although PGM removes it on load, because the filter remembers it.</summary>
    public HashSet<(int, int)> Y0Columns()
    {
        var columns = _byCol.Where(kv => kv.Value.Any(s => s.ys <= 0 && 0 <= s.ye)).Select(kv => kv.Key).ToHashSet();
        columns.UnionWith(_floorMarks);
        return columns;
    }

    /// <summary>The world's column extent, <c>(minX, minZ, maxX, maxZ)</c> inclusive, over every column any
    /// segment or floor mark holds.</summary>
    public (int MinX, int MinZ, int MaxX, int MaxZ) Extent()
    {
        var columns = _byCol.Keys.Concat(_floorMarks).ToList();
        return columns.Count == 0 ? (0, 0, -1, -1)
            : (columns.Min(c => c.Item1), columns.Min(c => c.Item2), columns.Max(c => c.Item1), columns.Max(c => c.Item2));
    }

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
    /// floor sits on, and a walk that took it would cross the walls as if they were not there.</para>
    ///
    /// <para><paramref name="opens"/> names the columns a player may break blocks in; there a door run — the
    /// glass or fence a map closes a doorway with — is taken out of its segment, so the floor it stands on is a
    /// surface with its doorway's room over it. Absent, every door stays shut.</para></summary>
    public IEnumerable<(int x, int z, int top, int clear)> StandingTops(Func<(int x, int z), bool>? opens = null)
    {
        foreach (var (cell, segments) in _byCol)
        {
            var spans = segments.Select(s => (s.ys, s.ye)).ToList();
            if (opens is not null && _doors.TryGetValue(cell, out var doors) && opens(cell))
                spans = Open(spans, doors);
            foreach (var (top, clear) in Walk.Standing(spans, []))
                yield return (cell.x, cell.z, top, clear);
        }
    }

    /// <summary>Solid spans with the door runs cut out of them.</summary>
    private static List<(int ys, int ye)> Open(List<(int ys, int ye)> spans, List<(int ys, int ye)> doors)
    {
        foreach (var (doorStart, doorEnd) in doors)
            spans = [.. spans.SelectMany(span => span.ye < doorStart || span.ys > doorEnd
                ? [span]
                : new[] { (span.ys, doorStart - 1), (doorEnd + 1, span.ye) }.Where(part => part.Item1 <= part.Item2))];
        return spans;
    }

    /// <summary>Whether the scan reached this column at all. A column it never read answers <c>IsAir</c> to
    /// everything, which is the right answer for a caller asking what is standing somewhere and the wrong
    /// one for a caller asking whether anything is: an unscanned column is not an empty one.</summary>
    public bool Scanned(int x, int z) => _byCol.ContainsKey((x, z));

    public bool IsSolid(int x, int y, int z)
        => _byCol.TryGetValue((x, z), out var segs) && segs.Any(s => s.ys <= y && y <= s.ye);

    public bool IsAir(int x, int y, int z) => !IsSolid(x, y, z);
}
