using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The emitter's placement knobs: the scythe's two independently-offsettable endpoints (the entry tail and
/// the wool end slide along the docking edge, and the piece each docks — spine / return leg — resizes with
/// the shift), the scythe entry's variable attachment width (measured ALONG the spine it docks, the same
/// w2/w4/w6 grammar as the donut's), and the donut attachment sliding along the ring's edge (only the
/// attachment moves, the ring is unchanged). Every variant must stay well-formed (no overlaps), keep its
/// template part order, and classify back to its family — the knobs move endpoints, never the identity.
/// </summary>
public sealed class EmitterPlacementKnobTests
{
    private static (int W, int H) Box => (20, 24);
    private const int LaneWidth = 2;

    private static (ShapeFamily Family, bool Overlap) Read(Compound a)
    {
        var (family, _) = ShapeClassifier.Classify(UnitPieces.AsPlan(a, "wool-box"), "wa-room");
        var cells = new HashSet<(int, int)>();
        var overlap = false;
        foreach (var p in a.Pieces)
            for (var x = p.Rect.X; x < p.Rect.X + p.Rect.Width; x++)
                for (var z = p.Rect.Z; z < p.Rect.Z + p.Rect.Height; z++)
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
            var a = ShapeEmitter.Emit(ShapeFamily.Scythe, Box.W, Box.H, LaneWidth, flip: flip, entryShift: entryShift, woolShift: woolShift);
            var (family, overlap) = Read(a);
            await Assert.That(family).IsEqualTo(ShapeFamily.Scythe);
            await Assert.That(overlap).IsFalse();
            // the template order survives every shift
            await Assert.That(a.Pieces.Select(p => p.Part!))
                .IsEquivalentTo(CompoundParts.Template(ShapeFamily.Scythe));
        }
    }

    [Test]
    public async Task A_shifted_entry_takes_the_spine_top_with_it()
    {
        // the docked piece resizes with the shift — a full-height spine over a dropped tail is a
        // different (wrong) shape
        var a = ShapeEmitter.Emit(ShapeFamily.Scythe, Box.W, Box.H, LaneWidth, entryShift: 4);
        var tail = a.Pieces.Single(p => p.Part == CompoundParts.Entry);
        var spine = a.Pieces.Single(p => p.Part == CompoundParts.EntryRun);
        await Assert.That(tail.Rect.Z).IsEqualTo(4);
        await Assert.That(spine.Rect.Z).IsEqualTo(4);     // shrunk from the top, flush with the tail
    }

    [Test]
    public async Task A_shifted_wool_takes_the_return_leg_with_it()
    {
        var a = ShapeEmitter.Emit(ShapeFamily.Scythe, Box.W, Box.H, LaneWidth, woolShift: 3);
        var ret = a.Pieces.Single(p => p.Part == CompoundParts.RoomRun);
        await Assert.That(a.Room!.Rect.Z).IsEqualTo(3);
        await Assert.That(ret.Rect.Z).IsEqualTo(3 + WoolBoxEmitter.RoomDepthCells);
    }

    [Test]
    [Arguments(4)]
    [Arguments(6)]
    public async Task Scythe_attachment_width_widens_the_tail_along_the_spine(int aw)
    {
        // the same w2/w4/w6 grammar as the donut's: the tail stacks parallel to the spine it docks,
        // never sticking away perpendicular — and a widened tail is still the same fold
        var a = ShapeEmitter.Emit(ShapeFamily.Scythe, Box.W, Box.H, LaneWidth, attachmentWidth: aw);
        var tail = a.Pieces.Single(p => p.Part == CompoundParts.Entry);
        await Assert.That(tail.Rect.Height).IsEqualTo(aw);     // widened along the docking edge
        await Assert.That(tail.Rect.Width).IsEqualTo(LaneWidth);     // never thickened perpendicular
        var (family, overlap) = Read(a);
        await Assert.That(family).IsEqualTo(ShapeFamily.Scythe);
        await Assert.That(overlap).IsFalse();
    }

    [Test]
    public async Task A_shifted_widened_entry_is_the_general_case()
    {
        var a = ShapeEmitter.Emit(ShapeFamily.Scythe, Box.W, Box.H, LaneWidth, entryShift: 3, attachmentWidth: 4);
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
        var a = ShapeEmitter.Emit(ShapeFamily.Donut, Box.W, Box.H, LaneWidth, attachments: attachments, attachmentWidth: aw, attachmentOffset: offset);
        var stub = a.Pieces.First(p => p.Part == CompoundParts.Entry);
        await Assert.That(stub.Rect.Z).IsEqualTo(offset);
        // only the attachment moves — the ring's bars and legs are byte-identical to the unshifted emission
        var b = ShapeEmitter.Emit(ShapeFamily.Donut, Box.W, Box.H, LaneWidth, attachments: attachments, attachmentWidth: aw);
        var ringA = a.Pieces.Where(p => p.Part is not (CompoundParts.Entry or CompoundParts.Room)).Select(p => string.Join(',', p.Rect));
        var ringB = b.Pieces.Where(p => p.Part is not (CompoundParts.Entry or CompoundParts.Room)).Select(p => string.Join(',', p.Rect));
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
            var a = ShapeEmitter.Emit(family, Box.W, Box.H, LaneWidth, flip: flip, roomPlacement: RoomPlacement.SideTuck);
            var (read, overlap) = Read(a);
            await Assert.That(read).IsEqualTo(family);
            await Assert.That(overlap).IsFalse();
            // the room hangs perpendicular off the room piece — its long axis flips vs the inline room
            await Assert.That(a.Room!.Rect.Width).IsEqualTo(WoolBoxEmitter.RoomDepthCells);
            await Assert.That(a.Room!.Rect.Height).IsEqualTo(LaneWidth);
        }
    }

    [Test]
    public async Task A_side_docked_room_shortens_the_room_run()
    {
        // inline: the return leg stops a room-depth short and the room caps it; side-docked: the leg's top
        // is the room's line — it no longer runs out to hold the room
        var inline_ = ShapeEmitter.Emit(ShapeFamily.Scythe, Box.W, Box.H, LaneWidth);
        var docked = ShapeEmitter.Emit(ShapeFamily.Scythe, Box.W, Box.H, LaneWidth, roomPlacement: RoomPlacement.SideTuck);
        var inlineLeg = inline_.Pieces.Single(p => p.Part == CompoundParts.RoomRun);
        var dockedLeg = docked.Pieces.Single(p => p.Part == CompoundParts.RoomRun);
        await Assert.That(inlineLeg.Rect.Z).IsEqualTo(WoolBoxEmitter.RoomDepthCells);
        await Assert.That(dockedLeg.Rect.Z).IsEqualTo(0);
        await Assert.That(docked.Room!.Rect.X).IsEqualTo(dockedLeg.Rect.X + LaneWidth);  // beside, not beyond
    }

    [Test]
    public async Task Side_dock_composes_with_the_endpoint_shifts()
    {
        var a = ShapeEmitter.Emit(ShapeFamily.Scythe, Box.W, Box.H, LaneWidth, roomPlacement: RoomPlacement.SideTuck, entryShift: 3, woolShift: 4, attachmentWidth: 4);
        var (family, overlap) = Read(a);
        await Assert.That(family).IsEqualTo(ShapeFamily.Scythe);
        await Assert.That(overlap).IsFalse();
        await Assert.That(a.Room!.Rect.Z).IsEqualTo(4);
    }

    [Test]
    public async Task Shift_clamps_and_family_guards_throw()
    {
        // a shift that leaves no spine above the bar, a width that overfills the spine (the tail would
        // border the bar and unmake the fold), an offset off the ring's edge, and a shift on a family
        // that has no offsettable endpoints are all rejected, never silently clamped
        await Assert.That(() => ShapeEmitter.Emit(ShapeFamily.Scythe, Box.W, Box.H, LaneWidth, entryShift: 21))
            .Throws<ArgumentException>();
        await Assert.That(() => ShapeEmitter.Emit(ShapeFamily.Scythe, Box.W, Box.H, LaneWidth, attachmentWidth: 22))
            .Throws<ArgumentException>();
        await Assert.That(() => ShapeEmitter.Emit(ShapeFamily.Donut, Box.W, Box.H, LaneWidth, attachmentOffset: 23))
            .Throws<ArgumentException>();
        await Assert.That(() => ShapeEmitter.Emit(ShapeFamily.Z, Box.W, Box.H, LaneWidth, entryShift: 2))
            .Throws<ArgumentException>();
    }
}
