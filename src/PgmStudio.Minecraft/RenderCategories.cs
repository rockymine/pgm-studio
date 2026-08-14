namespace PgmStudio.Minecraft;

/// <summary>The coarse kind a stage image sorts a column's visible block into when the render's job is
/// legibility rather than material identification — not <em>which</em> block, but <em>what kind of thing</em>
/// it is. <see cref="Void"/> is a column with no block recorded at all (unloaded, or a hole nothing covers);
/// every loaded column resolves to exactly one of the other four, via <see cref="BlockRoles"/>, the one
/// classification every render in this suite already shares.</summary>
public enum RenderCategory { Void, Water, Foliage, Structure, Ground }

/// <summary>
/// One false-colour palette for <see cref="RenderCategory"/>, shared by every top-down world read-back that
/// draws a legible diagram rather than a photograph (<c>docs/tools/capabilities.md</c>'s renderer section).
///
/// <para><b>Realism is the wrong goal for an image meant to be reasoned from.</b> A world's own materials do
/// not separate at a glance — stone, stone brick, cobblestone, andesite and gravel are all some shade of grey
/// because that is what those blocks look like in the game, so a render that paints them their own colour
/// paints a grey field and calls it done. A stage image exists to answer "where is the foliage, the built
/// structure, the open water, the bare ground" in one look, and the five hues below are chosen for maximum
/// pairwise separation on the colour wheel rather than for any resemblance to the material — verified in
/// <c>RenderCategoriesTests</c> so a future palette edit cannot quietly collapse two categories back into one
/// shade. <see cref="BlockPalette"/> keeps every block's real colour for the renders that are checking a
/// material choice rather than reading the map's shape (the terrain-paint picker, a theme preview, the
/// monument slice dump) — those two jobs want different colours and this table only ever answers the
/// legibility one.</para>
/// </summary>
public static class RenderCategories
{
    /// <summary>No block recorded at all — the same near-black every renderer in this suite already uses for
    /// "no data", so a hole in the world reads the same way in every stage image.</summary>
    public const int VoidRgb = 0x0E0E12;

    /// <summary>Open water — a saturated cyan distinct from every other category here and from the terrain
    /// tones <see cref="TerrainPalette"/> offers, so a river or a lane reads as water at a glance.</summary>
    public const int WaterRgb = 0x00B4D8;

    /// <summary>A tree or anything grown — a vibrant violet no terrain in the game ever wears, chosen so a
    /// canopy over its own colour of ground still separates from it (the failure a realistic green-on-green
    /// render cannot avoid).</summary>
    public const int FoliageRgb = 0xB026FF;

    /// <summary>A built surface — a strong orange, as far around the wheel from the foliage violet and the
    /// water cyan as the five-colour set allows.</summary>
    public const int StructureRgb = 0xFF7A1F;

    /// <summary>Natural ground — deliberately the most desaturated of the five, since it is the backdrop the
    /// other four are meant to stand out against rather than a reading of its own.</summary>
    public const int GroundRgb = 0x8A8578;

    /// <summary>A punchier green used only when a render isolates <see cref="RenderCategory.Ground"/> as the
    /// one category being asked about. <see cref="GroundRgb"/> is deliberately muted for the combined view,
    /// where it is the backdrop the other categories read against — an isolated ground layer has nothing to
    /// sit behind, so it gets a colour worth looking at on its own instead of inheriting the backdrop shade.</summary>
    public const int GroundHighlightRgb = 0x39D353;

    /// <summary>The colour a category reads as when a render isolates it as the one thing being asked about,
    /// rather than one of several categories sharing the frame. Every category but <see cref="RenderCategory.Ground"/>
    /// already reads as a strong accent in the combined view and keeps that same colour; only ground swaps its
    /// backdrop shade for <see cref="GroundHighlightRgb"/>.</summary>
    public static int HighlightOf(RenderCategory category) =>
        category == RenderCategory.Ground ? GroundHighlightRgb : ColourOf(category);

    public static int ColourOf(RenderCategory category) => category switch
    {
        RenderCategory.Void => VoidRgb,
        RenderCategory.Water => WaterRgb,
        RenderCategory.Foliage => FoliageRgb,
        RenderCategory.Structure => StructureRgb,
        RenderCategory.Ground => GroundRgb,
        _ => VoidRgb,
    };

    public static string NameOf(RenderCategory category) => category switch
    {
        RenderCategory.Void => "void",
        RenderCategory.Water => "water",
        RenderCategory.Foliage => "foliage",
        RenderCategory.Structure => "structure",
        RenderCategory.Ground => "ground",
        _ => "?",
    };

    /// <summary>Every non-void category, in the order a legend lists them.</summary>
    public static readonly RenderCategory[] Visible =
        [RenderCategory.Ground, RenderCategory.Structure, RenderCategory.Foliage, RenderCategory.Water];

    /// <summary>What a column's topmost recorded block reads as, from material alone. Liquid outranks foliage
    /// and a built surface outranks bare ground, the same priority every scan in this suite already gives a
    /// column's single answer; a block that is none of those — bare dirt, stone, sand, a thin decoration like
    /// a torch or a sign — is the ground itself.
    ///
    /// <para>This is the <b>estimate</b>: the only reading available for a world the studio scanned rather than
    /// built, and the reason it is wrong for a built world is B133's own finding — stone brick is stone brick
    /// whether it is a cottage wall or a plaza the painter finished. <see cref="Of(int, ProvenanceLayer?)"/> is
    /// the answer a built world can give instead.</para></summary>
    public static RenderCategory Of(int blockId) =>
        blockId == 0 ? RenderCategory.Void
        : BlockRoles.IsLiquid(blockId) ? RenderCategory.Water
        : BlockRoles.IsTree(blockId) || BlockRoles.IsFlora(blockId) ? RenderCategory.Foliage
        : BlockRoles.IsBuilt(blockId) ? RenderCategory.Structure
        : RenderCategory.Ground;

    /// <summary>What a column reads as when a build recorded which pass claimed it. Void and liquid stay
    /// material questions, since neither is ever what a pass claimed a column *for*. Everything else defers to
    /// the record, because two of the three material tests below can lie about a built thing.
    /// <para>A <see cref="ProvenanceLayer.Structure"/> claim wins over the foliage test as well as the built
    /// test, and the reason is <see cref="BlockRoles.IsLog"/>'s own: a log is the half of a tree that is also
    /// furniture, standing at the corner of a house as a post, so a building whose posts are spruce reads as a
    /// grove to a material test. A column a stamper or a dressing-placed building claimed is that building
    /// whatever block sits on top of it.</para>
    /// <para>A <see cref="ProvenanceLayer.Ground"/> claim still answers the foliage test first, because a tree
    /// carries no claim of its own — it stands on ground the rasterizer claimed, so the block is the only
    /// thing that can say a canopy is there. Ground then reads as ground even over a block
    /// <see cref="BlockRoles.IsBuilt"/> would call built, which is the case a plaza painted in a built-looking
    /// material needs. <paramref name="provenance"/> null falls back to the material estimate entirely: a
    /// scanned world, or a column the build never claimed.</para></summary>
    public static RenderCategory Of(int blockId, ProvenanceLayer? provenance) =>
        blockId == 0 ? RenderCategory.Void
        : BlockRoles.IsLiquid(blockId) ? RenderCategory.Water
        : provenance switch
        {
            ProvenanceLayer.Structure => RenderCategory.Structure,
            ProvenanceLayer.Ground => Grown(blockId) ? RenderCategory.Foliage : RenderCategory.Ground,
            _ => Grown(blockId) ? RenderCategory.Foliage
                : BlockRoles.IsBuilt(blockId) ? RenderCategory.Structure
                : RenderCategory.Ground,
        };

    private static bool Grown(int blockId) => BlockRoles.IsTree(blockId) || BlockRoles.IsFlora(blockId);
}
