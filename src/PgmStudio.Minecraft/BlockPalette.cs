namespace PgmStudio.Minecraft;

/// <summary>
/// Minecraft block ID → RGB colour + name lookup, for the surface render. Port of
/// <c>minecraft/colors.py</c> (P5): based on Minecraft's MapColor enum (1.8/1.9). Stained blocks
/// (wool=35, stained glass=95, stained clay=159, stained glass pane=160, carpet=171) use their
/// damage value as a sub-colour index into <see cref="StainColors"/>.
/// </summary>
public static class BlockPalette
{
    public readonly record struct Rgb(int R, int G, int B);

    private static readonly Dictionary<int, Rgb> StainColors = new()
    {
        [0]  = new(221, 221, 221), [1]  = new(216, 127,  51), [2]  = new(178,  76, 216),
        [3]  = new(102, 153, 216), [4]  = new(229, 229,  51), [5]  = new(127, 204,  25),
        [6]  = new(242, 127, 165), [7]  = new( 76,  76,  76), [8]  = new(153, 153, 153),
        [9]  = new( 76, 127, 153), [10] = new(127,  63, 178), [11] = new( 51,  76, 178),
        [12] = new(102,  76,  51), [13] = new(102, 127,  51), [14] = new(153,  51,  51),
        [15] = new( 25,  25,  25),
    };

    /// <summary>A block that shares its id with another and is told apart only by its data value — the stone,
    /// dirt, plank, sandstone, stone-brick, quartz and prismarine families of the numeric format. Unlike the
    /// sixteen-colour stained families, the data value here names a different block rather than a shade of one,
    /// so each carries its own name and colour.</summary>
    private static readonly Dictionary<(int Id, int Data), (string Name, Rgb Rgb)> Variants = new()
    {
        [(1, 1)] = ("Granite", new(149, 103, 85)),
        [(1, 2)] = ("Polished Granite", new(159, 114, 96)),
        [(1, 3)] = ("Diorite", new(190, 190, 190)),
        [(1, 4)] = ("Polished Diorite", new(200, 200, 202)),
        [(1, 5)] = ("Andesite", new(136, 136, 136)),
        [(1, 6)] = ("Polished Andesite", new(146, 148, 148)),
        [(3, 1)] = ("Coarse Dirt", new(134, 96, 67)),
        [(3, 2)] = ("Podzol", new(91, 60, 25)),
        [(5, 1)] = ("Spruce Planks", new(114, 84, 48)),
        [(5, 2)] = ("Birch Planks", new(192, 175, 121)),
        [(5, 3)] = ("Jungle Planks", new(160, 115, 80)),
        [(5, 4)] = ("Acacia Planks", new(168, 90, 50)),
        [(5, 5)] = ("Dark Oak Planks", new(66, 43, 20)),
        [(12, 1)] = ("Red Sand", new(190, 102, 33)),

        // Logs and leaves. Six woods share two ids between them, so without these a birch and a dark oak are
        // one picture — and the tree pickers exist precisely to show which wood a tree is cut from. The bark
        // takes its plank's colour so a wood reads the same wherever it appears, and the leaf a green of its
        // own: a spruce's blue-dark needles and a birch's pale yellow-green are most of what names them.
        // The log's low two bits are the wood; the upper bits carry the axis it was placed along.
        [(17, 1)] = ("Spruce Log", new(114, 84, 48)),
        [(17, 2)] = ("Birch Log", new(192, 175, 121)),
        [(17, 3)] = ("Jungle Log", new(160, 115, 80)),
        [(18, 1)] = ("Spruce Leaves", new(38, 92, 56)),
        [(18, 2)] = ("Birch Leaves", new(128, 167, 85)),
        [(18, 3)] = ("Jungle Leaves", new(42, 140, 30)),

        [(24, 1)] = ("Chiselled Sandstone", new(226, 196, 138)),
        [(24, 2)] = ("Smooth Sandstone", new(234, 208, 154)),
        [(98, 1)] = ("Mossy Stone Brick", new(115, 121, 105)),
        [(98, 2)] = ("Cracked Stone Brick", new(118, 117, 114)),
        [(98, 3)] = ("Chiselled Stone Brick", new(122, 122, 122)),
        [(155, 1)] = ("Chiselled Quartz", new(232, 227, 212)),
        [(155, 2)] = ("Quartz Pillar", new(236, 232, 219)),
        [(161, 1)] = ("Dark Oak Leaves", new(28, 88, 30)),
        [(162, 1)] = ("Dark Oak Log", new(66, 43, 20)),
        [(168, 1)] = ("Prismarine Bricks", new(99, 171, 158)),
        [(168, 2)] = ("Dark Prismarine", new(51, 91, 75)),
        [(179, 1)] = ("Chiselled Red Sandstone", new(170, 92, 40)),
        [(179, 2)] = ("Smooth Red Sandstone", new(175, 98, 44)),

        // Plants. Their base ids are all one generic plant-green, which makes a field of poppies and a field of
        // dandelions the same picture — and the whole point of a flower field is its colour, so each shade of
        // the flower blocks gets its own.
        [(31, 1)] = ("Tall Grass", new(94, 156, 62)),
        [(31, 2)] = ("Fern", new(72, 128, 54)),
        [(37, 0)] = ("Dandelion", new(226, 205, 62)),
        [(38, 0)] = ("Poppy", new(198, 62, 52)),
        [(38, 1)] = ("Blue Orchid", new(84, 168, 214)),
        [(38, 2)] = ("Allium", new(178, 138, 210)),
        [(38, 3)] = ("Azure Bluet", new(214, 224, 232)),
        [(38, 8)] = ("Oxeye Daisy", new(232, 236, 220)),
        [(175, 2)] = ("Double Tall Grass", new(102, 164, 66)),
        [(175, 3)] = ("Large Fern", new(78, 134, 58)),
    };

    /// <summary>Every (id, data) pair that names a variant block rather than a shade — what a picker offers so
    /// andesite is a list entry instead of a number an author has to know.</summary>
    public static IEnumerable<(int Id, int Data)> VariantBlocks
        => Variants.Keys.OrderBy(block => block.Id).ThenBy(block => block.Data);

    // Keyed by (blockId, blockData); blockData -1 = "any". Mirrors the _bc(...) calls in colors.py.
    private static readonly Dictionary<(int, int), Rgb> Table = BuildTable();

    private static readonly string[] StainColorNames =
    [
        "White", "Orange", "Magenta", "Light Blue", "Yellow", "Lime",
        "Pink", "Gray", "Light Gray", "Cyan", "Purple", "Blue",
        "Brown", "Green", "Red", "Black",
    ];

    private static readonly Dictionary<int, string> StainBlockBaseNames = new()
    {
        [35] = "Wool", [95] = "Stained Glass", [159] = "Stained Clay",
        [160] = "Stained Glass Pane", [171] = "Carpet",
    };

    private static readonly Dictionary<int, string> BlockNames = new()
    {
        [0] = "Air", [1] = "Stone", [2] = "Grass", [3] = "Dirt",
        [4] = "Cobblestone", [7] = "Bedrock", [8] = "Water", [9] = "Water",
        [10] = "Lava", [11] = "Lava", [12] = "Sand", [13] = "Gravel",
        [14] = "Gold Ore", [15] = "Iron Ore", [16] = "Coal Ore", [17] = "Oak Wood",
        [18] = "Oak Leaves", [19] = "Sponge", [20] = "Glass", [21] = "Lapis Ore",
        [22] = "Lapis Block", [24] = "Sandstone", [25] = "Note Block", [26] = "Bed",
        [30] = "Cobweb", [31] = "Tall Grass", [36] = "Piston Head", [41] = "Gold Block",
        [42] = "Iron Block", [43] = "Double Slab", [44] = "Slab", [45] = "Brick",
        [46] = "TNT", [47] = "Bookshelf", [48] = "Mossy Cobble", [49] = "Obsidian",
        [53] = "Oak Stairs", [54] = "Chest", [55] = "Redstone Wire", [57] = "Diamond Block",
        [58] = "Crafting Table", [61] = "Furnace", [67] = "Stone Stairs", [77] = "Stone Button",
        [80] = "Snow", [85] = "Oak Fence", [87] = "Netherrack", [88] = "Soul Sand",
        [89] = "Glowstone", [91] = "Jack o'Lantern", [96] = "Trapdoor", [97] = "Monster Egg",
        [98] = "Stone Bricks", [103] = "Melon", [106] = "Vines", [107] = "Fence Gate",
        [108] = "Brick Stairs", [109] = "Stone Brick Stairs", [111] = "Lily Pad", [112] = "Nether Bricks",
        [114] = "Nether Brick Stairs",
        [120] = "End Portal Frame", [121] = "End Stone", [123] = "Redstone Lamp", [124] = "Redstone Lamp",
        [125] = "Wood Slab", [126] = "Wood Slab", [128] = "Sandstone Stairs", [129] = "Emerald Ore",
        [130] = "Ender Chest", [131] = "Tripwire Hook", [133] = "Emerald Block", [134] = "Spruce Stairs",
        [135] = "Birch Stairs", [136] = "Jungle Stairs", [138] = "Beacon", [139] = "Cobblestone Wall",
        [143] = "Wood Button", [144] = "Mob Head", [145] = "Anvil", [146] = "Trapped Chest",
        [148] = "Weighted Pressure Plate (Heavy)", [155] = "Quartz Block", [156] = "Quartz Stairs", [161] = "Acacia Leaves",
        [162] = "Acacia Wood", [163] = "Acacia Stairs", [164] = "Dark Oak Stairs", [165] = "Slime Block",
        [166] = "Barrier", [167] = "Iron Trapdoor", [168] = "Prismarine", [169] = "Sea Lantern",
        [170] = "Hay Bale", [172] = "Hardened Clay", [173] = "Coal Block", [174] = "Packed Ice",
        [179] = "Red Sandstone", [180] = "Red Sandstone Stairs", [181] = "Red Double Slab", [182] = "Red Slab",
        [183] = "Spruce Fence Gate", [184] = "Birch Fence Gate", [185] = "Jungle Fence Gate",
        [186] = "Dark Oak Fence Gate", [187] = "Acacia Fence Gate", [188] = "Spruce Fence",
        [189] = "Birch Fence", [190] = "Jungle Fence", [191] = "Dark Oak Fence", [192] = "Acacia Fence",
        [193] = "Spruce Door", [196] = "Acacia Door", [197] = "Dark Oak Door",
        [5] = "Oak Planks", [6] = "Oak Sapling", [23] = "Dispenser", [27] = "Powered Rail",
        [28] = "Detector Rail", [29] = "Sticky Piston", [32] = "Dead Bush", [33] = "Piston",
        [34] = "Piston Head", [37] = "Dandelion", [38] = "Poppy", [39] = "Brown Mushroom",
        [40] = "Red Mushroom", [50] = "Torch", [51] = "Fire", [52] = "Mob Spawner",
        [56] = "Diamond Ore", [59] = "Wheat", [60] = "Farmland", [62] = "Burning Furnace",
        [63] = "Sign", [64] = "Oak Door", [65] = "Ladder", [66] = "Rail",
        [68] = "Wall Sign", [69] = "Lever", [70] = "Stone Pressure Plate", [71] = "Iron Door",
        [72] = "Wood Pressure Plate", [73] = "Redstone Ore", [74] = "Glowing Redstone Ore",
        [75] = "Redstone Torch (off)", [76] = "Redstone Torch", [78] = "Snow Layer", [79] = "Ice",
        [81] = "Cactus", [82] = "Clay", [83] = "Sugar Cane", [84] = "Jukebox", [86] = "Pumpkin",
        [90] = "Nether Portal", [92] = "Cake", [93] = "Repeater (off)", [94] = "Repeater (on)",
        [99] = "Brown Mushroom Block", [100] = "Red Mushroom Block", [101] = "Iron Bars",
        [102] = "Glass Pane", [104] = "Pumpkin Stem", [105] = "Melon Stem", [110] = "Mycelium",
        [113] = "Nether Brick Fence", [115] = "Nether Wart", [116] = "Enchanting Table",
        [117] = "Brewing Stand", [118] = "Cauldron", [119] = "End Portal", [122] = "Dragon Egg",
        [127] = "Cocoa", [132] = "Tripwire", [137] = "Command Block", [140] = "Flower Pot",
        [141] = "Carrots", [142] = "Potatoes", [147] = "Weighted Pressure Plate (Light)",
        [149] = "Comparator (off)", [150] = "Comparator (on)", [151] = "Daylight Sensor",
        [152] = "Redstone Block", [153] = "Nether Quartz Ore", [154] = "Hopper",
        [157] = "Activator Rail", [158] = "Dropper", [175] = "Double Plant", [176] = "Banner",
        [177] = "Wall Banner", [178] = "Daylight Sensor (inverted)", [194] = "Birch Door",
        [195] = "Jungle Door",
    };

    private static Dictionary<(int, int), Rgb> BuildTable()
    {
        var t = new Dictionary<(int, int), Rgb>();
        void Bc(int bid, int r, int g, int b) => t[(bid, -1)] = new(r, g, b);

        Bc(0, 0, 0, 0); Bc(1, 112, 112, 112); Bc(2, 127, 178, 56); Bc(3, 151, 94, 61);
        Bc(4, 112, 112, 112); Bc(7, 80, 80, 80); Bc(8, 64, 64, 255); Bc(9, 64, 64, 255);
        Bc(10, 255, 90, 0); Bc(11, 255, 90, 0); Bc(12, 247, 214, 163); Bc(13, 150, 140, 130);
        Bc(14, 143, 119, 72); Bc(15, 112, 112, 112); Bc(16, 112, 112, 112); Bc(17, 143, 119, 72);
        Bc(18, 0, 124, 0); Bc(19, 200, 200, 80); Bc(20, 180, 210, 230); Bc(21, 51, 76, 178);
        Bc(22, 74, 144, 226); Bc(24, 230, 200, 140); Bc(25, 143, 119, 72); Bc(26, 180, 100, 100);
        Bc(30, 220, 220, 220); Bc(31, 0, 160, 0); Bc(36, 255, 0, 255); Bc(41, 250, 238, 77);
        Bc(42, 200, 200, 210); Bc(43, 120, 120, 120); Bc(44, 120, 120, 120); Bc(45, 168, 70, 55);
        Bc(46, 255, 40, 40); Bc(47, 143, 119, 72); Bc(48, 100, 130, 100); Bc(49, 35, 20, 55);
        Bc(53, 143, 119, 72); Bc(54, 143, 100, 50); Bc(55, 200, 0, 0); Bc(57, 94, 237, 255);
        Bc(58, 143, 100, 50); Bc(61, 80, 80, 80); Bc(67, 112, 112, 112); Bc(77, 112, 112, 112);
        Bc(80, 240, 240, 255); Bc(85, 143, 119, 72); Bc(87, 112, 2, 0); Bc(88, 100, 80, 50);
        Bc(89, 240, 200, 100); Bc(91, 240, 150, 20); Bc(96, 143, 119, 72); Bc(97, 112, 112, 112);
        Bc(98, 112, 112, 112); Bc(103, 100, 170, 40); Bc(106, 0, 160, 0); Bc(107, 143, 119, 72);
        Bc(108, 168, 70, 55); Bc(109, 112, 112, 112); Bc(111, 60, 130, 40); Bc(112, 45, 20, 20);
        Bc(114, 45, 20, 20);
        Bc(120, 80, 80, 40); Bc(121, 220, 220, 180); Bc(123, 100, 80, 40); Bc(124, 240, 200, 100);
        Bc(125, 143, 119, 72); Bc(126, 143, 119, 72); Bc(128, 230, 200, 140); Bc(129, 100, 200, 100);
        Bc(130, 143, 100, 50); Bc(131, 143, 119, 72); Bc(133, 0, 200, 60); Bc(134, 110, 80, 50);
        Bc(135, 143, 119, 72); Bc(136, 120, 80, 40); Bc(138, 80, 220, 240); Bc(139, 112, 112, 112);
        Bc(143, 143, 119, 72); Bc(144, 100, 100, 100); Bc(145, 200, 200, 210); Bc(146, 143, 100, 50);
        Bc(148, 200, 200, 210); Bc(155, 240, 235, 220); Bc(156, 240, 235, 220);
        // Data 0 of these two ids is acacia, so their base colour is acacia's rather than a second oak.
        Bc(161, 106, 140, 60); Bc(162, 168, 90, 50); Bc(163, 110, 80, 50); Bc(164, 110, 60, 30); Bc(165, 200, 220, 80);
        Bc(166, 255, 0, 0); Bc(167, 200, 200, 210); Bc(168, 66, 140, 120); Bc(169, 80, 220, 240);
        Bc(170, 215, 185, 35); Bc(172, 160, 90, 40); Bc(173, 25, 25, 25); Bc(174, 200, 220, 255);
        Bc(179, 230, 200, 140); Bc(180, 230, 200, 140); Bc(181, 230, 200, 140); Bc(182, 230, 200, 140);
        Bc(183, 110, 60, 30); Bc(184, 190, 160, 100); Bc(185, 120, 80, 40); Bc(186, 110, 60, 30);
        Bc(187, 143, 119, 72); Bc(188, 110, 80, 50); Bc(189, 190, 160, 100); Bc(190, 120, 80, 40);
        Bc(191, 110, 60, 30); Bc(192, 143, 119, 72); Bc(193, 190, 160, 100); Bc(196, 110, 80, 50);

        Bc(5, 143, 119, 72);    Bc(6, 0, 124, 0);      Bc(23, 112, 112, 112); Bc(27, 112, 112, 112);
        Bc(28, 112, 112, 112);  Bc(29, 112, 112, 112); Bc(32, 0, 124, 0);     Bc(33, 112, 112, 112);
        Bc(34, 112, 112, 112);  Bc(37, 0, 124, 0);     Bc(38, 0, 124, 0);     Bc(39, 0, 124, 0);
        Bc(40, 0, 124, 0);      Bc(50, 143, 119, 72);  Bc(51, 255, 40, 40);   Bc(52, 35, 40, 45);
        Bc(56, 130, 170, 180);  Bc(59, 0, 124, 0);     Bc(60, 120, 80, 50);   Bc(62, 80, 80, 80);
        Bc(63, 143, 119, 72);   Bc(64, 143, 119, 72);  Bc(65, 143, 119, 72);  Bc(66, 112, 112, 112);
        Bc(68, 143, 119, 72);   Bc(69, 112, 112, 112); Bc(70, 112, 112, 112); Bc(71, 200, 200, 210);
        Bc(72, 143, 119, 72);   Bc(73, 150, 90, 90);   Bc(74, 150, 90, 90);   Bc(75, 130, 20, 20);
        Bc(76, 200, 0, 0);      Bc(78, 240, 240, 255); Bc(79, 200, 220, 255); Bc(81, 0, 124, 0);
        Bc(82, 164, 168, 184);  Bc(83, 0, 160, 0);     Bc(84, 143, 119, 72);  Bc(86, 220, 120, 40);
        Bc(90, 80, 30, 120);    Bc(92, 230, 220, 210); Bc(93, 112, 112, 112); Bc(94, 112, 112, 112);
        Bc(99, 120, 90, 60);    Bc(100, 200, 40, 40);  Bc(101, 200, 200, 210);Bc(102, 180, 210, 230);
        Bc(104, 0, 124, 0);     Bc(105, 0, 124, 0);    Bc(110, 110, 90, 110); Bc(113, 45, 20, 20);
        Bc(115, 120, 30, 30);   Bc(116, 150, 51, 51);  Bc(117, 150, 140, 120);Bc(118, 80, 80, 80);
        Bc(119, 15, 15, 25);    Bc(122, 30, 20, 40);   Bc(127, 150, 90, 40);  Bc(132, 143, 119, 72);
        Bc(137, 170, 120, 80);  Bc(140, 150, 90, 60);  Bc(141, 0, 140, 0);    Bc(142, 0, 140, 0);
        Bc(147, 250, 238, 77);  Bc(149, 112, 112, 112);Bc(150, 112, 112, 112);Bc(151, 160, 140, 100);
        Bc(152, 175, 24, 24);   Bc(153, 130, 60, 55);  Bc(154, 80, 80, 80);   Bc(157, 112, 112, 112);
        Bc(158, 112, 112, 112); Bc(175, 0, 150, 0);    Bc(176, 143, 119, 72); Bc(177, 143, 119, 72);
        Bc(178, 160, 140, 100); Bc(194, 190, 160, 100);Bc(195, 120, 80, 40);  Bc(197, 110, 60, 30);

        // Stained blocks: damage value indexes StainColors; the "any" (-1) entry → StainColors[0].
        foreach (var bid in (int[])[35, 95, 159, 160, 171])
        {
            foreach (var (meta, rgb) in StainColors) t[(bid, meta)] = rgb;
            t[(bid, -1)] = StainColors[0];
        }

        // Variants: one block id, several distinct blocks (andesite is stone, 1:5). Their own entries, so a
        // (id, data) exact lookup finds the variant rather than falling through to the base block's colour.
        foreach (var (block, variant) in Variants) t[block] = variant.Rgb;
        return t;
    }

    /// <summary>The data value a block is looked up under. Almost every block uses its own, but a log's upper
    /// bits carry the axis it was placed along and a leaf's carry its decay flags — neither says anything about
    /// what the block <em>is</em>, and an exact lookup on the raw value misses the table and paints every birch
    /// as an oak. Stated once here so a block's name and its colour can never disagree about it.</summary>
    private static int Variant(int blockId, int blockData)
        => blockId is Blocks.Log or Blocks.Leaves or Blocks.Log2 or Blocks.Leaves2 ? blockData & 3 : blockData;

    /// <summary>(r, g, b) for a block ID + data value, with a deterministic fallback for unknowns.</summary>
    public static Rgb Color(int blockId, int blockData)
    {
        blockData = Variant(blockId, blockData);
        if (Table.TryGetValue((blockId, blockData), out var exact)) return exact;
        if (Table.TryGetValue((blockId, -1), out var anyData)) return anyData;
        // Deterministic fallback for unknown blocks. NOTE: not byte-parity with colors.py — the
        // reference uses Python's built-in hash(), which isn't portable; known blocks (the table
        // above) are exact. A distinct, stable colour per (id,data) is all the overlay needs.
        var h = (uint)(blockId * 73856093 ^ blockData * 19349663) & 0xFFFFFFu;
        return new((int)(((h >> 16) & 0xFF) % 220), (int)(((h >> 8) & 0xFF) % 220), (int)((h & 0xFF) % 220));
    }

    /// <summary>Hex "#rrggbb" for a block ID + data value.</summary>
    public static string Hex(int blockId, int blockData)
    {
        var c = Color(blockId, blockData);
        return $"#{c.R:x2}{c.G:x2}{c.B:x2}";
    }

    /// <summary>How much of its colour the block at the very back of a side view keeps. Enough darkening to
    /// read as distance, not so much that oak leaves at the back stop being green.</summary>
    private const double BackShade = 0.42;

    /// <summary>Hex for a block seen at a distance: the block's own colour, darkened towards the back of the
    /// view. <paramref name="depth"/> runs 0 (front plane) to 1 (back), so a flat projection still reads as a
    /// volume without any of it being repainted grey — the shading carries the depth and the colour stays the
    /// export's own.</summary>
    public static string Hex(int blockId, int blockData, double depth)
    {
        var color = Color(blockId, blockData);
        var keep = 1 - (1 - BackShade) * Math.Clamp(depth, 0, 1);
        return $"#{(int)(color.R * keep):x2}{(int)(color.G * keep):x2}{(int)(color.B * keep):x2}";
    }

    /// <summary>Human-readable name for a block ID + data value.</summary>
    public static string Name(int blockId, int blockData)
    {
        if (Variants.TryGetValue((blockId, Variant(blockId, blockData)), out var variant)) return variant.Name;
        if (StainBlockBaseNames.TryGetValue(blockId, out var baseName))
        {
            var color = blockData is >= 0 and < 16 ? StainColorNames[blockData % 16] : "";
            return color.Length > 0 ? $"{color} {baseName}" : baseName;
        }
        return BlockNames.GetValueOrDefault(blockId, $"Block {blockId}");
    }
}
