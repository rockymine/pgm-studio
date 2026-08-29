using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Painting;

/// <summary>
/// The ground patterns an author made by hand, under the names they were saved as — the first set in this
/// library that came from a person rather than from a generator, which is why they are kept as presets rather
/// than left in one database.
///
/// <para><b>These are results, not derivations.</b> Every other preset here exists because some pass needs it;
/// these exist because someone chose them, and nothing can re-derive a choice. They are seeded so a fresh
/// studio opens on them and an agent can bind them by name, and pinned by
/// <c>StylePresetsTests</c> against the JSON they were authored as, so a hand edit that changes what one paints
/// has to be a deliberate one.</para>
///
/// <para>Unlike <see cref="HousePresets"/>'s materials these belong to no building: they are ground, and a
/// theme binds them to a bucket. The seed reads them alongside the house and theme materials.</para>
/// </summary>
public static class StylePresets
{
    // The nibbles the palettes below name, spelled once. A raw number in a palette says nothing about which
    // of a block's sixteen faces it is.
    private const int Coarse = 1, Podzol = 2;                            // dirt
    private const int Andesite = 5, Granite = 1, PolishedGranite = 2, Diorite = 3, PolishedDiorite = 4;
    private const int Spruce = 1, Birch = 2, Jungle = 3, Acacia = 4;     // planks
    private const int Cyan = 9, Gray = 7, LightGray = 8, Black = 15, Lime = 5, Green = 13;
    private const int Orange = 1, Brown = 12, LightBlue = 3;
    private const int RedSandstone = 179, RedSand = 1, SmoothSandstoneSlab = 9;
    private const int EndStone = 121, MushroomPores = 100, PrismarineBricks = 2;
    // Blocks that no pass names elsewhere, so they are spelled here rather than added to the palette's own list.
    private const int Snow = 80, Ice = 79, PackedIce = 174, DoubleStoneSlab = 43, Prismarine = 168;

    private static TerrainMaterial Block(int id, int data = 0) => new SolidMaterial(id, data);

    /// <summary>Two clays a mycelium bed is mottled from — the surface a mushroom island wants.</summary>
    public static TerrainMaterial MyceliumClaySurface => new CellMaterial(
        1, CellSize: 4, Jitter: 50, Warp: 4,
        [Block(Blocks.StainedClay, LightBlue), Block(Blocks.StainedClay, LightGray)]);

    /// <summary>Turf in two greens, the plainest of the clay surfaces.</summary>
    public static TerrainMaterial GrassClaySurface => new CellMaterial(
        1, CellSize: 4, Jitter: 50, Warp: 4,
        [Block(Blocks.StainedClay, Lime), Block(Blocks.StainedClay, Green)]);

    /// <summary>The same turf a shade down, for ground meant to read as shadowed.</summary>
    public static TerrainMaterial GrassClaySurfaceDark => new CellMaterial(
        1, CellSize: 4, Jitter: 50, Warp: 4,
        [Block(Blocks.Wool, Green), Block(Blocks.StainedClay, Green)]);

    /// <summary>The body under those surfaces: cyan clay against grey wool, carried through the depth.</summary>
    public static TerrainMaterial DarkStoneClayFill => new CellMaterial(
        1, CellSize: 4, Jitter: 50, Warp: 10,
        [Block(Blocks.StainedClay, Cyan), Block(Blocks.Wool, Gray)], Rise: 3);

    /// <summary>A fill of brown clay, spruce and hardened clay in flat cells — layered ground rather than
    /// blobs, which is what a small rise against a large cell gives.</summary>
    public static TerrainMaterial DirtClayFill => new VoronoiMaterial(
        1, CellSize: 10,
        [
            new VoronoiBand(Block(Blocks.StainedClay, Brown), 1),
            new VoronoiBand(Block(Blocks.Planks, Spruce), 2),
            new VoronoiBand(Block(Blocks.HardenedClay), 1),
        ], Rise: 2);

    /// <summary>Stone, andesite, gravel and cobble as one fractal field — the plain stone ground everything
    /// else here is a variation on.</summary>
    public static TerrainMaterial StoneFractal => new NoiseMaterial(
        1, Scale: 8, Octaves: 7,
        [Block(Blocks.Stone), Block(Blocks.Stone, Andesite), Block(Blocks.Gravel), Block(Blocks.Cobblestone)],
        Rise: 3);

    /// <summary>The stone fractal broken up by a second field: cells of cyan clay and grey wool cut through
    /// it, so the stone reads as strata with something else running in them.</summary>
    public static TerrainMaterial StoneDarkVoronoi => new VoronoiMaterial(
        7, CellSize: 10,
        [
            new VoronoiBand(new CellMaterial(
                1, CellSize: 10, Jitter: 50, Warp: 4,
                [Block(Blocks.StainedClay, Cyan), StoneFractal, Block(Blocks.Wool, Gray)], Rise: 4), 1),
            new VoronoiBand(StoneFractal, 1),
        ], Rise: 4);

    /// <summary>Soil in five browns — the ground a field or a wood stands on.</summary>
    public static TerrainMaterial DirtFractal => new NoiseMaterial(
        1, Scale: 8, Octaves: 8,
        [
            Block(Blocks.Planks, Jungle), Block(Blocks.Dirt, Coarse), Block(Blocks.Planks, Spruce),
            Block(Blocks.Dirt), Block(Blocks.Stone, Granite),
        ], Rise: 3);

    /// <summary>Rusted ground: red sandstone and red sand against orange clay and acacia.</summary>
    public static TerrainMaterial RustCells => new CellMaterial(
        1, CellSize: 5, Jitter: 50, Warp: 5,
        [
            Block(RedSandstone), Block(Blocks.Sand, RedSand), Block(Blocks.StainedClay, Orange),
            Block(Blocks.Planks, Acacia), Block(Blocks.HardenedClay), Block(Blocks.Stone, Granite),
        ], Rise: 3);

    /// <summary>Granite, polished granite, hardened clay and dirt — bare fired earth.</summary>
    public static TerrainMaterial TerracottaWithDirt => new CellMaterial(
        1, CellSize: 5, Jitter: 50, Warp: 5,
        [
            Block(Blocks.Stone, Granite), Block(Blocks.Stone, PolishedGranite),
            Block(Blocks.HardenedClay), Block(Blocks.Dirt),
        ], Rise: 3);

    /// <summary>Pale stone: diorite twice over, clay and light grey wool.</summary>
    public static TerrainMaterial WhiteStoneCells => new CellMaterial(
        1, CellSize: 5, Jitter: 50, Warp: 5,
        [
            Block(Blocks.Stone, Diorite), Block(Blocks.Stone, Diorite),
            Block(Blocks.Clay), Block(Blocks.Wool, LightGray),
        ], Rise: 3);

    /// <summary>Frozen ground — diorite and polished diorite under snow, packed ice and ice.</summary>
    public static TerrainMaterial IceSurface => new CellMaterial(
        1, CellSize: 7, Jitter: 45, Warp: 4,
        [
            Block(Blocks.Stone, Diorite), Block(Blocks.Stone, PolishedDiorite), Block(Snow),
            Block(PackedIce, LightGray), Block(Ice),
        ], Rise: 3);

    /// <summary>Everything sandy: sand, a sandstone slab's face, birch, end stone and mushroom pores.</summary>
    public static TerrainMaterial AllSand => new CellMaterial(
        1, CellSize: 5, Jitter: 50, Warp: 4,
        [
            Block(Blocks.Sand), Block(DoubleStoneSlab, SmoothSandstoneSlab), Block(Blocks.Planks, Birch),
            Block(EndStone), Block(MushroomPores),
        ], Rise: 3);

    /// <summary>Everything green, across four families — grass, two clays, wool and prismarine — which is what
    /// a green that does not repeat is made of.</summary>
    public static TerrainMaterial AllGreen => new CellMaterial(
        1, CellSize: 9, Jitter: 65, Warp: 7,
        [
            Block(Blocks.Grass), Block(Blocks.StainedClay, Lime), Block(Blocks.StainedClay, Green),
            Block(Blocks.Wool, Green), Block(Prismarine, PrismarineBricks),
        ], Rise: 3);

    /// <summary>Two-wide stripes climbing a wall at 45° — the one of these written for a face rather than for
    /// ground, which is why it is a wall diagonal and not a field.</summary>
    public static TerrainMaterial BlackLightGrayStripes => new WallDiagonalMaterial(
        [new WallStripe(Block(Blocks.Wool, LightGray), 2), new WallStripe(Block(Blocks.Wool, Black), 2)]);

    /// <summary>Every one, under the name it was authored as. The seed reads this list.</summary>
    public static IReadOnlyList<(string Name, TerrainMaterial Material)> All =>
    [
        ("grass clay surface", GrassClaySurface),
        ("grass clay surface dark", GrassClaySurfaceDark),
        ("mycelium clay surface", MyceliumClaySurface),
        ("dark stone clay fill", DarkStoneClayFill),
        ("dirt clay fill", DirtClayFill),
        ("stone fractal", StoneFractal),
        ("stone dark voronoi", StoneDarkVoronoi),
        ("dirt fractal", DirtFractal),
        ("rust cells", RustCells),
        ("terracotta with dirt", TerracottaWithDirt),
        ("white stone cells", WhiteStoneCells),
        ("ice surface", IceSurface),
        ("all sand", AllSand),
        ("all green", AllGreen),
        ("black light gray stripes", BlackLightGrayStripes),
    ];
}
