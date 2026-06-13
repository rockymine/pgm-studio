namespace PgmStudio.Analysis;

/// <summary>
/// Vertical-segment terrain index (port of studio/services/segments.py): solid Y-ranges per
/// (x,z) column. The single source for Y=0 presence (buildability void), surface/walkable
/// columns (traversability), and air-at-a-point (monument obstruction).
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

    /// <summary>Columns with any solid block — a walkable surface (≡ layer_surface).</summary>
    public HashSet<(int, int)> SurfaceColumns() => _byCol.Keys.ToHashSet();

    public bool IsSolid(int x, int y, int z)
        => _byCol.TryGetValue((x, z), out var segs) && segs.Any(s => s.ys <= y && y <= s.ye);

    public bool IsAir(int x, int y, int z) => !IsSolid(x, y, z);
}
