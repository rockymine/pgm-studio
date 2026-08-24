using PgmStudio.Minecraft.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

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
            TopDownColorMode.Category, TopDownSubject.Combined)!;
        var material = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null,
            TopDownColorMode.Material, TopDownSubject.Combined)!;

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
            TopDownColorMode.Material, TopDownSubject.Combined)!;

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
            TopDownColorMode.Category, TopDownSubject.Foliage)!;

        var groundPixel = PixelAt(foliageOnly.Pixels, foliageOnly.BlocksWide, 0, 0);
        var foliagePixel = PixelAt(foliageOnly.Pixels, foliageOnly.BlocksWide, 1, 0);
        var structurePixel = PixelAt(foliageOnly.Pixels, foliageOnly.BlocksWide, 2, 0);

        // The foliage column reads as the foliage highlight; the ground and structure columns — neither of
        // them foliage — read as the same flat context tone as each other, and neither equals the highlight.
        await Assert.That(foliagePixel).IsNotEqualTo(groundPixel);
        await Assert.That(groundPixel).IsEqualTo(structurePixel);
    }

    // ── the isolated foliage layer's point-and-radius mode ─────────────────────────────────────────────
    [Test]
    public async Task With_no_tree_points_the_foliage_layer_keeps_painting_the_leaf_mass()
    {
        // The documented fallback for a caller with no dressing document — a scanned world, or the disk-reading
        // overload with no `--dressing` file — is the reading every layer already had, not a blank picture.
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(1, 5, 0, Blocks.Leaves);

        var result = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null,
            TopDownColorMode.Category, TopDownSubject.Foliage, treePoints: null)!;

        await Assert.That(result.TreePointCount).IsEqualTo(0);
        // The leaf column still separates from its bare-ground neighbour, exactly as it always has — nothing
        // about the mass reading changed just because the mode exists.
        await Assert.That(PixelAt(result.Pixels, result.BlocksWide, 1, 0))
            .IsNotEqualTo(PixelAt(result.Pixels, result.BlocksWide, 0, 0));
    }

    [Test]
    public async Task Tree_points_switch_the_foliage_layer_from_mass_to_circles()
    {
        // A crown that would have painted every leaf cell (the mass) instead reads as one circle around the
        // authored anchor — so a cell the real crown never reached, but that lies inside the authored radius,
        // is filled anyway, and a real leaf cell the circle does not reach is left as context. On a flat world
        // every column shares the same relief shading, so comparing rendered pixels to each other (rather than
        // to a raw palette constant, which the shading factor would never match exactly) is what actually
        // isolates the claim.
        var world = new VoxelWorld();
        for (var x = 0; x < 8; x++)
            for (var z = 0; z < 8; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);
        world.SetBlock(1, 5, 1, Blocks.Leaves);      // a real leaf cell, outside the authored point's radius

        var points = new List<(int X, int Z, double Radius)> { (5, 5, 2) };
        var result = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null,
            TopDownColorMode.Category, TopDownSubject.Foliage, treePoints: points)!;

        await Assert.That(result.TreePointCount).IsEqualTo(1);

        var context = PixelAt(result.Pixels, result.BlocksWide, 0, 0);           // untouched by any circle
        var trunk = PixelAt(result.Pixels, result.BlocksWide, 5, 5);             // the authored anchor
        var crown = PixelAt(result.Pixels, result.BlocksWide, 6, 5);             // inside the radius, no real leaf
        var missedLeaf = PixelAt(result.Pixels, result.BlocksWide, 1, 1);        // a real leaf, outside the radius

        await Assert.That(crown).IsNotEqualTo(context);        // the authored radius is filled...
        await Assert.That(trunk).IsNotEqualTo(crown);          // ...the trunk marks itself apart from that fill...
        // ...and the real leaf three blocks off is left as context: the mass is genuinely not read here.
        await Assert.That(missedLeaf).IsEqualTo(context);
    }

    [Test]
    public async Task Every_tree_point_leaves_its_own_countable_trunk_even_where_crowns_overlap()
    {
        // The point of the mode: two crowns that would fuse into one blob in the mass reading still carry two
        // distinct trunk marks, because the trunk is drawn after every crown circle.
        var world = new VoxelWorld();
        for (var x = 0; x < 10; x++)
            for (var z = 0; z < 3; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);

        var points = new List<(int X, int Z, double Radius)> { (3, 1, 3), (6, 1, 3) };   // overlapping circles
        var result = TopDownRender.Render(AnvilRegion.FromWorld(world), map: null, yMax: null,
            TopDownColorMode.Category, TopDownSubject.Foliage, treePoints: points)!;

        var firstTrunk = PixelAt(result.Pixels, result.BlocksWide, 3, 1);
        var secondTrunk = PixelAt(result.Pixels, result.BlocksWide, 6, 1);
        var sharedCrown = PixelAt(result.Pixels, result.BlocksWide, 3, 0);   // inside the first circle only
        var context = PixelAt(result.Pixels, result.BlocksWide, 9, 2);      // inside neither circle

        // Both trunks read the same distinctive marker colour, and it is not the crown's own tint — exactly
        // what makes each one countable rather than lost inside the shared overlap.
        await Assert.That(firstTrunk).IsEqualTo(secondTrunk);
        await Assert.That(firstTrunk).IsNotEqualTo(sharedCrown);
        await Assert.That(sharedCrown).IsNotEqualTo(context);
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
