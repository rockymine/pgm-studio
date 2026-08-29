using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Painting;

/// <summary>
/// The built-in terrain finishes: the themes the library is seeded with, and the spread a new studio learns
/// what a rim, a wall, a surface and a fill do from.
///
/// <para>Here rather than beside the seeder because two things need them — the seeder that stores them and
/// anything that wants a finish without asking the database — and this is the lowest project both reach.</para>
///
/// <para>Each is read off a board that was built and looked at rather than invented, and each follows the two
/// placements the author states absolutely: a <b>voronoi belongs in the fill</b> and is made of stone, never
/// the surface; and a <b>field pattern's blocks are near shades of one ground</b>, so it carries a texture
/// rather than a border between two grounds.</para>
/// </summary>
public static class ThemePresets
{
    // The data nibbles the finishes name. Literals with a comment, the way the house presets are written:
    // Blocks carries the block and a nibble is what that block's variant happens to be.
    private const int Andesite = 5, Diorite = 3, Granite = 1;              // nibbles of block 1
    private const int CoarseDirt = 1, Podzol = 2;                          // nibbles of block 3
    private const int RedSand = 1;                                         // nibble of block 12
    private const int ChiseledSandstone = 1, SmoothSandstone = 2;          // nibbles of block 24
    private const int LightGrey = 8, Grey = 7, White = 0, Brown = 12;      // nibbles of block 159/172
    private const int MossyCobble = 48, StoneBrick = 98, Snow = 80;
    // The nibbles and the two blocks the authored pair below name, and nothing above does.
    private const int Cyan = 9, Green = 13, LightBlue = 3;                  // nibbles of block 159/35
    private const int SprucePlanks = 1;                                    // nibble of block 5
    private const int Mycelium = 110;
    private const int MossyStoneBrick = 1, CrackedStoneBrick = 2;          // nibbles of block 98

    private static TerrainMaterial Block(int id, int data = 0) => new SolidMaterial(id, data);

    /// <summary>A stack claimed downward from the top of its bucket, each band stating the courses it runs.</summary>
    private static TerrainMaterial Stack(params Band[] bands) => new LayeredMaterial(new BandStack(bands));

    /// <summary>A field of near shades, cut into bands by its stops in order.</summary>
    private static TerrainMaterial Field(uint seed, int scale, params TerrainMaterial[] stops)
        => new NoiseMaterial(seed, scale, Octaves: 2, stops);

    /// <summary>The body a board is cut out of: stone worn into cells, the grid line on the boundary and the
    /// middle taking whatever is left.</summary>
    private static TerrainMaterial StoneBody(uint seed, TerrainMaterial line, TerrainMaterial middle)
        => new VoronoiMaterial(seed, CellSize: 9, [new VoronoiBand(line, 1), new VoronoiBand(middle, 4)]);

    /// <summary>The team-tinted riser every finish shares the shape of: clay that takes the owning team's
    /// colour, falling through to a neutral shade on land no team owns.</summary>
    private static TerrainMaterial Tinted(int neutral)
        => new TeamTintedMaterial(Blocks.StainedClay, Block(Blocks.StainedClay, neutral));

    /// <summary>Grass over dirt, mottled — the ground a temperate board is played on.</summary>
    public static TerrainTheme Meadow { get; } = new()
    {
        Rim = new TopBand(Block(Blocks.QuartzBlock), 1),
        Surface = new TopBand(Stack(
            new Band(Field(21, 6, Block(Blocks.Grass), Block(Blocks.Grass), Block(Blocks.Dirt, Podzol)), 1),
            new Band(Block(Blocks.Dirt), 2)), 3),
        Wall = Tinted(LightGrey),
        Fill = StoneBody(22, Block(Blocks.Stone, Andesite), Block(Blocks.Stone)),
    };

    /// <summary>Sand over sandstone, with the rim in the chiselled variant — a desert board.</summary>
    public static TerrainTheme Dunes { get; } = new()
    {
        Rim = new TopBand(Block(Blocks.Sandstone, ChiseledSandstone), 1),
        Surface = new TopBand(Stack(
            new Band(Field(31, 7, Block(Blocks.Sand), Block(Blocks.Sand), Block(Blocks.Sand, RedSand)), 2),
            new Band(Block(Blocks.Sandstone), 2)), 4),
        Wall = Tinted(Brown),
        Fill = StoneBody(32, Block(Blocks.Sandstone, SmoothSandstone), Block(Blocks.Sandstone)),
    };

    /// <summary>Coarse ground over grey stone, burnt out — the finish a quarry or a scar is read in.</summary>
    public static TerrainTheme Ashfall { get; } = new()
    {
        Rim = new TopBand(Block(Blocks.Dirt, CoarseDirt), 1),
        Surface = new TopBand(Stack(
            new Band(Field(41, 5, Block(Blocks.Gravel), Block(Blocks.Dirt, CoarseDirt), Block(Blocks.Gravel)), 1),
            new Band(Block(Blocks.Stone, Andesite), 2)), 3),
        Wall = Tinted(Grey),
        Fill = StoneBody(42, Block(StoneBrick, CrackedStoneBrick), Block(Blocks.Stone, Andesite)),
    };

    /// <summary>Snow over packed ground, the rim in bare quartz — a board played white.</summary>
    public static TerrainTheme Firnline { get; } = new()
    {
        Rim = new TopBand(Block(Blocks.QuartzBlock), 1),
        Surface = new TopBand(Stack(
            new Band(Block(Snow), 1),
            new Band(Field(51, 6, Block(Blocks.StainedClay, White), Block(Blocks.Stone, Diorite),
                Block(Blocks.StainedClay, White)), 2)), 3),
        Wall = Tinted(White),
        Fill = StoneBody(52, Block(Blocks.Stone, Diorite), Block(Blocks.Stone)),
    };

    /// <summary>Hardened clay over clay — the warm, banded ground a mesa board is cut from.</summary>
    public static TerrainTheme Claybed { get; } = new()
    {
        Rim = new TopBand(Block(Blocks.StainedClay, Brown), 1),
        Surface = new TopBand(Stack(
            new Band(Field(61, 5, Block(Blocks.HardenedClay), Block(Blocks.StainedClay, Brown),
                Block(Blocks.HardenedClay)), 2),
            new Band(Block(Blocks.Clay), 2)), 4),
        Wall = Tinted(Brown),
        Fill = StoneBody(62, Block(Blocks.Stone, Granite), Block(Blocks.HardenedClay)),
    };

    /// <summary>Grass running onto worn stone, mossy at the edge — the overgrown ruin a town board sits in.</summary>
    public static TerrainTheme Oldstone { get; } = new()
    {
        Rim = new TopBand(Block(MossyCobble), 1),
        Surface = new TopBand(Stack(
            new Band(Field(71, 8, Block(Blocks.Grass), Block(Blocks.Stone, Andesite), Block(Blocks.Grass)), 1),
            new Band(Block(Blocks.Dirt), 2)), 3),
        Wall = Tinted(LightGrey),
        Fill = StoneBody(72, Block(StoneBrick, MossyStoneBrick), Block(StoneBrick)),
    };

    // ── the two an author made by hand ────────────────────────────────────────────────────────────────
    // Both put the whole finish in the FILL and switch every other bucket off, which is the opposite of how
    // the six above are built: a rim, a wall and a surface each claim part of a column, and these two claim
    // none of it so one stack runs the full depth. Bedrock is relative, so the stack keeps its proportions
    // whatever the ground stands at. Written out rather than shared with a helper because the shape is the
    // author's statement, and a helper here would be a fourth reader of it.

    /// <summary>The body both of them stand on: brown clay over spruce over hardened clay in flat cells, and
    /// under it the cyan-and-grey stone the two clay fills are cut from.</summary>
    private static Band[] ClayBody =>
    [
        new Band(new VoronoiMaterial(1, 10,
        [
            new VoronoiBand(Block(Blocks.StainedClay, Brown), 1),
            new VoronoiBand(Block(Blocks.Planks, SprucePlanks), 2),
            new VoronoiBand(Block(Blocks.HardenedClay), 1),
        ], Rise: 2), 3),
        new Band(new VoronoiMaterial(1, 10,
        [
            new VoronoiBand(Block(Blocks.StainedClay, Cyan), 1),
            new VoronoiBand(Block(Blocks.StainedClay, LightGrey), 2),
            new VoronoiBand(Block(Blocks.Wool, Grey), 1),
        ], Rise: 3), 1),
    ];

    /// <summary>Turf in wool, clay and real grass over the clay body — a green board with no rim and no wall,
    /// so the ground reads as one thing from the top of a cliff to the bottom of it.</summary>
    public static TerrainTheme ClayGrassland { get; } = new()
    {
        Bedrock = BedrockSpec.TerrainRelative(12),
        Rim = new TopBand(Block(Blocks.Stone), 1, Enabled: false),
        Surface = new TopBand(Block(Blocks.Stone), 1, Enabled: false),
        Wall = Block(Blocks.Stone),
        WallEnabled = false,
        Fill = new LayeredMaterial(new BandStack(
        [
            new Band(new CellMaterial(1, 4, 50, 4,
                [Block(Blocks.Wool, Green), Block(Blocks.StainedClay, Green), Block(Blocks.Grass)]), 1),
            .. ClayBody,
        ])),
    };

    /// <summary>The same board in mushroom colours: light blue and light grey clay over real mycelium.</summary>
    public static TerrainTheme ClayMycelium { get; } = new()
    {
        Bedrock = BedrockSpec.TerrainRelative(12),
        Rim = new TopBand(Block(Blocks.Stone), 1, Enabled: false),
        Surface = new TopBand(Block(Blocks.Stone), 1, Enabled: false),
        Wall = Block(Blocks.Stone),
        WallEnabled = false,
        Fill = new LayeredMaterial(new BandStack(
        [
            new Band(new CellMaterial(1, 4, 50, 4,
                [Block(Blocks.StainedClay, LightBlue), Block(Blocks.StainedClay, LightGrey),
                 Block(Mycelium)]), 1),
            .. ClayBody,
        ])),
    };

    /// <summary>The eight, each under the name it is stored as — six built for the showcase and two an author
    /// made.</summary>
    public static readonly IReadOnlyList<(string Name, TerrainTheme Theme)> All =
    [
        ("meadow", Meadow),
        ("dunes", Dunes),
        ("ashfall", Ashfall),
        ("firnline", Firnline),
        ("claybed", Claybed),
        ("oldstone", Oldstone),
        ("clay grassland", ClayGrassland),
        ("clay mycelium", ClayMycelium),
    ];
}
