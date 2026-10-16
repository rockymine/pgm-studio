using fNbt;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The defence chest a marker stands: on the ground at its own block, fronting the edge it names, with an air
/// block above so the lid opens, and a full 27-slot supply — round-tripped through a region file so the tile
/// entities and items actually serialise.
/// </summary>
public sealed class DefenseChestTests
{
    private static Dictionary<(int X, int Z), int> Flat(int minX, int minZ, int maxX, int maxZ, int top = 8)
    {
        var surface = new Dictionary<(int, int), int>();
        for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
            surface[(x, z)] = top;
        return surface;
    }

    [Test]
    public async Task A_chest_stands_on_the_ground_at_its_block_fronting_its_edge()
    {
        var world = new VoxelWorld();
        DefenseChest.Place(world, Flat(-2, -2, 3, 8), -1, 4, RoomEdge.NegX);

        await Assert.That(world.GetBlock(-1, 8, 4)).IsEqualTo((Blocks.Chest, 4));   // west-facing, at the ground
        await Assert.That(world.GetBlock(-1, 9, 4).Id).IsEqualTo(Blocks.Air);       // the one air block, to open the lid
    }

    [Test]
    public async Task The_edge_decides_which_way_the_chest_fronts()
    {
        var world = new VoxelWorld();
        DefenseChest.Place(world, Flat(-2, -2, 3, 8), 2, 4, RoomEdge.PosX);

        await Assert.That(world.GetBlock(2, 8, 4)).IsEqualTo((Blocks.Chest, 5));    // east-facing
    }

    [Test]
    public async Task A_defence_chest_carries_the_full_loadout()
    {
        var world = new VoxelWorld();
        DefenseChest.Place(world, Flat(-2, -2, 3, 8), -1, 4, RoomEdge.NegX);

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
