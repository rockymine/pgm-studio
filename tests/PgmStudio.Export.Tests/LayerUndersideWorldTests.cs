using PgmStudio.Geom.Relief;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export.Tests;

/// <summary>A layer's underside reaches the blocks: the rasterizer's lowered floor is what the world is filled
/// from, so the rock hangs in the built world and not only in the columns it reports.</summary>
public sealed class LayerUndersideWorldTests
{
    [Test]
    public async Task The_built_world_hangs_rock_under_a_floating_layer_down_to_its_underside()
    {
        var island = SketchLayer.Ground([new SketchShape
        {
            Id = "body", Type = "rectangle", Operation = "add", MinX = 0, MinZ = 0, MaxX = 21, MaxZ = 21, BaseHeight = 1,
        }]);
        island.Id = "island";
        island.BaseY = 40;
        island.Underside = new UndersideSpec { Taper = 1 };
        var layout = new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "none", Center = new SketchCenter() },
            Layers = [island],
        };

        var world = TerrainBuilder.Build(SketchRasterizer.RasterizeColumns(layout)).World;

        await Assert.That(world.GetBlock(0, 37, 10)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(world.GetBlock(0, 36, 10)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(world.GetBlock(10, 27, 10)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(world.GetBlock(10, 26, 10)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(world.GetBlock(10, 41, 10)).IsEqualTo((Blocks.Air, 0));
    }
}
