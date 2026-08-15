namespace PgmStudio.Minecraft;

/// <summary>What a block is doing in a world, as against what it is made of.</summary>
public enum BlockRole
{
    /// <summary>Nothing.</summary>
    Air,
    /// <summary>Water or lava, still or flowing.</summary>
    Liquid,
    /// <summary>The wood and leaves of something standing over the ground.</summary>
    Tree,
    /// <summary>What grows on the ground: flowers, grass, crops, the things with roots.</summary>
    Flora,
    /// <summary>What an author stands on the ground: a fence, a torch, a rail, a head, a carpet.</summary>
    Decoration,
    /// <summary>Everything else — a block that ground or a build is made of. Which of the two it is belongs to
    /// the ground vocabulary and to each pass's own material list, not here.</summary>
    Material,
}

/// <summary>
/// One classification of every block by its role, shared by every pass that has to tell the ground from what is
/// standing on it.
///
/// <para>It exists because the question was previously answered seven times. Each render carried its own
/// "what must I step past" list, written as a negative and local to that one render, and seven lists written
/// that way agreed wherever a map had tested them and diverged wherever none had. The divergences were not
/// theoretical: the wooden fences were a build material to one pass and absent from four others, the rails were
/// in five lists, two, or four depending on which rail, and fifteen blocks — mob heads, anvils, daylight
/// sensors, spawners, banners, flower pots, cactus — were in no list at all and so were read as terrain by
/// every pass that met them.</para>
///
/// <para>The fix is to state what a block <em>is</em> once, positively, and let each pass compose the roles it
/// needs. A surface scan steps past <see cref="StandsOnGround"/>; a height profile wants that plus air and
/// liquid; a roof finder wants that plus the glass it must see through. None of them owns the answer.</para>
///
/// <para><b>Roles are about placement, not about shape or material.</b> A carpet is decoration though it is
/// dyed wool, because an author lays it on finished ground; a snow layer is decoration for the same reason,
/// even on a map made of snow. A log is a tree rather than a build material because that is what it is doing
/// nine times in ten, and the one pass that treats a log as a post says so itself. Full-cube furniture — a
/// furnace, a crafting table, a bookshelf, a jukebox — stays <see cref="BlockRole.Material"/>: it is built into
/// a wall as often as it is stood on a floor, and the ground vocabulary already names full cubes.</para>
/// </summary>
public static class BlockRoles
{
    /// <summary>Water and lava, still and flowing.</summary>
    private static readonly HashSet<int> Liquids = [8, 9, 10, 11];

    /// <summary>Logs and leaves, in both of their id pairs — the two families, not a third list of the same
    /// four ids (<see cref="BlockFamilies"/>).</summary>
    private static readonly HashSet<int> Trees = [.. BlockFamilies.Logs, .. BlockFamilies.Leaves];

    /// <summary>What grows. A huge mushroom's cap and stem are not here: they are full cubes an author builds
    /// with, and the ground vocabulary already names the stem as pale stone.</summary>
    private static readonly HashSet<int> Growing =
    [
        6,      // sapling
        31,     // tall grass, fern, dead shrub
        32,     // dead bush
        37,     // dandelion
        38,     // poppy, orchid, allium, tulips, daisy
        39,     // brown mushroom
        40,     // red mushroom
        59,     // wheat
        81,     // cactus
        83,     // sugar cane
        104,    // pumpkin stem
        105,    // melon stem
        106,    // vines
        111,    // lily pad
        115,    // nether wart
        127,    // cocoa
        141,    // carrots
        142,    // potatoes
        175,    // double plant — sunflower, lilac, tall grass, large fern, rose bush, peony
    ];

    /// <summary>What is placed rather than grown, and is not what the ground is made of.</summary>
    private static readonly HashSet<int> Placed =
    [
        23, 158,                    // dispenser, dropper
        26,                         // bed
        27, 28, 66, 157,            // rails: powered, detector, plain, activator
        29, 33, 34, 36,             // pistons and their heads
        30,                         // cobweb
        50, 75, 76,                 // torch, redstone torch unlit and lit
        51,                         // fire
        52,                         // mob spawner
        54, 130, 146,               // chest, ender chest, trapped chest
        55,                         // redstone wire
        63, 68,                     // sign, wall sign
        64, 71, 193, 194, 195, 196, 197,   // doors: oak, iron, spruce, birch, jungle, acacia, dark oak
        65,                         // ladder
        69,                         // lever
        70, 72, 147, 148,           // pressure plates: stone, wood, gold, iron
        77, 143,                    // buttons: stone, wood
        78,                         // snow layer
        85, 113, 188, 189, 190, 191, 192,  // fences: oak, nether brick, spruce, birch, jungle, dark oak, acacia
        92,                         // cake
        93, 94,                     // repeater, unpowered and powered
        96, 167,                    // trapdoors: wood, iron
        101,                        // iron bars
        107, 183, 184, 185, 186, 187,      // fence gates: oak, spruce, birch, jungle, dark oak, acacia
        116,                        // enchantment table
        117,                        // brewing stand
        118,                        // cauldron
        119, 120,                   // end portal and its frame
        122,                        // dragon egg
        131, 132,                   // tripwire hook, tripwire
        137,                        // command block
        138,                        // beacon
        139,                        // cobblestone wall
        140,                        // flower pot
        144,                        // mob head
        145,                        // anvil
        149, 150,                   // comparator, unpowered and powered
        151, 178,                   // daylight sensor and its inverted form
        154,                        // hopper
        171,                        // carpet
        176, 177,                   // banner, wall banner
    ];

    /// <summary>The materials that do not fill their cube: the three families that are thinner than their
    /// block, composed rather than relisted (<see cref="BlockFamilies"/>). Only the single slabs are in it —
    /// a double slab is a whole block and keeps its own id, which is why <see cref="BlockFamilies.DoubleSlabs"/>
    /// is not one of the three.</summary>
    private static readonly HashSet<int> PartialMaterials =
        [.. BlockFamilies.SingleSlabs, .. BlockFamilies.Stairs, .. BlockFamilies.Panes];

    /// <summary>The placed blocks that do fill their cube after all. Most decoration is thinner than its block —
    /// a fence, a torch, a carpet — but a dropper is a solid box with a face on it, and a pass asking about
    /// shape has to be told the difference from a pass asking about placement.</summary>
    private static readonly HashSet<int> FullDecorations = [23, 29, 33, 52, 137, 138, 158];

    /// <summary>Whether the block fills its cube. This is <b>shape, not role</b>: the two are separate questions
    /// and a block answers them independently — a dropper is decoration and a full cube, a stone slab is a
    /// material and is not. It is what the ground vocabulary means by naming only full blocks, and what a pass
    /// means when it reports a partial block on the surface as something standing on the ground rather than as
    /// the ground.
    ///
    /// <para>Leaves and logs fill their cube, so a tree is full; nothing that grows does, since even a cactus
    /// stands a sixteenth short on every side.</para></summary>
    public static bool IsFullCube(int blockId) => Of(blockId) switch
    {
        BlockRole.Air or BlockRole.Liquid or BlockRole.Flora => false,
        BlockRole.Tree => true,
        BlockRole.Decoration => FullDecorations.Contains(blockId),
        _ => !PartialMaterials.Contains(blockId),
    };

    /// <summary>The role a block plays.</summary>
    public static BlockRole Of(int blockId) =>
        blockId == 0 ? BlockRole.Air
        : Liquids.Contains(blockId) ? BlockRole.Liquid
        : Trees.Contains(blockId) ? BlockRole.Tree
        : Growing.Contains(blockId) ? BlockRole.Flora
        : Placed.Contains(blockId) ? BlockRole.Decoration
        : BlockRole.Material;

    public static bool IsLiquid(int blockId) => Liquids.Contains(blockId);
    public static bool IsTree(int blockId) => Trees.Contains(blockId);

    public static bool IsFlora(int blockId) => Growing.Contains(blockId);
    public static bool IsDecoration(int blockId) => Placed.Contains(blockId);

    /// <summary>Whether the block stands on the ground rather than being it — a tree, something grown, or
    /// something placed. The composite five of the seven old lists were reaching for; air and liquid are asked
    /// separately, because a pass that steps past a fern does not always step past water.</summary>
    public static bool StandsOnGround(int blockId) =>
        Trees.Contains(blockId) || Growing.Contains(blockId) || Placed.Contains(blockId);

    /// <summary>Whether a pass reading the <em>shape of a build</em> may look through the block: everything
    /// standing on the ground except a log. A log is the exception because it is structural as often as it is
    /// scenery — the corner posts of a house are logs, and a roof finder that looks through them loses the
    /// measure that tells a framed house from a shed. A pass reading the ground itself wants
    /// <see cref="StandsOnGround"/> instead, where a trunk is part of the tree it belongs to.</summary>
    public static bool SeenThrough(int blockId) =>
        BlockFamilies.IsLeaf(blockId) || Growing.Contains(blockId) || Placed.Contains(blockId);

    /// <summary>Blocks a world does not put on its own surface — the material signature of something built.
    /// Deliberately excludes stone, cobblestone, andesite, gravel, sand and clay: all of those generate
    /// naturally in the open, so including them would classify every outcrop and river bank as architecture.
    ///
    /// <para>Material rather than height is what separates a building from the ground, because elevation
    /// alone cannot: a hut and the boulder beside it are both flat-topped lumps standing over their
    /// surroundings, and a long hall on level ground has no elevation signature at all.</para></summary>
    private static readonly HashSet<int> BuiltSurfaces =
    [
        5, 20, 24, 35, 43, 44, 45, 47, 53, 54, 57, 58, 61, 62, 64, 65, 67, 71, 80, 82, 84, 87, 89, 91, 95,
        96, 98, 101, 102, 108, 109, 112, 114, 116, 117, 118, 120, 125, 126, 128, 133, 134, 135, 136, 138,
        145, 146, 152, 155, 158, 159, 160, 163, 164, 165, 168, 169, 170, 171, 172, 173, 179, 180, 181, 182,
        188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 201, 202, 206, 208, 214, 215, 216,
    ];

    public static bool IsBuilt(int blockId) => BuiltSurfaces.Contains(blockId);

    /// <summary>Whether the block is the terrain itself: not air, not something standing on the ground, not
    /// something built, not a liquid, and not a log — so a pass reading the natural surface steps past a
    /// building's own courses and the water over them to reach the ground underneath.</summary>
    public static bool IsNaturalGround(int blockId) =>
        blockId != 0 && !SeenThrough(blockId) && !IsBuilt(blockId) && !IsLiquid(blockId) && !BlockFamilies.IsLog(blockId);
}
