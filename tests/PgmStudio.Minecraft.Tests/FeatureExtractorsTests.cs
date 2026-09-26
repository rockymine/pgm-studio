using fNbt;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Exercises the extractors against a synthetic in-memory chunk (no real game files, per convention).
/// Corpus-level agreement against a scanned world is covered by the RoundTrip <c>--extract</c> harness.
/// </summary>
public class FeatureExtractorsTests
{
    // index within a 16³ section, axis order y,z,x (matches the Anvil numeric layout).
    private static int Idx(int x, int y, int z) => (y << 8) | (z << 4) | x;

    private static void SetNibble(byte[] packed, int index, int value)
    {
        var b = index >> 1;
        packed[b] = (index & 1) == 0
            ? (byte)((packed[b] & 0xF0) | (value & 0x0F))
            : (byte)((packed[b] & 0x0F) | ((value & 0x0F) << 4));
    }

    private static AnvilRegion.Chunk BuildChunk()
    {
        var blocks = new byte[4096];
        var data = new byte[2048];

        // wool (35) at (x1,y3,z2), damage 14 → red
        blocks[Idx(1, 3, 2)] = 35;
        SetNibble(data, Idx(1, 3, 2), 14);
        // iron_block (42) at (x4,y6,z5)
        blocks[Idx(4, 6, 5)] = 42;

        var section = new NbtCompound
        {
            new NbtByte("Y", 0),
            new NbtByteArray("Blocks", blocks),
            new NbtByteArray("Data", data),
        };

        var chest = new NbtCompound
        {
            new NbtString("id", "Chest"),
            new NbtInt("x", 10), new NbtInt("y", 11), new NbtInt("z", 12),
            new NbtList("Items", new[]
            {
                new NbtCompound
                {
                    new NbtByte("Slot", 0),
                    new NbtString("id", "minecraft:bow"),
                    new NbtShort("Damage", 0),
                    new NbtByte("Count", 1),
                },
            }),
        };

        var spawner = new NbtCompound
        {
            new NbtString("id", "MobSpawner"),
            new NbtInt("x", 20), new NbtInt("y", 21), new NbtInt("z", 22),
            new NbtInt("SpawnCount", 4),
            new NbtCompound("SpawnData")
            {
                new NbtCompound("Item")
                {
                    new NbtString("id", "minecraft:wool"),
                    new NbtShort("Damage", 5),
                },
            },
        };

        var level = new NbtCompound("Level")
        {
            new NbtList("Sections", new[] { section }),
            new NbtList("TileEntities", new[] { chest, spawner }),
        };
        return new AnvilRegion.Chunk(0, 0, level);
    }

    [Test]
    public async Task Wools_FindBlockWithColorFromDamage()
    {
        var w = FeatureExtractors.Wools([BuildChunk()]).Single();
        await Assert.That(w.WorldX).IsEqualTo(1);
        await Assert.That(w.WorldY).IsEqualTo(3);
        await Assert.That(w.WorldZ).IsEqualTo(2);
        await Assert.That(w.Color).IsEqualTo("red");   // damage 14
    }

    [Test]
    public async Task Resources_LabelIronBlock()
    {
        var r = FeatureExtractors.Resources([BuildChunk()]).Single();
        await Assert.That(r.ResourceType).IsEqualTo("iron_block");
        await Assert.That((r.WorldX, r.WorldY, r.WorldZ)).IsEqualTo((4, 6, 5));
    }

    [Test]
    public async Task Chests_ReadInventoryFromTileEntity()
    {
        var c = FeatureExtractors.Chests([BuildChunk()]).Single();
        await Assert.That(c.ChestType).IsEqualTo("chest");
        await Assert.That(c.ItemId).IsEqualTo("minecraft:bow");
        await Assert.That(c.Count).IsEqualTo(1);
        await Assert.That((c.WorldX, c.WorldY, c.WorldZ)).IsEqualTo((10, 11, 12));
    }

    [Test]
    public async Task Spawners_FlagWoolRespawnerFromSpawnData()
    {
        var s = FeatureExtractors.Spawners([BuildChunk()]).Single();
        await Assert.That(s.SpawnsWool).IsTrue();
        await Assert.That(s.SpawnItemId).IsEqualTo("minecraft:wool");
        await Assert.That(s.SpawnCount).IsEqualTo(4);
        await Assert.That((s.WorldX, s.WorldY, s.WorldZ)).IsEqualTo((20, 21, 22));
    }

    [Test]
    public async Task Surface_HighestBlockPerColumn()
    {
        var surf = FeatureExtractors_SurfaceByColumn();
        // wool column (1,2) surfaces at y=3; iron column (4,5) at y=6.
        await Assert.That(surf.TryGetValue((1, 2), out var w) ? w : default).IsEqualTo((3, 35));
        await Assert.That(surf.TryGetValue((4, 5), out var i) ? i : default).IsEqualTo((6, 42));
        await Assert.That(surf.Count).IsEqualTo(2);
    }

    private static Dictionary<(int, int), (int Y, int Id)> FeatureExtractors_SurfaceByColumn() =>
        SurfaceExtractors.Surface([BuildChunk()]).ToDictionary(s => (s.WorldX, s.WorldZ), s => (s.WorldY, s.BlockId));

    [Test]
    public async Task Segments_OneInclusiveRunPerSolidColumn()
    {
        var segs = FeatureExtractors.Segments([BuildChunk()]).OrderBy(s => s.WorldX).ToList();
        await Assert.That(segs.Count).IsEqualTo(2);
        // wool column (1,2): single solid cell at y=3
        await Assert.That((segs[0].WorldX, segs[0].WorldZ, segs[0].WorldYStart, segs[0].WorldYEnd)).IsEqualTo((1, 2, 3, 3));
        // iron column (4,5): single solid cell at y=6
        await Assert.That((segs[1].WorldX, segs[1].WorldZ, segs[1].WorldYStart, segs[1].WorldYEnd)).IsEqualTo((4, 5, 6, 6));
    }

    private static AnvilRegion.Chunk FloorChunk()
    {
        var blocks = new byte[4096];
        blocks[Idx(0, 0, 0)] = 36;   // block-36 marker at the world floor
        blocks[Idx(1, 0, 0)] = 95;   // stained-glass floor sheet
        blocks[Idx(2, 1, 0)] = 95;   // the sheet's second course
        blocks[Idx(3, 0, 0)] = 1;    // stone
        var section = new NbtCompound
        {
            new NbtByte("Y", 0), new NbtByteArray("Blocks", blocks), new NbtByteArray("Data", new byte[2048]),
        };
        var level = new NbtCompound("Level")
        {
            new NbtList("Sections", new[] { section }),
        };
        return new AnvilRegion.Chunk(0, 0, level);
    }

    [Test]
    public async Task A_marker_or_glass_sheet_at_the_floor_is_a_floor_mark_and_no_segment()
    {
        var marks = FeatureExtractors.FloorMarks([FloorChunk()]).Select(m => (m.WorldX, m.WorldZ, m.BlockId)).ToHashSet();
        await Assert.That(marks.SetEquals(new[] { (0, 0, 36), (1, 0, 95) })).IsTrue();

        var segs = FeatureExtractors.Segments([FloorChunk()]).Select(s => (s.WorldX, s.WorldZ, s.WorldYStart, s.WorldYEnd)).ToList();
        await Assert.That(segs).IsEquivalentTo(new[] { (3, 0, 0, 0) });
    }

    private static AnvilRegion.Chunk PassageChunk()
    {
        var blocks = new byte[4096];
        blocks[Idx(0, 0, 0)] = 9;    // water laid at the world floor
        blocks[Idx(1, 8, 0)] = 9;    // a lake's water, well above it
        for (var y = 0; y < 5; y++) { blocks[Idx(2, y, 0)] = 1; blocks[Idx(3, y, 0)] = 1; blocks[Idx(4, y, 0)] = 1; }
        blocks[Idx(2, 5, 0)] = 30;   // a cobweb over stone
        blocks[Idx(3, 5, 0)] = 64;   // a wooden door's two halves over stone
        blocks[Idx(3, 6, 0)] = 64;
        blocks[Idx(4, 5, 0)] = 107;  // a fence gate over stone
        var section = new NbtCompound
        {
            new NbtByte("Y", 0), new NbtByteArray("Blocks", blocks), new NbtByteArray("Data", new byte[2048]),
        };
        return new AnvilRegion.Chunk(0, 0, new NbtCompound("Level") { new NbtList("Sections", new[] { section }) });
    }

    [Test]
    public async Task A_cobweb_a_door_and_a_gate_are_walked_through_and_floor_water_is_a_mark()
    {
        var segs = FeatureExtractors.Segments([PassageChunk()]).Select(s => (s.WorldX, s.WorldZ, s.WorldYStart, s.WorldYEnd)).ToHashSet();
        await Assert.That(segs.SetEquals(new[] { (1, 0, 8, 8), (2, 0, 0, 4), (3, 0, 0, 4), (4, 0, 0, 4) }))
            .IsTrue().Because("each passage ends its column's run at the stone, and only the lake's water is ground");

        var marks = FeatureExtractors.FloorMarks([PassageChunk()]).Select(m => (m.WorldX, m.WorldZ, m.BlockId)).ToHashSet();
        await Assert.That(marks.SetEquals(new[] { (0, 0, 9) })).IsTrue();
    }

    [Test]
    public async Task A_door_on_solid_ground_is_a_door_run_and_a_glass_floor_over_air_is_not()
    {
        var blocks = new byte[4096];
        for (var y = 0; y < 5; y++) blocks[Idx(0, y, 0)] = 1;
        blocks[Idx(0, 5, 0)] = 160; blocks[Idx(0, 6, 0)] = 160;   // a pane doorway on stone
        blocks[Idx(1, 8, 0)] = 20;                                // a glass floor with air under it
        for (var y = 0; y < 5; y++) blocks[Idx(2, y, 0)] = 1;
        blocks[Idx(2, 5, 0)] = 113;                               // a nether brick fence on stone
        var section = new NbtCompound
        {
            new NbtByte("Y", 0), new NbtByteArray("Blocks", blocks), new NbtByteArray("Data", new byte[2048]),
        };
        var chunk = new AnvilRegion.Chunk(0, 0, new NbtCompound("Level") { new NbtList("Sections", new[] { section }) });

        var runs = FeatureExtractors.DoorRuns([chunk]).Select(r => (r.WorldX, r.WorldZ, r.WorldYStart, r.WorldYEnd)).ToHashSet();
        await Assert.That(runs.SetEquals(new[] { (0, 0, 5, 6), (2, 0, 5, 5) })).IsTrue();
    }
}
