using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The emitter's placement knobs: the scythe's two independently-offsettable endpoints (the entry tail and
/// the wool end slide along the docking edge, and the piece each docks — spine / return leg — resizes with
/// the shift), the scythe entry's variable attachment width (measured ALONG the spine it docks, the same
/// w2/w4/w6 grammar as the donut's), and the donut attachment sliding along the ring's edge (only the
/// attachment moves, the ring is unchanged). Every variant must stay well-formed (no overlaps), keep its
/// template slot order, and classify back to its family — the knobs move endpoints, never the identity.
/// </summary>
public sealed class EmitterPlacementKnobTests
{
    private static WoolBox Box => new(0, 0, 20, 24);
    private const int Cw = 2;

    private static (ShapeFamily Family, bool Overlap) Read(EmittedApproach a)
    {
        var (family, _) = ShapeClassifier.Classify(WoolBoxEmitter.AsPlan(a), a.WoolRoom.Id);
        var cells = new HashSet<(int, int)>();
        var overlap = false;
        foreach (var p in a.Terrain.Append(a.WoolRoom))
            for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++)
                    if (!cells.Add((x, z))) overlap = true;
        return (family, overlap);
    }

    [Test]
    [Arguments(2, 0)]
    [Arguments(8, 0)]
    [Arguments(0, 3)]
    [Arguments(4, 6)]
    public async Task Scythe_endpoint_shifts_keep_the_family(int entryShift, int woolShift)
    {
        foreach (var flip in new[] { false, true })
        {
            var a = WoolBoxEmitter.Emit(ShapeFamily.Scythe, Box, Cw, flip: flip,
                entryShift: entryShift, woolShift: woolShift);
            var (family, overlap) = Read(a);
            await Assert.That(family).IsEqualTo(ShapeFamily.Scythe);
            await Assert.That(overlap).IsFalse();
            // the template order survives every shift
            await Assert.That(a.Terrain.Select(p => p.Slot).Append(a.WoolRoom.Slot))
                .IsEquivalentTo(ApproachSlots.Template(ShapeFamily.Scythe));
        }
    }

    [Test]
    public async Task A_shifted_entry_takes_the_spine_top_with_it()
    {
        // the docked piece resizes with the shift — a full-height spine over a dropped tail is a
        // different (wrong) shape
        var a = WoolBoxEmitter.Emit(ShapeFamily.Scythe, Box, Cw, entryShift: 4);
        var tail = a.Terrain.Single(p => p.Slot == ApproachSlots.Entry);
        var spine = a.Terrain.Single(p => p.Slot == ApproachSlots.EntryRun);
        await Assert.That(tail.Rect[1]).IsEqualTo(4);
        await Assert.That(spine.Rect[1]).IsEqualTo(4);     // shrunk from the top, flush with the tail
    }

    [Test]
    public async Task A_shifted_wool_takes_the_return_leg_with_it()
    {
        var a = WoolBoxEmitter.Emit(ShapeFamily.Scythe, Box, Cw, woolShift: 3);
        var ret = a.Terrain.Single(p => p.Slot == ApproachSlots.RoomRun);
        await Assert.That(a.WoolRoom.Rect[1]).IsEqualTo(3);
        await Assert.That(ret.Rect[1]).IsEqualTo(3 + WoolBoxEmitter.RoomDepthCells);
    }

    [Test]
    [Arguments(4)]
    [Arguments(6)]
    public async Task Scythe_attachment_width_widens_the_tail_along_the_spine(int aw)
    {
        // the same w2/w4/w6 grammar as the donut's: the tail stacks parallel to the spine it docks,
        // never sticking away perpendicular — and a widened tail is still the same fold
        var a = WoolBoxEmitter.Emit(ShapeFamily.Scythe, Box, Cw, attachmentWidth: aw);
        var tail = a.Terrain.Single(p => p.Slot == ApproachSlots.Entry);
        await Assert.That(tail.Rect[3]).IsEqualTo(aw);     // widened along the docking edge
        await Assert.That(tail.Rect[2]).IsEqualTo(Cw);     // never thickened perpendicular
        var (family, overlap) = Read(a);
        await Assert.That(family).IsEqualTo(ShapeFamily.Scythe);
        await Assert.That(overlap).IsFalse();
    }

    [Test]
    public async Task A_shifted_widened_entry_is_the_general_case()
    {
        var a = WoolBoxEmitter.Emit(ShapeFamily.Scythe, Box, Cw, entryShift: 3, attachmentWidth: 4);
        var (family, overlap) = Read(a);
        await Assert.That(family).IsEqualTo(ShapeFamily.Scythe);
        await Assert.That(overlap).IsFalse();
    }

    [Test]
    [Arguments(2, 0, 1)]
    [Arguments(6, 4, 1)]
    [Arguments(2, 0, 2)]
    public async Task Donut_attachment_slides_along_the_ring_edge(int offset, int aw, int attachments)
    {
        var a = WoolBoxEmitter.Emit(ShapeFamily.Donut, Box, Cw,
            attachments: attachments, attachmentWidth: aw, attachmentOffset: offset);
        var stub = a.Terrain.First(p => p.Slot == ApproachSlots.Entry);
        await Assert.That(stub.Rect[1]).IsEqualTo(offset);
        // only the attachment moves — the ring's bars and legs are byte-identical to the unshifted emission
        var b = WoolBoxEmitter.Emit(ShapeFamily.Donut, Box, Cw, attachments: attachments, attachmentWidth: aw);
        var ringA = a.Terrain.Where(p => p.Slot != ApproachSlots.Entry).Select(p => string.Join(',', p.Rect));
        var ringB = b.Terrain.Where(p => p.Slot != ApproachSlots.Entry).Select(p => string.Join(',', p.Rect));
        await Assert.That(ringA).IsEquivalentTo(ringB);
        var (family, overlap) = Read(a);
        await Assert.That(family).IsEqualTo(ShapeFamily.Donut);
        await Assert.That(overlap).IsFalse();
    }

    [Test]
    [Arguments(ShapeFamily.Z)]
    [Arguments(ShapeFamily.Scythe)]
    public async Task Side_docked_wool_keeps_the_family(ShapeFamily family)
    {
        foreach (var flip in new[] { false, true })
        {
            var a = WoolBoxEmitter.Emit(family, Box, Cw, flip: flip, roomPlacement: RoomPlacement.SideTuck);
            var (read, overlap) = Read(a);
            await Assert.That(read).IsEqualTo(family);
            await Assert.That(overlap).IsFalse();
            // the room hangs perpendicular off the terminal piece — its long axis flips vs the inline room
            await Assert.That(a.WoolRoom.Rect[2]).IsEqualTo(WoolBoxEmitter.RoomDepthCells);
            await Assert.That(a.WoolRoom.Rect[3]).IsEqualTo(Cw);
        }
    }

    [Test]
    public async Task A_side_docked_room_shortens_the_terminal_piece()
    {
        // inline: the return leg stops a room-depth short and the room caps it; side-docked: the leg's top
        // is the room's line — it no longer runs out to hold the room
        var inline_ = WoolBoxEmitter.Emit(ShapeFamily.Scythe, Box, Cw);
        var docked = WoolBoxEmitter.Emit(ShapeFamily.Scythe, Box, Cw, roomPlacement: RoomPlacement.SideTuck);
        var inlineLeg = inline_.Terrain.Single(p => p.Slot == ApproachSlots.RoomRun);
        var dockedLeg = docked.Terrain.Single(p => p.Slot == ApproachSlots.RoomRun);
        await Assert.That(inlineLeg.Rect[1]).IsEqualTo(WoolBoxEmitter.RoomDepthCells);
        await Assert.That(dockedLeg.Rect[1]).IsEqualTo(0);
        await Assert.That(docked.WoolRoom.Rect[0]).IsEqualTo(dockedLeg.Rect[0] + Cw);  // beside, not beyond
    }

    [Test]
    public async Task Side_dock_composes_with_the_endpoint_shifts()
    {
        var a = WoolBoxEmitter.Emit(ShapeFamily.Scythe, Box, Cw,
            roomPlacement: RoomPlacement.SideTuck, entryShift: 3, woolShift: 4, attachmentWidth: 4);
        var (family, overlap) = Read(a);
        await Assert.That(family).IsEqualTo(ShapeFamily.Scythe);
        await Assert.That(overlap).IsFalse();
        await Assert.That(a.WoolRoom.Rect[1]).IsEqualTo(4);
    }

    [Test]
    public async Task Shift_clamps_and_family_guards_throw()
    {
        // a shift that leaves no spine above the bar, a width that overfills the spine (the tail would
        // border the bar and unmake the fold), an offset off the ring's edge, and a shift on a family
        // that has no offsettable endpoints are all rejected, never silently clamped
        await Assert.That(() => WoolBoxEmitter.Emit(ShapeFamily.Scythe, Box, Cw, entryShift: 21))
            .Throws<ComposeException>();
        await Assert.That(() => WoolBoxEmitter.Emit(ShapeFamily.Scythe, Box, Cw, attachmentWidth: 22))
            .Throws<ComposeException>();
        await Assert.That(() => WoolBoxEmitter.Emit(ShapeFamily.Donut, Box, Cw, attachmentOffset: 23))
            .Throws<ComposeException>();
        await Assert.That(() => WoolBoxEmitter.Emit(ShapeFamily.Z, Box, Cw, entryShift: 2))
            .Throws<ComposeException>();
    }
}
