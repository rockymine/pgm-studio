using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>One plant the flora overlay may place: the block, and whether standing in it changes anything for
/// a player. <see cref="Tall"/> plants occupy two blocks and are placed as a pair.</summary>
/// <param name="Class">Cosmetic plants scatter freely; anything that hides a player is gameplay and gets
/// mirrored across the orbit.</param>
public readonly record struct Plant(int Id, int Data, bool Tall, PropClass Class);

/// <summary>
/// The blocks the dressing stage places, named once. It is the sibling of <see cref="TerrainPalette"/> for the
/// terrain painter: a curated vocabulary rather than a restriction, since a spec names ids directly.
///
/// <para>Two things here are load-bearing rather than cosmetic. <b>Leaves carry the no-decay bit</b>, because a
/// leaf block placed without it is checked by the game and falls apart the moment a player joins — a built map
/// is not a grown one and has no logs registered to keep them. And a <b>tall plant is two blocks</b> whose
/// upper half must be flagged as such, or it drops as an item on the first block update.</para>
/// </summary>
public static class DressingPalette
{
    // ── plants ──────────────────────────────────────────────────────────────────
    public const int TallGrassBlock = 31;      // data 1 = grass, 2 = fern
    public const int YellowFlower = 37;        // dandelion
    public const int RedFlower = 38;           // data selects poppy / orchid / allium / tulips / daisy
    public const int DoublePlant = 175;        // two blocks; upper half is data 8
    public const int LilyPad = 111;

    /// <summary>The upper half of a two-block plant: the data value that marks a block as the top of a double
    /// plant, which is what stops it dropping on the next block update.</summary>
    public const int DoublePlantUpper = 8;

    public static readonly Plant Grass = new(TallGrassBlock, 1, Tall: false, PropClass.Cosmetic);
    public static readonly Plant Fern = new(TallGrassBlock, 2, Tall: false, PropClass.Cosmetic);
    public static readonly Plant Dandelion = new(YellowFlower, 0, Tall: false, PropClass.Cosmetic);
    public static readonly Plant Poppy = new(RedFlower, 0, Tall: false, PropClass.Cosmetic);
    public static readonly Plant BlueOrchid = new(RedFlower, 1, Tall: false, PropClass.Cosmetic);
    public static readonly Plant OxeyeDaisy = new(RedFlower, 8, Tall: false, PropClass.Cosmetic);

    /// <summary>Two-block grass. It hides a crouching player, so unlike every other plant it is gameplay and is
    /// mirrored across the orbit.</summary>
    public static readonly Plant TallGrass = new(DoublePlant, 2, Tall: true, PropClass.Gameplay);
    /// <summary>Two-block fern — gameplay for the same reason as tall grass.</summary>
    public static readonly Plant LargeFern = new(DoublePlant, 3, Tall: true, PropClass.Gameplay);

    /// <summary>The flowers a flower field draws from, in the order a share noise picks between them.</summary>
    public static readonly Plant[] Flowers = [Poppy, Dandelion, BlueOrchid, OxeyeDaisy];

    // ── ground a plant will grow on ─────────────────────────────────────────────
    /// <summary>How readily a painted surface accepts flora, by the block on top of it: grass and dirt take it
    /// fully, sand sparsely, and everything else — the quartz of a plaza, the wool of a monument, a path's
    /// gravel — takes none. The overlay is masked by the paint beneath it, which is the whole reason the
    /// dressing pass runs after the painter rather than before.</summary>
    public static double SoilShare(int blockId, int blockData) => blockId switch
    {
        Blocks.Grass => 1.0,
        Blocks.Dirt => blockData == 2 ? 0.8 : 1.0,   // podzol takes a little less than dirt or coarse dirt
        Blocks.Sand => 0.35,
        Blocks.Gravel => 0.3,
        Mycelium => 0.9,
        _ => 0,
    };

    private const int Mycelium = 110;

    /// <summary>Whether a block on top of a column was <em>stamped</em> there — a room floor, an approach wall,
    /// a monument, an objective — rather than left by the painter. The painter only ever writes terrain
    /// materials, so anything else standing on a surface belongs to something the map is played through, and
    /// neither a path's paving nor a prop's footing may take it.
    ///
    /// <para>Stated once, here, because two passes ask it: the dressing pass, to decide what it may repaint or
    /// stand on, and the scope resolver, to decide which columns are off limits entirely. Two lists would drift
    /// and the drift would show as a road eating a monument.</para></summary>
    public static bool IsStamp(int blockId) => blockId
        is Blocks.Bedrock or Blocks.Obsidian or Blocks.Wool or Blocks.GoldBlock or Blocks.IronBlock
        or Blocks.EmeraldBlock or Blocks.Chest or Blocks.StainedGlass or Blocks.StainedGlassPane or Blocks.Air;

    // ── trees ───────────────────────────────────────────────────────────────────
    /// <summary>The bit that stops the game checking a leaf block for decay. A built map has no growing tree
    /// behind its leaves, so without this the whole crown disappears shortly after the map loads.</summary>
    public const int LeafNoDecay = 4;

    /// <summary>The log-orientation bits that give the <b>all-bark</b> variant: bark on all six faces, no cut-grain
    /// end caps. A built tree's wood is scenery, not a felled trunk, and its limbs run in every direction — so the
    /// wood reads as bark all over rather than showing the pale end grain of an upright log wherever a branch turns.
    /// OR'd onto a wood's two type bits; the colour lookup masks it off (<c>data &amp; 3</c>), so it never disturbs
    /// which wood a log paints as.</summary>
    public const int LogAllBark = 12;

    /// <summary>The six woods a tree of either form can be cut from.</summary>
    public static readonly IReadOnlyList<TreeWood> Woods =
    [
        new("oak", Blocks.Log, 0, Blocks.Leaves, 0),
        new("birch", Blocks.Log, 2, Blocks.Leaves, 2),
        new("spruce", Blocks.Log, 1, Blocks.Leaves, 1),
        new("jungle", Blocks.Log, 3, Blocks.Leaves, 3),
        new("acacia", Blocks.Log2, 0, Blocks.Leaves2, 0),
        new("dark oak", Blocks.Log2, 1, Blocks.Leaves2, 1),
    ];

    public static TreeWood WoodNamed(string name)
        => Woods.FirstOrDefault(wood => wood.Name == name) ?? Woods[0];

    /// <summary>The vanilla species: each its own wood, canopy profile and proportions. The profiles are what
    /// separate them — a notched cone is a spruce and a flat umbrella on a leaning trunk is an acacia, and
    /// neither is a knob setting of the other.</summary>
    public static readonly IReadOnlyList<TreeSpecies> Species =
    [
        // The heights are the vanilla ones, which are shorter than they feel: how much bare trunk a tree shows
        // is its height less its canopy's courses, so a species listed two blocks too tall grows a stalk.
        new("oak", Woods[0], CanopyProfile.Blob, Height: 8, CanopyRadius: 2.6),
        new("birch", Woods[1], CanopyProfile.Blob, Height: 9, CanopyRadius: 2.2),
        new("spruce", Woods[2], CanopyProfile.Cone, Height: 13, CanopyRadius: 3.0),
        new("jungle", Woods[3], CanopyProfile.Blob, Height: 13, CanopyRadius: 3.2),
        new("acacia", Woods[4], CanopyProfile.Umbrella, Height: 8, CanopyRadius: 4.0, Lean: 3),
        new("dark oak", Woods[5], CanopyProfile.Blob, Height: 9, CanopyRadius: 3.4, WideTrunk: true),
    ];

    public static TreeSpecies SpeciesNamed(string name)
        => Species.FirstOrDefault(species => species.Name == name) ?? Species[0];
}
