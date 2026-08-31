using System.Text.Json.Serialization;
using PgmStudio.Geom;

namespace PgmStudio.Geom;

/// <summary>
/// An axis-aligned rectangle on an integer cell grid: an <b>origin plus an extent</b> — <see cref="X"/>,
/// <see cref="Z"/> is the min corner and <see cref="Width"/> × <see cref="Height"/> the cell counts along
/// each axis. <see cref="MaxX"/>/<see cref="MaxZ"/> are therefore <b>exclusive</b>: one past the far edge,
/// so <c>MaxX - X == Width</c> and a rect's cells are <c>x ∈ [X, MaxX)</c>.
///
/// <para>This replaces the bare <c>int[]</c> the plan layer passed around as <c>[x, z, w, h]</c>, where the
/// convention lived only in comments — a three-element array compiled, and reading <c>[3]</c> as a depth
/// rather than a height compiled too.</para>
///
/// <para><b>Not <see cref="Rect"/></b>, its neighbour in this namespace, which is the opposite convention
/// throughout: world <b>blocks</b>, fractional, and an inclusive corner pair. Nor <see cref="BlockRect"/>,
/// which shares this arithmetic exactly — integer, exclusive far edge — and differs only in reading world
/// blocks where this reads grid cells. A cell is several blocks wide, so one standing in for the other is off
/// by that factor and compiles.</para>
/// </summary>
/// <param name="X">Min-corner cell on the x axis.</param>
/// <param name="Z">Min-corner cell on the z axis.</param>
/// <param name="Width">Cell count along x. Zero or negative means empty.</param>
/// <param name="Height">Cell count along z. Zero or negative means empty.</param>
[JsonConverter(typeof(CellRectJsonConverter))]
public readonly record struct CellRect(int X, int Z, int Width, int Height)
{
    /// <summary>One past the far edge on x — <c>X + Width</c>.</summary>
    public int MaxX => X + Width;

    /// <summary>One past the far edge on z — <c>Z + Height</c>.</summary>
    public int MaxZ => Z + Height;

    /// <summary>The cell count the rect covers.</summary>
    public int Area => Width * Height;

    /// <summary>True when the rect covers no cells.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>A rect from an <b>exclusive</b> corner pair, the inverse of <see cref="MaxX"/>/<see cref="MaxZ"/>.</summary>
    public static CellRect FromBounds(int minX, int minZ, int maxX, int maxZ) =>
        new(minX, minZ, maxX - minX, maxZ - minZ);

    /// <summary>A rect from an <b>inclusive</b> corner pair — the convention a cell-set extent is read in
    /// (both corners are occupied cells), so the extent gains the one cell the exclusive form does not count.</summary>
    public static CellRect FromInclusive(int minX, int minZ, int maxX, int maxZ) =>
        new(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);

    /// <summary>Whether the cell <c>(x, z)</c> lies inside — half-open, matching <see cref="MaxX"/>/<see cref="MaxZ"/>.</summary>
    public bool Contains(int x, int z) => x >= X && x < MaxX && z >= Z && z < MaxZ;

    /// <summary>Whether the two rects share at least one cell (touching edges do not count).</summary>
    public bool Intersects(CellRect other) =>
        X < other.MaxX && other.X < MaxX && Z < other.MaxZ && other.Z < MaxZ;

    /// <summary>The same extent moved by <c>(dx, dz)</c> cells.</summary>
    public CellRect Translate(int dx, int dz) => new(X + dx, Z + dz, Width, Height);

    /// <summary>The wire form the plan format stores: <c>[x, z, w, h]</c>.</summary>
    public int[] ToArray() => [X, Z, Width, Height];

    /// <summary>Read the wire form. A shorter array reads as zeroes rather than throwing, matching the
    /// tolerance the hand-written plan format had when it was a bare <c>int[]</c>.</summary>
    public static CellRect FromArray(IReadOnlyList<int>? a) => a is null
        ? default
        : new(At(a, 0), At(a, 1), At(a, 2), At(a, 3));

    private static int At(IReadOnlyList<int> a, int i) => i < a.Count ? a[i] : 0;

    public override string ToString() => $"[{X},{Z} {Width}x{Height}]";
}
