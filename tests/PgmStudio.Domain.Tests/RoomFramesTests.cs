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
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 10), footprint: null, shellBound: true, 5, 5, [FullTopEntry], null, out var refusal)!;
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
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 20), footprint: null, shellBound: true, 5, 15, [FullTopEntry], null, out _)!;
        await Assert.That((frame.MaxX - frame.MinX, frame.MaxZ - frame.MinZ)).IsEqualTo((8, 18));
        await Assert.That(frame.Doors[0].Edge).IsEqualTo(RoomEdge.NegZ);
        await Assert.That(frame.Doors[0].Width).IsEqualTo(4);
        // The marker sits deep at the dead end — the pad follows it, not the room centre.
        await Assert.That((frame.Pad.MinX, frame.Pad.MinZ, frame.Pad.Size)).IsEqualTo((4, 14, 2));
    }

    [Test]
    public async Task A_piece_too_small_for_a_room_refuses_with_WX2()
    {
        // WX2 refuses one span, the room's own — the pad and the clear floor it keeps. A 5×5 piece insets to
        // 3×3 and cannot hold that, whatever is or is not bound over it.
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 5, 5), footprint: null, shellBound: true, 2.5, 2.5,
            [(0, 0, 5, 0)], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!.Rule).IsEqualTo("WX2");
    }

    [Test]
    public async Task A_footprint_too_small_for_walls_keeps_its_room_and_simply_has_no_building()
    {
        // The contents are what a room is, and they need the same floor whether or not something stands over
        // them. A 5×5 footprint holds a pad and its four chest corners; it cannot hold walls, so the walls are
        // not there — the same rectangle either way, and no refusal for asking.
        var piece = new BlockRect(0, 0, 5, 5);
        var footprint = new BlockRect(0, 0, 5, 5);
        foreach (var bound in new[] { false, true })
        {
            var room = RoomFrames.Resolve(piece, footprint, bound, 2, 2, [(0, 0, 5, 0)], null, out var refusal);
            await Assert.That(refusal).IsNull();
            await Assert.That((room!.MinX, room.MinZ, room.MaxX, room.MaxZ)).IsEqualTo((0, 0, 5, 5));
            await Assert.That(room.Pad.Size).IsEqualTo(2);
            await Assert.That(room.Wall).IsEqualTo(0);                    // no walls stand at this span
            await Assert.That(RoomFrames.InteriorCorners(room)).Contains((0, 0));
        }

        // Two more blocks on each axis and the same binding raises them.
        var roomy = RoomFrames.Resolve(new BlockRect(0, 0, 7, 7), new BlockRect(0, 0, 7, 7), shellBound: true,
            3, 3, [(0, 0, 7, 0)], null, out _)!;
        await Assert.That(roomy.Wall).IsEqualTo(1);
    }

    [Test]
    public async Task A_footprint_reaching_past_its_piece_refuses_with_WX12()
    {
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 10), new BlockRect(1, 1, 12, 9), shellBound: true,
            5, 5, [(0, 0, 10, 0)], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!.Rule).IsEqualTo("WX12");
    }

    [Test]
    public async Task An_authored_footprint_takes_the_place_of_the_piece_inset()
    {
        // The piece is the ground and the region; the footprint is the building raised on it. A 7×14 hall
        // set toward the back of a 20×20 piece is what the two being one rectangle could not say.
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 20, 20), new BlockRect(6, 5, 13, 19), shellBound: true,
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
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 9, 9), footprint: null, shellBound: true, 4.5, 4.5, [(0, 0, 9, 0)], null, out _)!;
        await Assert.That((frame.Pad.MinX, frame.Pad.MinZ, frame.Pad.Size)).IsEqualTo((3, 3, 3));
        await Assert.That(frame.Pad.Shifted).IsFalse();
        await Assert.That((frame.Pad.CenterX, frame.Pad.CenterZ)).IsEqualTo((4.5, 4.5));
    }

    [Test]
    public async Task Centre_parity_pad_degrades_to_1x1_jointly_in_a_minimum_interior()
    {
        // An 8×10 piece: the 4-across interior can't clear a 3×3, so the pad is 1×1 — never a 1×3.
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 8, 10), footprint: null, shellBound: true, 4.5, 4.5, [(0, 0, 8, 0)], null, out _)!;
        await Assert.That(frame.Pad.Size).IsEqualTo(1);
    }

    [Test]
    public async Task Corner_cell_marker_shifts_the_pad_inward_and_the_centre_follows()
    {
        // Cell-4 board, marker at a corner cell's centre: block (2,2) is the interior boundary, so the
        // ideal pad sits flush against two walls and shifts to keep the clearance (WX4); the exported
        // point is the shifted pad's centre (WX5).
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 8, 8), footprint: null, shellBound: true, 2, 2, [(0, 0, 8, 0)], null, out _)!;
        await Assert.That(frame.Pad.Shifted).IsTrue();
        await Assert.That((frame.Pad.MinX, frame.Pad.MinZ)).IsEqualTo((3, 3));
        await Assert.That((frame.Pad.CenterX, frame.Pad.CenterZ)).IsEqualTo((4.0, 4.0));
    }

    [Test]
    public async Task Mixed_parity_marker_refuses_with_WX3()
    {
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 10), footprint: null, shellBound: true, 5, 2.5, [FullTopEntry], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!.Rule).IsEqualTo("WX3");
    }

    // ── WX6/WX7 — entries and door widths ───────────────────────────────────────────────────────────────

    [Test]
    public async Task No_entry_interface_refuses_with_WX6()
    {
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 10), footprint: null, shellBound: true, 5, 5, [], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!.Rule).IsEqualTo("WX6");
    }

    [Test]
    public async Task Every_entry_interface_cuts_its_own_door()
    {
        // A land seam on the top edge and a build-zone edge on the left both open doors (WX6).
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 12, 12), footprint: null, shellBound: true, 6, 6,
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
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 10, 10), footprint: null, shellBound: true, 5, 5, [], RoomEdge.PosX, out _)!;
        await Assert.That(frame.Doors.Count).IsEqualTo(1);
        await Assert.That(frame.Doors[0]).IsEqualTo(new RoomDoor(RoomEdge.PosX, 3, 4));
    }

    // ── monument capacity ───────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Monument_slots_fill_corners_then_the_back_wall_and_skip_the_door_opening()
    {
        // The minimum room: 4×4 interior, 2-wide door → 4 corners + 2 back mids; the two door-row mid
        // cells sit inside the door opening and are never seated.
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 8, 8), footprint: null, shellBound: true, 4, 4, [], RoomEdge.NegZ, out _)!;
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
    public async Task A_spawn_piece_opens_with_ground_in_front_of_its_door_and_the_iron_stands_in_it()
    {
        // The default footprint keeps one block of clean floor on three sides and DefaultDoorGap in front of
        // the door, so a 20×20 piece facing −z opens as an 18×14 room with five blocks of ground ahead of it.
        // The cube stands in that ground, and the room gives up nothing to make space.
        var room = RoomFrames.ResolveRoom(new BlockRect(0, 0, 20, 20), footprint: null, shellBound: true,
            10, 16, [], RoomEdge.NegZ, [(10, 1.5)], out var refusal)!;
        await Assert.That(refusal).IsNull();
        await Assert.That((room.Frame.MinX, room.Frame.MinZ, room.Frame.MaxX, room.Frame.MaxZ))
            .IsEqualTo((1, 5, 19, 19));

        var iron = room.Iron[0];
        await Assert.That(iron.Placeable).IsTrue();
        await Assert.That((iron.MinX, iron.MinZ, iron.Size)).IsEqualTo((9, 0, RoomFrames.IronSpan));
        // In front of the room rather than beside it, with the standing room the rule keeps.
        await Assert.That(iron.MinZ + iron.Size).IsLessThanOrEqualTo(room.Frame.MinZ - RoomFrames.IronGap);
    }

    [Test]
    public async Task A_cube_is_one_size_whatever_its_markers_parity()
    {
        // No ladder: a grid-line marker and a block-centre marker both centre the same cube, each put back on
        // the block lattice. Nothing about the marker chooses a size.
        var piece = new BlockRect(0, 0, 20, 20);
        var onGrid = RoomFrames.ResolveRoom(piece, null, shellBound: true, 10, 16, [], RoomEdge.NegZ,
            [(10, 1.5)], out _)!.Iron[0];
        var onCentre = RoomFrames.ResolveRoom(piece, null, shellBound: true, 10, 16, [], RoomEdge.NegZ,
            [(6.5, 1.5)], out _)!.Iron[0];
        await Assert.That(onGrid.Size).IsEqualTo(RoomFrames.IronSpan);
        await Assert.That(onCentre.Size).IsEqualTo(RoomFrames.IronSpan);
        await Assert.That((onGrid.MinX, onGrid.MinZ)).IsEqualTo((9, 0));
        await Assert.That((onCentre.MinX, onCentre.MinZ)).IsEqualTo((5, 0));
    }

    [Test]
    public async Task The_room_keeps_the_footprint_it_was_given_whatever_the_iron_asks_for()
    {
        // Whether a cube seats, is refused, or there are two of them, the shell is the one WX1 resolved with
        // no iron on the piece at all.
        var piece = new BlockRect(0, 0, 20, 20);
        var alone = RoomFrames.Resolve(piece, null, shellBound: true, 10, 16, [], RoomEdge.NegZ, out _)!;
        (double X, double Z)[][] cases =
        [
            [(10, 1.5)],                    // one cube, seated
            [(6.5, 1.5), (13.5, 1.5)],      // two, both seated
            [(10, 4.5)],                    // one too close to the shell to seat
        ];
        foreach (var irons in cases)
        {
            var room = RoomFrames.ResolveRoom(piece, null, shellBound: true, 10, 16, [], RoomEdge.NegZ,
                [.. irons], out _)!;
            await Assert.That((room.Frame.MinX, room.Frame.MinZ, room.Frame.MaxX, room.Frame.MaxZ))
                .IsEqualTo((alone.MinX, alone.MinZ, alone.MaxX, alone.MaxZ));
        }
    }

    [Test]
    public async Task A_cube_with_no_clear_air_to_the_shell_is_unplaceable_and_the_room_stands()
    {
        // WX9 — an unplaceable marker is not an error: it stamps nothing, the room takes its full footprint,
        // and the marker stays on the board for validation to flag.
        var room = RoomFrames.ResolveRoom(new BlockRect(0, 0, 20, 20), footprint: null, shellBound: true,
            10, 16, [], RoomEdge.NegZ, [(10, 4.5)], out var refusal)!;
        await Assert.That(refusal).IsNull();
        await Assert.That(room.Iron[0].Placeable).IsFalse();
        await Assert.That((room.Frame.MinX, room.Frame.MinZ, room.Frame.MaxX, room.Frame.MaxZ))
            .IsEqualTo((1, 5, 19, 19));
    }

    [Test]
    public async Task A_cube_that_would_hang_off_the_piece_is_unplaceable()
    {
        var room = RoomFrames.ResolveRoom(new BlockRect(0, 0, 20, 20), footprint: null, shellBound: true,
            10, 16, [], RoomEdge.NegZ, [(0.5, 1.5)], out _)!;
        await Assert.That(room.Iron[0].Placeable).IsFalse();
    }

    [Test]
    public async Task Iron_landing_is_mirror_consistent()
    {
        // Away-from-zero rounding is what makes an orbit image of the cube cover the images of its cells
        // instead of a row one block off. With the shell no longer yielding, this is all that has to mirror.
        var piece = new BlockRect(0, 0, 20, 20);
        var west = RoomFrames.ResolveRoom(piece, null, shellBound: true, 10, 16, [], RoomEdge.NegZ,
            [(6.5, 1.5)], out _)!.Iron[0];
        var east = RoomFrames.ResolveRoom(piece, null, shellBound: true, 10, 16, [], RoomEdge.NegZ,
            [(13.5, 1.5)], out _)!.Iron[0];
        await Assert.That(west.Placeable).IsTrue();
        await Assert.That(east.Placeable).IsTrue();
        await Assert.That(20 - (east.MinX + east.Size)).IsEqualTo(west.MinX);
        await Assert.That(east.MinZ).IsEqualTo(west.MinZ);
    }

    [Test]
    public async Task A_larger_room_gains_capacity_from_its_longer_walls()
    {
        var small = RoomFrames.Resolve(new BlockRect(0, 0, 8, 8), footprint: null, shellBound: true, 4, 4, [], RoomEdge.NegZ, out _)!;
        var large = RoomFrames.Resolve(new BlockRect(0, 0, 14, 14), footprint: null, shellBound: true, 7, 7, [], RoomEdge.NegZ, out _)!;
        var smallSeats = RoomFrames.MonumentSlots(small, small.Doors[0]).Count;
        var largeSeats = RoomFrames.MonumentSlots(large, large.Doors[0]).Count;
        await Assert.That(largeSeats).IsGreaterThan(smallSeats);
    }
}
