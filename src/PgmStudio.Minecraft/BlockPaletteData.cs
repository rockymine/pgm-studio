namespace PgmStudio.Minecraft;

/// <summary>
/// One palette row: a block id, the normalized metadata it answers for (<see cref="BlockPaletteData.Any"/>
/// for the block's base row), the colour and the display name.
/// </summary>
internal readonly record struct PaletteEntry(int Id, int Meta, uint Rgb, string Name);

/// <summary>
/// The Java 1.8 colour and name table — every block id 0–197 and every metadata sub-type within them.
///
/// <para><b>Where a colour comes from.</b> Each is the alpha-masked mean of that block's 16×16 texture in
/// the 1.8 asset set, taken from the face a top-down render actually sees: a log shows its rings rather
/// than its bark, grass and podzol their tops, hay its bound end. Three families depart from the raw mean
/// on purpose. <b>Biome-tinted</b> blocks — grass, leaves, vines, sugar cane, lily pad, tall grass, water —
/// are multiplied by a fixed temperate (plains/forest) tint, because a static render has no biome to
/// sample; spruce and birch leaves instead carry the constant tint the game applies to them everywhere.
/// <b>Ores</b> lean on their accent rather than the mean, which is four-fifths stone matrix and would print
/// coal, iron, gold and diamond as the same grey. <b>Alpha-heavy</b> blocks (glass, panes, bars, torches,
/// cobweb, rails) average only their opaque pixels, so they read as the material rather than as the
/// background they mostly are.</para>
///
/// <para>Rows are grouped by family so a sub-type set can be read as a unit, and the families that repeat
/// across many ids — the six woods, the eight stone slabs, the three dye families — are expanded from one
/// declaration rather than restated per id, which is what kept the old table's spruce door birch-coloured
/// and its acacia door spruce-coloured.</para>
/// </summary>
internal static class BlockPaletteData
{
    /// <summary>The metadata marker for a block's base row — the colour every unmatched sub-type falls to.</summary>
    internal const int Any = -1;

    /// <summary>The dye order shared by wool, carpet, stained glass, panes and stained clay.</summary>
    private static readonly string[] DyeNames =
    [
        "White", "Orange", "Magenta", "Light Blue", "Yellow", "Lime", "Pink", "Gray",
        "Light Gray", "Cyan", "Purple", "Blue", "Brown", "Green", "Red", "Black",
    ];

    // The three dye families do NOT share a colour ramp: wool is the dye applied to a fibrous white,
    // stained glass is the saturated dye itself, and stained clay is that dye burnt into terracotta —
    // which is why the old single ramp printed a brown-clay floor the same colour as brown wool.
    private static readonly uint[] WoolRgb =
    [
        0xDDDDDD, 0xEB8844, 0xC354CD, 0x6689D3, 0xDECF2A, 0x41CD34, 0xD88198, 0x434343,
        0xABABAB, 0x287697, 0x7B2FBE, 0x253192, 0x51301A, 0x3B511A, 0xB3312C, 0x1A1A1A,
    ];

    private static readonly uint[] StainedGlassRgb =
    [
        0xFFFFFF, 0xD87F33, 0xB24CD8, 0x6699D8, 0xE5E533, 0x7FCC19, 0xF27FA5, 0x4C4C4C,
        0x999999, 0x4C7F99, 0x7F3FB2, 0x334CB2, 0x664C33, 0x667F33, 0x993333, 0x191919,
    ];

    private static readonly uint[] StainedClayRgb =
    [
        0xD1B2A1, 0xA05325, 0x95576C, 0x706C8A, 0xBA8523, 0x677535, 0xA24E4E, 0x392823,
        0x876B62, 0x575B5B, 0x764656, 0x4A3A5B, 0x4D3324, 0x4C532A, 0x8E3C2E, 0x251710,
    ];

    /// <summary>A wood type and the colours its whole product line takes.</summary>
    private readonly record struct Wood(string Name, uint Planks, uint LogTop, uint Leaves, uint Sapling, uint Door);

    private static readonly Wood[] Woods =
    [
        new("Oak",      0xA2814E, 0x9C7F4E, 0x48782E, 0x4E6F37, 0x86653A),
        new("Spruce",   0x725430, 0x6C4D2D, 0x33582F, 0x3F5A32, 0x5E4429),
        new("Birch",    0xC4B078, 0xC7B47D, 0x6E8C49, 0x7F9B62, 0xC0AD79),
        new("Jungle",   0xA87C5C, 0x9A7248, 0x3E7B23, 0x386E2A, 0x9C7355),
        new("Acacia",   0xA95C33, 0xA9622E, 0x55772C, 0x77872E, 0xA05730),
        new("Dark Oak", 0x422B14, 0x5E4629, 0x3E6E26, 0x40602A, 0x3E2812),
    ];

    // Per-wood ids, in the Woods order. Fences and gates run …jungle, dark oak, acacia; doors run
    // …jungle, acacia, dark oak — 1.8 numbered the two families in different orders.
    private static readonly int[] StairIds = [53, 134, 135, 136, 163, 164];
    private static readonly int[] FenceIds = [85, 188, 189, 190, 192, 191];
    private static readonly int[] FenceGateIds = [107, 183, 184, 185, 187, 186];
    private static readonly int[] DoorIds = [64, 193, 194, 195, 196, 197];

    private static readonly (string Name, uint Rgb)[] StoneKinds =
    [
        ("Stone", 0x7E7E7E), ("Granite", 0x9A6752), ("Polished Granite", 0x9C6C5B), ("Diorite", 0xACACAE),
        ("Polished Diorite", 0xC4C4C7), ("Andesite", 0x8A8A8D), ("Polished Andesite", 0x8E8E91),
    ];

    private static readonly (string Name, uint Rgb)[] StoneBrickKinds =
    [
        ("Stone Bricks", 0x7A7A7A), ("Mossy Stone Bricks", 0x74796A),
        ("Cracked Stone Bricks", 0x767676), ("Chiseled Stone Bricks", 0x797979),
    ];

    private static readonly (string Name, uint Rgb)[] StoneSlabKinds =
    [
        ("Stone", 0xA8A8A8), ("Sandstone", 0xD9CFA1), ("Wooden", 0xA2814E), ("Cobblestone", 0x7A7A7A),
        ("Brick", 0x96604D), ("Stone Brick", 0x7A7A7A), ("Nether Brick", 0x2D171B), ("Quartz", 0xECE9E2),
    ];

    /// <summary>Every palette row, base entries and sub-types alike.</summary>
    internal static IReadOnlyList<PaletteEntry> Entries { get; } = Build();

    private static List<PaletteEntry> Build()
    {
        var rows = new List<PaletteEntry>(512);
        void Block(int id, string name, uint rgb) => rows.Add(new PaletteEntry(id, Any, rgb, name));
        void Variant(int id, int meta, string name, uint rgb) => rows.Add(new PaletteEntry(id, meta, rgb, name));

        // ---- Woods -----------------------------------------------------------------------------------
        // Planks, saplings, slabs, logs and leaves are metadata sub-types; stairs, fences, gates and doors
        // each got their own id, so the same six colours are spread over four id families.
        for (var wood = 0; wood < Woods.Length; wood++)
        {
            var (name, planks, logTop, leaves, sapling, door) = Woods[wood];
            Variant(5, wood, $"{name} Planks", planks);
            Variant(6, wood, $"{name} Sapling", sapling);
            Variant(125, wood, $"Double {name} Wood Slab", planks);
            Variant(126, wood, $"{name} Wood Slab", planks);
            // The first four woods fill 17/18; acacia and dark oak arrived later on 162/161.
            if (wood < 4)
            {
                Variant(17, wood, $"{name} Log", logTop);
                Variant(18, wood, $"{name} Leaves", leaves);
            }
            else
            {
                Variant(162, wood - 4, $"{name} Log", logTop);
                Variant(161, wood - 4, $"{name} Leaves", leaves);
            }
            Block(StairIds[wood], $"{name} Stairs", planks);
            Block(FenceIds[wood], $"{name} Fence", planks);
            Block(FenceGateIds[wood], $"{name} Fence Gate", planks);
            Block(DoorIds[wood], $"{name} Door", door);
        }

        // ---- Dye families ----------------------------------------------------------------------------
        for (var dye = 0; dye < DyeNames.Length; dye++)
        {
            Variant(35, dye, $"{DyeNames[dye]} Wool", WoolRgb[dye]);
            Variant(171, dye, $"{DyeNames[dye]} Carpet", WoolRgb[dye]);
            Variant(95, dye, $"{DyeNames[dye]} Stained Glass", StainedGlassRgb[dye]);
            Variant(160, dye, $"{DyeNames[dye]} Stained Glass Pane", StainedGlassRgb[dye]);
            Variant(159, dye, $"{DyeNames[dye]} Stained Clay", StainedClayRgb[dye]);
        }
        Block(35, "Wool", WoolRgb[0]);
        Block(171, "Carpet", WoolRgb[0]);
        Block(95, "Stained Glass", StainedGlassRgb[0]);
        Block(160, "Stained Glass Pane", StainedGlassRgb[0]);
        Block(159, "Stained Clay", StainedClayRgb[0]);

        // ---- Stone families --------------------------------------------------------------------------
        for (var kind = 0; kind < StoneKinds.Length; kind++)
            Variant(1, kind, StoneKinds[kind].Name, StoneKinds[kind].Rgb);
        for (var kind = 0; kind < StoneBrickKinds.Length; kind++)
            Variant(98, kind, StoneBrickKinds[kind].Name, StoneBrickKinds[kind].Rgb);
        for (var kind = 0; kind < StoneSlabKinds.Length; kind++)
        {
            Variant(44, kind, $"{StoneSlabKinds[kind].Name} Slab", StoneSlabKinds[kind].Rgb);
            Variant(43, kind, $"Double {StoneSlabKinds[kind].Name} Slab", StoneSlabKinds[kind].Rgb);
        }
        // A silverfish block wears its host's texture exactly, so it takes the host's colour.
        Variant(97, 0, "Infested Stone", 0x7E7E7E);
        Variant(97, 1, "Infested Cobblestone", 0x7A7A7A);
        for (var kind = 0; kind < StoneBrickKinds.Length; kind++)
            Variant(97, kind + 2, $"Infested {StoneBrickKinds[kind].Name}", StoneBrickKinds[kind].Rgb);

        Block(0, "Air", 0x000000);
        Block(4, "Cobblestone", 0x7A7A7A);
        Block(7, "Bedrock", 0x565656);
        Block(48, "Mossy Cobblestone", 0x6E7B62);
        Block(49, "Obsidian", 0x15121F);
        Block(67, "Cobblestone Stairs", 0x7A7A7A);
        Block(109, "Stone Brick Stairs", 0x7A7A7A);
        Block(139, "Cobblestone Wall", 0x7A7A7A);
        Variant(139, 0, "Cobblestone Wall", 0x7A7A7A);
        Variant(139, 1, "Mossy Cobblestone Wall", 0x6E7B62);

        // ---- Ground ----------------------------------------------------------------------------------
        // Grass, its tall form and the crops carry the plains foliage tint; a podzol or coarse-dirt top is
        // its own texture, not a shade of dirt.
        Block(2, "Grass Block", 0x79C05A);
        Variant(3, 0, "Dirt", 0x866043);
        Variant(3, 1, "Coarse Dirt", 0x7E5A3C);
        Variant(3, 2, "Podzol", 0x5D421F);
        Block(13, "Gravel", 0x837F7E);
        Variant(12, 0, "Sand", 0xDBD3A0);
        Variant(12, 1, "Red Sand", 0xBE6621);
        Block(60, "Farmland", 0x6A4A2C);
        Block(82, "Clay", 0xA4A8B8);
        Block(110, "Mycelium", 0x6F6265);
        Block(172, "Hardened Clay", 0x975D43);

        Variant(24, 0, "Sandstone", 0xD9CFA1);
        Variant(24, 1, "Chiseled Sandstone", 0xD6CB9C);
        Variant(24, 2, "Smooth Sandstone", 0xD8CEA0);
        Block(128, "Sandstone Stairs", 0xD9CFA1);
        Variant(179, 0, "Red Sandstone", 0xBA6A28);
        Variant(179, 1, "Chiseled Red Sandstone", 0xB96827);
        Variant(179, 2, "Smooth Red Sandstone", 0xBA6A28);
        Block(180, "Red Sandstone Stairs", 0xBA6A28);
        Block(181, "Double Red Sandstone Slab", 0xBA6A28);
        Block(182, "Red Sandstone Slab", 0xBA6A28);

        // ---- Liquids, ice and snow -------------------------------------------------------------------
        Block(8, "Water", 0x3F76E4);
        Block(9, "Water", 0x3F76E4);
        Block(10, "Lava", 0xD45A12);
        Block(11, "Lava", 0xD45A12);
        Block(78, "Snow Layer", 0xF5FAFA);
        Block(79, "Ice", 0x91B7FB);
        Block(80, "Snow Block", 0xF5FAFA);
        Block(174, "Packed Ice", 0x8EB9E8);

        // ---- Ores and mineral blocks -----------------------------------------------------------------
        // Accent-weighted: the raw texture mean of any ore is the stone it is embedded in.
        Block(14, "Gold Ore", 0xE7B33B);
        Block(15, "Iron Ore", 0xD8AF93);
        Block(16, "Coal Ore", 0x373737);
        Block(21, "Lapis Lazuli Ore", 0x2B5CB8);
        Block(56, "Diamond Ore", 0x45DDD2);
        Block(73, "Redstone Ore", 0xAA1E1E);
        Block(74, "Redstone Ore", 0xC42222);
        Block(129, "Emerald Ore", 0x17C25A);
        Block(153, "Nether Quartz Ore", 0xA5665E);
        Block(22, "Lapis Lazuli Block", 0x1E438C);
        Block(41, "Gold Block", 0xF9DE4E);
        Block(42, "Iron Block", 0xDBDBDB);
        Block(57, "Diamond Block", 0x62DBD6);
        Block(133, "Emerald Block", 0x2CD965);
        Block(152, "Redstone Block", 0xA81E12);
        Block(173, "Coal Block", 0x191919);

        // ---- Plants ----------------------------------------------------------------------------------
        Block(31, "Tall Grass", 0x6EA83E);
        Variant(31, 0, "Shrub", 0x7A6A3D);
        Variant(31, 1, "Tall Grass", 0x6EA83E);
        Variant(31, 2, "Fern", 0x5E9B36);
        Block(32, "Dead Bush", 0x946428);
        Block(37, "Dandelion", 0xD3D82F);
        Variant(38, 0, "Poppy", 0xB02A20);
        Variant(38, 1, "Blue Orchid", 0x2E8FBB);
        Variant(38, 2, "Allium", 0xA97BC6);
        Variant(38, 3, "Azure Bluet", 0xDDE0E3);
        Variant(38, 4, "Red Tulip", 0xB32A25);
        Variant(38, 5, "Orange Tulip", 0xC87018);
        Variant(38, 6, "White Tulip", 0xD8DCD8);
        Variant(38, 7, "Pink Tulip", 0xD69ABF);
        Variant(38, 8, "Oxeye Daisy", 0xE0E2C6);
        Block(38, "Flower", 0xB02A20);
        Block(39, "Brown Mushroom", 0x9A7458);
        Block(40, "Red Mushroom", 0xC6423B);
        Block(59, "Wheat", 0xA89B3E);
        Block(81, "Cactus", 0x557F30);
        Block(83, "Sugar Cane", 0x82B563);
        Block(86, "Pumpkin", 0xC07615);
        Block(91, "Jack o'Lantern", 0xC98A21);
        Block(103, "Melon", 0x6E8C21);
        Block(104, "Pumpkin Stem", 0x74923D);
        Block(105, "Melon Stem", 0x74923D);
        Block(106, "Vines", 0x416F26);
        Block(111, "Lily Pad", 0x2C6B1E);
        Block(115, "Nether Wart", 0x6E1417);
        Block(127, "Cocoa", 0x9C5B22);
        Block(141, "Carrots", 0x4E8A2C);
        Block(142, "Potatoes", 0x57922F);
        Block(170, "Hay Bale", 0xA68D1A);
        Variant(175, 0, "Sunflower", 0xDCC423);
        Variant(175, 1, "Lilac", 0xBFA9C6);
        Variant(175, 2, "Double Tall Grass", 0x6EA83E);
        Variant(175, 3, "Large Fern", 0x5E9B36);
        Variant(175, 4, "Rose Bush", 0xA83A30);
        Variant(175, 5, "Peony", 0xD0B5CE);
        Block(175, "Double Plant", 0x6EA83E);

        // Mushroom-block metadata selects which faces show cap, pore or stem rather than a material, so the
        // three surfaces are given entries and the rest of the nibble falls to the cap.
        Block(99, "Brown Mushroom Block", 0x9A7458);
        Variant(99, 0, "Brown Mushroom Block", 0xBFAF95);
        Variant(99, 10, "Mushroom Stem", 0xCBC4AB);
        Variant(99, 15, "Mushroom Stem", 0xCBC4AB);
        Block(100, "Red Mushroom Block", 0xC6423B);
        Variant(100, 0, "Red Mushroom Block", 0xBFAF95);
        Variant(100, 10, "Mushroom Stem", 0xCBC4AB);
        Variant(100, 15, "Mushroom Stem", 0xCBC4AB);

        // ---- Built materials -------------------------------------------------------------------------
        Block(19, "Sponge", 0xC2BE49);
        Variant(19, 0, "Sponge", 0xC2BE49);
        Variant(19, 1, "Wet Sponge", 0xA5A63C);
        Block(20, "Glass", 0xC0DCE0);
        Block(102, "Glass Pane", 0xC0DCE0);
        Block(30, "Cobweb", 0xE0E0E0);
        Block(45, "Bricks", 0x96604D);
        Block(108, "Brick Stairs", 0x96604D);
        Block(46, "TNT", 0xA03B2E);
        Block(47, "Bookshelf", 0x6E563A);
        Block(89, "Glowstone", 0xAB8354);
        Block(101, "Iron Bars", 0xA5A5A5);
        Block(112, "Nether Bricks", 0x2D171B);
        Block(113, "Nether Brick Fence", 0x2D171B);
        Block(114, "Nether Brick Stairs", 0x2D171B);
        Block(87, "Netherrack", 0x6F3634);
        Block(88, "Soul Sand", 0x52403A);
        Block(121, "End Stone", 0xDDDCA5);
        Variant(155, 0, "Quartz Block", 0xECE9E2);
        Variant(155, 1, "Chiseled Quartz Block", 0xE9E5DC);
        Variant(155, 2, "Quartz Pillar", 0xECE9E1);
        Block(156, "Quartz Stairs", 0xECE9E2);
        Variant(168, 0, "Prismarine", 0x63AB9E);
        Variant(168, 1, "Prismarine Bricks", 0x6BB5A6);
        Variant(168, 2, "Dark Prismarine", 0x345A4F);
        Block(169, "Sea Lantern", 0xACC7BE);
        Block(165, "Slime Block", 0x78C05A);
        Block(166, "Barrier", 0xD42A2A);

        // ---- Utility, redstone and decoration --------------------------------------------------------
        Block(23, "Dispenser", 0x6F6F6F);
        Block(25, "Note Block", 0x66432D);
        Block(26, "Bed", 0x9B322D);
        Block(27, "Powered Rail", 0x8B7B55);
        Block(28, "Detector Rail", 0x7E7156);
        Block(29, "Sticky Piston", 0x7C8B54);
        Block(33, "Piston", 0x9C8151);
        Block(34, "Piston Head", 0x9C8151);
        Block(36, "Moving Piston", 0x9C8151);
        Block(50, "Torch", 0xDFAF4A);
        Block(51, "Fire", 0xDB7A18);
        Block(52, "Mob Spawner", 0x1E2A34);
        Block(54, "Chest", 0xA2763C);
        Block(55, "Redstone Wire", 0xA01A0F);
        Block(58, "Crafting Table", 0x7C5634);
        Block(61, "Furnace", 0x6F6F6F);
        Block(62, "Furnace", 0x6F6F6F);
        Block(63, "Sign", 0xA2814E);
        Block(65, "Ladder", 0x8A6B3F);
        Block(66, "Rail", 0x8B7B55);
        Block(68, "Wall Sign", 0xA2814E);
        Block(69, "Lever", 0x7C6647);
        Block(70, "Stone Pressure Plate", 0x7E7E7E);
        Block(71, "Iron Door", 0xC4C4C4);
        Block(72, "Wooden Pressure Plate", 0xA2814E);
        Block(75, "Redstone Torch (off)", 0x5C1E14);
        Block(76, "Redstone Torch", 0xC42B1D);
        Block(77, "Stone Button", 0x7E7E7E);
        Block(84, "Jukebox", 0x6B4A2E);
        Block(90, "Nether Portal", 0x691FA5);
        Block(92, "Cake", 0xEEDECD);
        Block(93, "Repeater (off)", 0xA8A8A8);
        Block(94, "Repeater", 0xAEA8A8);
        Block(96, "Trapdoor", 0x7C6037);
        Block(116, "Enchanting Table", 0x96413F);
        Block(117, "Brewing Stand", 0x7C6A55);
        Block(118, "Cauldron", 0x4A4A4A);
        Block(119, "End Portal", 0x0C0C14);
        Block(120, "End Portal Frame", 0x567C65);
        Block(122, "Dragon Egg", 0x12101A);
        Block(123, "Redstone Lamp (off)", 0x6F4823);
        Block(124, "Redstone Lamp", 0xB78453);
        Block(130, "Ender Chest", 0x23383A);
        Block(131, "Tripwire Hook", 0x8A7754);
        Block(132, "Tripwire", 0x7A7A7A);
        Block(137, "Command Block", 0xB0865B);
        Block(138, "Beacon", 0x79E0DA);
        Block(140, "Flower Pot", 0x7A4A3A);
        Block(143, "Wooden Button", 0xA2814E);
        Block(144, "Mob Head", 0x8A8A8A);
        Block(145, "Anvil", 0x4A4A4A);
        Variant(145, 0, "Anvil", 0x4C4C4C);
        Variant(145, 1, "Chipped Anvil", 0x494949);
        Variant(145, 2, "Damaged Anvil", 0x464646);
        Block(146, "Trapped Chest", 0xA2763C);
        Block(147, "Weighted Pressure Plate (Light)", 0xF9DE4E);
        Block(148, "Weighted Pressure Plate (Heavy)", 0xDBDBDB);
        Block(149, "Comparator (off)", 0xA8A8A8);
        Block(150, "Comparator", 0xB4A8A8);
        Block(151, "Daylight Sensor", 0x8A7A5C);
        Block(154, "Hopper", 0x4A4A4A);
        Block(157, "Activator Rail", 0x8A6C50);
        Block(158, "Dropper", 0x6F6F6F);
        Block(167, "Iron Trapdoor", 0xC4C4C4);
        Block(176, "Banner", 0xA2814E);
        Block(177, "Wall Banner", 0xA2814E);
        Block(178, "Daylight Sensor (inverted)", 0x7A6C50);

        return rows;
    }
}
