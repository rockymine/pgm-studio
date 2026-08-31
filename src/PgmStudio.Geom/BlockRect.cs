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
}
