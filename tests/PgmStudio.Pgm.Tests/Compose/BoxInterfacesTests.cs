using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The valid-edges data model (G41-B): <see cref="BoxInterfaces.Of"/> reads a box's four edges off the
/// emitted shape as candidate <see cref="BoxEdgeInterface"/>s — long/short span, wool-touching (never-dock),
/// terrain reach — the multi-interface set G80's per-family docking modes execute over. Shape-relative: the
/// verdict is read from the shape, so it moves with the room's position rather than naming a box coordinate.
/// </summary>
public sealed class BoxInterfacesTests
{
    private const int Cw = 2;

    private static IReadOnlyList<BoxEdgeInterface> Edges(ShapeFamily family, int w, int h, bool flip = false)
    {
        var shape = ShapeEmitter.Emit(family, w, h, Cw, flip);
        return BoxInterfaces.Of(shape, w, h);
    }

    private static BoxEdgeInterface Edge(IReadOnlyList<BoxEdgeInterface> es, BoxEdge e) => es.Single(x => x.Edge == e);

    [Test]
    public async Task I_docks_its_mouth_edge_the_room_edge_never_docks()
    {
        // an I lane: entry on the top edge (the mouth), the wool room on the bottom → bottom is never-dock
        var es = Edges(ShapeFamily.I, 6, 12);
        await Assert.That(Edge(es, BoxEdge.Top).Dockable).IsTrue();
        await Assert.That(Edge(es, BoxEdge.Bottom).TouchesRoom).IsTrue();
        await Assert.That(Edge(es, BoxEdge.Bottom).Dockable).IsFalse();
        // the void side edges reach no terrain, so nothing docks them either
        await Assert.That(Edge(es, BoxEdge.Left).Dockable).IsFalse();
        await Assert.That(BoxInterfaces.Dockable(ShapeEmitter.Emit(ShapeFamily.I, 6, 12, Cw), 6, 12))
            .IsEquivalentTo(new[] { Edge(es, BoxEdge.Top) });
    }

    [Test]
    public async Task Span_is_long_on_the_boxs_longer_sides()
    {
        // a 6×12 box: the 6-cell top/bottom edges are short, the 12-cell left/right are long
        var es = Edges(ShapeFamily.I, 6, 12);
        await Assert.That(Edge(es, BoxEdge.Top).Span).IsEqualTo(EdgeSpan.Short);
        await Assert.That(Edge(es, BoxEdge.Left).Span).IsEqualTo(EdgeSpan.Long);
    }

    [Test]
    public async Task Clamp_exposes_two_dockable_short_edges_its_room_edge_sealed()
    {
        // the clamp's two bars sit on the short top/bottom edges (the entries); the wool room seals one side
        var es = Edges(ShapeFamily.Clamp, 4, 5);
        await Assert.That(Edge(es, BoxEdge.Top).Span).IsEqualTo(EdgeSpan.Short);
        await Assert.That(Edge(es, BoxEdge.Top).Dockable).IsTrue();
        await Assert.That(Edge(es, BoxEdge.Bottom).Span).IsEqualTo(EdgeSpan.Short);
        await Assert.That(Edge(es, BoxEdge.Bottom).Dockable).IsTrue();
        // the room bridges to one long side → that edge never docks (the multi-interface set is the rest)
        var sealed_ = es.Single(e => e.TouchesRoom);
        await Assert.That(sealed_.Dockable).IsFalse();
        await Assert.That(BoxInterfaces.Dockable(ShapeEmitter.Emit(ShapeFamily.Clamp, 4, 5, Cw), 4, 5).Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task U_docks_the_leg_edge_the_wool_edge_never_docks()
    {
        // U/H enter at the bottom (the legs); the wool caps the crossbar at the top → top never-docks
        var es = Edges(ShapeFamily.U, 6, 6);
        await Assert.That(Edge(es, BoxEdge.Top).TouchesRoom).IsTrue();
        await Assert.That(Edge(es, BoxEdge.Top).Dockable).IsFalse();
        await Assert.That(Edge(es, BoxEdge.Bottom).Dockable).IsTrue();
    }

    [Test]
    public async Task Validity_moves_with_the_shape_not_a_fixed_box_edge()
    {
        // flipping the shape mirrors x; the never-dock verdict tracks where the room actually is, so the
        // dockable set is read from the shape, never a hard-coded edge
        var box = (w: 12, h: 14);
        var normal = BoxInterfaces.Dockable(ShapeEmitter.Emit(ShapeFamily.L, box.w, box.h, Cw, flip: false), box.w, box.h);
        var flipped = BoxInterfaces.Dockable(ShapeEmitter.Emit(ShapeFamily.L, box.w, box.h, Cw, flip: true), box.w, box.h);
        // both are non-empty and every dockable edge genuinely has terrain and no room contact
        await Assert.That(normal).IsNotEmpty();
        await Assert.That(flipped).IsNotEmpty();
        foreach (var e in normal.Concat(flipped))
        {
            await Assert.That(e.HasTerrain).IsTrue();
            await Assert.That(e.TouchesRoom).IsFalse();
        }
    }
}
