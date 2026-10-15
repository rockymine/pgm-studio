using PgmStudio.Geom;
namespace PgmStudio.Geom;

/// <summary>An axis-aligned rectangle in world <b>blocks</b>, min inclusive and max exclusive.
///
/// <para>Same arithmetic as <see cref="CellRect"/>, one unit up: a plan cell is several blocks wide, and this
/// is what a cell rect becomes once a plan's cell size is applied. They stay two types for that reason — the
/// factor between them is invisible to the compiler, so a cell rect passed where blocks are meant is a map
/// built at a fifth of its size and no error anywhere. Where one is derived from the other the conversion is
/// explicit, in <c>PlanVoids</c> and <c>PlanCompiler</c>.</para></summary>
public readonly record struct BlockRect(int MinX, int MinZ, int MaxX, int MaxZ)
{
    public int Width => MaxX - MinX;
    public int Depth => MaxZ - MinZ;
    public double CenterX => (MinX + MaxX) / 2.0;
    public double CenterZ => (MinZ + MaxZ) / 2.0;

    /// <summary>The smallest rect holding every one of <paramref name="columns"/>, which must not be empty —
    /// the footprint a read of a world's columns is drawn over.</summary>
    public static BlockRect Around(IEnumerable<(int X, int Z)> columns)
    {
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        foreach (var (x, z) in columns)
        {
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
        }
        if (minX > maxX) throw new ArgumentException("no column to hold", nameof(columns));
        return new BlockRect(minX, minZ, maxX + 1, maxZ + 1);
    }
}
