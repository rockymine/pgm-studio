using PgmStudio.Minecraft;

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
}
