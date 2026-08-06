using fNbt;
using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The bedrock wall's defence chest: one set into each of the wall's two faces, a full 27-slot supply, with a
/// single air block above so the lid opens — round-tripped through a region file so the tile entities and items
/// actually serialise.
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

    [Test]
    public async Task A_chest_is_set_into_each_face_with_an_air_block_above()
    {
        var world = new VoxelWorld();
        var surface = Flat(-2, -2, 3, 12);
        // A wall thin across X (faces at x=0 and x=1), long along Z, up to y=20.
        StructureStamper.StampWall(world, 0, 0, 2, 12, 20);
        WallDefenseChest.Stamp(world, surface, 0, 0, 2, 12, 20);

        // zMid = (0 + 12 - 1) / 2 = 5. A chest in each face at the ground line, facing out.
        await Assert.That(world.GetBlock(0, 8, 5)).IsEqualTo((Blocks.Chest, 4));   // -X face → west
        await Assert.That(world.GetBlock(1, 8, 5)).IsEqualTo((Blocks.Chest, 5));   // +X face → east
        // The one carved air block above each, so the lid can open — the wall was solid bedrock there.
        await Assert.That(world.GetBlock(0, 9, 5).Id).IsEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(1, 9, 5).Id).IsEqualTo(Blocks.Air);
        // and still bedrock above the pocket — a one-block niche, not an open shaft.
        await Assert.That(world.GetBlock(0, 10, 5).Id).IsEqualTo(Blocks.Bedrock);
    }

    [Test]
    public async Task Both_chests_carry_the_full_defence_loadout()
    {
        var world = new VoxelWorld();
        StructureStamper.StampWall(world, 0, 0, 2, 12, 20);
        WallDefenseChest.Stamp(world, Flat(-2, -2, 3, 12), 0, 0, 2, 12, 20);

        var dir = Path.Combine(Path.GetTempPath(), "walldef_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(world, dir);
            var tiles = new List<NbtCompound>();
            foreach (var mca in Directory.GetFiles(dir, "*.mca"))
                foreach (var chunk in AnvilRegion.ReadChunks(mca))
                    if (chunk.Level.Get<NbtList>("TileEntities") is { } te)
                        tiles.AddRange(te.OfType<NbtCompound>());

            var chests = tiles.Where(t => t.Get<NbtString>("id")?.Value == "Chest").ToList();
            await Assert.That(chests.Count).IsEqualTo(2);                                    // one per face
            await Assert.That(chests.All(c => c.Get<NbtList>("Items")!.Count == 27)).IsTrue();  // a full chest

            var items = chests[0].Get<NbtList>("Items")!.OfType<NbtCompound>().ToList();
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

            // Every stackable slot is a half-stack (32); no slot exceeds it.
            await Assert.That(items.All(i => i.Get<NbtByte>("Count")!.Value <= 32)).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
