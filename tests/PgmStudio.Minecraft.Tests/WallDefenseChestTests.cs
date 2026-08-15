using fNbt;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The bedrock wall's defence chest: one or two set into a single face (by lane width), a full 27-slot supply
/// each, with an air block above so the lid opens and bedrock left behind so the wall still stands — round-tripped
/// through a region file so the tile entities and items actually serialise.
/// </summary>
public sealed class WallDefenseChestTests
{
    // A flat surface (first air at y=8) over the wall and the ground either side of it.
    private static Dictionary<(int X, int Z), int> Flat(int minX, int minZ, int maxX, int maxZ, int top = 8)
    {
        var surface = new Dictionary<(int, int), int>();
        for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
            surface[(x, z)] = top;
        return surface;
    }

    private static int ChestCount(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int loY, int hiY)
    {
        var count = 0;
        for (var x = minX; x < maxX; x++)
        for (var z = minZ; z < maxZ; z++)
        for (var y = loY; y <= hiY; y++)
            if (world.GetBlock(x, y, z).Id == Blocks.Chest) count++;
        return count;
    }

    [Test]
    public async Task Only_one_face_is_opened_so_a_full_bedrock_wall_stands_behind_the_chest()
    {
        var world = new VoxelWorld();
        // A wall thin across X (columns x=0 min face, x=1 max face), lane 8 (→ one chest), up to y=20.
        StructureStamper.StampWall(world, 0, 0, 2, 8, 20);
        WallDefenseChest.Stamp(world, Flat(-2, -2, 3, 8), 0, 0, 2, 8, onMinFace: true);

        // Lane 8 → one chest, set into the min face (x=0) at the ground line, facing out (west).
        await Assert.That(ChestCount(world, 0, 0, 2, 8, 0, 20)).IsEqualTo(1);
        await Assert.That(world.GetBlock(0, 8, 4)).IsEqualTo((Blocks.Chest, 4));
        await Assert.That(world.GetBlock(0, 9, 4).Id).IsEqualTo(Blocks.Air);      // the one air block, to open the lid
        await Assert.That(world.GetBlock(0, 10, 4).Id).IsEqualTo(Blocks.Bedrock); // pocket closed above

        // The wall still stands: the column behind the chest is bedrock, and the far face is solid its whole
        // height — break the chest and you meet bedrock, not a way through.
        await Assert.That(world.GetBlock(1, 8, 4).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(world.GetBlock(1, 9, 4).Id).IsEqualTo(Blocks.Bedrock);
        for (var y = 0; y <= 20; y++)
            await Assert.That(world.GetBlock(1, y, 4).Id).IsEqualTo(Blocks.Bedrock);
    }

    [Test]
    public async Task The_authored_side_decides_which_face_is_opened()
    {
        // The same wall with the chest side flipped: it moves to the far column and turns to face the other
        // approach, and the column it left is bedrock again. Which team can reach the supply is the whole
        // point of the choice, so nothing about it may fall out of the footprint's coordinates.
        var world = new VoxelWorld();
        StructureStamper.StampWall(world, 0, 0, 2, 8, 20);
        WallDefenseChest.Stamp(world, Flat(-2, -2, 3, 8), 0, 0, 2, 8, onMinFace: false);

        await Assert.That(ChestCount(world, 0, 0, 2, 8, 0, 20)).IsEqualTo(1);
        await Assert.That(world.GetBlock(1, 8, 4)).IsEqualTo((Blocks.Chest, 5));   // east-facing, in the max face
        await Assert.That(world.GetBlock(1, 9, 4).Id).IsEqualTo(Blocks.Air);
        for (var y = 0; y <= 20; y++)
            await Assert.That(world.GetBlock(0, y, 4).Id).IsEqualTo(Blocks.Bedrock);
    }

    [Test]
    public async Task A_narrow_lane_gets_one_chest_a_wide_lane_two()
    {
        var narrow = new VoxelWorld();
        StructureStamper.StampWall(narrow, 0, 0, 2, WallDefenseChest.SingleChestMaxLane, 20);
        WallDefenseChest.Stamp(narrow, Flat(-2, -2, 3, WallDefenseChest.SingleChestMaxLane), 0, 0, 2, WallDefenseChest.SingleChestMaxLane, onMinFace: true);
        await Assert.That(ChestCount(narrow, 0, 0, 2, WallDefenseChest.SingleChestMaxLane, 0, 20)).IsEqualTo(1);

        var wide = new VoxelWorld();
        var wideLane = WallDefenseChest.SingleChestMaxLane + 4;
        StructureStamper.StampWall(wide, 0, 0, 2, wideLane, 20);
        WallDefenseChest.Stamp(wide, Flat(-2, -2, 3, wideLane), 0, 0, 2, wideLane, onMinFace: true);
        await Assert.That(ChestCount(wide, 0, 0, 2, wideLane, 0, 20)).IsEqualTo(2);
    }

    [Test]
    public async Task A_defence_chest_carries_the_full_loadout()
    {
        var world = new VoxelWorld();
        StructureStamper.StampWall(world, 0, 0, 2, 8, 20);
        WallDefenseChest.Stamp(world, Flat(-2, -2, 3, 8), 0, 0, 2, 8, onMinFace: true);

        var dir = Path.Combine(Path.GetTempPath(), "walldef_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(world, dir);
            var tiles = new List<NbtCompound>();
            foreach (var mca in Directory.GetFiles(dir, "*.mca"))
                foreach (var chunk in AnvilRegion.ReadChunks(mca))
                    if (chunk.Level.Get<NbtList>("TileEntities") is { } te)
                        tiles.AddRange(te.OfType<NbtCompound>());

            var chest = tiles.Single(t => t.Get<NbtString>("id")?.Value == "Chest");
            var items = chest.Get<NbtList>("Items")!.OfType<NbtCompound>().ToList();
            await Assert.That(items.Count).IsEqualTo(27);                  // a full single chest

            int Total(string id, int? damage = null) => items
                .Where(i => i.Get<NbtString>("id")!.Value == id && (damage is null || i.Get<NbtShort>("Damage")!.Value == damage))
                .Sum(i => i.Get<NbtByte>("Count")!.Value);

            await Assert.That(Total("minecraft:planks", 5)).IsEqualTo(384);    // dark oak
            await Assert.That(Total("minecraft:planks", 1)).IsEqualTo(224);    // spruce
            await Assert.That(Total("minecraft:crafting_table")).IsEqualTo(128);
            await Assert.That(Total("minecraft:end_stone")).IsEqualTo(32);
            await Assert.That(Total("minecraft:redstone_block")).IsEqualTo(32);

            // Two Efficiency II iron pickaxes.
            var picks = items.Where(i => i.Get<NbtString>("id")!.Value == "minecraft:iron_pickaxe").ToList();
            await Assert.That(picks.Count).IsEqualTo(2);
            var ench = picks[0].Get<NbtCompound>("tag")!.Get<NbtList>("ench")!.OfType<NbtCompound>().Single();
            await Assert.That(ench.Get<NbtShort>("id")!.Value).IsEqualTo((short)32);   // efficiency
            await Assert.That(ench.Get<NbtShort>("lvl")!.Value).IsEqualTo((short)2);

            await Assert.That(items.All(i => i.Get<NbtByte>("Count")!.Value <= 32)).IsTrue();   // half-stacks
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
