namespace PgmStudio.Minecraft;

/// <summary>
/// Which bits of a 1.8 block's four-bit metadata pick its <em>appearance</em>, and which only carry state.
/// <para>
/// A legacy block packs two unrelated things into one nibble: the sub-type (which wood, which stone, which
/// dye) and the placement state (axis, facing, half, decay, growth, level, power). A colour table keyed on
/// the raw nibble therefore misses every rotated log and every decaying leaf — an east-west oak log is
/// <c>17:8</c>, not <c>17:0</c>, and a leaf block that has been marked persistent is <c>18:4</c>. Normalizing
/// first is what makes an exact <c>(id, meta)</c> match viable at all.
/// </para>
/// <para>
/// Most blocks have no visual sub-type, so the default is a zero mask: every metadata value collapses onto
/// the block's single entry. Only the ids listed here carry a mask, and three need arithmetic a mask cannot
/// express — the anvil's damage lives in the high bits, the quartz pillar's three axis values are one
/// texture, and a double plant's upper half does not record which plant it belongs to.
/// </para>
/// </summary>
public static class BlockVariants
{
    /// <summary>Legacy block ids are a byte, so every table indexed by id is this long.</summary>
    public const int BlockIdCount = 256;

    private const int Anvil = 145;
    private const int QuartzBlock = 155;
    private const int DoublePlant = 175;

    /// <summary>
    /// The metadata value a double plant's upper half normalizes to. The upper half stores only its facing,
    /// not which of the six plants it belongs to, so it cannot resolve to a variant and deliberately lands
    /// on a value no variant claims — the block's base colour.
    /// </summary>
    private const int DoublePlantUpperHalf = 8;

    private static readonly byte[] VariantMask = BuildMasks();

    /// <summary>
    /// The metadata value to look a block's colour up under: the sub-type bits, with orientation, half,
    /// decay, growth and power stripped. Negative data (the "any variant" caller convention) and every
    /// block without sub-types normalize to <c>0</c>, the base variant.
    /// </summary>
    public static int Normalize(int blockId, int blockData)
    {
        if (blockData <= 0 || (uint)blockId >= BlockIdCount) return 0;
        var meta = blockData & 0xF;
        return blockId switch
        {
            // Damage in bits 2–3, facing in bits 0–1.
            Anvil => (meta >> 2) & 0x3,
            // 0 plain, 1 chiseled, 2–4 the pillar's three axes — one texture, so they share an entry.
            QuartzBlock => meta >= 2 ? 2 : meta,
            // Bit 3 marks the upper half, whose remaining bits are facing rather than the plant.
            DoublePlant => (meta & 0x8) != 0 ? DoublePlantUpperHalf : meta & 0x7,
            _ => meta & VariantMask[blockId],
        };
    }

    /// <summary>Whether a block's appearance depends on its metadata at all.</summary>
    public static bool HasVariants(int blockId) =>
        (uint)blockId < BlockIdCount &&
        (VariantMask[blockId] != 0 || blockId is Anvil or QuartzBlock or DoublePlant);

    private static byte[] BuildMasks()
    {
        var mask = new byte[BlockIdCount];
        void Sub(byte bits, params int[] ids)
        {
            foreach (var id in ids) mask[id] = bits;
        }

        // Two sub-type bits, upper two bits state: logs store the axis there (and 12–15 = bark on every
        // face), leaves the decay-check and persistence flags.
        Sub(0x3, 17, 18, 161, 162);

        // Two sub-type bits, no state: the stone-brick, sandstone, prismarine, dirt and tall-grass families.
        Sub(0x3, 3, 24, 31, 98, 168, 179);

        // Three sub-type bits. Slabs put the "upper half" flag in bit 3; a double stone slab reuses bits 0–2
        // for its seamless forms, so the same mask lands each on its ordinary twin. Saplings put the growth
        // stage in bit 3, the stone family simply runs to seven values.
        Sub(0x7, 1, 5, 6, 43, 44, 97, 125, 126, 181, 182);

        // One sub-type bit: sand/red sand, dry/wet sponge, cobblestone/mossy wall.
        Sub(0x1, 12, 19, 139);

        // The whole nibble is the sub-type: the five dye families, the nine small flowers, and the mushroom
        // blocks (whose metadata selects which faces show cap, pore or stem).
        Sub(0xF, 35, 38, 95, 99, 100, 159, 160, 171);

        return mask;
    }
}
