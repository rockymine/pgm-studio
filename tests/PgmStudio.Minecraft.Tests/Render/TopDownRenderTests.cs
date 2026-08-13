using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The stage-image top-down: a column read per block over an in-memory <see cref="VoxelWorld"/>, via
/// <see cref="AnvilRegion.FromWorld"/> — no region file involved.</summary>
public sealed class TopDownRenderTests
{
    [Test]
    public async Task Render_reads_a_flat_platform_to_the_right_extent()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 4; x++)
            for (var z = 0; z < 3; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);

        var result = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.BlocksWide).IsEqualTo(4);
        await Assert.That(result.BlocksHigh).IsEqualTo(3);
        await Assert.That(result.ColumnCount).IsEqualTo(12);
        await Assert.That(result.LowestY).IsEqualTo(5);
        await Assert.That(result.HighestY).IsEqualTo(5);
    }

    [Test]
    public async Task Render_returns_null_for_an_empty_world()
    {
        var result = TopDownRender.Render(AnvilRegion.FromWorld(new VoxelWorld()), map: null, yMax: null);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task A_taller_column_reads_as_a_step_against_its_north_neighbour()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(0, 5, 1, Blocks.Stone);
        world.SetBlock(0, 9, 1, Blocks.Stone);   // the south column stands four blocks taller

        var result = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null)!;

        await Assert.That(result.LowestY).IsEqualTo(5);
        await Assert.That(result.HighestY).IsEqualTo(9);
    }
}
