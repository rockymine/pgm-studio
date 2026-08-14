namespace PgmStudio.Minecraft;

/// <summary>
/// Which ids the geometry-carrying fields of a house style are allowed to name. <see cref="HouseWindows"/> and
/// <see cref="HouseStyle"/> already say, in their own docstrings, which <b>kind</b> of block a field wants — a
/// stair for the two corners of an arch, a slab for the beam or the band between them — because that geometry
/// is carried in the block's own facing and half-height data rather than in a material a style could resolve.
/// Nothing turned that word into a set an id could be tested against, so a style could name any block at all and
/// nothing noticed; this is that set (<see cref="HouseStyleRules.BlockKind"/>).
///
/// <para>A <b>double</b> slab (43, 125, 181) is deliberately not counted as a slab here. It ignores the
/// upper/lower-half bit a window or a door head writes into its data and always renders as the full cube, which
/// is the same fault a whole-block id in the field would cause — a solid lintel instead of an arch, or a band
/// with no gap in it — so it is refused exactly as one.</para>
/// </summary>
public static class BlockKinds
{
    // The full 1.8 stair set: one id per wood plus cobblestone, brick, stone brick, nether brick, sandstone,
    // quartz and red sandstone. Every one of them carries its facing and its upside-down flag the same way,
    // which is the only property this class cares about.
    private static readonly HashSet<int> Stairs =
        [53, 67, 108, 109, 114, 128, 134, 135, 136, 156, 163, 164, 180];

    // Single (half-height) slabs only. Their double counterparts are a different id family entirely and are
    // named below instead — a double slab is a full cube regardless of data, so it is never a substitute.
    private static readonly HashSet<int> SingleSlabs = [44, 126, 182];
    private static readonly HashSet<int> DoubleSlabs = [43, 125, 181];

    private static readonly HashSet<int> Panes = [Blocks.GlassPane, Blocks.StainedGlassPane];

    private static readonly HashSet<int> Logs = [Blocks.Log, Blocks.Log2];

    // The ground families a roof or a verge is never laid in: earth, turf and sediment, whatever a building
    // actually stands on. Stone, brick, sandstone and every dressed material stay unlisted — the rule bars
    // what a building is founded on from also being what it is roofed with, not stone as a category.
    private static readonly HashSet<int> Ground = [2, 3, 12, 13, 60, 110];

    public static bool IsStair(int blockId) => Stairs.Contains(blockId);

    public static bool IsSlab(int blockId) => SingleSlabs.Contains(blockId);

    public static bool IsDoubleSlab(int blockId) => DoubleSlabs.Contains(blockId);

    public static bool IsPane(int blockId) => Panes.Contains(blockId);

    public static bool IsLog(int blockId) => Logs.Contains(blockId);

    public static bool IsGround(int blockId) => Ground.Contains(blockId);
}
