using fNbt;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Round-trip tests: blocks written by <see cref="AnvilRegionWriter"/> must read back identically
/// through <see cref="AnvilRegion"/> — across multiple chunks, sections, regions, negative coords,
/// the data nibble, and the section-Y / height extremes.
/// </summary>
public sealed class AnvilRegionWriterTests
{
    [Test]
    public async Task Written_blocks_round_trip_through_the_reader()
    {
        var expected = new List<WorldBlock>
        {
            new(0, 0, 0, 7, 0),          // bedrock at world origin
            new(5, 64, 5, 1, 0),         // stone
            new(3, 70, 9, 35, 14),       // red wool — exercises the data nibble
            new(20, 80, 33, 95, 3),      // stained glass in a different chunk
            new(-1, 10, -1, 159, 5),     // negative coords → chunk/region -1
            new(200, 255, 200, 160, 7),  // panes, another region, max height
        };

        var world = new VoxelWorld();
        foreach (var b in expected) world.SetBlock(b.X, b.Y, b.Z, b.Id, b.Data);

        var dir = Path.Combine(Path.GetTempPath(), "anvilwriter_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(world, dir);

            var read = new List<WorldBlock>();
            foreach (var mca in Directory.GetFiles(dir, "*.mca"))
                foreach (var chunk in AnvilRegion.ReadChunks(mca))
                    read.AddRange(AnvilRegion.Blocks(chunk));

            static IOrderedEnumerable<WorldBlock> Sorted(IEnumerable<WorldBlock> bs)
                => bs.OrderBy(b => b.X).ThenBy(b => b.Y).ThenBy(b => b.Z);

            await Assert.That(read.Count).IsEqualTo(expected.Count);
            await Assert.That(Sorted(read).SequenceEqual(Sorted(expected))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Chunks_carry_the_loader_required_light_and_heightmap_tags()
    {
        // The 1.8 chunk loader aborts without a 256-int HeightMap and per-section 2048-byte BlockLight +
        // SkyLight nibble arrays; a Biomes array is expected too. This guards against silently dropping them.
        var world = new VoxelWorld();
        world.SetBlock(0, 0, 0, Blocks.Bedrock);
        world.SetBlock(0, 64, 0, Blocks.Stone);   // highest solid in column (0,0) → heightmap 65

        var dir = Path.Combine(Path.GetTempPath(), "anvillight_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(world, dir);
            var chunk = AnvilRegion.ReadChunks(Directory.GetFiles(dir, "*.mca").Single()).Single();

            var heightMap = chunk.Level.Get<NbtIntArray>("HeightMap");
            await Assert.That(heightMap!.Value.Length).IsEqualTo(256);
            await Assert.That(heightMap.Value[0]).IsEqualTo(65);                       // column (0,0), z*16+x = 0
            await Assert.That(chunk.Level.Get<NbtByteArray>("Biomes")!.Value.Length).IsEqualTo(256);
            await Assert.That(chunk.Level.Get<NbtByte>("LightPopulated")!.Value).IsEqualTo((byte)1);

            foreach (var s in chunk.Level.Get<NbtList>("Sections")!.Cast<NbtCompound>())
            {
                await Assert.That(s.Get<NbtByteArray>("BlockLight")!.Value.Length).IsEqualTo(2048);
                await Assert.That(s.Get<NbtByteArray>("SkyLight")!.Value.Length).IsEqualTo(2048);
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Get_block_returns_air_for_unset_cells_and_the_value_for_set_ones()
    {
        var world = new VoxelWorld();
        world.SetBlock(10, 20, 30, 35, 6);

        await Assert.That(world.GetBlock(10, 20, 30)).IsEqualTo((35, 6));
        await Assert.That(world.GetBlock(0, 0, 0)).IsEqualTo((0, 0));       // never set
        await Assert.That(world.GetBlock(10, 21, 30)).IsEqualTo((0, 0));    // set neighbour, this cell air
    }

    [Test]
    public void Height_out_of_range_throws()
    {
        var world = new VoxelWorld();
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetBlock(0, 256, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetBlock(0, -1, 0, 1));
    }

    [Test]
    public async Task FromWorld_reads_the_same_blocks_as_a_round_trip_through_a_region_file()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 0, 0, Blocks.Bedrock);
        world.SetBlock(5, 64, 5, 1, 0);
        world.SetBlock(3, 70, 9, 35, 14);
        world.SetBlock(-1, 10, -1, 159, 5);

        var fromWorld = AnvilRegion.FromWorld(world).SelectMany(AnvilRegion.Blocks)
            .OrderBy(b => b.X).ThenBy(b => b.Y).ThenBy(b => b.Z).ToList();

        var dir = Path.Combine(Path.GetTempPath(), "anvilfromworld_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(world, dir);
            var fromDisk = Directory.GetFiles(dir, "*.mca").SelectMany(AnvilRegion.ReadChunks)
                .SelectMany(AnvilRegion.Blocks).OrderBy(b => b.X).ThenBy(b => b.Y).ThenBy(b => b.Z).ToList();

            await Assert.That(fromWorld.SequenceEqual(fromDisk)).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public void FromWorld_does_not_disturb_a_later_file_write_of_the_same_world()
    {
        // A tile entity / entity NBT tag can only ever belong to one parent tree. FromWorld and Write both
        // build a chunk's Level compound from the same VoxelWorld.ChunkData, so calling FromWorld first —
        // the shape a generator's in-memory stage render takes, ahead of the region files it also writes —
        // must not leave the writer unable to re-parent those same tags into its own tree.
        var world = new VoxelWorld();
        world.SetBlock(0, 64, 0, Blocks.Stone);
        world.AddTileEntity(0, 0, SignBuilder.Sign(0, 65, 0, [new SignLine("hello")]));

        _ = AnvilRegion.FromWorld(world).SelectMany(AnvilRegion.Blocks).ToList();

        var dir = Path.Combine(Path.GetTempPath(), "anvilreuse_" + Guid.NewGuid().ToString("N"));
        try { AnvilRegionWriter.Write(world, dir); }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }
    /// <summary>A second build into the same directory leaves that world alone in it. A region the new
    /// geometry does not reach is still a region a server loads, so a stale file left beside the fresh ones
    /// is read back as part of the new map — and the same spec no longer rebuilds the same map
    /// (<c>B102</c>).</summary>
    [Test]
    public async Task A_rebuild_leaves_no_region_the_new_world_did_not_write()
    {
        var wide = new VoxelWorld();
        wide.SetBlock(0, 64, 0, Blocks.Stone);
        wide.SetBlock(2000, 64, 2000, Blocks.Stone);   // a second region, far enough to have its own file

        var narrow = new VoxelWorld();
        narrow.SetBlock(0, 64, 0, Blocks.Stone);

        var dir = Path.Combine(Path.GetTempPath(), "anvilrebuild_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(wide, dir);
            await Assert.That(Directory.GetFiles(dir, "*.mca").Length).IsEqualTo(2);

            AnvilRegionWriter.Write(narrow, dir);

            var left = Directory.GetFiles(dir, "*.mca").Select(Path.GetFileName).ToList();
            await Assert.That(left.Count).IsEqualTo(1)
                .Because($"the far region is gone with the geometry that made it: {string.Join(", ", left)}");
            await Assert.That(left[0]).IsEqualTo("r.0.0.mca");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }
}
