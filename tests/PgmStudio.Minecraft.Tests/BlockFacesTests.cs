using PgmStudio.Domain;
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
    public async Task A_log_shows_its_sawn_end_on_the_axis_it_lies_along_and_a_trees_log_shows_none()
    {
        var upright = BlockFaces.Of(17, 0)!.Value;
        var alongX = BlockFaces.Of(17, 4)!.Value;
        var alongZ = BlockFaces.Of(17, 9)!.Value;
        var barkAllRound = BlockFaces.Of(17, 12)!.Value;

        await Assert.That((upright.Grain, upright.Top)).IsEqualTo((Grain.Up, "log_oak_top"));
        await Assert.That((alongX.Grain, alongX.Top)).IsEqualTo((Grain.AlongX, "log_oak_top"));
        await Assert.That((alongZ.Grain, alongZ.Top)).IsEqualTo((Grain.AlongZ, "log_spruce_top"));
        await Assert.That(barkAllRound.Top).IsEqualTo("log_oak");
        await Assert.That(BlockFaces.Of(162, 1)!.Value.Side).IsEqualTo("log_big_oak");
    }

    [Test]
    public async Task A_hay_bale_shows_its_cut_end_on_the_axis_it_lies_along()
    {
        await Assert.That(BlockFaces.Of(170, 0)!.Value.Grain).IsEqualTo(Grain.Up);
        await Assert.That(BlockFaces.Of(170, 4)!.Value.Grain).IsEqualTo(Grain.AlongX);
        await Assert.That(BlockFaces.Of(170, 8)!.Value).IsEqualTo(
            new BlockFaces("hay_block_top", "hay_block_side", FaceForm.Cube, Grain: Grain.AlongZ));
    }

    [Test]
    public async Task A_dispenser_wears_its_front_on_the_side_it_faces_and_none_facing_up()
    {
        var east = BlockFaces.Of(23, 5)!.Value;
        await Assert.That((east.Front, east.Facing)).IsEqualTo(("dispenser_front_horizontal", RoomEdge.PosX));
        await Assert.That(BlockFaces.Of(23, 1)!.Value.Front).IsNull();
        await Assert.That(BlockFaces.Of(61, 2)!.Value.Front).IsEqualTo("furnace_front_off");
    }

    [Test]
    public async Task What_no_shape_is_named_for_is_not_drawn()
    {
        foreach (var id in (int[])[0, 93, 94, 117, 131])
            await Assert.That(BlockFaces.Of(id, 0)!.Value.Form).IsEqualTo(FaceForm.Hidden);
    }

    [Test]
    public async Task A_torch_is_crossed_quads_of_its_own_sprite_lit_or_not()
    {
        foreach (var (id, sprite) in ((int, string)[])
                 [(50, "torch_on"), (75, "redstone_torch_off"), (76, "redstone_torch_on")])
        {
            var torch = BlockFaces.Of(id, 5)!.Value;
            await Assert.That((torch.Form, torch.Top)).IsEqualTo((FaceForm.Cross, sprite));
        }
    }

    [Test]
    public async Task What_lies_on_the_ground_or_hangs_on_a_wall_is_drawn_on_its_shape()
    {
        foreach (var (id, data, sprite) in ((int, int, string)[])
                 [(55, 7, "redstone_dust_cross"), (65, 3, "ladder"), (106, 1, "vine"), (111, 0, "waterlily"),
                  (171, 14, "wool_colored_red")])
        {
            var faces = BlockFaces.Of(id, data)!.Value;
            await Assert.That((faces.Form, faces.Top)).IsEqualTo((FaceForm.Cube, sprite));
            await Assert.That(BlockShape.Of(id, data)).IsNotNull();
        }
    }

    [Test]
    public async Task Redstone_wire_is_tinted_by_its_power_the_way_the_game_tints_it()
    {
        await Assert.That(BlockFaces.Of(55, 15)!.Value.Tint).IsEqualTo(0xFF3200u);
        await Assert.That(BlockFaces.Of(55, 0)!.Value.Tint).IsEqualTo(0x4C0000u);
        await Assert.That(BlockFaces.RedstonePower(8)).IsEqualTo(0xB70000u);
        var reds = Enumerable.Range(1, 15).Select(power => (BlockFaces.RedstonePower(power) >> 16) & 0xFF).ToList();
        await Assert.That(reds.Zip(reds.Skip(1)).All(pair => pair.Second > pair.First)).IsTrue();
    }

    [Test]
    public async Task A_lily_pad_keeps_the_one_green_the_game_gives_it()
    {
        await Assert.That(BlockFaces.Of(111, 0)!.Value.Tint).IsEqualTo(0x208030u);
    }

    [Test]
    public async Task A_chest_wears_the_faces_cut_from_its_own_texture_with_its_front_where_its_data_looks()
    {
        var chest = BlockFaces.Of(54, 4)!.Value;
        await Assert.That((chest.Top, chest.Side, chest.Front)).IsEqualTo(("chest_normal_top", "chest_normal_side", "chest_normal_front"));
        await Assert.That(chest.Facing).IsEqualTo(RoomEdge.NegX);
        await Assert.That(BlockFaces.Of(146, 5)!.Value.Front).IsEqualTo("chest_trapped_front");
        await Assert.That(BlockFaces.Of(130, 3)!.Value.Facing).IsEqualTo(RoomEdge.PosZ);
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
    public async Task A_grass_block_side_is_re_tinted_through_its_overlay()
    {
        var grass = BlockFaces.Of(2, 0)!.Value;
        await Assert.That(grass.Side).IsEqualTo("grass_side");
        await Assert.That(grass.SideOverlay).IsEqualTo("grass_side_overlay");
    }

    [Test]
    public async Task The_detail_blocks_are_drawn_in_the_sprites_named_for_them()
    {
        foreach (var (id, data, top, side) in ((int, int, string, string)[])
                 [(63, 0, "planks_oak", "planks_oak"), (96, 0, "trapdoor", "trapdoor"),
                  (167, 0, "iron_trapdoor", "iron_trapdoor"), (101, 0, "iron_bars", "iron_bars"),
                  (64, 0, "door_wood_lower", "door_wood_lower"), (64, 8, "door_wood_upper", "door_wood_upper"),
                  (197, 0, "door_dark_oak_lower", "door_dark_oak_lower"), (27, 8, "rail_golden_powered", "rail_golden"),
                  (151, 0, "daylight_detector_top", "daylight_detector_side"),
                  (178, 0, "daylight_detector_inverted_top", "daylight_detector_side"),
                  (154, 0, "hopper_top", "hopper_outside"), (26, 8, "bed_head_top", "bed_head_side")])
        {
            var faces = BlockFaces.Of(id, data)!.Value;
            await Assert.That((faces.Form, faces.Top, faces.Side)).IsEqualTo((FaceForm.Cube, top, side));
        }
    }
}
