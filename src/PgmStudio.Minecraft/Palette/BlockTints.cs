namespace PgmStudio.Minecraft.Palette;

/// <summary>
/// Which blocks the biome colours, and which of the three colours each one takes.
///
/// <para>Membership is by <c>(id, data)</c> rather than by id alone, because two of the six woods are the
/// exception: spruce and birch leaves carry the constant tint the game applies to them everywhere, so they
/// are the same green in a desert as in a jungle while oak, jungle, acacia and dark oak leaves follow the
/// ground. Everything else in the set is tinted whatever its variant.</para>
///
/// <para>It is a fact about a <em>block</em> — what a client multiplies its texture by — which is why it sits
/// beside <see cref="BlockPalette"/> and not in <see cref="BiomeTint"/>, whose table is a fact about a
/// biome.</para>
/// </summary>
public static class BlockTints
{
    // The leaf ids carry four biome-tinted woods between them: oak and jungle on 18, acacia and dark oak on
    // 161. Spruce (18:1) and birch (18:2) are absent because the game tints them constantly.
    private static readonly HashSet<(int Id, int Data)> Leaves =
        [(Blocks.Leaves, 0), (Blocks.Leaves, 3), (Blocks.Leaves2, 0), (Blocks.Leaves2, 1)];

    // Whole ids, every variant of them.
    private static readonly Dictionary<int, TintChannel> ByBlock = new()
    {
        [2] = TintChannel.Grass,          // grass block
        [31] = TintChannel.Grass,         // shrub, grass, fern
        [175] = TintChannel.Grass,        // the two-block plants, tall grass and large fern among them
        [83] = TintChannel.Grass,         // sugar cane
        [106] = TintChannel.Foliage,      // vines
        [111] = TintChannel.Foliage,      // lily pad
        [8] = TintChannel.Water,
        [9] = TintChannel.Water,
    };

    /// <summary>The colour this block reads, or <see cref="TintChannel.None"/> for one the biome does not
    /// touch — which is nearly every block there is.</summary>
    public static TintChannel Of(int blockId, int blockData)
    {
        if (blockId is Blocks.Leaves or Blocks.Leaves2)
            return Leaves.Contains((blockId, BlockVariants.Normalize(blockId, blockData) & 0x3))
                ? TintChannel.Foliage
                : TintChannel.None;
        return ByBlock.TryGetValue(blockId, out var channel) ? channel : TintChannel.None;
    }

    /// <summary>Whether the biome moves this block's colour at all — what a caller checks before paying for a
    /// per-column lookup.</summary>
    public static bool IsTinted(int blockId, int blockData) => Of(blockId, blockData) != TintChannel.None;
}
