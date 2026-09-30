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
        Carpet = 171, Chest = 54;

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
}
