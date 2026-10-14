using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The valid-edges data model: <see cref="BoxInterfaces.Of"/> reads a box's four edges off the emitted
/// shape as <see cref="BoxEdgeInterface"/> <b>facts</b> — its length, and the parts of the pieces reaching it,
/// the room included. It observes; it does not judge — the dockability <em>rules</em> over
/// these facts are the docking gate's. Shape-relative: the facts are read from the shape, so they move with the
/// room's position rather than naming a box coordinate.
/// </summary>
public sealed class BoxInterfacesTests
{
    private const int LaneWidth = 2;

    private static IReadOnlyList<BoxEdgeInterface> Edges(CompoundForm family, int w, int h, bool flip = false)
    {
        var shape = ShapeEmitter.Emit(family, w, h, LaneWidth, flip);
        return BoxInterfaces.Of(shape, w, h);
    }

    private static BoxEdgeInterface Edge(IReadOnlyList<BoxEdgeInterface> es, BoxEdge e) => es.Single(x => x.Edge == e);

    private static bool TouchesRoom(BoxEdgeInterface edge) => edge.Parts.Contains(CompoundParts.Room);

    private static bool HasGround(BoxEdgeInterface edge) => edge.Parts.Any(part => part != CompoundParts.Room);

    [Test]
    public async Task I_the_room_edge_is_wool_touched_the_mouth_edge_is_clear_terrain()
    {
        // an I lane: entry on the top edge (the mouth), the wool room on the bottom
        var es = Edges(CompoundForm.I, 6, 12);
        await Assert.That(TouchesRoom(Edge(es, BoxEdge.Bottom))).IsTrue();      // room sits on the bottom edge
        await Assert.That(HasGround(Edge(es, BoxEdge.Top))).IsTrue();          // the entry reaches the top edge
        await Assert.That(TouchesRoom(Edge(es, BoxEdge.Top))).IsFalse();
        // the void side edges reach no terrain (a fact, not a verdict)
        await Assert.That(HasGround(Edge(es, BoxEdge.Left))).IsFalse();
        await Assert.That(HasGround(Edge(es, BoxEdge.Right))).IsFalse();
    }

    [Test]
    public async Task The_slots_on_an_edge_are_the_pieces_that_reach_it()
    {
        // the facts carry the template parts on each edge, room included — the raw observation the gate maps to
        // dock roles. The I's top edge is the entry, its bottom the room, its void sides carry nothing.
        var es = Edges(CompoundForm.I, 6, 12);
        await Assert.That(Edge(es, BoxEdge.Top).Parts).IsEquivalentTo(new[] { CompoundParts.Entry });
        await Assert.That(Edge(es, BoxEdge.Bottom).Parts).IsEquivalentTo(new[] { CompoundParts.Room });
        await Assert.That(Edge(es, BoxEdge.Left).Parts).IsEmpty();
    }

    // the interval facts: an edge carries the per-piece stretches ordered along it — a shape presenting two
    // pieces to one edge yields two disjoint intervals with the gap between them, which the flat part list
    // could never say
    [Test]
    public async Task An_edge_carries_disjoint_intervals_per_piece()
    {
        // the clamp's mouth edge (bottom) holds BOTH leg entries, the bay's gap between them
        var mouth = Edge(Edges(CompoundForm.Clamp, 6, 6), BoxEdge.Bottom);
        await Assert.That(mouth.Intervals.Count).IsEqualTo(2);
        await Assert.That(mouth.Intervals.All(i => i.Part == CompoundParts.Entry)).IsTrue();
        await Assert.That(mouth.Intervals[0].Start + mouth.Intervals[0].Length < mouth.Intervals[1].Start).IsTrue();

        // the U's two legs on its mouth edge
        await Assert.That(Edge(Edges(CompoundForm.U, 6, 6), BoxEdge.Bottom).Intervals.Count).IsEqualTo(2);

        // a single-piece edge is one interval spanning the piece
        var top = Edge(Edges(CompoundForm.I, 6, 12), BoxEdge.Top);
        await Assert.That(top.Intervals.Count).IsEqualTo(1);
        await Assert.That(top.Intervals[0].Part).IsEqualTo(CompoundParts.Entry);
    }

    [Test]
    public async Task An_edge_is_as_long_as_its_side_of_the_box()
    {
        var es = Edges(CompoundForm.I, 6, 12);
        await Assert.That(Edge(es, BoxEdge.Top).LengthCells).IsEqualTo(6);
        await Assert.That(Edge(es, BoxEdge.Left).LengthCells).IsEqualTo(12);
    }

    [Test]
    public async Task Clamp_presents_its_legs_and_touches_the_clamped_wool_on_one_edge()
    {
        // the redefined clamp docks like a U: its two legs present terrain on the bottom mouth (and the sides),
        // and the wool is clamped between them at the top — exactly one edge (the top) touches the wool
        var es = Edges(CompoundForm.Clamp, 6, 6);
        await Assert.That(HasGround(Edge(es, BoxEdge.Bottom))).IsTrue();       // the two-leg mouth
        await Assert.That(Edge(es, BoxEdge.Bottom).Intervals.Count).IsEqualTo(2);
        await Assert.That(es.Count(TouchesRoom)).IsEqualTo(1);          // exactly one edge is wool-touched
        await Assert.That(es.Single(TouchesRoom).Edge).IsEqualTo(BoxEdge.Top);
    }

    [Test]
    public async Task U_the_wool_edge_is_touched_the_leg_edge_is_clear_terrain()
    {
        // U/H enter at the bottom (the legs); the wool caps the crossbar at the top
        var es = Edges(CompoundForm.U, 6, 6);
        await Assert.That(TouchesRoom(Edge(es, BoxEdge.Top))).IsTrue();
        await Assert.That(HasGround(Edge(es, BoxEdge.Bottom))).IsTrue();
        await Assert.That(TouchesRoom(Edge(es, BoxEdge.Bottom))).IsFalse();
    }

    [Test]
    public async Task Facts_are_read_from_the_shape_not_a_fixed_box_edge()
    {
        // flipping the shape mirrors x; the wool-touch facts track where the room actually is, so which edges
        // it touches changes — the facts are read off the shape, not a hard-coded box edge (an L's room caps a
        // corner, touching two edges; the flip moves it to the mirror corner)
        var box = (w: 12, h: 14);
        var normal = BoxInterfaces.Of(ShapeEmitter.Emit(CompoundForm.L, box.w, box.h, LaneWidth, flip: false), box.w, box.h);
        var flipped = BoxInterfaces.Of(ShapeEmitter.Emit(CompoundForm.L, box.w, box.h, LaneWidth, flip: true), box.w, box.h);
        var normalRoom = normal.Where(TouchesRoom).Select(e => e.Edge).ToHashSet();
        var flippedRoom = flipped.Where(TouchesRoom).Select(e => e.Edge).ToHashSet();
        await Assert.That(normalRoom).IsNotEmpty();
        await Assert.That(flippedRoom).IsNotEmpty();
        await Assert.That(normalRoom.SetEquals(flippedRoom)).IsFalse();     // the flip moved the room's edges
        await Assert.That(normal.Any(HasGround)).IsTrue();
        await Assert.That(flipped.Any(HasGround)).IsTrue();
    }
}
