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
}
