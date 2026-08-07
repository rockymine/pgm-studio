namespace PgmStudio.Minecraft;

/// <summary>
/// Block id + metadata → one colour, one <c>"#rrggbb"</c> string and one display name: the table the
/// top-down surface render, the layer overlays, the terrain-paint picker and the monument slice dump all
/// read, so a swatch anywhere in the studio is the colour the export will place.
///
/// <para><b>Lookup is a three-step hierarchy.</b> The raw metadata is first normalized by
/// <see cref="BlockVariants.Normalize"/>, which strips the bits carrying orientation, half, decay, growth
/// or power and keeps only the sub-type — without it an east-west oak log (<c>17:8</c>) or a persistent
/// leaf block (<c>18:4</c>) would miss its own entry. The masked <c>(id, meta)</c> pair is then matched
/// exactly; failing that the block's base colour answers; failing that a deterministic hash gives an
/// unknown block a stable colour of its own rather than a shared placeholder. Negative data is the caller's
/// "any variant" convention and skips the sub-type step entirely, so a wool of unknown colour answers with
/// the family's base rather than with white in particular.</para>
///
/// <para><b>Storage is two flat arrays per attribute</b>, indexed by the packed key
/// <c>(blockId &lt;&lt; 4) | meta</c> for sub-types and by the raw id for base entries. A legacy id is a
/// byte and metadata a nibble, so the sub-type table is a fixed 4096 slots — small enough to stay resident
/// and dense enough that a surface render's per-cell lookup is two array reads with no hashing, no boxed
/// tuple key and no allocation. Hex strings are formatted once at build time and handed back interned, so
/// rendering a whole world's surface allocates nothing per cell.</para>
///
/// <para>Where the colours come from — texture means, the temperate tint applied to biome-coloured blocks,
/// and the accent weighting that keeps ores apart — is documented on <see cref="BlockPaletteData"/> and in
/// <c>docs/contracts/block-palette.md</c>.</para>
/// </summary>
public static class BlockPalette
{
    public readonly record struct Rgb(int R, int G, int B);

    private const int MetaBits = 4;
    private const int MetaCount = 1 << MetaBits;
    private const int VariantSlots = BlockVariants.BlockIdCount * MetaCount;
    private const int Absent = -1;

    // Sub-type tables, indexed by (blockId << 4) | normalizedMeta; base tables, indexed by blockId.
    private static readonly int[] VariantRgb = new int[VariantSlots];
    private static readonly int[] BaseRgb = new int[BlockVariants.BlockIdCount];
    private static readonly string?[] VariantHex = new string?[VariantSlots];
    private static readonly string?[] BaseHex = new string?[BlockVariants.BlockIdCount];
    private static readonly string?[] VariantName = new string?[VariantSlots];
    private static readonly string?[] BaseName = new string?[BlockVariants.BlockIdCount];

    static BlockPalette()
    {
        Array.Fill(VariantRgb, Absent);
        Array.Fill(BaseRgb, Absent);

        foreach (var entry in BlockPaletteData.Entries)
        {
            if (entry.Meta == BlockPaletteData.Any)
            {
                BaseRgb[entry.Id] = (int)entry.Rgb;
                BaseHex[entry.Id] = ToHex(entry.Rgb);
                BaseName[entry.Id] = entry.Name;
            }
            else
            {
                var slot = (entry.Id << MetaBits) | entry.Meta;
                VariantRgb[slot] = (int)entry.Rgb;
                VariantHex[slot] = ToHex(entry.Rgb);
                VariantName[slot] = entry.Name;
            }
        }

        // A family declared only as sub-types (the stone kinds, the woods, the slabs) still needs an answer
        // for a metadata value no sub-type claims — an out-of-range nibble, or a double plant's upper half.
        // Its first sub-type is that answer, so every listed block resolves without reaching the hash.
        for (var id = 0; id < BlockVariants.BlockIdCount; id++)
        {
            if (BaseRgb[id] != Absent || VariantRgb[id << MetaBits] == Absent) continue;
            BaseRgb[id] = VariantRgb[id << MetaBits];
            BaseHex[id] = VariantHex[id << MetaBits];
            BaseName[id] = VariantName[id << MetaBits];
        }
    }

    /// <summary>Every <c>(id, data)</c> pair a block picker offers — the sub-types worth naming in a menu,
    /// ordered by id then data. The colour and name each one shows come from the same table every render
    /// reads, so a picker cannot offer a swatch the export would not place.</summary>
    public static IEnumerable<(int Id, int Data)> VariantBlocks
        => BlockPaletteData.PickerVariants.OrderBy(block => block.Id).ThenBy(block => block.Data);

    /// <summary>The colour for a block id + data value, with a deterministic fallback for unknown blocks.</summary>
    public static Rgb Color(int blockId, int blockData)
    {
        var packed = PackedRgb(blockId, blockData);
        return new Rgb((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF);
    }

    /// <summary>
    /// The colour as a packed <c>0xRRGGBB</c> integer — what a renderer that writes pixels wants, since it
    /// skips both the <see cref="Rgb"/> struct and the hex string.
    /// </summary>
    public static int PackedRgb(int blockId, int blockData)
    {
        if ((uint)blockId >= BlockVariants.BlockIdCount) return FallbackRgb(blockId, blockData);
        if (blockData >= 0)
        {
            var variant = VariantRgb[(blockId << MetaBits) | BlockVariants.Normalize(blockId, blockData)];
            if (variant != Absent) return variant;
        }
        var known = BaseRgb[blockId];
        return known != Absent ? known : FallbackRgb(blockId, blockData);
    }

    /// <summary>How much of its colour the block at the very back of a side view keeps. Enough darkening to
    /// read as distance, not so much that oak leaves at the back stop being green.</summary>
    private const double BackShade = 0.42;

    /// <summary>Hex for a block seen at a distance: the block's own colour, darkened towards the back of the
    /// view. <paramref name="depth"/> runs 0 (front plane) to 1 (back), so a flat projection still reads as a
    /// volume without any of it being repainted grey — the shading carries the depth and the colour stays the
    /// export's own. Not interned: a shade is one point on a continuum, not a table entry.</summary>
    public static string Hex(int blockId, int blockData, double depth)
    {
        var color = Color(blockId, blockData);
        var keep = 1 - (1 - BackShade) * Math.Clamp(depth, 0, 1);
        return $"#{(int)(color.R * keep):x2}{(int)(color.G * keep):x2}{(int)(color.B * keep):x2}";
    }

    /// <summary>Hex <c>"#rrggbb"</c> for a block id + data value. Known blocks return a shared instance.</summary>
    public static string Hex(int blockId, int blockData)
    {
        if ((uint)blockId >= BlockVariants.BlockIdCount) return ToHex((uint)FallbackRgb(blockId, blockData));
        if (blockData >= 0)
        {
            var variant = VariantHex[(blockId << MetaBits) | BlockVariants.Normalize(blockId, blockData)];
            if (variant is not null) return variant;
        }
        return BaseHex[blockId] ?? ToHex((uint)FallbackRgb(blockId, blockData));
    }

    /// <summary>Human-readable name for a block id + data value, sub-type included where one applies.</summary>
    public static string Name(int blockId, int blockData)
    {
        if ((uint)blockId >= BlockVariants.BlockIdCount) return $"Block {blockId}";
        if (blockData >= 0)
        {
            var variant = VariantName[(blockId << MetaBits) | BlockVariants.Normalize(blockId, blockData)];
            if (variant is not null) return variant;
        }
        return BaseName[blockId] ?? $"Block {blockId}";
    }

    /// <summary>
    /// A stable colour for a block the table does not list, so two unknown blocks stay visually distinct
    /// instead of sharing one placeholder. Kept below 220 per channel so an unknown never reads as one of
    /// the bright markers a real block uses.
    /// </summary>
    private static int FallbackRgb(int blockId, int blockData)
    {
        var hash = (uint)(blockId * 73856093 ^ blockData * 19349663) & 0xFFFFFFu;
        var r = ((hash >> 16) & 0xFF) % 220;
        var g = ((hash >> 8) & 0xFF) % 220;
        var b = (hash & 0xFF) % 220;
        return (int)((r << 16) | (g << 8) | b);
    }

    private static string ToHex(uint rgb) => $"#{rgb & 0xFFFFFF:x6}";
}
