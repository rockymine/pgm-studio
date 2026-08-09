namespace PgmStudio.Domain;

/// <summary>
/// An axis-aligned block volume in world coordinates, min and max both <b>inclusive</b>. One type covers
/// every role a block volume plays — the region an author boxed for a scan, the volume a stamper fills,
/// the casing a detector proposes — because they are the same six numbers under the same convention and
/// splitting them by role only duplicated the helpers.
/// <para>
/// It is the single source of truth for both halves of an objective: the blocks that get placed, and the
/// <c>&lt;region&gt;</c> emitted around them. The two must agree — PGM builds the goal from the blocks
/// matching <c>materials</c> <i>inside</i> the region, so a region that misses the structure yields a
/// zero-health goal with nothing but a warning. Compute it once and share it; never let the stamper and
/// the region generator derive it independently.
/// </para>
/// </summary>
public readonly record struct BlockBox(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;
    public int Depth => MaxZ - MinZ + 1;

    /// <summary>The exclusive max a PGM cuboid wants: a cuboid spans blocks <c>[min, max)</c>, so its
    /// <c>max</c> attribute is one past the last block on each axis.</summary>
    public (int X, int Y, int Z) CuboidMax => (MaxX + 1, MaxY + 1, MaxZ + 1);

    public bool Contains(int x, int y, int z) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;

    /// <summary>The same box grown by <paramref name="margin"/> blocks on every face.</summary>
    public BlockBox Expand(int margin) =>
        new(MinX - margin, MinY - margin, MinZ - margin, MaxX + margin, MaxY + margin, MaxZ + margin);

    /// <summary>Whether a 16×16 chunk column overlaps this box horizontally — the cheap pre-filter that
    /// keeps a scan from decoding chunks it cannot draw a candidate from.</summary>
    public bool IntersectsChunk(int chunkX, int chunkZ)
    {
        int x0 = chunkX * 16, z0 = chunkZ * 16;
        return x0 + 15 >= MinX && x0 <= MaxX && z0 + 15 >= MinZ && z0 <= MaxZ;
    }
}
