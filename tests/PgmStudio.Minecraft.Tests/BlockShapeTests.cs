using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The part of its cell a block fills. What is held is the shape a player reads the block by: a slab is half
/// a cube and which half is its top bit, a stair's step stands on the side its low bits name and turns over
/// with the upside-down bit, and a fence, a pane and a wall reach out only to the sides they meet.
/// </summary>
public sealed class BlockShapeTests
{
    private const int Stone = 1, Leaves = 18, Glass = 20, OakStairs = 53, OakFence = 85, GlassPane = 102,
        OakGate = 107, CobblestoneWall = 139, StoneSlab = 44, RedstoneWire = 55, Ladder = 65, Vine = 106,
        Carpet = 171, Chest = 54, StandingSign = 63, WallSign = 68, Lever = 69, StoneButton = 77, WoodButton = 143,
        OakDoor = 64, OakTrapdoor = 96, IronTrapdoor = 167, IronBars = 101, StonePlate = 70, Rail = 66,
        FlowerPot = 140, DaylightSensor = 151, EnchantingTable = 116, EndPortalFrame = 120, Bed = 26, Cake = 92,
        SnowLayer = 78, Hopper = 154, Anvil = 145;

    private const double Sixteenth = 1.0 / 16;

    [Test]
    public async Task A_whole_block_fills_its_cell()
    {
        await Assert.That(BlockShape.Of(Stone, 0)).IsNull();
    }

    [Test]
    public async Task A_slab_is_its_lower_half_unless_its_top_bit_is_set()
    {
        await Assert.That(BlockShape.Of(StoneSlab, 0)!.Single()).IsEqualTo(new CellBox(0, 0, 0, 1, 0.5, 1));
        await Assert.That(BlockShape.Of(StoneSlab, 8)!.Single()).IsEqualTo(new CellBox(0, 0.5, 0, 1, 1, 1));
    }

    [Test]
    public async Task A_stair_climbs_toward_the_side_its_low_bits_name()
    {
        var east = BlockShape.Of(OakStairs, 0)!;
        var north = BlockShape.Of(OakStairs, 3)!;

        await Assert.That(east).Contains(new CellBox(0, 0, 0, 1, 0.5, 1));
        await Assert.That(east).Contains(new CellBox(0.5, 0.5, 0, 1, 1, 1));
        await Assert.That(north).Contains(new CellBox(0, 0.5, 0, 1, 1, 0.5));
    }

    [Test]
    public async Task An_upside_down_stair_hangs_its_base_from_the_top_and_its_step_below()
    {
        var hung = BlockShape.Of(OakStairs, 4)!;

        await Assert.That(hung).Contains(new CellBox(0, 0.5, 0, 1, 1, 1));
        await Assert.That(hung).Contains(new CellBox(0.5, 0, 0, 1, 0.5, 1));
    }

    [Test]
    public async Task A_fence_meeting_nothing_is_its_post_and_one_joined_east_reaches_the_east_edge()
    {
        var alone = BlockShape.Of(OakFence, 0)!;
        var joined = BlockShape.Of(OakFence, 0, Joins.East)!;

        await Assert.That(alone.Max(box => box.MaxX)).IsEqualTo(0.625);
        await Assert.That(joined.Max(box => box.MaxX)).IsEqualTo(1.0);
        await Assert.That(joined.Min(box => box.MinX)).IsEqualTo(0.375);
    }

    [Test]
    public async Task A_pane_meeting_nothing_crosses_its_cell_both_ways()
    {
        var alone = BlockShape.Of(GlassPane, 0)!;

        await Assert.That(alone.Min(box => box.MinX)).IsEqualTo(0.0);
        await Assert.That(alone.Max(box => box.MaxX)).IsEqualTo(1.0);
        await Assert.That(alone.Min(box => box.MinZ)).IsEqualTo(0.0);
        await Assert.That(alone.Max(box => box.MaxZ)).IsEqualTo(1.0);
    }

    [Test]
    public async Task A_wall_running_straight_through_has_no_post()
    {
        var straight = BlockShape.Of(CobblestoneWall, 0, Joins.North | Joins.South)!;
        var corner = BlockShape.Of(CobblestoneWall, 0, Joins.North | Joins.East)!;

        await Assert.That(straight.Max(box => box.MaxY)).IsEqualTo(0.8125);
        await Assert.That(corner.Max(box => box.MaxY)).IsEqualTo(1.0);
    }

    [Test]
    public async Task A_closed_gate_spans_its_cell_and_an_open_one_leaves_only_its_posts()
    {
        var closed = BlockShape.Of(OakGate, 0)!;
        var open = BlockShape.Of(OakGate, 4)!;

        await Assert.That(closed.Length).IsEqualTo(4);
        await Assert.That(open.Length).IsEqualTo(2);
        await Assert.That(open.All(box => box.MaxX <= 0.125 || box.MinX >= 0.875)).IsTrue();
    }

    [Test]
    public async Task A_carpet_and_a_wire_are_a_sheet_on_the_floor_of_their_cell()
    {
        foreach (var id in (int[])[Carpet, RedstoneWire])
        {
            var boxes = BlockShape.Of(id, 0)!;
            await Assert.That(boxes.Single()).IsEqualTo(new CellBox(0, 0, 0, 1, Sixteenth, 1));
            await Assert.That(BlockShape.Sheet(boxes)).IsTrue();
        }
    }

    /// <summary>A ladder's data is the way it looks, so it hangs on the side of its cell opposite that — north
    /// 2 on the south side, east 5 on the west.</summary>
    [Test]
    public async Task A_ladder_hangs_on_the_side_opposite_the_way_it_looks()
    {
        await Assert.That(BlockShape.Of(Ladder, 2)!.Single()).IsEqualTo(new CellBox(0, 0, 1 - Sixteenth, 1, 1, 1));
        await Assert.That(BlockShape.Of(Ladder, 3)!.Single()).IsEqualTo(new CellBox(0, 0, 0, 1, 1, Sixteenth));
        await Assert.That(BlockShape.Of(Ladder, 4)!.Single()).IsEqualTo(new CellBox(1 - Sixteenth, 0, 0, 1, 1, 1));
        await Assert.That(BlockShape.Of(Ladder, 5)!.Single()).IsEqualTo(new CellBox(0, 0, 0, Sixteenth, 1, 1));
    }

    [Test]
    public async Task A_vine_is_a_sheet_on_every_side_it_clings_to_or_under_the_ceiling()
    {
        var southAndEast = BlockShape.Of(Vine, 1 | 8)!;
        var hanging = BlockShape.Of(Vine, 0)!;

        await Assert.That(southAndEast.Length).IsEqualTo(2);
        await Assert.That(southAndEast).Contains(new CellBox(0, 0, 1 - Sixteenth, 1, 1, 1));
        await Assert.That(southAndEast).Contains(new CellBox(1 - Sixteenth, 0, 0, 1, 1, 1));
        await Assert.That(hanging.Single()).IsEqualTo(new CellBox(0, 1 - Sixteenth, 0, 1, 1, 1));
    }

    [Test]
    public async Task A_chest_is_inset_a_sixteenth_and_fourteen_sixteenths_tall_and_is_no_sheet()
    {
        var chest = BlockShape.Of(Chest, 2)!;

        await Assert.That(chest.Single()).IsEqualTo(new CellBox(Sixteenth, 0, Sixteenth, 1 - Sixteenth, 14.0 / 16, 1 - Sixteenth));
        await Assert.That(BlockShape.Sheet(chest)).IsFalse();
        await Assert.That(BlockShape.Sheet(BlockShape.Of(OakGate, 0))).IsFalse();
    }

    [Test]
    public async Task A_fence_meets_a_whole_block_but_not_leaves_or_glass_and_a_pane_meets_glass()
    {
        await Assert.That(BlockShape.Meets(JoinKind.Fence, Stone, neighbourFillsCell: true)).IsTrue();
        await Assert.That(BlockShape.Meets(JoinKind.Fence, Leaves, neighbourFillsCell: true)).IsFalse();
        await Assert.That(BlockShape.Meets(JoinKind.Fence, Glass, neighbourFillsCell: true)).IsFalse();
        await Assert.That(BlockShape.Meets(JoinKind.Fence, OakGate, neighbourFillsCell: false)).IsTrue();
        await Assert.That(BlockShape.Meets(JoinKind.Pane, Glass, neighbourFillsCell: true)).IsTrue();
        await Assert.That(BlockShape.Meets(JoinKind.Pane, OakStairs, neighbourFillsCell: false)).IsFalse();
    }

    [Test]
    public async Task A_closed_bottom_trapdoor_is_a_three_sixteenths_slab_and_a_top_one_hangs_from_the_ceiling()
    {
        var bottom = BlockShape.Of(OakTrapdoor, 0)!.Single();
        var top = BlockShape.Of(OakTrapdoor, 8)!.Single();

        await Assert.That(bottom.MaxY).IsEqualTo(3.0 / 16);
        await Assert.That(top.MinY).IsEqualTo(1 - 3.0 / 16);
        await Assert.That(top.MaxY).IsEqualTo(1.0);
    }

    [Test]
    public async Task An_open_trapdoor_stands_against_the_side_it_is_hinged_on()
    {
        var south = BlockShape.Of(IronTrapdoor, 4)!.Single();
        var north = BlockShape.Of(IronTrapdoor, 5)!.Single();
        var east = BlockShape.Of(IronTrapdoor, 6)!.Single();
        var west = BlockShape.Of(IronTrapdoor, 7)!.Single();

        await Assert.That(south).IsEqualTo(new CellBox(0, 0, 1 - 3.0 / 16, 1, 1, 1));
        await Assert.That(north).IsEqualTo(new CellBox(0, 0, 0, 1, 1, 3.0 / 16));
        await Assert.That(east).IsEqualTo(new CellBox(1 - 3.0 / 16, 0, 0, 1, 1, 1));
        await Assert.That(west).IsEqualTo(new CellBox(0, 0, 0, 3.0 / 16, 1, 1));
    }

    [Test]
    public async Task A_wall_sign_facing_north_hangs_against_the_south_side_of_its_cell()
    {
        var board = BlockShape.Of(WallSign, 2)!.Single();

        await Assert.That(board.MinZ).IsEqualTo(1 - 2.0 / 16);
        await Assert.That(board.MaxZ).IsEqualTo(1.0);
        await Assert.That((board.MinY, board.MaxY)).IsEqualTo((0.25, 0.75));
        await Assert.That(BlockShape.Of(WallSign, 5)!.Single().MinX).IsEqualTo(0.0);
    }

    [Test]
    public async Task A_standing_sign_is_a_post_under_a_board_turned_to_the_nearest_axis()
    {
        var south = BlockShape.Of(StandingSign, 0)!;
        var west = BlockShape.Of(StandingSign, 4)!;

        await Assert.That(south).Contains(new CellBox(7.0 / 16, 0, 7.0 / 16, 9.0 / 16, 0.5, 9.0 / 16));
        await Assert.That(south.Max(box => box.MaxX - box.MinX)).IsEqualTo(1.0);
        await Assert.That(south.Max(box => box.MaxZ - box.MinZ)).IsEqualTo(2.0 / 16);
        await Assert.That(west.Max(box => box.MaxZ - box.MinZ)).IsEqualTo(1.0);
        await Assert.That(west.Max(box => box.MaxX - box.MinX)).IsEqualTo(2.0 / 16);
    }

    [Test]
    public async Task A_button_is_a_two_texel_box_on_the_face_that_holds_it_and_a_pressed_one_is_a_texel()
    {
        var onEastFacing = BlockShape.Of(StoneButton, 1)!.Single();
        var onFloor = BlockShape.Of(WoodButton, 5)!.Single();
        var pressed = BlockShape.Of(StoneButton, 1 | 8)!.Single();

        await Assert.That(onEastFacing.MinX).IsEqualTo(0.0);
        await Assert.That(onEastFacing.MaxX).IsEqualTo(2.0 / 16);
        await Assert.That(onFloor.MaxY).IsEqualTo(2.0 / 16);
        await Assert.That(pressed.MaxX).IsEqualTo(Sixteenth);
    }

    [Test]
    public async Task A_lever_is_a_three_texel_base_on_its_wall_floor_or_ceiling()
    {
        await Assert.That(BlockShape.Of(Lever, 4)!.Single().MaxZ).IsEqualTo(1.0);
        await Assert.That(BlockShape.Of(Lever, 4)!.Single().MinZ).IsEqualTo(1 - 3.0 / 16);
        await Assert.That(BlockShape.Of(Lever, 5)!.Single().MaxY).IsEqualTo(3.0 / 16);
        await Assert.That(BlockShape.Of(Lever, 7)!.Single().MinY).IsEqualTo(1 - 3.0 / 16);
    }

    [Test]
    public async Task A_closed_door_stands_on_the_side_its_facing_names_and_an_open_one_swings_about_its_hinge()
    {
        var closedWest = BlockShape.Of(OakDoor, BlockShape.DoorData(0, 0))!.Single();
        var openLeft = BlockShape.Of(OakDoor, BlockShape.DoorData(4, 0))!.Single();
        var openRight = BlockShape.Of(OakDoor, BlockShape.DoorData(4, 1 | 8))!.Single();

        await Assert.That(closedWest).IsEqualTo(new CellBox(0, 0, 0, 3.0 / 16, 1, 1));
        await Assert.That(openLeft).IsEqualTo(new CellBox(0, 0, 0, 1, 1, 3.0 / 16));
        await Assert.That(openRight).IsEqualTo(new CellBox(0, 0, 1 - 3.0 / 16, 1, 1, 1));
    }

    [Test]
    public async Task The_upper_half_of_a_door_takes_its_facing_from_the_lower_half_and_keeps_its_own_hinge()
    {
        var upper = BlockShape.DoorData(8 | 1, 6);

        await Assert.That(upper & 7).IsEqualTo(6);
        await Assert.That(upper & 8).IsEqualTo(8);
        await Assert.That(upper & 16).IsEqualTo(16);
        await Assert.That(BlockShape.Of(OakDoor, upper)!.Single()).IsEqualTo(BlockShape.Of(OakDoor, BlockShape.DoorData(6, 1))!.Single());
    }

    [Test]
    public async Task Iron_bars_join_like_a_pane_and_a_plate_and_a_rail_are_sheets()
    {
        await Assert.That(BlockShape.Joining(IronBars)).IsEqualTo(JoinKind.Pane);
        await Assert.That(BlockShape.Of(IronBars, 0, Joins.East)!.Max(box => box.MaxX)).IsEqualTo(1.0);
        await Assert.That(BlockShape.Sheet(BlockShape.Of(StonePlate, 0))).IsTrue();
        await Assert.That(BlockShape.Sheet(BlockShape.Of(Rail, 0))).IsTrue();
    }

    [Test]
    public async Task The_small_boxes_are_the_height_the_game_draws_them()
    {
        await Assert.That(BlockShape.Of(FlowerPot, 0)!.Single().MaxY).IsEqualTo(6.0 / 16);
        await Assert.That(BlockShape.Of(DaylightSensor, 0)!.Single().MaxY).IsEqualTo(6.0 / 16);
        await Assert.That(BlockShape.Of(EnchantingTable, 0)!.Single().MaxY).IsEqualTo(0.75);
        await Assert.That(BlockShape.Of(EndPortalFrame, 0)!.Single().MaxY).IsEqualTo(13.0 / 16);
        await Assert.That(BlockShape.Of(Bed, 0)!.Single().MaxY).IsEqualTo(9.0 / 16);
        await Assert.That(BlockShape.Of(Cake, 0)!.Single()).IsEqualTo(new CellBox(Sixteenth, 0, Sixteenth, 1 - Sixteenth, 0.5, 1 - Sixteenth));
        await Assert.That(BlockShape.Of(SnowLayer, 2)!.Single().MaxY).IsEqualTo(3.0 / 8);
        await Assert.That(BlockShape.Of(SnowLayer, 7)).IsNull();
    }

    [Test]
    public async Task A_hopper_is_a_bowl_over_a_neck_and_an_anvil_turns_its_top_with_its_axis_bit()
    {
        var hopper = BlockShape.Of(Hopper, 0)!;
        var alongZ = BlockShape.Of(Anvil, 0)!;
        var alongX = BlockShape.Of(Anvil, 1)!;

        await Assert.That(hopper).Contains(new CellBox(0, 10.0 / 16, 0, 1, 1, 1));
        await Assert.That(hopper).Contains(new CellBox(4.0 / 16, 4.0 / 16, 4.0 / 16, 12.0 / 16, 10.0 / 16, 12.0 / 16));
        await Assert.That(alongX.Max(box => box.MaxX - box.MinX)).IsEqualTo(1.0);
        await Assert.That(alongZ.Max(box => box.MaxZ - box.MinZ)).IsEqualTo(1.0);
        await Assert.That(alongZ.Max(box => box.MaxX - box.MinX)).IsLessThan(1.0);
    }

    [Test]
    public async Task A_sign_a_door_and_a_trapdoor_are_details_and_a_hopper_and_a_fence_are_not()
    {
        foreach (var id in (int[])[StandingSign, WallSign, OakDoor, OakTrapdoor, StoneButton, Lever, Rail, FlowerPot])
            await Assert.That(BlockShape.Detail(id)).IsTrue();
        foreach (var id in (int[])[Hopper, OakFence, Anvil, Bed])
            await Assert.That(BlockShape.Detail(id)).IsFalse();
    }
}
