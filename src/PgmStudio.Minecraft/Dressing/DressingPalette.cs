using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>One plant the flora overlay may place: the block, and whether standing in it changes anything for a
/// player. <see cref="Tall"/> plants occupy two blocks, are placed as a pair, and hide a crouching player —
/// which is what keeps them off a goal's own ground.</summary>
public readonly record struct Plant(int Id, int Data, bool Tall);

/// <summary>
/// The blocks the dressing stage places, named once. It is the sibling of <see cref="Painting.TerrainPalette"/> for the
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
    public const int DeadBushBlock = 32;
    public const int CactusBlock = 81;
    public const int BrownMushroomBlock = 39;
    public const int RedMushroomBlock = 40;
    public const int WheatBlock = 59;
    public const int CarrotsBlock = 141;
    public const int PotatoesBlock = 142;

    /// <summary>A crop's last growth stage: data 0 is just sown and this is ready. Wheat, carrots and potatoes
    /// all count eight.</summary>
    public const int CropRipe = 7;

    /// <summary>The dirt variant that is podzol.</summary>
    public const int PodzolData = 2;

    /// <summary>The tallest cactus the overlay grows, in blocks: a cactus is one to this many courses of the
    /// block, stacked.</summary>
    public const int CactusTallest = 4;

    /// <summary>The upper half of a two-block plant: the data value that marks a block as the top of a double
    /// plant, which is what stops it dropping on the next block update.</summary>
    public const int DoublePlantUpper = 8;

    public static readonly Plant Grass = new(TallGrassBlock, 1, Tall: false);
    public static readonly Plant Fern = new(TallGrassBlock, 2, Tall: false);
    public static readonly Plant Dandelion = new(YellowFlower, 0, Tall: false);
    public static readonly Plant Poppy = new(RedFlower, 0, Tall: false);
    public static readonly Plant BlueOrchid = new(RedFlower, 1, Tall: false);
    public static readonly Plant OxeyeDaisy = new(RedFlower, 8, Tall: false);

    /// <summary>Two-block grass. It hides a crouching player, which is why a goal's own ground refuses
    /// it.</summary>
    public static readonly Plant TallGrass = new(DoublePlant, 2, Tall: true);
    /// <summary>Two-block fern — cover for the same reason as tall grass.</summary>
    public static readonly Plant LargeFern = new(DoublePlant, 3, Tall: true);

    /// <summary>The flowers a flower field draws from, in the order a share noise picks between them.</summary>
    public static readonly Plant[] Flowers = [Poppy, Dandelion, BlueOrchid, OxeyeDaisy];

    /// <summary>A dead bush — dry ground's cover, on sand, clay and dirt.</summary>
    public static readonly Plant DeadBush = new(DeadBushBlock, 0, Tall: false);
    /// <summary>A cactus: one to <see cref="CactusTallest"/> blocks stacked, on sand only, with nothing solid
    /// beside any of them.</summary>
    public static readonly Plant Cactus = new(CactusBlock, 0, Tall: false);

    public static readonly Plant BrownMushroom = new(BrownMushroomBlock, 0, Tall: false);
    public static readonly Plant RedMushroom = new(RedMushroomBlock, 0, Tall: false);

    /// <summary>The mushrooms a cover draws from, brown two in three.</summary>
    public static readonly Plant[] Mushrooms = [BrownMushroom, BrownMushroom, RedMushroom];

    /// <summary>The block a crop word sows, or null for a word that is not a crop.</summary>
    public static int? CropBlock(string crop) => crop switch
    {
        CropKinds.Wheat => WheatBlock,
        CropKinds.Carrots => CarrotsBlock,
        CropKinds.Potatoes => PotatoesBlock,
        _ => null,
    };

    // ── ground a plant will grow on ─────────────────────────────────────────────
    /// <summary>How readily a painted surface accepts flora, by the block on top of it: grass and dirt take it
    /// fully, sand and clay sparsely, and everything else — the quartz of a plaza, the wool of a monument, a
    /// path's gravel — takes none. The overlay is masked by the paint beneath it, which is the whole reason the
    /// dressing pass runs after the painter rather than before. What may grow there is <see cref="SoilOf"/>'s.</summary>
    public static double SoilShare(int blockId, int blockData) => SoilOf(blockId) switch
    {
        Soil.Fertile => blockId == Blocks.Dirt && blockData == PodzolData ? 0.8 : 1.0,   // podzol takes a little less
        Soil.Mycelium or Soil.Farmland => 1.0,
        Soil.Sand or Soil.Clay => 0.35,
        _ => 0,
    };

    /// <summary>What a surface block lets grow. A 1.8 grass tuft, fern or flower stays only on grass and dirt
    /// and drops at the first update anywhere else; a dead bush takes sand, hardened and stained clay and dirt;
    /// a cactus takes sand alone; mycelium takes a mushroom and nothing else; farmland takes a crop.</summary>
    public static Soil SoilOf(int blockId) => blockId switch
    {
        Blocks.Grass or Blocks.Dirt => Soil.Fertile,
        Blocks.Mycelium => Soil.Mycelium,
        Blocks.Farmland => Soil.Farmland,
        Blocks.Sand => Soil.Sand,
        Blocks.HardenedClay or Blocks.StainedClay => Soil.Clay,
        _ => Soil.None,
    };

    /// <summary>Whether a tree's foot may stand on this block: grass and the three dirts, and nothing else.
    /// A tree is a thing that grew where it stands, so the block under its trunk is the one that says so —
    /// gravel, clay, stone and a path's paving are ground a tree was never rooted in, whatever else grows on
    /// them.
    ///
    /// <para>Stricter than <see cref="SoilShare"/>, which admits sand and clay at a third because a dead bush
    /// or a cactus in them is ordinary and a trunk out of them is not. Two questions, two answers: what will
    /// <em>grow</em> on a surface, and what a tree may be <em>rooted</em> in. The three dirts share one id, so
    /// the data is not read.</para>
    /// </summary>
    public static bool RootsInto(int blockId) => blockId is Blocks.Grass or Blocks.Dirt;

    /// <summary>Whether a mushroom stays on this block by day: podzol and mycelium. 1.8 drops a mushroom
    /// wherever the light reaches 13 unless the block under it is one of the two, and an open meadow is lit
    /// past that every day.</summary>
    public static bool KeepsMushroom(int blockId, int blockData) =>
        blockId == Blocks.Mycelium || (blockId == Blocks.Dirt && blockData == PodzolData);

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

    /// <summary>The woods a tree can be cut from — the block pair each <see cref="Species"/> row names. The six
    /// vanilla pairs, and a willow's dark-oak log under oak leaves, which is how the author's own willows are
    /// built.</summary>
    public static readonly IReadOnlyList<TreeWood> Woods =
    [
        new("oak", Blocks.Log, 0, Blocks.Leaves, 0),
        new("birch", Blocks.Log, 2, Blocks.Leaves, 2),
        new("spruce", Blocks.Log, 1, Blocks.Leaves, 1),
        new("jungle", Blocks.Log, 3, Blocks.Leaves, 3),
        new("acacia", Blocks.Log2, 0, Blocks.Leaves2, 0),
        new("dark oak", Blocks.Log2, 1, Blocks.Leaves2, 1),
        new("willow", Blocks.Log2, 1, Blocks.Leaves, 0),
    ];

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
        new("willow", Woods[6], CanopyProfile.Weeping, Height: 11, CanopyRadius: 5.0),
    ];

    public static TreeSpecies SpeciesNamed(string name)
        => Species.FirstOrDefault(species => species.Name == name) ?? Species[0];
}

/// <summary>The kind of ground a plant stands on, as far as what grows on it goes.</summary>
public enum Soil
{
    /// <summary>Nothing grows: paving, stone, gravel, a monument.</summary>
    None,
    /// <summary>Grass and dirt: grass, ferns, flowers and tall grass; a dead bush on dirt; a mushroom on
    /// podzol.</summary>
    Fertile,
    /// <summary>Mycelium: a mushroom, and nothing else.</summary>
    Mycelium,
    /// <summary>Sand: a dead bush or a cactus.</summary>
    Sand,
    /// <summary>Hardened and stained clay: a dead bush.</summary>
    Clay,
    /// <summary>Farmland: a crop, and nothing else.</summary>
    Farmland,
}
