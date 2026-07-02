using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Terrain synthesis: bedrock floor at y=0, stone filling each column's <c>[YFloor, YTop)</c> span above
/// it, stacked segments handled independently, and the reported surface top = the tallest <c>YTop</c>.
/// </summary>
public sealed class SketchTerrainBuilderTests
{
    [Test]
    public async Task Bedrock_floor_and_stone_fill_a_ground_column()
    {
        var terrain = SketchTerrainBuilder.Build([(0, 0, 0, 4)]);   // 4-thick ground column
        var w = terrain.World;

        await Assert.That(w.GetBlock(0, 0, 0)).IsEqualTo((Blocks.Bedrock, 0));   // floor
        await Assert.That(w.GetBlock(0, 1, 0)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(w.GetBlock(0, 3, 0)).IsEqualTo((Blocks.Stone, 0));     // top solid = YTop-1
        await Assert.That(w.GetBlock(0, 4, 0)).IsEqualTo((Blocks.Air, 0));       // first air = YTop
        await Assert.That(terrain.SurfaceTop[(0, 0)]).IsEqualTo(4);
    }

    [Test]
    public async Task Floating_segment_leaves_a_void_over_the_bedrock_floor()
    {
        var terrain = SketchTerrainBuilder.Build([(5, 10, 10, 13)]);   // sky bridge, no ground
        var w = terrain.World;

        await Assert.That(w.GetBlock(5, 0, 10)).IsEqualTo((Blocks.Bedrock, 0));  // floor under footprint
        await Assert.That(w.GetBlock(5, 5, 10)).IsEqualTo((Blocks.Air, 0));      // void between
        await Assert.That(w.GetBlock(5, 10, 10)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(w.GetBlock(5, 12, 10)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(w.GetBlock(5, 13, 10)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(terrain.SurfaceTop[(5, 10)]).IsEqualTo(13);
    }

    [Test]
    public async Task Stacked_segments_on_one_cell_fill_independently_and_surface_is_the_tallest()
    {
        var terrain = SketchTerrainBuilder.Build([(2, 2, 0, 2), (2, 2, 5, 8)]);
        var w = terrain.World;

        await Assert.That(w.GetBlock(2, 0, 2)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(w.GetBlock(2, 1, 2)).IsEqualTo((Blocks.Stone, 0));   // lower segment
        await Assert.That(w.GetBlock(2, 3, 2)).IsEqualTo((Blocks.Air, 0));     // gap
        await Assert.That(w.GetBlock(2, 6, 2)).IsEqualTo((Blocks.Stone, 0));   // upper segment
        await Assert.That(terrain.SurfaceTop[(2, 2)]).IsEqualTo(8);
    }
}
