using PgmStudio.Minecraft.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The ground/heightmap and relief/contour stage images — one renderer, read with and without
/// contour lines.</summary>
public sealed class HeightProfileRenderTests
{
    private static VoxelWorld Steps()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 6; x++)
            for (var z = 0; z < 4; z++)
                world.SetBlock(x, 5 + x, z, Blocks.Stone);   // a six-block staircase, one column at a time
        return world;
    }

    [Test]
    public async Task Render_reports_the_ground_span_ignoring_a_tree_stood_on_it()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(0, 6, 0, Blocks.Log);
        world.SetBlock(0, 7, 0, Blocks.Log);
        world.SetBlock(0, 8, 0, Blocks.Leaves);

        var result = HeightProfileRender.Render(AnvilRegion.FromWorld(world), contourInterval: 0,
            greyscale: false, markWater: false);

        // Ground is the stone the tree stands on, not the top of its canopy.
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.LowestY).IsEqualTo(5);
        await Assert.That(result.HighestY).IsEqualTo(5);
    }

    [Test]
    public async Task Contours_change_the_pixels_a_bare_ramp_draws()
    {
        var withoutContours = HeightProfileRender.Render(AnvilRegion.FromWorld(Steps()), contourInterval: 2,
            greyscale: true, markWater: false, drawContours: false)!;
        var withContours = HeightProfileRender.Render(AnvilRegion.FromWorld(Steps()), contourInterval: 2,
            greyscale: true, markWater: false, drawContours: true)!;

        await Assert.That(withoutContours.Pixels.SequenceEqual(withContours.Pixels)).IsFalse();
    }

    [Test]
    public async Task A_flooded_column_is_reported_separately_from_dry_ground()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(0, 6, 0, Blocks.Water);
        world.SetBlock(1, 5, 0, Blocks.Stone);   // dry neighbour

        var result = HeightProfileRender.Render(AnvilRegion.FromWorld(world), contourInterval: 0,
            greyscale: false, markWater: true)!;

        await Assert.That(result.Flooded.Count).IsEqualTo(1);
        await Assert.That(result.Flooded.ContainsKey((0, 0))).IsTrue();
    }

    [Test]
    public async Task Run_appends_a_scale_legend_that_grows_the_written_png()
    {
        var outPng = Path.Combine(Path.GetTempPath(), $"heightmap-legend-{Guid.NewGuid():N}.png");
        try
        {
            var exit = HeightProfileRender.Run(Steps(), outPng, scale: 2, contourInterval: 2,
                greyscale: false, markWater: false, drawContours: true, name: "test");
            await Assert.That(exit).IsEqualTo(0);

            var (width, height) = PngTestUtil.Dimensions(File.ReadAllBytes(outPng));
            await Assert.That(width).IsEqualTo(12);        // 6 columns wide at scale 2
            await Assert.That(height).IsGreaterThan(8);    // 4 rows at scale 2, plus the legend strip
        }
        finally { File.Delete(outPng); }
    }
}
