using fNbt;

namespace PgmStudio.Minecraft;

/// <summary>
/// The defence chest a bedrock approach wall (ST4) carries: a full 27-slot supply of building material and the
/// tools to place it, embedded in each of the wall's two long faces so both teams get the same. The wall is a
/// team's line to hold, and a chest at it turns the wall from a bare slab into a place a defence is built —
/// planks and crafting tables to wall up, end stone and a redstone block to reinforce, and a pair of Efficiency
/// pickaxes to cut back through when the line moves.
///
/// <para>Each chest is <b>set into the face</b> with a single air block above it: the chest replaces the one
/// bedrock block at the approach's ground level, and the block over it is carved to air so the chest opens
/// (a solid block directly above a chest blocks its lid). The result reads as a niche in the wall rather than a
/// box sitting in front of it, and it cannot be pushed off or walled in.</para>
/// </summary>
public static class WallDefenseChest
{
    // A chest slot is loaded to a half-stack, the map's chosen ration; 27 half-stacks (plus the two pickaxes,
    // one to a slot) is exactly a full single chest.
    private const int PerSlot = 32;
    private const int EnchEfficiency = 32;   // 1.8 Efficiency enchantment id

    private const int DarkOakPlanks = 5;     // minecraft:planks damage — dark oak
    private const int SprucePlanks = 1;      // minecraft:planks damage — spruce

    /// <summary>Embed a defence chest in each long face of the wall over <c>[minX, maxX) × [minZ, maxZ)</c>,
    /// resting at the approach's own ground level (the surface just outside the face). The wall is thin across
    /// the seam and long along it; the two faces are its long sides, one per team.</summary>
    public static void Stamp(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int minX, int minZ, int maxX, int maxZ, int topY)
    {
        if (maxX <= minX || maxZ <= minZ) return;

        // The seam runs along the wall's long axis; the faces are the two ends of its short (across-seam) axis.
        if (maxX - minX <= maxZ - minZ)
        {
            var zMid = (minZ + maxZ - 1) / 2;
            Embed(world, surfaceTop, minX, zMid, dirX: -1, dirZ: 0, topY);
            Embed(world, surfaceTop, maxX - 1, zMid, dirX: +1, dirZ: 0, topY);
        }
        else
        {
            var xMid = (minX + maxX - 1) / 2;
            Embed(world, surfaceTop, xMid, minZ, dirX: 0, dirZ: -1, topY);
            Embed(world, surfaceTop, xMid, maxZ - 1, dirX: 0, dirZ: +1, topY);
        }
    }

    // Set a chest into the face column (faceX, faceZ), fronting the approach (the direction dirX/dirZ points).
    // It sits at the approach's ground Y, with the block above carved to air so the lid can open.
    private static void Embed(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int faceX, int faceZ, int dirX, int dirZ, int topY)
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
