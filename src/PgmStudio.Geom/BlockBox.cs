using System.Text.Json.Serialization;

namespace PgmStudio.Geom;

/// <summary>
/// An axis-aligned block volume in world coordinates, min and max both <b>inclusive</b>. One type covers
/// every role a block volume plays — the region an author boxed for a scan, the volume a stamper fills,
/// the casing a detector proposes, the frame a view is drawn over — because they are the same six numbers
/// under the same convention and splitting them by role only duplicated the helpers.
/// <para>
/// It is the single source of truth for both halves of an objective: the blocks that get placed, and the
/// <c>&lt;region&gt;</c> emitted around them. The two must agree — PGM builds the goal from the blocks
/// matching <c>materials</c> <i>inside</i> the region, so a region that misses the structure yields a
/// zero-health goal with nothing but a warning. Compute it once and share it; never let the stamper and
/// the region generator derive it independently.
/// </para>
/// <para>
/// It sits in <c>Geom</c> rather than <c>Domain</c> because the Configure client is a consumer and, being
/// WASM, reaches only <c>Contracts</c> and <c>Geom</c> — so a copy of it grew there under another name for
/// as long as the shared one was somewhere the client could not see. The type is geometry; what an
/// objective's box <i>means</i> is the paragraph above, and that travels with it.
/// </para>
/// </summary>
/// <param name="MinX">Its west edge, inclusive.</param>
/// <param name="MinY">Its floor, inclusive.</param>
/// <param name="MinZ">Its north edge, inclusive.</param>
/// <param name="MaxX">Its east edge, inclusive.</param>
/// <param name="MaxY">Its ceiling, inclusive.</param>
/// <param name="MaxZ">Its south edge, inclusive.</param>
public readonly record struct BlockBox(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    /// <summary>How many blocks it spans east–west.</summary>
    public int Width => MaxX - MinX + 1;

    /// <summary>How many blocks it spans vertically.</summary>
    public int Height => MaxY - MinY + 1;

    /// <summary>How many blocks it spans north–south.</summary>
    public int Depth => MaxZ - MinZ + 1;

    /// <summary>The centre of the volume's footprint, on the block lattice's half-steps — a 5-wide casing
    /// starting at x=0 is centred on 2.5, not on 2. What an objective's marker is placed at.</summary>
    public double CentreX => (MinX + MaxX + 1) / 2.0;

    /// <inheritdoc cref="CentreX"/>
    public double CentreZ => (MinZ + MaxZ + 1) / 2.0;

    /// <summary>The exclusive max a PGM cuboid wants: a cuboid spans blocks <c>[min, max)</c>, so its
    /// <c>max</c> attribute is one past the last block on each axis.</summary>
    /// <remarks>Not serialized. A tuple crosses as <c>{item1, item2, item3}</c>, which names nothing a
    /// caller can read, and this is derived from the six numbers beside it — so a wire field for it would be
    /// a second, worse spelling of what is already there.</remarks>
    [JsonIgnore]
    public (int X, int Y, int Z) CuboidMax => (MaxX + 1, MaxY + 1, MaxZ + 1);

    public bool Contains(int x, int y, int z) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;

    /// <summary>Whether two volumes share any block. Inclusive on both bounds, like everything else here.</summary>
    public bool Intersects(BlockBox other) =>
        MinX <= other.MaxX && MaxX >= other.MinX
        && MinY <= other.MaxY && MaxY >= other.MinY
        && MinZ <= other.MaxZ && MaxZ >= other.MinZ;

    /// <summary>The wire form a caller states a volume in: six comma-separated integers,
    /// <c>x0,y0,z0,x1,y1,z1</c>. The corners are read in either order and normalised, because a box drawn
    /// from the far corner is the same volume and refusing it would be arithmetic pedantry.
    /// <para>It lives on the type rather than at the routes that read it, so that every route reads a stated
    /// volume the same way and answers the same thing for one it cannot read.</para></summary>
    public static bool TryParse(string? text, out BlockBox box)
    {
        box = default;
        if (text is null) return false;
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 6) return false;
        var corners = new int[6];
        for (var index = 0; index < 6; index++)
            if (!int.TryParse(parts[index], System.Globalization.NumberStyles.Integer,
                              System.Globalization.CultureInfo.InvariantCulture, out corners[index]))
                return false;
        box = new BlockBox(
            Math.Min(corners[0], corners[3]), Math.Min(corners[1], corners[4]), Math.Min(corners[2], corners[5]),
            Math.Max(corners[0], corners[3]), Math.Max(corners[1], corners[4]), Math.Max(corners[2], corners[5]));
        return true;
    }

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
