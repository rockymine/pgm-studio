using PgmStudio.Domain;

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
        var frame = RoomFrames.Resolve(0, 0, 10, 10, 5, 5, [FullTopEntry], null, out var refusal)!;
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
        var frame = RoomFrames.Resolve(0, 0, 10, 20, 5, 15, [FullTopEntry], null, out _)!;
        await Assert.That((frame.MaxX - frame.MinX, frame.MaxZ - frame.MinZ)).IsEqualTo((8, 18));
        await Assert.That(frame.Doors[0].Edge).IsEqualTo(RoomEdge.NegZ);
        await Assert.That(frame.Doors[0].Width).IsEqualTo(4);
        // The marker sits deep at the dead end — the pad follows it, not the room centre.
        await Assert.That((frame.Pad.MinX, frame.Pad.MinZ, frame.Pad.Size)).IsEqualTo((4, 14, 2));
    }

    [Test]
    public async Task Too_small_piece_refuses_with_WX2()
    {
        var frame = RoomFrames.Resolve(0, 0, 7, 7, 3.5, 3.5, [(0, 0, 7, 0)], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!).Contains("WX2");
    }

    // ── WX3/WX4/WX5 — the pad ───────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Centre_parity_marker_takes_a_3x3_pad_when_the_interior_affords_it()
    {
        // A 9×9 piece: shell 7×7, interior 5×5 — the 3×3 pad fits with exactly the one-block ring.
        var frame = RoomFrames.Resolve(0, 0, 9, 9, 4.5, 4.5, [(0, 0, 9, 0)], null, out _)!;
        await Assert.That((frame.Pad.MinX, frame.Pad.MinZ, frame.Pad.Size)).IsEqualTo((3, 3, 3));
        await Assert.That(frame.Pad.Shifted).IsFalse();
        await Assert.That((frame.Pad.CenterX, frame.Pad.CenterZ)).IsEqualTo((4.5, 4.5));
    }

    [Test]
    public async Task Centre_parity_pad_degrades_to_1x1_jointly_in_a_minimum_interior()
    {
        // An 8×10 piece: the 4-across interior can't clear a 3×3, so the pad is 1×1 — never a 1×3.
        var frame = RoomFrames.Resolve(0, 0, 8, 10, 4.5, 4.5, [(0, 0, 8, 0)], null, out _)!;
        await Assert.That(frame.Pad.Size).IsEqualTo(1);
    }

    [Test]
    public async Task Corner_cell_marker_shifts_the_pad_inward_and_the_centre_follows()
    {
        // Cell-4 board, marker at a corner cell's centre: block (2,2) is the interior boundary, so the
        // ideal pad sits flush against two walls and shifts to keep the clearance (WX4); the exported
        // point is the shifted pad's centre (WX5).
        var frame = RoomFrames.Resolve(0, 0, 8, 8, 2, 2, [(0, 0, 8, 0)], null, out _)!;
        await Assert.That(frame.Pad.Shifted).IsTrue();
        await Assert.That((frame.Pad.MinX, frame.Pad.MinZ)).IsEqualTo((3, 3));
        await Assert.That((frame.Pad.CenterX, frame.Pad.CenterZ)).IsEqualTo((4.0, 4.0));
    }

    [Test]
    public async Task Mixed_parity_marker_refuses_with_WX3()
    {
        var frame = RoomFrames.Resolve(0, 0, 10, 10, 5, 2.5, [FullTopEntry], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!).Contains("WX3");
    }

    // ── WX6/WX7 — entries and door widths ───────────────────────────────────────────────────────────────

    [Test]
    public async Task No_entry_interface_refuses_with_WX6()
    {
        var frame = RoomFrames.Resolve(0, 0, 10, 10, 5, 5, [], null, out var refusal);
        await Assert.That(frame).IsNull();
        await Assert.That(refusal!).Contains("WX6");
    }

    [Test]
    public async Task Every_entry_interface_cuts_its_own_door()
    {
        // A land seam on the top edge and a build-zone edge on the left both open doors (WX6).
        var frame = RoomFrames.Resolve(0, 0, 12, 12, 6, 6,
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
        var frame = RoomFrames.Resolve(0, 0, 10, 10, 5, 5, [], RoomEdge.PosX, out _)!;
        await Assert.That(frame.Doors.Count).IsEqualTo(1);
        await Assert.That(frame.Doors[0]).IsEqualTo(new RoomDoor(RoomEdge.PosX, 3, 4));
    }

    // ── monument capacity ───────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Monument_slots_fill_corners_then_the_back_wall_and_skip_the_door_opening()
    {
        // The minimum room: 4×4 interior, 2-wide door → 4 corners + 2 back mids; the two door-row mid
        // cells sit inside the door opening and are never seated.
        var frame = RoomFrames.Resolve(0, 0, 8, 8, 4, 4, [], RoomEdge.NegZ, out _)!;
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

    [Test]
    public async Task A_larger_room_gains_capacity_from_its_longer_walls()
    {
        var small = RoomFrames.Resolve(0, 0, 8, 8, 4, 4, [], RoomEdge.NegZ, out _)!;
        var large = RoomFrames.Resolve(0, 0, 14, 14, 7, 7, [], RoomEdge.NegZ, out _)!;
        var smallSeats = RoomFrames.MonumentSlots(small, small.Doors[0]).Count;
        var largeSeats = RoomFrames.MonumentSlots(large, large.Doors[0]).Count;
        await Assert.That(largeSeats).IsGreaterThan(smallSeats);
    }
}
