namespace PgmStudio.Domain;

/// <summary>
/// A stamped structure's block volume, min and max both inclusive. It is the single source of truth for
/// both halves of an objective: the blocks that get placed, and the <c>&lt;region&gt;</c> emitted around
/// them. The two must agree — PGM builds the goal from the blocks matching <c>materials</c> <i>inside</i>
/// the region, so a region that misses the structure yields a zero-health goal with nothing but a warning.
/// Compute it once and share it; never let the stamper and the region generator derive it independently.
/// </summary>
public readonly record struct BlockBox(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;
    public int Depth => MaxZ - MinZ + 1;

    /// <summary>The exclusive max a PGM cuboid wants: a cuboid spans blocks <c>[min, max)</c>, so its
    /// <c>max</c> attribute is one past the last block on each axis.</summary>
    public (int X, int Y, int Z) CuboidMax => (MaxX + 1, MaxY + 1, MaxZ + 1);
}
