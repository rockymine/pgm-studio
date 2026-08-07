using fNbt;

namespace PgmStudio.Minecraft;

/// <summary>
/// The defence chest a bedrock approach wall (ST4) carries: a full 27-slot supply of building material and the
/// tools to place it, set into <b>one</b> face of the wall. The wall is a team's line to hold, and a chest at it
/// turns the wall from a bare slab into a place a defence is built — planks and crafting tables to wall up, end
/// stone and a redstone block to reinforce, and a pair of Efficiency pickaxes to cut back through when the line
/// moves.
///
/// <para>Only one face is touched, so the wall stays a wall. The chest replaces the one bedrock block at the
/// approach's ground level and the block over it is carved to air so the lid opens (a solid block directly above
/// a chest blocks it) — a niche, not a box in front. Crucially the column <em>behind</em> the chest is left as
/// bedrock, and so is the whole far face, so a full vertical bedrock wall still stands: break the chest and you
/// meet bedrock, not a way through. One or two chests ride the face by how wide the lane is.</para>
/// </summary>
public static class WallDefenseChest
{
    // A chest slot is loaded to a half-stack, the map's chosen ration; 27 half-stacks (plus the two pickaxes,
    // one to a slot) is exactly a full single chest.
    private const int PerSlot = 32;
    private const int EnchEfficiency = 32;   // 1.8 Efficiency enchantment id

    private const int DarkOakPlanks = 5;     // minecraft:planks damage — dark oak
    private const int SprucePlanks = 1;      // minecraft:planks damage — spruce

    /// <summary>A lane this wide or narrower carries one chest; a wider one carries two.</summary>
    public const int SingleChestMaxLane = 10;

    /// <summary>Set one or two defence chests into a single face of the wall over <c>[minX, maxX) × [minZ, maxZ)</c>,
    /// resting at the approach's own ground level. The wall is thin across the seam and long along it (the lane);
    /// the number of chests follows the lane width, and only one face is opened so a full bedrock wall stands
    /// behind them. <paramref name="onMinFace"/> is which face that is — the low-coordinate side across the seam,
    /// or the high one — and it is the map's answer to which team the supply is for. A wall thinner than two
    /// blocks across is left whole: there would be no bedrock to back a chest with.</summary>
    public static void Stamp(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int minX, int minZ, int maxX, int maxZ, bool onMinFace)
    {
        if (maxX <= minX || maxZ <= minZ) return;

        var thinAlongX = maxX - minX <= maxZ - minZ;
        var thickness = thinAlongX ? maxX - minX : maxZ - minZ;
        if (thickness < 2) return;   // one block thick — a chest anywhere would breach it, so place none

        var laneWidth = thinAlongX ? maxZ - minZ : maxX - minX;
        var count = laneWidth <= SingleChestMaxLane ? 1 : 2;

        // One face only, so the other face and the column behind each chest stay solid. A chest fronts the
        // approach it serves: outward from the wall, away from the seam.
        if (thinAlongX)
            foreach (var z in Positions(minZ, laneWidth, count))
                Embed(world, surfaceTop, onMinFace ? minX : maxX - 1, z, dirX: onMinFace ? -1 : 1, dirZ: 0);
        else
            foreach (var x in Positions(minX, laneWidth, count))
                Embed(world, surfaceTop, x, onMinFace ? minZ : maxZ - 1, dirX: 0, dirZ: onMinFace ? -1 : 1);
    }

    // Where the chests sit along the lane: evenly spaced, so one lands mid-wall and two split it into thirds.
    private static IEnumerable<int> Positions(int lo, int length, int count)
    {
        for (var i = 0; i < count; i++) yield return lo + (int)((long)(i + 1) * length / (count + 1));
    }

    // Set a chest into the face column (faceX, faceZ), fronting the approach (the direction dirX/dirZ points).
    // It sits at the approach's ground Y, with the block above carved to air so the lid can open. Only this
    // column is touched, so the column behind it (deeper into the wall) stays bedrock.
    private static void Embed(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int faceX, int faceZ, int dirX, int dirZ)
    {
        // The approach column is one block out from the face; its first air is the Y a player stands on, and the
        // height the chest sits at so it is reached from the ground rather than the middle of the wall.
        var groundTop = surfaceTop.TryGetValue((faceX + dirX, faceZ + dirZ), out var outside)
            ? outside
            : surfaceTop.GetValueOrDefault((faceX, faceZ), 1);
        if (groundTop < 1 || groundTop >= VoxelWorld.MaxHeight - 1) return;

        // A chest's facing data is the direction its front points — out towards the approach.
        var facing = dirX == -1 ? 4 : dirX == 1 ? 5 : dirZ == -1 ? 2 : 3;
        world.SetBlock(faceX, groundTop, faceZ, Blocks.Chest, facing);
        world.SetBlock(faceX, groundTop + 1, faceZ, Blocks.Air);   // the one air block that lets the lid open
        world.AddTileEntity(faceX, faceZ, ChestBuilder.Chest(faceX, groundTop, faceZ, Contents()));
    }

    /// <summary>The 27-slot loadout, assigned to slots in order: dark-oak and spruce planks and crafting tables
    /// to build with, end stone and a redstone block to reinforce, and two Efficiency II iron pickaxes.</summary>
    public static IEnumerable<(int Slot, NbtCompound Item)> Contents()
    {
        var items = new List<NbtCompound>();
        items.AddRange(ChestBuilder.Stacks(384, PerSlot, count => ChestBuilder.Item("minecraft:planks", count, DarkOakPlanks)));
        items.AddRange(ChestBuilder.Stacks(224, PerSlot, count => ChestBuilder.Item("minecraft:planks", count, SprucePlanks)));
        items.AddRange(ChestBuilder.Stacks(128, PerSlot, count => ChestBuilder.Item("minecraft:crafting_table", count)));
        items.AddRange(ChestBuilder.Stacks(32, PerSlot, count => ChestBuilder.Item("minecraft:end_stone", count)));
        items.AddRange(ChestBuilder.Stacks(32, PerSlot, count => ChestBuilder.Item("minecraft:redstone_block", count)));
        items.Add(ChestBuilder.Enchanted("minecraft:iron_pickaxe", 1, 0, (EnchEfficiency, 2)));
        items.Add(ChestBuilder.Enchanted("minecraft:iron_pickaxe", 1, 0, (EnchEfficiency, 2)));
        return items.Select((item, slot) => (slot, item));
    }
}
