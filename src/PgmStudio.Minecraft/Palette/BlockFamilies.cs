namespace PgmStudio.Minecraft.Palette;

/// <summary>
/// Which ids belong to a block family — stairs, slabs, panes, logs, leaves, soil — stated once, here.
///
/// <para><b>A family is a value, and the value is what was shared.</b> Two vocabularies ask about blocks and
/// they ask different things: <see cref="BlockRoles"/> asks what a block is <em>doing</em> in a world, so a
/// scan can tell the ground from what stands on it; a house style's gate asks what geometry a block
/// <em>carries</em>, so a field naming a stair gets a stair. Neither question is the other's and merging them
/// would have made one of them lie. What they genuinely had in common was the id lists underneath, and those
/// were written out twice: <c>BlockRoles.PartialMaterials</c> was exactly <see cref="Stairs"/> ∪
/// <see cref="SingleSlabs"/> ∪ <see cref="Panes"/> — the same eighteen ids, one list naming them and one
/// spelling them as literals — <c>BlockRoles.Trees</c> was <see cref="Logs"/> ∪ <see cref="Leaves"/>, and
/// <c>IsLog</c> was written in both classes, once as a set and once as <c>blockId is 17 or 162</c>. They
/// agreed, and they agreed by nobody having edited one of them.</para>
///
/// <para><b>What is not here.</b> A family is membership by id and nothing else, so a list that answers a
/// question rather than naming a family stays with the pass that asks it:
/// <see cref="Dressing.DressingPalette.IsStamp"/> is what the dressing pass may not overwrite — a closed list
/// of what the stampers make, which is a claim about passes and not about blocks — and
/// <see cref="BlockPalette"/> is colour, built from texture means. Both were counted as duplicates of this and
/// neither is one; only the two vocabularies above shared anything.</para>
///
/// <para>Membership is by id alone and never by metadata, which is what makes these usable from a gate that
/// has only an id to check. Where a variant matters — which wood a log is, whether a leaf decays — that is
/// <see cref="BlockVariants"/>'s question.</para>
/// </summary>
public static class BlockFamilies
{
    /// <summary>The full 1.8 stair set: one id per wood plus cobblestone, brick, stone brick, nether brick,
    /// sandstone, quartz and red sandstone. Every one carries its facing and its upside-down flag the same
    /// way, which is what a field asking for a stair is asking for.</summary>
    public static readonly IReadOnlySet<int> Stairs =
        new HashSet<int> { 53, 67, 108, 109, 114, 128, 134, 135, 136, 156, 163, 164, 180 };

    /// <summary>Half-height slabs: stone, wooden, red sandstone. These are the ones that fill half their cube
    /// and read the upper/lower-half bit a window band or a door head writes.</summary>
    public static readonly IReadOnlySet<int> SingleSlabs = new HashSet<int> { 44, 126, 182 };

    /// <summary><b>Double</b> slabs, which are a different id family and never a substitute for
    /// <see cref="SingleSlabs"/>: a double slab ignores the half bit and always renders as the full cube, so a
    /// field wanting a slab gets a solid lintel instead of an arch, or a band with no gap in it. Named rather
    /// than merely absent, because a gate refusing one wants to say which mistake was made.</summary>
    public static readonly IReadOnlySet<int> DoubleSlabs = new HashSet<int> { 43, 125, 181 };

    /// <summary>Glass panes, plain and stained — thin in both horizontal axes.</summary>
    public static readonly IReadOnlySet<int> Panes =
        new HashSet<int> { Blocks.GlassPane, Blocks.StainedGlassPane };

    /// <summary>Tree wood, in the two id pairs the numeric format splits the six woods across.</summary>
    public static readonly IReadOnlySet<int> Logs = new HashSet<int> { Blocks.Log, Blocks.Log2 };

    /// <summary>Tree leaves, in the same two id pairs.</summary>
    public static readonly IReadOnlySet<int> Leaves = new HashSet<int> { Blocks.Leaves, Blocks.Leaves2 };

    /// <summary>Earth, turf and sediment — what a building stands <em>on</em>: grass, dirt, sand, gravel,
    /// farmland and mycelium. Stone, brick, sandstone and every dressed material are deliberately absent: the
    /// rule this serves bars a building's footing from also being its roof, and does not bar stone as a
    /// category.
    ///
    /// <para><b>It is soil and not "ground", and the name is the fix.</b> It used to be <c>IsGround</c>
    /// beside <see cref="BlockRoles.IsNaturalGround"/>, which is the opposite kind of answer — everything left
    /// after built, liquid, log and grown are taken away, derived rather than listed. A caller reaching for
    /// "is this ground" had to know which of the two it wanted and nothing at the call site said so.</para>
    /// </summary>
    public static readonly IReadOnlySet<int> Soil = new HashSet<int> { 2, 3, 12, 13, 60, 110 };

    /// <summary>Whether the id is a stair, whichever material it is cut from.</summary>
    public static bool IsStair(int blockId) => Stairs.Contains(blockId);

    /// <summary>Whether the id is a half-height slab. A double slab answers false, deliberately —
    /// see <see cref="DoubleSlabs"/>.</summary>
    public static bool IsSlab(int blockId) => SingleSlabs.Contains(blockId);

    /// <summary>Whether the id is a double slab: a full cube wearing a slab's name.</summary>
    public static bool IsDoubleSlab(int blockId) => DoubleSlabs.Contains(blockId);

    /// <summary>The wood of a tree, in both id pairs. Nameable apart from the leaves because it is the half of
    /// a tree that is also furniture: an author stands a log at each corner of a house as a post, so a pass
    /// reading a building's shape has to see one where a pass reading the ground steps past it, and a roof may
    /// never be laid in one.</summary>
    public static bool IsLog(int blockId) => Logs.Contains(blockId);

    /// <summary>The leaves of a tree, in both id pairs.</summary>
    public static bool IsLeaf(int blockId) => Leaves.Contains(blockId);

    /// <summary>Whether the id is earth, turf or sediment — see <see cref="Soil"/> for why it is not called
    /// ground.</summary>
    public static bool IsSoil(int blockId) => Soil.Contains(blockId);
}
