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

    private static int PixelAt(byte[] pixels, int width, int col, int row)
    {
        var offset = (row * width + col) * 3;
        return (pixels[offset] << 16) | (pixels[offset + 1] << 8) | pixels[offset + 2];
    }

    [Test]
    public async Task Default_mode_paints_stone_and_andesite_the_same_ground_colour_even_though_the_real_blocks_differ()
    {
        // The bug B98 reports: two near-identical greys (stone 1:0, andesite 1:5) are indistinguishable as
        // materials, and a legible render must not try to keep them apart — both are simply "ground".
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(1, 5, 0, 1, 5);   // andesite: id 1, data 5

        var result = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null)!;

        await Assert.That(PixelAt(result.Pixels, result.BlocksWide, 0, 0)).IsEqualTo(PixelAt(result.Pixels, result.BlocksWide, 1, 0));
    }

    [Test]
    public async Task Default_mode_disagrees_with_material_mode_for_a_block_the_category_scheme_recolours()
    {
        // Proves the new default (Category) is not merely Material under another name: on a stone column, the
        // two modes must produce different pixels, because Category discards the real per-block colour on
        // purpose.
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);

        var category = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null,
            TopDownColorMode.Category, TopDownLayer.Combined)!;
        var material = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null,
            TopDownColorMode.Material, TopDownLayer.Combined)!;

        await Assert.That(PixelAt(category.Pixels, 1, 0, 0)).IsNotEqualTo(PixelAt(material.Pixels, 1, 0, 0));
    }

    [Test]
    public async Task Material_mode_still_tells_stone_and_andesite_apart()
    {
        // The inverse of the "default mode" test above: Material mode is kept precisely so a caller checking
        // an actual paint choice can still ask for the real per-block colours.
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(1, 5, 0, 1, 5);   // andesite

        var result = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null,
            TopDownColorMode.Material, TopDownLayer.Combined)!;

        await Assert.That(PixelAt(result.Pixels, result.BlocksWide, 0, 0))
            .IsNotEqualTo(PixelAt(result.Pixels, result.BlocksWide, 1, 0));
    }

    [Test]
    public async Task Foliage_and_ground_columns_paint_different_categories_even_side_by_side()
    {
        // The exact failure a realistic render cannot avoid: a tree standing on ground its own colour is
        // close to. Leaves next to stone must separate under the category scheme.
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(1, 5, 0, Blocks.Leaves);

        var result = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null)!;

        await Assert.That(PixelAt(result.Pixels, result.BlocksWide, 0, 0))
            .IsNotEqualTo(PixelAt(result.Pixels, result.BlocksWide, 1, 0));
    }

    [Test]
    public async Task A_non_combined_layer_only_highlights_its_own_category()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);     // ground
        world.SetBlock(1, 5, 0, Blocks.Leaves);    // foliage
        world.SetBlock(2, 5, 0, 5);                // planks — a built surface

        var foliageOnly = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null,
            TopDownColorMode.Category, TopDownLayer.Foliage)!;

        var groundPixel = PixelAt(foliageOnly.Pixels, foliageOnly.BlocksWide, 0, 0);
        var foliagePixel = PixelAt(foliageOnly.Pixels, foliageOnly.BlocksWide, 1, 0);
        var structurePixel = PixelAt(foliageOnly.Pixels, foliageOnly.BlocksWide, 2, 0);

        // The foliage column reads as the foliage highlight; the ground and structure columns — neither of
        // them foliage — read as the same flat context tone as each other, and neither equals the highlight.
        await Assert.That(foliagePixel).IsNotEqualTo(groundPixel);
        await Assert.That(groundPixel).IsEqualTo(structurePixel);
    }

    [Test]
    public async Task Emit_writes_a_legend_strip_that_grows_the_image_beyond_its_block_extent()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 4; x++)
            for (var z = 0; z < 3; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);

        var outPng = Path.Combine(Path.GetTempPath(), $"topdown-legend-{Guid.NewGuid():N}.png");
        try
        {
            var exit = TopDownRender.Run(world, outPng, map: null, scale: 2, yMax: null, name: "test");
            await Assert.That(exit).IsEqualTo(0);

            var bytes = File.ReadAllBytes(outPng);
            var (width, height) = PngTestUtil.Dimensions(bytes);
            // 4x3 blocks at scale 2 is an 8x6 board; the legend strip has to make the file taller than that.
            await Assert.That(width).IsEqualTo(8);
            await Assert.That(height).IsGreaterThan(6);
        }
        finally { File.Delete(outPng); }
    }
}
