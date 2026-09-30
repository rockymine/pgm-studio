using PgmStudio.Domain;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>How a block is drawn in a picture made with its sprites.</summary>
public enum FaceForm
{
    /// <summary>A cube: the top sprite on the faces seen from above and below, the side sprite on the four
    /// others — on the boxes <see cref="BlockShape"/> names, for a block that fills less than its cell.</summary>
    Cube,

    /// <summary>Two crossed quads through the cell's centre, the way a plant or a torch is drawn.</summary>
    Cross,

    /// <summary>Not drawn at all — a sign, a rail, a door, a trapdoor, a lever, a button, a pressure plate, a
    /// repeater, iron bars: a block no shape here is named for, so the ray passes through it.</summary>
    Hidden,
}

/// <summary>Which way a block's grain runs: the axis out of whose two faces its end shows — a log's sawn
/// rings, a quartz pillar's cap.</summary>
public enum Grain
{
    Up,
    AlongX,
    AlongZ,
}

/// <summary>
/// The sprites one block shows. <paramref name="Top"/> is its end — the faces seen from above and below on a
/// block standing up, the two faces its <paramref name="Grain"/> runs out of on one lying down.
/// <paramref name="Tint"/> is a fixed multiplier for a block the game colours the same everywhere — spruce
/// and birch leaves, a lily pad, redstone wire at its power — or white for one <see cref="BlockTints"/> would otherwise tint and the game does not;
/// null leaves it to the biome. <paramref name="SideOverlay"/> is the mask a grass block's side is re-tinted
/// through. <paramref name="Front"/> is the sprite on the one side a chest looks out of, the side
/// <paramref name="Facing"/> names; null wears the side sprite all round.
/// </summary>
public readonly record struct BlockFaces(string Top, string Side, FaceForm Form, uint? Tint = null,
                                         string? SideOverlay = null, Grain Grain = Grain.Up,
                                         string? Front = null, RoomEdge Facing = RoomEdge.NegZ)
{
    private static readonly string[] Woods = ["oak", "spruce", "birch", "jungle", "acacia", "big_oak"];

    private static readonly string[] Dyes =
    [
        "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
        "silver", "cyan", "purple", "blue", "brown", "green", "red", "black",
    ];

    private static readonly string[] Flowers =
    [
        "flower_rose", "flower_blue_orchid", "flower_allium", "flower_houstonia", "flower_tulip_red",
        "flower_tulip_orange", "flower_tulip_white", "flower_tulip_pink", "flower_oxeye_daisy",
    ];

    private static readonly string[] DoublePlants = ["sunflower", "syringa", "grass", "fern", "rose", "paeonia"];

    /// <summary>What nothing is drawn for.</summary>
    public static readonly BlockFaces None = new("", "", FaceForm.Hidden);

    private const uint Untinted = 0xFFFFFF;

    private static readonly HashSet<int> Thin =
    [
        0, 26, 27, 28, 36, 51, 63, 64, 66, 68, 69, 70, 71, 72, 77, 90, 92, 93, 94, 96, 101, 117, 118, 131, 132,
        140, 143, 144, 147, 148, 149, 150, 157, 167, 176, 177, 193, 194, 195, 196, 197,
    ];

    /// <summary>The sprites <paramref name="id"/>:<paramref name="data"/> shows, or null for a block no sprite
    /// is named for — which a picture draws in its palette colour instead. A block the terrain palette offers
    /// answers the faces <see cref="BlockLook"/> measured, so a sprite name is stated once.</summary>
    public static BlockFaces? Of(int id, int data)
    {
        if (Thin.Contains(id)) return None;
        if (Plant(id, data) is { } plant) return plant;
        if (id == Blocks.Grass)
            return new BlockFaces("grass_top", "grass_side", FaceForm.Cube, SideOverlay: "grass_side_overlay");
        if (BlockLook.Top(id, data) is { } top && BlockLook.Side(id, data) is { } side)
            return Cube(top.Texture, side.Texture);
        return Other(id, data);
    }

    /// <summary>The top sprite of the plant a double plant's lower half is, for its upper half to take.</summary>
    public static BlockFaces UpperHalf(int lowerData)
    {
        var name = DoublePlants[Math.Clamp(lowerData & 7, 0, DoublePlants.Length - 1)];
        return new BlockFaces($"double_plant_{name}_top", "", FaceForm.Cross,
            Tint: name is "grass" or "fern" ? null : Untinted);
    }

    private static BlockFaces Cube(string top, string side, uint? tint = null) => new(top, side, FaceForm.Cube, tint);

    private static BlockFaces Cross(string sprite, uint? tint = Untinted) => new(sprite, "", FaceForm.Cross, tint);

    private static BlockFaces? Plant(int id, int data) => id switch
    {
        6 => Cross($"sapling_{(data & 7) switch { 1 => "spruce", 2 => "birch", 3 => "jungle", 4 => "acacia", 5 => "roofed_oak", _ => "oak" }}"),
        30 => Cross("web"),
        31 => (data & 3) switch { 1 => Cross("tallgrass", null), 2 => Cross("fern", null), _ => Cross("deadbush") },
        32 => Cross("deadbush"),
        37 => Cross("flower_dandelion"),
        38 => Cross(Flowers[Math.Clamp(data, 0, Flowers.Length - 1)]),
        39 => Cross("mushroom_brown"),
        40 => Cross("mushroom_red"),
        59 => Cross("wheat_stage_7"),
        83 => Cross("reeds", null),
        115 => Cross("nether_wart_stage_2"),
        141 => Cross("carrots_stage_3"),
        142 => Cross("potatoes_stage_3"),
        175 when (data & 8) == 0 => new BlockFaces(
            $"double_plant_{DoublePlants[Math.Clamp(data & 7, 0, DoublePlants.Length - 1)]}_bottom", "",
            FaceForm.Cross, (data & 7) is 2 or 3 ? null : Untinted),
        _ => null,
    };

    private static BlockFaces? Other(int id, int data) => id switch
    {
        7 => Cube("bedrock", "bedrock"),
        8 or 9 => Cube("water_still", "water_still"),
        10 or 11 => Cube("lava_still", "lava_still"),
        14 => Cube("gold_ore", "gold_ore"),
        21 => Cube("lapis_ore", "lapis_ore"),
        56 => Cube("diamond_ore", "diamond_ore"),
        73 or 74 => Cube("redstone_ore", "redstone_ore"),
        129 => Cube("emerald_ore", "emerald_ore"),
        153 => Cube("quartz_ore", "quartz_ore"),
        41 => Cube("gold_block", "gold_block"),
        57 => Cube("diamond_block", "diamond_block"),
        152 => Cube("redstone_block", "redstone_block"),
        17 or 162 => Log(id, data),
        18 or 161 => Leaves(id, data),
        20 or 102 => Cube("glass", "glass"),
        95 or 160 => Cube($"glass_{Dyes[data & 15]}", $"glass_{Dyes[data & 15]}"),
        35 => Cube($"wool_colored_{Dyes[data & 15]}", $"wool_colored_{Dyes[data & 15]}"),
        159 => Cube($"hardened_clay_stained_{Dyes[data & 15]}", $"hardened_clay_stained_{Dyes[data & 15]}"),
        43 or 44 => Slab(data),
        125 or 126 => Cube($"planks_{Woods[(data & 7) % Woods.Length]}", $"planks_{Woods[(data & 7) % Woods.Length]}"),
        181 or 182 => Cube("red_sandstone_top", (id, data) == (181, 8) ? "red_sandstone_top" : "red_sandstone_normal"),
        53 or 85 or 107 => Cube("planks_oak", "planks_oak"),
        134 or 183 or 188 => Cube("planks_spruce", "planks_spruce"),
        135 or 184 or 189 => Cube("planks_birch", "planks_birch"),
        136 or 185 or 190 => Cube("planks_jungle", "planks_jungle"),
        163 or 187 or 192 => Cube("planks_acacia", "planks_acacia"),
        164 or 186 or 191 => Cube("planks_big_oak", "planks_big_oak"),
        Blocks.Chest => Chest("normal", data),
        146 => Chest("trapped", data),
        130 => Chest("ender", data),
        50 => Cross("torch_on"),
        75 => Cross("redstone_torch_off"),
        Blocks.RedstoneTorch => Cross("redstone_torch_on"),
        Blocks.RedstoneWire => new BlockFaces("redstone_dust_cross", "", FaceForm.Cube, Tint: RedstonePower(data)),
        Blocks.Ladder => Cube("ladder", "ladder"),
        Blocks.Vine => Cube("vine", "vine"),
        111 => Cube("waterlily", "waterlily", LilyPadGreen),
        171 => Cube($"wool_colored_{Dyes[data & 15]}", $"wool_colored_{Dyes[data & 15]}"),
        67 => Cube("cobblestone", "cobblestone"),
        108 => Cube("brick", "brick"),
        109 => Cube("stonebrick", "stonebrick"),
        113 or 114 => Cube("nether_brick", "nether_brick"),
        128 => Cube("sandstone_top", "sandstone_normal"),
        156 => Cube("quartz_block_top", "quartz_block_side"),
        180 => Cube("red_sandstone_top", "red_sandstone_normal"),
        139 => (data & 1) == 1 ? Cube("cobblestone_mossy", "cobblestone_mossy") : Cube("cobblestone", "cobblestone"),
        24 => Cube("sandstone_top", (data & 3) switch { 1 => "sandstone_carved", 2 => "sandstone_smooth", _ => "sandstone_normal" }),
        179 => Cube("red_sandstone_top", (data & 3) switch { 1 => "red_sandstone_carved", 2 => "red_sandstone_smooth", _ => "red_sandstone_normal" }),
        155 => (data & 7) switch
        {
            1 => Cube("quartz_block_chiseled_top", "quartz_block_chiseled"),
            3 => new BlockFaces("quartz_block_lines_top", "quartz_block_lines", FaceForm.Cube, Grain: Grain.AlongX),
            4 => new BlockFaces("quartz_block_lines_top", "quartz_block_lines", FaceForm.Cube, Grain: Grain.AlongZ),
            >= 2 => Cube("quartz_block_lines_top", "quartz_block_lines"),
            _ => Cube("quartz_block_top", "quartz_block_side"),
        },
        46 => Cube("tnt_top", "tnt_side"),
        47 => Cube("planks_oak", "bookshelf"),
        52 => Cube("mob_spawner", "mob_spawner"),
        58 => Cube("crafting_table_top", "crafting_table_side"),
        61 or 62 => Cube("furnace_top", "furnace_side"),
        78 or 80 => Cube("snow", "snow"),
        81 => Cube("cactus_top", "cactus_side"),
        84 => Cube("jukebox_top", "jukebox_side"),
        86 or 91 => Cube("pumpkin_top", "pumpkin_side"),
        87 => Cube("netherrack", "netherrack"),
        89 => Cube("glowstone", "glowstone"),
        97 => (data & 7) switch
        {
            1 => Cube("cobblestone", "cobblestone"),
            2 => Cube("stonebrick", "stonebrick"),
            _ => Cube("stone", "stone"),
        },
        99 => Cube("mushroom_block_skin_brown", "mushroom_block_skin_brown"),
        100 => Cube("mushroom_block_skin_red", "mushroom_block_skin_red"),
        123 => Cube("redstone_lamp_off", "redstone_lamp_off"),
        124 => Cube("redstone_lamp_on", "redstone_lamp_on"),
        169 => Cube("sea_lantern", "sea_lantern"),
        _ => null,
    };

    /// <summary>A log by its axis bits: standing up, lying along x, lying along z, or bark on all six faces —
    /// the last is a tree's, and the only one that shows no sawn end.</summary>
    private static BlockFaces Log(int id, int data)
    {
        var wood = Woods[Math.Min((data & 3) + (id == Blocks.Log2 ? 4 : 0), Woods.Length - 1)];
        return (data & 12) switch
        {
            4 => new BlockFaces($"log_{wood}_top", $"log_{wood}", FaceForm.Cube, Grain: Grain.AlongX),
            8 => new BlockFaces($"log_{wood}_top", $"log_{wood}", FaceForm.Cube, Grain: Grain.AlongZ),
            12 => Cube($"log_{wood}", $"log_{wood}"),
            _ => Cube($"log_{wood}_top", $"log_{wood}"),
        };
    }

    /// <summary>A chest, drawn from the faces <see cref="BlockTextureSet"/> cuts out of its entity texture,
    /// its latch on the side its data says it looks toward.</summary>
    private static BlockFaces Chest(string kind, int data) =>
        new($"chest_{kind}_top", $"chest_{kind}_side", FaceForm.Cube, Tint: Untinted, Front: $"chest_{kind}_front",
            Facing: BlockGeometry.Front(data) ?? RoomEdge.NegZ);

    /// <summary>The colour the game multiplies a lily pad by, the same in every biome.</summary>
    private const uint LilyPadGreen = 0x208030;

    /// <summary>The colour the game multiplies redstone wire's greyscale sprite by at a power of
    /// <paramref name="data"/>, from a dull red at none to a bright one at fifteen, in the game's own single
    /// precision.</summary>
    public static uint RedstonePower(int data)
    {
        var power = Math.Clamp(data, 0, 15);
        var share = power / 15f;
        var red = power == 0 ? 0.3f : share * 0.6f + 0.4f;
        var green = Math.Max(0f, share * share * 0.7f - 0.5f);
        var blue = Math.Max(0f, share * share * 0.6f - 0.7f);
        return ((uint)(red * 255f) << 16) | ((uint)(green * 255f) << 8) | (uint)(blue * 255f);
    }

    private static BlockFaces Leaves(int id, int data)
    {
        var kind = Math.Min((data & 3) + (id == Blocks.Leaves2 ? 4 : 0), Woods.Length - 1);
        var sprite = $"leaves_{Woods[kind]}";
        // Spruce and birch leaves are the one pair the game colours alike in every biome.
        return kind switch
        {
            1 => Cube(sprite, sprite, 0x619961),
            2 => Cube(sprite, sprite, 0x80A755),
            _ => Cube(sprite, sprite),
        };
    }

    private static BlockFaces Slab(int data) => (data & 7) switch
    {
        1 => Cube("sandstone_top", "sandstone_normal"),
        2 => Cube("planks_oak", "planks_oak"),
        3 => Cube("cobblestone", "cobblestone"),
        4 => Cube("brick", "brick"),
        5 => Cube("stonebrick", "stonebrick"),
        6 => Cube("nether_brick", "nether_brick"),
        7 => Cube("quartz_block_top", "quartz_block_side"),
        _ => Cube("stone_slab_top", "stone_slab_side"),
    };
}
