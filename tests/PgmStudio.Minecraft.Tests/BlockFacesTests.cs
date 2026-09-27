using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Which sprites a block shows. The invariant worth holding is coverage: every block a terrain theme may be
/// painted with names a sprite for both of its faces, so a finish is never drawn in a palette colour by
/// accident. The rest pin the few blocks the game draws differently from their neighbours.
/// </summary>
public sealed class BlockFacesTests
{
    [Test]
    public async Task Every_block_a_theme_may_paint_names_a_sprite_for_both_faces()
    {
        foreach (var block in TerrainPalette.Paintable)
        {
            var faces = BlockFaces.Of(block.Id, block.Data);
            await Assert.That(faces).IsNotNull();
            await Assert.That(faces!.Value.Form).IsEqualTo(FaceForm.Cube);
            await Assert.That(faces.Value.Top).IsNotEmpty();
            await Assert.That(faces.Value.Side).IsNotEmpty();
        }
    }

    [Test]
    public async Task What_is_too_thin_to_read_the_ground_by_is_not_drawn()
    {
        foreach (var id in (int[])[0, 50, 63, 66, 171])
            await Assert.That(BlockFaces.Of(id, 0)!.Value.Form).IsEqualTo(FaceForm.Hidden);
    }

    [Test]
    public async Task Spruce_and_birch_leaves_keep_their_own_colour_and_oak_leaves_take_the_biome()
    {
        await Assert.That(BlockFaces.Of(18, 1)!.Value.Tint).IsEqualTo(0x619961u);
        await Assert.That(BlockFaces.Of(18, 2)!.Value.Tint).IsEqualTo(0x80A755u);
        await Assert.That(BlockFaces.Of(18, 0)!.Value.Tint).IsNull();
    }

    [Test]
    public async Task A_plant_is_crossed_quads_and_a_double_plant_top_is_the_plant_below_it()
    {
        await Assert.That(BlockFaces.Of(31, 1)!.Value.Form).IsEqualTo(FaceForm.Cross);
        await Assert.That(BlockFaces.UpperHalf(2).Top).IsEqualTo("double_plant_grass_top");
        await Assert.That(BlockFaces.UpperHalf(2).Tint).IsNull();
        await Assert.That(BlockFaces.UpperHalf(4).Top).IsEqualTo("double_plant_rose_top");
        await Assert.That(BlockFaces.UpperHalf(4).Tint).IsEqualTo(0xFFFFFFu);
    }

    [Test]
    public async Task An_upright_log_shows_its_rings_on_top_and_a_felled_one_its_bark()
    {
        await Assert.That(BlockFaces.Of(17, 0)!.Value.Top).IsEqualTo("log_oak_top");
        await Assert.That(BlockFaces.Of(17, 4)!.Value.Top).IsEqualTo("log_oak");
        await Assert.That(BlockFaces.Of(162, 1)!.Value.Side).IsEqualTo("log_big_oak");
    }

    [Test]
    public async Task A_grass_block_side_is_re_tinted_through_its_overlay()
    {
        var grass = BlockFaces.Of(2, 0)!.Value;
        await Assert.That(grass.Side).IsEqualTo("grass_side");
        await Assert.That(grass.SideOverlay).IsEqualTo("grass_side_overlay");
    }
}
