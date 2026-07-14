using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The wool-box emitter's <b>slot roles</b> (the piece vocabulary, docs/contracts/map-generation.md §5.3).
/// Every emitted piece carries its template slot (<see cref="GrownPiece.Slot"/>); the load-bearing invariant is
/// that a family emits the <b>same slots in the same order</b> as its <see cref="ApproachSlots.Template"/> —
/// a stable, named sequence the shift/width/docking rules can target instead of raw geometry.
/// </summary>
public sealed class WoolBoxEmitterTests
{
    // a box comfortably large for every family's turns at cw = 2
    private static WoolBox Box => new(0, 0, 20, 24);
    private const int Cw = 2;

    // the emitted slots in emit order, terrain then the room (the form the §2 template is written in)
    private static string Slots(EmittedApproach a) =>
        string.Join(" · ", a.Terrain.Select(p => p.Slot).Append(a.WoolRoom.Slot));

    private static string Template(ShapeFamily f) => string.Join(" · ", ApproachSlots.Template(f));

    [Test]
    [Arguments(ShapeFamily.I)]
    [Arguments(ShapeFamily.L)]
    [Arguments(ShapeFamily.Z)]
    [Arguments(ShapeFamily.Scythe)]
    [Arguments(ShapeFamily.Clamp)]
    [Arguments(ShapeFamily.U)]
    [Arguments(ShapeFamily.H)]
    [Arguments(ShapeFamily.Donut)]
    public async Task Family_emits_its_template_slots_in_order(ShapeFamily family)
    {
        var a = WoolBoxEmitter.Emit(family, Box, Cw);
        await Assert.That(Slots(a)).IsEqualTo(Template(family));
    }

    [Test]
    [Arguments(ShapeFamily.L)]
    [Arguments(ShapeFamily.Z)]
    [Arguments(ShapeFamily.Scythe)]
    [Arguments(ShapeFamily.H)]
    [Arguments(ShapeFamily.Donut)]
    public async Task Flip_preserves_the_slot_sequence(ShapeFamily family)
    {
        // mirroring across the box's vertical centre changes handedness, never the template
        var normal = WoolBoxEmitter.Emit(family, Box, Cw);
        var flipped = WoolBoxEmitter.Emit(family, Box, Cw, flip: true);
        await Assert.That(Slots(flipped)).IsEqualTo(Slots(normal));
    }

    [Test]
    public async Task U_and_H_differ_by_exactly_the_room_run_stub()
    {
        // the emitter side of the classifier's overhang test: H inserts a room-run between crossbar and wool
        var u = WoolBoxEmitter.Emit(ShapeFamily.U, Box, Cw);
        var h = WoolBoxEmitter.Emit(ShapeFamily.H, Box, Cw);
        await Assert.That(u.Terrain.Any(p => p.Slot == ApproachSlots.RoomRun)).IsFalse();
        await Assert.That(h.Terrain.Count(p => p.Slot == ApproachSlots.RoomRun)).IsEqualTo(1);
        // both are the two-leg branch: a bar and two entry legs
        await Assert.That(u.Terrain.Count(p => p.Slot == ApproachSlots.Entry)).IsEqualTo(2);
        await Assert.That(h.Terrain.Count(p => p.Slot == ApproachSlots.Entry)).IsEqualTo(2);
    }

    [Test]
    public async Task The_room_piece_carries_the_room_slot_and_wool_room_role()
    {
        var a = WoolBoxEmitter.Emit(ShapeFamily.I, Box, Cw);
        await Assert.That(a.WoolRoom.Slot).IsEqualTo(ApproachSlots.Room);
        await Assert.That(a.WoolRoom.Role).IsEqualTo(PlanRoles.WoolRoom);   // slot is distinct from the map role
        // terrain pieces keep the map-level piece role (the slot is the shape-internal taxonomy)
        await Assert.That(a.Terrain.All(p => p.Role == PlanRoles.Piece)).IsTrue();
    }

    [Test]
    public async Task Donut_second_attachment_adds_another_entry_stub()
    {
        var one = WoolBoxEmitter.Emit(ShapeFamily.Donut, Box, Cw, attachments: 1);
        var two = WoolBoxEmitter.Emit(ShapeFamily.Donut, Box, Cw, attachments: 2);
        await Assert.That(one.Terrain.Count(p => p.Slot == ApproachSlots.Entry)).IsEqualTo(1);
        await Assert.That(two.Terrain.Count(p => p.Slot == ApproachSlots.Entry)).IsEqualTo(2);
    }

    [Test]
    public async Task Donut_wool_extend_adds_a_run_holding_the_wool_out()
    {
        var tucked = WoolBoxEmitter.Emit(ShapeFamily.Donut, Box, Cw);
        var held = WoolBoxEmitter.Emit(ShapeFamily.Donut, Box, Cw, woolExtend: true);
        await Assert.That(tucked.Terrain.Any(p => p.Slot == ApproachSlots.Run)).IsFalse();
        await Assert.That(held.Terrain.Count(p => p.Slot == ApproachSlots.Run)).IsEqualTo(1);
    }

    [Test]
    [Arguments(ShapeFamily.I)]
    [Arguments(ShapeFamily.L)]
    [Arguments(ShapeFamily.Z)]
    [Arguments(ShapeFamily.Scythe)]
    [Arguments(ShapeFamily.Clamp)]
    [Arguments(ShapeFamily.U)]
    [Arguments(ShapeFamily.H)]
    [Arguments(ShapeFamily.Donut)]
    public async Task Piece_count_is_stable_across_sizes(ShapeFamily family)
    {
        // a family emits the same number of pieces regardless of box size — collinear pieces are never merged,
        // so "the entry is piece N" stays a usable rule
        var small = WoolBoxEmitter.Emit(family, new WoolBox(0, 0, 16, 20), Cw);
        var large = WoolBoxEmitter.Emit(family, new WoolBox(0, 0, 28, 34), Cw);
        await Assert.That(large.Terrain.Count).IsEqualTo(small.Terrain.Count);
        await Assert.That(small.Terrain.Count + 1).IsEqualTo(ApproachSlots.Template(family).Count); // +room
    }
}
