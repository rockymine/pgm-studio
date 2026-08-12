namespace PgmStudio.Geom.Relief;

/// <summary>
/// The ground a relief is solved over: the block cells a shape group rasterizes into, held as a dense grid
/// over their bounding box so the solver's neighbour reads are array indexing rather than dictionary probes.
/// Membership follows the sketch rasterizer's rule exactly — a cell is inside when its <em>centre</em> falls
/// in the ring — so the field is solved over the same cells the world is built from.
///
/// <para>The outline is not only a clip. Every sweep in the solver refuses to step off it, which makes it a
/// no-flow boundary: the surface neither climbs nor falls as it meets the void unless a mark says so, and it
/// bends around a notch rather than through it.</para>
/// </summary>
public sealed class Footprint
{
    public readonly int MinX, MinZ, Width, Depth;
    private readonly bool[] _inside;

    public Footprint(int minX, int minZ, int width, int depth)
    {
        MinX = minX; MinZ = minZ; Width = width; Depth = depth;
        _inside = new bool[Math.Max(0, width) * Math.Max(0, depth)];
    }

    /// <summary>How many cells are land.</summary>
    public int Count
    {
        get
        {
            var total = 0;
            foreach (var cell in _inside) if (cell) total++;
            return total;
        }
    }

    public int Cells => _inside.Length;
    public int Index(int x, int z) => (z - MinZ) * Width + (x - MinX);
    public bool InBounds(int x, int z) => x >= MinX && z >= MinZ && x < MinX + Width && z < MinZ + Depth;
    public bool Inside(int x, int z) => InBounds(x, z) && _inside[Index(x, z)];
    public bool InsideAt(int index) => index >= 0 && index < _inside.Length && _inside[index];
    public (int X, int Z) CellAt(int index) => (MinX + index % Width, MinZ + index / Width);

    public IEnumerable<(int X, int Z)> Land()
    {
        for (var index = 0; index < _inside.Length; index++)
            if (_inside[index]) yield return CellAt(index);
    }

    public void Add(IEnumerable<(int X, int Z)> cells)
    {
        foreach (var (x, z) in cells) if (InBounds(x, z)) _inside[Index(x, z)] = true;
    }

    public void Remove(IEnumerable<(int X, int Z)> cells)
    {
        foreach (var (x, z) in cells) if (InBounds(x, z)) _inside[Index(x, z)] = false;
    }

    /// <summary>The footprint a set of add/subtract rings covers, sized to their combined bounds plus a
    /// margin. The margin exists so a mark placed just past an edge still has grid to be clipped against.</summary>
    public static Footprint Of(IEnumerable<(double[][] Ring, bool Subtract)> parts, int margin = 2)
    {
        var rings = parts.ToList();
        var points = rings.SelectMany(part => part.Ring).ToList();
        if (points.Count == 0) return new Footprint(0, 0, 0, 0);

        int minX = (int)Math.Floor(points.Min(point => point[0])) - margin;
        int maxX = (int)Math.Ceiling(points.Max(point => point[0])) + margin;
        int minZ = (int)Math.Floor(points.Min(point => point[1])) - margin;
        int maxZ = (int)Math.Ceiling(points.Max(point => point[1])) + margin;

        var footprint = new Footprint(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
        foreach (var (ring, subtract) in rings)
        {
            var cells = Rasterize(ring, minX, minZ, maxX, maxZ);
            if (subtract) footprint.Remove(cells); else footprint.Add(cells);
        }
        return footprint;
    }

    /// <summary>A footprint over the given bounds holding exactly the listed cells — how a caller that has
    /// already rasterized a layout hands its own columns in.</summary>
    public static Footprint Over(IReadOnlyCollection<(int X, int Z)> cells, int margin = 2)
    {
        if (cells.Count == 0) return new Footprint(0, 0, 0, 0);
        int minX = cells.Min(cell => cell.X) - margin, maxX = cells.Max(cell => cell.X) + margin;
        int minZ = cells.Min(cell => cell.Z) - margin, maxZ = cells.Max(cell => cell.Z) + margin;
        var footprint = new Footprint(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
        footprint.Add(cells);
        return footprint;
    }

    public static List<(int X, int Z)> Rasterize(double[][] ring, int minX, int minZ, int maxX, int maxZ)
    {
        var cells = new List<(int X, int Z)>();
        if (ring.Length < 3) return cells;
        for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
                if (Polygon.PointInRing(x + 0.5, z + 0.5, ring)) cells.Add((x, z));
        return cells;
    }
}
