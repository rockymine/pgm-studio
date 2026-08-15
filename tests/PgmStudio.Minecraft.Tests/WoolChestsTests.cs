using fNbt;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Wool-cage chests: two chests in each of the four interior corners (8 total), with the A/B loadouts,
/// round-tripped through a region file so the tile entities + items actually serialise.
/// </summary>
public sealed class WoolChestsTests
{
    [Test]
    public async Task Places_eight_chests_with_the_A_and_B_loadouts()
    {
        var world = new VoxelWorld();
        var frame = RoomFrames.Resolve(-5, -5, 5, 5, 0, 0, [(-5, -5, 5, -5)], null, out _)!;
        WoolChests.Stamp(world, frame, floorY: 64);

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

    [Test]
    public async Task A_corner_chest_opens_away_from_both_walls_it_touches()
    {
        // Door on the NegZ (north) wall — the entry axis is Z, so every corner chest faces along Z, away
        // from whichever of its two walls sits on that axis. The two corners nearest the door (low Z) face
        // south (data 3, PosZ); the two nearest the back wall (high Z) face north (data 2, NegZ). Neither
        // value ever points at the wall the corner actually sits against.
        var world = new VoxelWorld();
        var frame = RoomFrames.Resolve(-5, -5, 5, 5, 0, 0, [(-5, -5, 5, -5)], null, out _)!;
        WoolChests.Stamp(world, frame, floorY: 64);

        await Assert.That(world.GetBlock(-3, 65, -3).Data).IsEqualTo(3);   // near-door corner (west) → south
        await Assert.That(world.GetBlock(2, 65, -3).Data).IsEqualTo(3);    // near-door corner (east) → south
        await Assert.That(world.GetBlock(-3, 65, 2).Data).IsEqualTo(2);    // back-wall corner (west) → north
        await Assert.That(world.GetBlock(2, 65, 2).Data).IsEqualTo(2);     // back-wall corner (east) → north
    }

    [Test]
    public async Task A_door_on_a_side_wall_turns_every_chest_to_face_along_X_instead()
    {
        // Door on the NegX (west) wall: the entry axis is X, so facing follows X instead of Z — the two
        // corners nearest the door face east (data 5, PosX), the two nearest the far wall face west (data 4).
        var world = new VoxelWorld();
        var frame = RoomFrames.Resolve(-5, -5, 5, 5, 0, 0, [(-5, -5, -5, 5)], null, out _)!;
        WoolChests.Stamp(world, frame, floorY: 64);

        await Assert.That(world.GetBlock(-3, 65, -3).Data).IsEqualTo(5);   // near-door corner (north) → east
        await Assert.That(world.GetBlock(-3, 65, 2).Data).IsEqualTo(5);    // near-door corner (south) → east
        await Assert.That(world.GetBlock(2, 65, -3).Data).IsEqualTo(4);    // far-wall corner (north) → west
        await Assert.That(world.GetBlock(2, 65, 2).Data).IsEqualTo(4);     // far-wall corner (south) → west
    }
}
