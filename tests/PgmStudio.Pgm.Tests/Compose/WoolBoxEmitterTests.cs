using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The wool-box emitter's <b>part roles</b> (the piece vocabulary, docs/generator/model.md §5.3).
/// Every emitted piece carries its template part (<see cref="Piece.Part"/>); the load-bearing invariant is
/// that a family emits the <b>same parts in the same order</b> as its <see cref="CompoundParts.Template"/> —
/// a stable, named sequence the shift/width/docking rules can target instead of raw geometry.
/// </summary>
public sealed class WoolBoxEmitterTests
{
    // a box comfortably large for every family's turns at laneWidth = 2
    private static (int W, int H) Box => (20, 24);
    private const int LaneWidth = 2;

    // the emitted parts in emit order, terrain then the room (the form the §2 template is written in)
    private static string Slots(Compound a) =>
        string.Join(" · ", a.Pieces.Select(p => p.Part));

    private static string Template(CompoundForm f) => string.Join(" · ", CompoundParts.Template(f));

    [Test]
    [Arguments(CompoundForm.I)]
    [Arguments(CompoundForm.L)]
    [Arguments(CompoundForm.Z)]
    [Arguments(CompoundForm.Scythe)]
    [Arguments(CompoundForm.Clamp)]
    [Arguments(CompoundForm.U)]
    [Arguments(CompoundForm.H)]
    [Arguments(CompoundForm.Donut)]
    public async Task Family_emits_its_template_slots_in_order(CompoundForm family)
    {
        var a = ShapeEmitter.Emit(family, Box.W, Box.H, LaneWidth);
        await Assert.That(Slots(a)).IsEqualTo(Template(family));
    }

    [Test]
    [Arguments(CompoundForm.L)]
    [Arguments(CompoundForm.Z)]
    [Arguments(CompoundForm.Scythe)]
    [Arguments(CompoundForm.H)]
    [Arguments(CompoundForm.Donut)]
    public async Task Flip_preserves_the_part_sequence(CompoundForm family)
    {
        // mirroring across the box's vertical centre changes handedness, never the template
        var normal = ShapeEmitter.Emit(family, Box.W, Box.H, LaneWidth);
        var flipped = ShapeEmitter.Emit(family, Box.W, Box.H, LaneWidth, flip: true);
        await Assert.That(Slots(flipped)).IsEqualTo(Slots(normal));
    }

    [Test]
    public async Task U_and_H_differ_by_exactly_the_room_run_stub()
    {
        // the emitter side of the classifier's overhang test: H inserts a room-run between crossbar and wool
        var u = ShapeEmitter.Emit(CompoundForm.U, Box.W, Box.H, LaneWidth);
        var h = ShapeEmitter.Emit(CompoundForm.H, Box.W, Box.H, LaneWidth);
        await Assert.That(u.Pieces.Any(p => p.Part == CompoundParts.RoomRun)).IsFalse();
        await Assert.That(h.Pieces.Count(p => p.Part == CompoundParts.RoomRun)).IsEqualTo(1);
        // both are the two-leg branch: a bar and two entry legs
        await Assert.That(u.Pieces.Count(p => p.Part == CompoundParts.Entry)).IsEqualTo(2);
        await Assert.That(h.Pieces.Count(p => p.Part == CompoundParts.Entry)).IsEqualTo(2);
    }

    [Test]
    public async Task The_room_piece_carries_the_room_part_and_wool_room_role()
    {
        var a = ShapeEmitter.Emit(CompoundForm.I, Box.W, Box.H, LaneWidth);
        var (pieces, marker) = UnitPieces.Lay(a, 0, 0, "wool", "wool-room", new BoxRef("wool", BoxKind.Wool));
        var room = pieces.Single(p => p.Id == marker!.Piece);
        await Assert.That(room.Part).IsEqualTo(CompoundParts.Room);
        await Assert.That(room.Role).IsEqualTo(PlanRoles.WoolRoom);   // part is distinct from the map role
        // terrain pieces keep the map-level piece role (the part is the shape-internal taxonomy)
        await Assert.That(pieces.Where(p => p.Part != CompoundParts.Room).All(p => p.Role == PlanRoles.Piece)).IsTrue();
    }

    [Test]
    public async Task Donut_second_attachment_adds_another_entry_stub()
    {
        var one = ShapeEmitter.Emit(CompoundForm.Donut, Box.W, Box.H, LaneWidth, attachments: 1);
        var two = ShapeEmitter.Emit(CompoundForm.Donut, Box.W, Box.H, LaneWidth, attachments: 2);
        await Assert.That(one.Pieces.Count(p => p.Part == CompoundParts.Entry)).IsEqualTo(1);
        await Assert.That(two.Pieces.Count(p => p.Part == CompoundParts.Entry)).IsEqualTo(2);
    }

    [Test]
    public async Task Donut_wool_extend_adds_a_run_holding_the_wool_out()
    {
        var tucked = ShapeEmitter.Emit(CompoundForm.Donut, Box.W, Box.H, LaneWidth);
        var held = ShapeEmitter.Emit(CompoundForm.Donut, Box.W, Box.H, LaneWidth, woolExtend: true);
        await Assert.That(tucked.Pieces.Any(p => p.Part == CompoundParts.Run)).IsFalse();
        await Assert.That(held.Pieces.Count(p => p.Part == CompoundParts.Run)).IsEqualTo(1);
    }

    [Test]
    [Arguments(CompoundForm.I)]
    [Arguments(CompoundForm.L)]
    [Arguments(CompoundForm.Z)]
    [Arguments(CompoundForm.Scythe)]
    [Arguments(CompoundForm.Clamp)]
    [Arguments(CompoundForm.U)]
    [Arguments(CompoundForm.H)]
    [Arguments(CompoundForm.Donut)]
    public async Task Piece_count_is_stable_across_sizes(CompoundForm family)
    {
        // a family emits the same number of pieces regardless of box size — collinear pieces are never merged,
        // so "the entry is piece N" stays a usable rule
        var small = ShapeEmitter.Emit(family, 16, 20, LaneWidth);
        var large = ShapeEmitter.Emit(family, 28, 34, LaneWidth);
        await Assert.That(large.Pieces.Count).IsEqualTo(small.Pieces.Count);
        await Assert.That(small.Pieces.Count).IsEqualTo(CompoundParts.Template(family).Count); // +room
    }
}
