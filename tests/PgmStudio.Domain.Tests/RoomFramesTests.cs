using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// The WX rules (docs/world-export/structures.md) over the design's test set: footprints from the piece
/// (WX1/WX2), the parity-driven square pad with clearance shift (WX3–WX5), and doors on the entry
/// interfaces at wall-parity widths (WX6/WX7).
/// </summary>
public sealed class RoomFramesTests
{
    private static readonly (double MinX, double MinZ, double MaxX, double MaxZ) FullTopEntry = (0, 0, 10, 0);

    // ── WX1/WX2 — footprints ────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Baseline_10x10_piece_yields_the_shipped_8x8_shell()
    {
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 10), footprint: null, walled: true, 5, 5, [FullTopEntry], null, out var refusal)!;
        await Assert.That(refusal).IsNull();
        await Assert.That((frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ)).IsEqualTo((1, 1, 9, 9));
        await Assert.That((frame.Pad.MinX, frame.Pad.MinZ, frame.Pad.Size)).IsEqualTo((4, 4, 2));
        await Assert.That(frame.Pad.Shifted).IsFalse();
        // 6-across interior → the common 4-wide door, centred.
        await Assert.That(frame.Doors[0]).IsEqualTo(new RoomDoor(RoomEdge.NegZ, 3, 4));
    }

    [Test]
    public async Task Deep_10x20_piece_yields_an_8x18_shell_with_the_door_on_the_short_edge()
    {
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 20), footprint: null, walled: true, 5, 15, [FullTopEntry], null, out _)!;
        await Assert.That((frame.MaxX - frame.MinX, frame.MaxZ - frame.MinZ)).IsEqualTo((8, 18));
        await Assert.That(frame.Doors[0].Edge).IsEqualTo(RoomEdge.NegZ);
        await Assert.That(frame.Doors[0].Width).IsEqualTo(4);
        // The marker sits deep at the dead end — the pad follows it, not the room centre.
        await Assert.That((frame.Pad.MinX, frame.Pad.MinZ, frame.Pad.Size)).IsEqualTo((4, 14, 2));
    }

    [Test]
    public async Task Too_small_piece_refuses_with_WX2()
    {
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 7, 7), footprint: null, walled: true, 3.5, 3.5, [(0, 0, 7, 0)], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!.Rule).IsEqualTo("WX2");
    }

    [Test]
    public async Task An_unwalled_room_holds_at_four_across_where_a_walled_one_needs_six()
    {
        // A wall is what the interior is inset by and what the pad keeps its clearance to, so a room on open
        // ground pays neither: the same 5×5 footprint that cannot carry a shell carries a pad and its four
        // chest corners perfectly well.
        var open = RoomFrames.Resolve(new BlockRect(0, 0, 5, 5), new BlockRect(0, 0, 5, 5), walled: false,
            2, 2, [(0, 0, 5, 0)], null, out var openRefusal);
        await Assert.That(openRefusal).IsNull();
        await Assert.That(open!.Pad.Size).IsEqualTo(2);
        // With no wall the whole footprint is interior, so the chests seat in its own corners.
        await Assert.That(RoomFrames.InteriorCorners(open)).Contains((0, 0));

        var walled = RoomFrames.Resolve(new BlockRect(0, 0, 5, 5), new BlockRect(0, 0, 5, 5), walled: true,
            2, 2, [(0, 0, 5, 0)], null, out var walledRefusal);
        await Assert.That(walled).IsNull();
        await Assert.That(walledRefusal!.Rule).IsEqualTo("WX2");
    }

    [Test]
    public async Task A_footprint_reaching_past_its_piece_refuses_with_WX12()
    {
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 10), new BlockRect(1, 1, 12, 9), walled: true,
            5, 5, [(0, 0, 10, 0)], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!.Rule).IsEqualTo("WX12");
    }

    [Test]
    public async Task An_authored_footprint_takes_the_place_of_the_piece_inset()
    {
        // The piece is the ground and the region; the footprint is the building raised on it. A 7×14 hall
        // set toward the back of a 20×20 piece is what the two being one rectangle could not say.
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 20, 20), new BlockRect(6, 5, 13, 19), walled: true,
            10, 16, [], RoomEdge.NegZ, out var refusal)!;
        await Assert.That(refusal).IsNull();
        await Assert.That((frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ)).IsEqualTo((6, 5, 13, 19));
        // The door is cut into the footprint's own wall, not the piece's edge.
        await Assert.That(frame.Doors[0].Edge).IsEqualTo(RoomEdge.NegZ);
        await Assert.That(frame.Doors[0].Lo).IsGreaterThanOrEqualTo(6);
        await Assert.That(frame.Doors[0].Lo + frame.Doors[0].Width).IsLessThanOrEqualTo(13);
    }

    // ── WX3/WX4/WX5 — the pad ───────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Centre_parity_marker_takes_a_3x3_pad_when_the_interior_affords_it()
    {
        // A 9×9 piece: shell 7×7, interior 5×5 — the 3×3 pad fits with exactly the one-block ring.
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 9, 9), footprint: null, walled: true, 4.5, 4.5, [(0, 0, 9, 0)], null, out _)!;
        await Assert.That((frame.Pad.MinX, frame.Pad.MinZ, frame.Pad.Size)).IsEqualTo((3, 3, 3));
        await Assert.That(frame.Pad.Shifted).IsFalse();
        await Assert.That((frame.Pad.CenterX, frame.Pad.CenterZ)).IsEqualTo((4.5, 4.5));
    }

    [Test]
    public async Task Centre_parity_pad_degrades_to_1x1_jointly_in_a_minimum_interior()
    {
        // An 8×10 piece: the 4-across interior can't clear a 3×3, so the pad is 1×1 — never a 1×3.
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 8, 10), footprint: null, walled: true, 4.5, 4.5, [(0, 0, 8, 0)], null, out _)!;
        await Assert.That(frame.Pad.Size).IsEqualTo(1);
    }

    [Test]
    public async Task Corner_cell_marker_shifts_the_pad_inward_and_the_centre_follows()
    {
        // Cell-4 board, marker at a corner cell's centre: block (2,2) is the interior boundary, so the
        // ideal pad sits flush against two walls and shifts to keep the clearance (WX4); the exported
        // point is the shifted pad's centre (WX5).
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 8, 8), footprint: null, walled: true, 2, 2, [(0, 0, 8, 0)], null, out _)!;
        await Assert.That(frame.Pad.Shifted).IsTrue();
        await Assert.That((frame.Pad.MinX, frame.Pad.MinZ)).IsEqualTo((3, 3));
        await Assert.That((frame.Pad.CenterX, frame.Pad.CenterZ)).IsEqualTo((4.0, 4.0));
    }

    [Test]
    public async Task Mixed_parity_marker_refuses_with_WX3()
    {
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 10), footprint: null, walled: true, 5, 2.5, [FullTopEntry], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!.Rule).IsEqualTo("WX3");
    }

    // ── WX6/WX7 — entries and door widths ───────────────────────────────────────────────────────────────

    [Test]
    public async Task No_entry_interface_refuses_with_WX6()
    {
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 10), footprint: null, walled: true, 5, 5, [], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!.Rule).IsEqualTo("WX6");
    }

    [Test]
    public async Task Every_entry_interface_cuts_its_own_door()
    {
        // A land seam on the top edge and a build-zone edge on the left both open doors (WX6).
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 12, 12), footprint: null, walled: true, 6, 6,
            [(2, 0, 10, 0), (0, 3, 0, 9)], null, out _)!;
        await Assert.That(frame.Doors.Count).IsEqualTo(2);
        await Assert.That(frame.Doors.Select(d => d.Edge))
            .IsEquivalentTo([RoomEdge.NegZ, RoomEdge.NegX]);
    }

    [Test]
    public async Task Door_width_follows_the_wall()
    {
        await Assert.That(RoomFrames.DoorWidth(4)).IsEqualTo(2);    // the minimum interior
        await Assert.That(RoomFrames.DoorWidth(5)).IsEqualTo(3);    // odd wall → odd door
        await Assert.That(RoomFrames.DoorWidth(6)).IsEqualTo(4);    // the common width
        await Assert.That(RoomFrames.DoorWidth(7)).IsEqualTo(3);
        await Assert.That(RoomFrames.DoorWidth(16)).IsEqualTo(4);
    }

    [Test]
    public async Task Spawn_door_centres_on_its_yaw_derived_wall()
    {
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 10), footprint: null, walled: true, 5, 5, [], RoomEdge.PosX, out _)!;
        await Assert.That(frame.Doors.Count).IsEqualTo(1);
        await Assert.That(frame.Doors[0]).IsEqualTo(new RoomDoor(RoomEdge.PosX, 3, 4));
    }

    // ── monument capacity ───────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Monument_slots_fill_corners_then_the_back_wall_and_skip_the_door_opening()
    {
        // The minimum room: 4×4 interior, 2-wide door → 4 corners + 2 back mids; the two door-row mid
        // cells sit inside the door opening and are never seated.
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 8, 8), footprint: null, walled: true, 4, 4, [], RoomEdge.NegZ, out _)!;
        var slots = RoomFrames.MonumentSlots(frame, frame.Doors[0]);
        await Assert.That(slots.Count).IsEqualTo(6);
        // Corners first: the door wall pair (z=2), then the back pair (z=5).
        await Assert.That((slots[0].X, slots[0].Z)).IsEqualTo((2, 2));
        await Assert.That((slots[1].X, slots[1].Z)).IsEqualTo((5, 2));
        await Assert.That((slots[2].X, slots[2].Z)).IsEqualTo((2, 5));
        await Assert.That((slots[3].X, slots[3].Z)).IsEqualTo((5, 5));
        // The fill: back-wall mids only.
        await Assert.That(slots.Skip(4).All(s => s.Z == 5)).IsTrue();
    }

    // ── WX8/WX9 — iron beside the room ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Iron_at_a_back_cell_centre_shrinks_the_shell_and_takes_a_3x3()
    {
        // The 10×15 piece: spawn marker in the front region, centre-parity iron in the back — the shell
        // gives up the back strip (8×9), the cube takes 3×3, one block of air between them.
        var room = RoomFrames.ResolveRoom(new BlockRect(0, 0, 10, 15), footprint: null, walled: true, 5, 5, [], RoomEdge.NegZ, [(7.5, 12.5)], out _)!;
        var iron = room.Iron[0];
        await Assert.That(iron.Placeable).IsTrue();
        await Assert.That((iron.MinX, iron.MinZ, iron.Size)).IsEqualTo((6, 11, 3));
        await Assert.That((room.Frame.MinX, room.Frame.MinZ, room.Frame.MaxX, room.Frame.MaxZ))
            .IsEqualTo((1, 1, 9, 10));
        // Separated: the shell ends at z=10, the cube starts at z=11.
        await Assert.That(room.Frame.MaxZ).IsLessThanOrEqualTo(iron.MinZ - RoomFrames.IronGap);
    }

    [Test]
    public async Task Grid_line_iron_carries_the_full_4x4()
    {
        var room = RoomFrames.ResolveRoom(new BlockRect(0, 0, 10, 15), footprint: null, walled: true, 5, 5, [], RoomEdge.NegZ, [(5, 10)], out _)!;
        var iron = room.Iron[0];
        await Assert.That((iron.MinX, iron.MinZ, iron.Size)).IsEqualTo((3, 8, 4));
        await Assert.That((room.Frame.MaxZ - room.Frame.MinZ)).IsEqualTo(6);   // shell yielded to 8×6
    }

    [Test]
    public async Task Iron_the_room_cannot_host_is_unplaceable_and_the_room_stands()
    {
        // The 10×10 piece with both markers: the shell cannot shrink below 6×6 and even a 3×3 cube finds
        // no clear strip — the room has priority, the marker resolves unplaceable, nothing else changes.
        var room = RoomFrames.ResolveRoom(new BlockRect(0, 0, 10, 10), footprint: null, walled: true, 5, 5, [], RoomEdge.NegZ, [(2.5, 2.5)], out _)!;
        await Assert.That(room.Iron[0].Placeable).IsFalse();
        await Assert.That((room.Frame.MinX, room.Frame.MinZ, room.Frame.MaxX, room.Frame.MaxZ))
            .IsEqualTo((1, 1, 9, 9));
    }

    [Test]
    public async Task Iron_whose_axes_disagree_settles_half_a_block_off_centre()
    {
        // A grid line in x but a block centre in z centres no square cube. Rather than refuse it, the ladder
        // runs and the cube lands on the nearest block lattice — the whole 4×4, half a block south of the mark.
        var room = RoomFrames.ResolveRoom(new BlockRect(0, 0, 10, 15), footprint: null, walled: true, 5, 5, [], RoomEdge.NegZ, [(5, 12.5)], out _)!;
        var iron = room.Iron[0];
        await Assert.That(iron.Placeable).IsTrue();
        await Assert.That((iron.MinX, iron.MinZ, iron.Size)).IsEqualTo((3, 11, 4));
        await Assert.That((room.Frame.MinX, room.Frame.MinZ, room.Frame.MaxX, room.Frame.MaxZ))
            .IsEqualTo((1, 1, 9, 10));
    }

    [Test]
    public async Task A_column_of_iron_down_one_side_stamps_every_cube()
    {
        // The authoring case the refusal ate: three markers in a line down the long side of a 25×15 spawn
        // piece, on a cell grid whose odd size puts them on a grid line in x and a block centre in z. The
        // shell yields once, on the axis that has room, and every cube is placed.
        var room = RoomFrames.ResolveRoom(new BlockRect(30, -130, 55, -115), footprint: null, walled: true, 45, -125, [], RoomEdge.PosZ,
            [(35, -127.5), (35, -122.5), (35, -117.5)], out var refusal)!;
        await Assert.That(refusal).IsNull();
        await Assert.That(room.Iron.All(i => i.Placeable)).IsTrue();
        await Assert.That(room.Iron.Select(i => i.Size).Distinct().Single()).IsEqualTo(4);
        // The room kept the far side of the piece rather than falling back to a default shell.
        await Assert.That(room.Frame.MinX).IsGreaterThanOrEqualTo(38);
        await Assert.That((room.Frame.MaxX, room.Frame.MinZ, room.Frame.MaxZ)).IsEqualTo((54, -129, -116));
    }

    [Test]
    public async Task Iron_whose_axes_disagree_is_still_mirror_consistent()
    {
        // The same half-block landing has to reflect: away-from-zero rounding is what makes an orbit image of
        // the cube cover the images of its cells, instead of a row one block off.
        var west = RoomFrames.ResolveRoom(new BlockRect(0, 0, 20, 8), footprint: null, walled: true, 4.5, 3.5, [], RoomEdge.NegZ, [(14, 3.5)], out _)!;
        var east = RoomFrames.ResolveRoom(new BlockRect(0, 0, 20, 8), footprint: null, walled: true, 15.5, 3.5, [], RoomEdge.NegZ, [(6, 3.5)], out _)!;
        await Assert.That(west.Iron[0].Placeable).IsTrue();
        await Assert.That(east.Iron[0].Placeable).IsTrue();
        await Assert.That(20 - (east.Iron[0].MinX + east.Iron[0].Size)).IsEqualTo(west.Iron[0].MinX);
        await Assert.That((20 - east.Frame.MaxX, 20 - east.Frame.MinX)).IsEqualTo((west.Frame.MinX, west.Frame.MaxX));
    }

    [Test]
    public async Task Iron_resolution_is_mirror_consistent()
    {
        // A wide flat piece and its mirror image (marker west + iron east, then marker east + iron west):
        // the shrink choice must mirror with the geometry, or orbit images stamp different rooms.
        var west = RoomFrames.ResolveRoom(new BlockRect(0, 0, 20, 8), footprint: null, walled: true, 4.5, 3.5, [], RoomEdge.NegZ, [(14, 4)], out _)!;
        var east = RoomFrames.ResolveRoom(new BlockRect(0, 0, 20, 8), footprint: null, walled: true, 15.5, 3.5, [], RoomEdge.NegZ, [(6, 4)], out _)!;
        await Assert.That(west.Iron[0].Placeable).IsTrue();
        await Assert.That((west.Frame.MinX, west.Frame.MaxX)).IsEqualTo((1, 11));
        await Assert.That((20 - east.Frame.MaxX, 20 - east.Frame.MinX)).IsEqualTo((1, 11));
        await Assert.That(20 - (east.Iron[0].MinX + east.Iron[0].Size)).IsEqualTo(west.Iron[0].MinX);
    }

    [Test]
    public async Task A_larger_room_gains_capacity_from_its_longer_walls()
    {
        var small = RoomFrames.Resolve(new BlockRect(0, 0, 8, 8), footprint: null, walled: true, 4, 4, [], RoomEdge.NegZ, out _)!;
        var large = RoomFrames.Resolve(new BlockRect(0, 0, 14, 14), footprint: null, walled: true, 7, 7, [], RoomEdge.NegZ, out _)!;
        var smallSeats = RoomFrames.MonumentSlots(small, small.Doors[0]).Count;
        var largeSeats = RoomFrames.MonumentSlots(large, large.Doors[0]).Count;
        await Assert.That(largeSeats).IsGreaterThan(smallSeats);
    }
}
