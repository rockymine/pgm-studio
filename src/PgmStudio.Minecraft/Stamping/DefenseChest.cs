using fNbt;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// The defence chest the map's two unbreakable structures carry: a full 27-slot supply of building material and
/// the tools to place it — planks and crafting tables to wall up, end stone and a redstone block to reinforce,
/// and a pair of Efficiency pickaxes to cut back through when the line moves.
///
/// <para>It sits at <b>two</b> places, and both are ground a team holds. A defence chest marker (ST4) stands one
/// on the ground where it is placed, beside the approach wall it serves; a goal takes one on the ground beside
/// the monument a team is defending — on the terrain, not in the bedrock plate buried under it.</para>
///
/// <para><c>Embed</c> is what both go through: the chest replaces one block and the block over it is carved to
/// air so the lid opens, since a solid block directly above a chest blocks it.</para>
/// </summary>
public static class DefenseChest
{
    // A chest slot is loaded to a half-stack, the map's chosen ration; 27 half-stacks (plus the two pickaxes,
    // one to a slot) is exactly a full single chest.
    private const int PerSlot = 32;
    private const int EnchEfficiency = 32;   // 1.8 Efficiency enchantment id

    private const int DarkOakPlanks = 5;     // minecraft:planks damage — dark oak
    private const int SprucePlanks = 1;      // minecraft:planks damage — spruce

    /// <summary>Set a defence chest on the ground at (<paramref name="x"/>, <paramref name="z"/>), fronting the
    /// edge <paramref name="facing"/> names: it rests on that column's solved surface, so it is reached from the
    /// ground however the terrain runs under it.</summary>
    public static void Place(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop, int x, int z, RoomEdge facing)
    {
        if (!surfaceTop.TryGetValue((x, z), out var groundTop)) groundTop = 1;
        Embed(world, x, groundTop, z, BlockGeometry.Fronting(facing));
    }

    /// <summary>Set one chest at <paramref name="x"/>/<paramref name="y"/>/<paramref name="z"/>, facing
    /// <paramref name="facing"/>, and carve the block over it to air so the lid can open. Two blocks and one
    /// tile entity, and nothing else — where the niche goes is the caller's, because the wall and the goal's
    /// plate open it in two different ways.</summary>
    public static void Embed(VoxelWorld world, int x, int y, int z, int facing)
    {
        if (y < 1 || y >= VoxelWorld.MaxHeight - 1) return;
        ChestBuilder.Place(world, x, y, z, facing, Contents());
        world.SetBlock(x, y + 1, z, Blocks.Air);   // the one air block that lets the lid open
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
