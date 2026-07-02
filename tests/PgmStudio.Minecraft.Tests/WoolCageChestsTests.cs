using fNbt;
using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Wool-cage chests: two chests in each of the four interior corners (8 total), with the A/B loadouts,
/// round-tripped through a region file so the tile entities + items actually serialise.
/// </summary>
public sealed class WoolCageChestsTests
{
    [Test]
    public async Task Places_eight_chests_with_the_A_and_B_loadouts()
    {
        var world = new VoxelWorld();
        WoolCageChests.Stamp(world, anchorX: 0, anchorZ: 0, floorY: 64);

        // Chest blocks at all four interior corners, bottom (y=65) + top (y=66).
        await Assert.That(world.GetBlock(-3, 65, -3).Id).IsEqualTo(Blocks.Chest);
        await Assert.That(world.GetBlock(2, 66, 2).Id).IsEqualTo(Blocks.Chest);

        var dir = Path.Combine(Path.GetTempPath(), "chests_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(world, dir);

            var tiles = new List<NbtCompound>();
            foreach (var mca in Directory.GetFiles(dir, "*.mca"))
                foreach (var chunk in AnvilRegion.ReadChunks(mca))
                    if (chunk.Level.Get<NbtList>("TileEntities") is { } te)
                        tiles.AddRange(te.OfType<NbtCompound>());

            var chests = tiles.Where(t => t.Get<NbtString>("id")?.Value == "Chest").ToList();
            await Assert.That(chests.Count).IsEqualTo(8);

            // Each chest is a full 27-slot loadout.
            await Assert.That(chests.All(c => c.Get<NbtList>("Items")!.Count == 27)).IsTrue();

            // A lower chest (A): planks ×16 in slot 0, a Speed potion in row 1.
            var chestA = chests.First(c => c.Get<NbtInt>("y")!.Value == 65);
            var itemsA = chestA.Get<NbtList>("Items")!.OfType<NbtCompound>().ToList();
            var slot0 = itemsA.First(i => i.Get<NbtByte>("Slot")!.Value == 0);
            await Assert.That(slot0.Get<NbtString>("id")!.Value).IsEqualTo("minecraft:planks");
            await Assert.That(slot0.Get<NbtByte>("Count")!.Value).IsEqualTo((byte)16);
            var slot9 = itemsA.First(i => i.Get<NbtByte>("Slot")!.Value == 9);
            await Assert.That(slot9.Get<NbtString>("id")!.Value).IsEqualTo("minecraft:potion");
            await Assert.That(slot9.Get<NbtShort>("Damage")!.Value).IsEqualTo((short)8194);

            // An upper chest (B): an enchanted bow with two enchantments.
            var chestB = chests.First(c => c.Get<NbtInt>("y")!.Value == 66);
            var bow = chestB.Get<NbtList>("Items")!.OfType<NbtCompound>()
                .First(i => i.Get<NbtString>("id")!.Value == "minecraft:bow");
            var ench = bow.Get<NbtCompound>("tag")!.Get<NbtList>("ench")!;
            await Assert.That(ench.Count).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
