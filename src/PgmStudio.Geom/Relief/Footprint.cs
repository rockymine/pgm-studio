namespace PgmStudio.Geom.Relief;

/// <summary>
/// The ground a height field is read over: block cells held as a dense grid over their bounding box, so a
/// neighbour read is array indexing rather than a dictionary probe. A cell is inside when the world has a
/// column there.
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
}
