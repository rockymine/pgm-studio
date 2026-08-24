using PgmStudio.Geom;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Terrain synthesis: bedrock floor at y=0, stone filling each column's <c>[YFloor, YTop)</c> span above
/// it, stacked segments handled independently, and the reported surface top = the tallest <c>YTop</c>.
/// </summary>
public sealed class TerrainBuilderTests
{
    [Test]
    public async Task Bedrock_floor_and_stone_fill_a_ground_column()
    {
        var terrain = TerrainBuilder.Build([Seg(0, 0, 0, 4)]);   // 4-thick ground column
        var w = terrain.World;

        await Assert.That(w.GetBlock(0, 0, 0)).IsEqualTo((Blocks.Bedrock, 0));   // floor
        await Assert.That(w.GetBlock(0, 1, 0)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(w.GetBlock(0, 3, 0)).IsEqualTo((Blocks.Stone, 0));     // top solid = YTop-1
        await Assert.That(w.GetBlock(0, 4, 0)).IsEqualTo((Blocks.Air, 0));       // first air = YTop
        await Assert.That(terrain.SurfaceTop[(0, 0)]).IsEqualTo(4);
    }

    [Test]
    public async Task Floating_segment_stands_over_void_all_the_way_down()
    {
        var terrain = TerrainBuilder.Build([Seg(5, 10, 10, 13)]);   // sky bridge, no ground
        var w = terrain.World;

        await Assert.That(w.GetBlock(5, 0, 10)).IsEqualTo((Blocks.Air, 0));      // nothing stands on the floor
        await Assert.That(w.GetBlock(5, 5, 10)).IsEqualTo((Blocks.Air, 0));      // void between
        await Assert.That(w.GetBlock(5, 10, 10)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(w.GetBlock(5, 12, 10)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(w.GetBlock(5, 13, 10)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(terrain.SurfaceTop[(5, 10)]).IsEqualTo(13);
    }

    [Test]
    public async Task Stacked_segments_on_one_cell_fill_independently_and_surface_is_the_tallest()
    {
        var terrain = TerrainBuilder.Build([Seg(2, 2, 0, 2), Seg(2, 2, 5, 8)]);
        var w = terrain.World;

        await Assert.That(w.GetBlock(2, 0, 2)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(w.GetBlock(2, 1, 2)).IsEqualTo((Blocks.Stone, 0));   // lower segment
        await Assert.That(w.GetBlock(2, 3, 2)).IsEqualTo((Blocks.Air, 0));     // gap
        await Assert.That(w.GetBlock(2, 6, 2)).IsEqualTo((Blocks.Stone, 0));   // upper segment
        await Assert.That(terrain.SurfaceTop[(2, 2)]).IsEqualTo(8);
    }

    /// <summary>A ground-layer segment, for a test whose subject is the fill rather than the stack.</summary>
    private static ColumnSegment Seg(int x, int z, int floor, int top) => new(x, z, floor, top, "ground");

    /// <summary>One board, both cases: the floor goes under ground that rests on it and not under the slab
    /// standing over the void beside it. Plating the slab would put a floor under the fall off a bridge and
    /// add the column to the Y0 set a void filter reads.</summary>
    [Test]
    public async Task The_floor_goes_under_ground_and_not_under_a_slab_beside_it()
    {
        var terrain = TerrainBuilder.Build([Seg(0, 0, 0, 4), Seg(9, 9, 14, 18)]);
        var world = terrain.World;

        await Assert.That(world.GetBlock(0, 0, 0).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(world.GetBlock(9, 0, 9).Id).IsEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(9, 14, 9).Id).IsEqualTo(Blocks.Stone);    // and still stands
    }
}
