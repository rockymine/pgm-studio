using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The paint/surface stage image: ground material read once decoration, liquid and built structure
/// are set aside.</summary>
public sealed class SurfaceReportTests
{
    [Test]
    public async Task Render_reads_the_right_extent_over_mixed_ground_and_structure()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 3; x++)
            for (var z = 0; z < 2; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);
        world.SetBlock(0, 6, 0, 5);   // a plank on top of one column — a built surface, not ground

        var result = SurfaceReport.Render(AnvilRegion.FromWorld(world), topMaterials: 8);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.BlocksWide).IsEqualTo(3);
        await Assert.That(result.BlocksHigh).IsEqualTo(2);
        // The planked column is built, so the ground-only material map holds the other five.
        await Assert.That(result.Ground.Count).IsEqualTo(5);
        await Assert.That(result.Ground.ContainsKey((0, 0))).IsFalse();
    }

    [Test]
    public async Task A_full_cube_no_family_names_is_counted_rather_than_silently_painted_magenta()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 2; x++)
            for (var z = 0; z < 2; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);   // named by the "grey stone" family
        world.SetBlock(0, 5, 0, Blocks.Bedrock);         // a full cube no family claims, by design

        var result = SurfaceReport.Render(AnvilRegion.FromWorld(world), topMaterials: 8);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Unnamed.Count).IsEqualTo(1);
        await Assert.That(result.Unnamed[0].Name).IsEqualTo("Bedrock");
        await Assert.That(result.Unnamed[0].Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_board_entirely_within_the_vocabulary_reports_no_unnamed_blocks()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 2; x++)
            for (var z = 0; z < 2; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);

        var result = SurfaceReport.Render(AnvilRegion.FromWorld(world), topMaterials: 8);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Unnamed).IsEmpty();
    }

    [Test]
    public async Task Run_appends_a_scale_legend_that_grows_the_written_png()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 3; x++)
            for (var z = 0; z < 2; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);

        var outPng = Path.Combine(Path.GetTempPath(), $"surface-legend-{Guid.NewGuid():N}.png");
        try
        {
            var exit = SurfaceReport.Run(world, outPng, scale: 2, topMaterials: 8);
            await Assert.That(exit).IsEqualTo(0);

            var (width, height) = PngTestUtil.Dimensions(File.ReadAllBytes(outPng));
            await Assert.That(width).IsEqualTo(6);      // 3 columns wide at scale 2
            await Assert.That(height).IsGreaterThan(4); // 2 rows at scale 2, plus the legend strip
        }
        finally { File.Delete(outPng); }
    }
}
