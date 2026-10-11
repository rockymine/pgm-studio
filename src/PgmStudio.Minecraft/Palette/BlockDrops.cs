namespace PgmStudio.Minecraft.Palette;

/// <summary>
/// What breaking a block puts in a player's inventory and has no use there: the <b>clutter</b> a map's
/// <c>&lt;itemremove&gt;</c> lists so a player who breaks their way through the scenery does not walk off with it.
///
/// <para>Two things are clutter. A block that does not fill its cube (<see cref="BlockRoles.IsFullCube"/>) — a
/// flower, a button, a torch, a fence, a stair — drops itself, which builds nothing. And some blocks of either
/// shape drop an item that is not a block at all: leaves a sapling and an apple, gravel flint, tall grass seeds.
/// A full block dropping itself is building material and is never clutter, so stone, dirt, logs and planks
/// stay; a block whose drop is a full block (grass to dirt, stone to cobblestone) yields nothing here.</para>
///
/// <para>Names are PGM's material names, the ones an <c>&lt;item&gt;</c> is written with. A block that may
/// also drop itself under silk touch is listed by its ordinary drop, since no kit carries the enchantment.</para>
/// </summary>
public static class BlockDrops
{
    /// <summary>The item each non-full block drops when broken. A non-full block absent here drops nothing a
    /// player can pick up (fire, a portal, a piston head, a cake).</summary>
    private static readonly Dictionary<int, string[]> NonFullDrops = new()
    {
        [6] = ["sapling"],
        [26] = ["bed"],
        [27] = ["powered rail"], [28] = ["detector rail"], [66] = ["rails"], [157] = ["activator rail"],
        [30] = ["string", "web"],
        [31] = ["seeds", "long grass"],
        [32] = ["dead bush"],
        [37] = ["yellow flower"],
        [38] = ["red rose"],
        [39] = ["brown mushroom"],
        [40] = ["red mushroom"],
        [44] = ["step"], [126] = ["wood step"], [182] = ["stone slab2"],
        [50] = ["torch"],
        [53] = ["wood stairs"], [67] = ["cobblestone stairs"], [108] = ["brick stairs"], [109] = ["smooth stairs"],
        [114] = ["nether brick stairs"], [128] = ["sandstone stairs"], [134] = ["spruce wood stairs"],
        [135] = ["birch wood stairs"], [136] = ["jungle wood stairs"], [156] = ["quartz stairs"],
        [163] = ["acacia stairs"], [164] = ["dark oak stairs"], [180] = ["red sandstone stairs"],
        [54] = ["chest"], [146] = ["trapped chest"],
        [55] = ["redstone"],
        [59] = ["wheat", "seeds"],
        [63] = ["sign"], [68] = ["sign"],
        [64] = ["wood door"], [71] = ["iron door"], [193] = ["spruce door item"], [194] = ["birch door item"],
        [195] = ["jungle door item"], [196] = ["acacia door item"], [197] = ["dark oak door item"],
        [65] = ["ladder"],
        [69] = ["lever"],
        [70] = ["stone plate"], [72] = ["wood plate"], [147] = ["gold plate"], [148] = ["iron plate"],
        [75] = ["redstone torch on"], [76] = ["redstone torch on"],
        [77] = ["stone button"], [143] = ["wood button"],
        [78] = ["snow ball"],
        [81] = ["cactus"],
        [83] = ["sugar cane"],
        [85] = ["fence"], [113] = ["nether fence"], [188] = ["spruce fence"], [189] = ["birch fence"],
        [190] = ["jungle fence"], [191] = ["dark oak fence"], [192] = ["acacia fence"],
        [93] = ["diode"], [94] = ["diode"],
        [96] = ["trap door"], [167] = ["iron trapdoor"],
        [101] = ["iron fence"],
        [102] = ["thin glass"], [160] = ["stained glass pane"],
        [104] = ["pumpkin seeds"], [105] = ["melon seeds"],
        [106] = ["vine"],
        [107] = ["fence gate"], [183] = ["spruce fence gate"], [184] = ["birch fence gate"],
        [185] = ["jungle fence gate"], [186] = ["dark oak fence gate"], [187] = ["acacia fence gate"],
        [111] = ["water lily"],
        [115] = ["nether stalk"],
        [116] = ["enchantment table"],
        [117] = ["brewing stand item"],
        [118] = ["cauldron item"],
        [127] = ["ink sack"],
        [131] = ["tripwire hook"], [132] = ["string"],
        [139] = ["cobble wall"],
        [140] = ["flower pot item"],
        [141] = ["carrot item"], [142] = ["potato item"],
        [144] = ["skull item"],
        [145] = ["anvil"],
        [149] = ["redstone comparator"], [150] = ["redstone comparator"],
        [151] = ["daylight detector"], [178] = ["daylight detector"],
        [154] = ["hopper"],
        [171] = ["carpet"],
        [175] = ["double plant"],
        [176] = ["banner"], [177] = ["banner"],
    };

    /// <summary>The item that is not a block, dropped by a block of any shape.</summary>
    private static readonly Dictionary<int, string[]> ItemDrops = new()
    {
        [13] = ["flint"],                       // gravel
        [16] = ["coal"],                        // coal ore
        [18] = ["sapling", "apple"],            // leaves (oak drops apples)
        [161] = ["sapling"],                    // leaves2
        [21] = ["ink sack"],                    // lapis ore
        [47] = ["book"],                        // bookshelf
        [56] = ["diamond"],                     // diamond ore
        [73] = ["redstone"], [74] = ["redstone"], // redstone ore, lit and unlit
        [80] = ["snow ball"],                   // snow block
        [82] = ["clay ball"],                   // clay
        [89] = ["glowstone dust"],              // glowstone
        [99] = ["brown mushroom"], [100] = ["red mushroom"], // huge mushroom
        [103] = ["melon", "melon seeds"],       // melon block
        [129] = ["emerald"],                    // emerald ore
        [153] = ["quartz"],                     // quartz ore
    };

    /// <summary>The clutter breaking <paramref name="blockId"/> yields: its own item where it is not a full
    /// block, and any item that is not a block. Empty for a full block that drops a block.</summary>
    public static IEnumerable<string> Clutter(int blockId)
    {
        if (ItemDrops.TryGetValue(blockId, out var items))
            foreach (var item in items) yield return item;
        if (!BlockRoles.IsFullCube(blockId) && NonFullDrops.TryGetValue(blockId, out var own))
            foreach (var item in own) yield return item;
    }

    /// <summary>The clutter a world holding <paramref name="blockIds"/> can yield, each item once, in the order
    /// the block ids are given.</summary>
    public static List<string> Clutter(IEnumerable<int> blockIds) =>
        blockIds.SelectMany(Clutter).Distinct().ToList();
}
