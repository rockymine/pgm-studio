using fNbt;
using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Exercises the extractors against a synthetic in-memory chunk (no real game files, per convention).
/// Corpus-level byte-exact parity vs the Python parquet oracles is covered by the RoundTrip
/// <c>--extract</c> harness (11/11 maps).
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
        LayerExtractors.Surface([BuildChunk()]).ToDictionary(s => (s.WorldX, s.WorldZ), s => (s.WorldY, s.BlockId));

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
}
