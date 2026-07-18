namespace PgmStudio.Domain;

/// <summary>
/// An inclusive integer axis-aligned box in block space — <see cref="MinX"/>..<see cref="MaxX"/> etc. all
/// inclusive. It serves two roles that share this one shape and convention:
/// <list type="bullet">
/// <item>a <b>stamped structure's block volume</b> — the single source of truth for both halves of an
/// objective: the blocks that get placed, and the <c>&lt;region&gt;</c> emitted around them. The two must
/// agree — PGM builds the goal from the blocks matching <c>materials</c> <i>inside</i> the region, so a
/// region that misses the structure yields a zero-health goal with nothing but a warning. Compute it once
/// and share it; never let the stamper and the region generator derive it independently.</item>
/// <item>a <b>scanned region</b> (world coords) — the box the author drew around a monument area. Bounds
/// both the scan and the candidate anchors; the dominant false-positive filter (off-site signs fall
/// outside it).</item>
/// </list>
/// A drawing frame with <i>exclusive</i> maxes is a different convention for a different job and stays its
/// own type — do not fold it in here.
/// </summary>
public readonly record struct BlockBox(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;
    public int Depth => MaxZ - MinZ + 1;

    /// <summary>The exclusive max a PGM cuboid wants: a cuboid spans blocks <c>[min, max)</c>, so its
    /// <c>max</c> attribute is one past the last block on each axis.</summary>
    public (int X, int Y, int Z) CuboidMax => (MaxX + 1, MaxY + 1, MaxZ + 1);

    /// <summary>True when the point lies inside the box (all bounds inclusive).</summary>
    public bool Contains(int x, int y, int z) =>
        x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;

    /// <summary>The box grown by <paramref name="m"/> blocks on every side.</summary>
    public BlockBox Expand(int m) => new(MinX - m, MinY - m, MinZ - m, MaxX + m, MaxY + m, MaxZ + m);

    /// <summary>True when the 16×16 chunk column at <paramref name="chunkX"/>/<paramref name="chunkZ"/>
    /// overlaps the box horizontally.</summary>
    public bool IntersectsChunk(int chunkX, int chunkZ)
    {
        int x0 = chunkX * 16, z0 = chunkZ * 16;
        return x0 + 15 >= MinX && x0 <= MaxX && z0 + 15 >= MinZ && z0 <= MaxZ;
    }
}
