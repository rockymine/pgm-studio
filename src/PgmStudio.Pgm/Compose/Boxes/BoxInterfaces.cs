using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The span class of a box edge relative to its box: the longer sides are <see cref="Long"/>, the
/// shorter <see cref="Short"/> (docs/contracts/map-generation.md §4). A square box has no short edge — its
/// edges all read <see cref="Long"/>.</summary>
public enum EdgeSpan { Long, Short }

/// <summary>One <b>interval</b> of a box edge — the stretch a single piece presents to it: the interval's
/// <see cref="Start"/> along the edge (box-local cells; x for a horizontal edge, z for a vertical one), its
/// <see cref="LengthCells"/>, and the template <see cref="Slot"/> of the piece (the terminal room as the room
/// slot). The §1.5 interface primitive at fact level: a shape whose face is two arm tips presents <b>two
/// disjoint intervals on one edge</b> with the recess between them — which the flat slot list could never
/// say, and which the multi-interval docking and offer rules bind to.</summary>
public sealed record EdgeInterval(int Start, int LengthCells, string Slot);

/// <summary>
/// The <b>observed facts</b> about one box edge, read off the emitted shape (§4): which edge, its
/// <see cref="Span"/> (long vs short), and its <see cref="Intervals"/> — the per-piece stretches on the edge,
/// ordered along it, each carrying the template slot (<see cref="ApproachSlots"/>) of the piece presenting it,
/// the room included. These are neutral observations — <b>no policy</b>. <see cref="Slots"/> is the flat
/// per-interval slot view the gate's rules read; <see cref="TouchesRoom"/> and <see cref="HasTerrain"/> are
/// convenience reads over it.
///
/// <para>Whether an edge may actually <em>receive a dock</em> is a <b>rule, not a fact</b>: it must land on an
/// entry (docking) slot, must not seal the wool, and must satisfy the family's span/count demand. Those rules
/// are the <b>G80 docking gate</b> (<see cref="DockingGate"/>), which maps these slots to dock roles and
/// applies the demand — deliberately not baked in here, so every docking rule lives in one place (a room edge
/// is legally docked at the elevation stage, G81, which is exactly why "room ⇒ never-dock" is a policy, not a
/// fact). It is <b>shape-relative</b>: the intervals are read off the shape, so the facts move with it — a
/// room at a different corner, an entry shift, a flipped handedness.</para>
/// </summary>
public sealed record BoxEdgeInterface(BoxEdge Edge, EdgeSpan Span, int LengthCells, IReadOnlyList<EdgeInterval> Intervals)
{
    /// <summary>The slots on this edge, one per interval — the flat view the gate's per-slot rules read.</summary>
    public IReadOnlyList<string> Slots => Intervals.Select(i => i.Slot).ToList();

    /// <summary>The wool room reaches this edge (its rect is flush with it). A convenience read over
    /// <see cref="Intervals"/>; the "room ⇒ never-dock" verdict over it is the gate's, not this fact's.</summary>
    public bool TouchesRoom => Intervals.Any(i => i.Slot == ApproachSlots.Room);

    /// <summary>Walkable terrain (any non-room interval) reaches this edge. A convenience read over
    /// <see cref="Intervals"/>.</summary>
    public bool HasTerrain => Intervals.Any(i => i.Slot != ApproachSlots.Room);
}

/// <summary>
/// The valid-edges <b>data model</b> (G41-B, intervals G93): read a box's four edges as
/// <see cref="BoxEdgeInterface"/> <b>facts</b> off the emitted shape filling it. It <b>observes; it does not
/// judge</b> — the multi-interval vocabulary a box exposes (four edges, each classified long/short and
/// carrying the per-piece intervals on it), retiring both the single-mouth assumption and the flat slot list.
/// The <b>rules</b> that turn these facts into a dockability verdict — which edges are legal docks, which
/// slots never connect, how many a family demands — are the <see cref="DockingGate"/> (G80) over this model.
/// The box-perimeter sibling of the shape-relative boundary read (<see cref="BodyEdges"/>): this reads the
/// box's four edge lines, that reads the shape's whole classified outline — the two agree wherever the shape
/// reaches the box edge.
/// </summary>
public static class BoxInterfaces
{
    /// <summary>The four edges of a <paramref name="boxW"/>×<paramref name="boxH"/> box as
    /// <see cref="BoxEdgeInterface"/> facts for the emitted <paramref name="shape"/> filling it (box-local
    /// cells). Every edge is returned — the gate (G80) decides which are dockable.</summary>
    public static IReadOnlyList<BoxEdgeInterface> Of(EmittedShape shape, int boxW, int boxH) =>
        Of(shape.Terrain, shape.Room, boxW, boxH);

    /// <summary>The four edges of a <b>terminal-free</b> <see cref="ShapeBody"/> filling a
    /// <paramref name="boxW"/>×<paramref name="boxH"/> box — the hub/frontline read. Same edge facts as the
    /// approach overload, but <b>no room</b> to fold in (a body has no terminal), so every interval is a terrain
    /// piece: the offerable surface a designation publishes its <see cref="EdgeOffer"/>s over.</summary>
    public static IReadOnlyList<BoxEdgeInterface> Of(ShapeBody body, int boxW, int boxH) =>
        Of(body.Pieces, null, boxW, boxH);

    private static IReadOnlyList<BoxEdgeInterface> Of(
        IReadOnlyList<(int[] Rect, string Slot)> terrain, int[]? room, int boxW, int boxH)
    {
        BoxEdgeInterface Edge(BoxEdge e, int length, int perpendicular, Func<int[], bool> on, Func<int[], (int Start, int Len)> along)
        {
            var intervals = new List<EdgeInterval>();
            foreach (var (r, slot) in terrain)
                if (on(r)) { var (s, l) = along(r); intervals.Add(new EdgeInterval(s, l, slot)); }
            if (room is not null && on(room)) { var (s, l) = along(room); intervals.Add(new EdgeInterval(s, l, ApproachSlots.Room)); }
            intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
            return new(e, length >= perpendicular ? EdgeSpan.Long : EdgeSpan.Short, length, intervals);
        }
        (int, int) AlongX(int[] r) => (r[0], r[2]);
        (int, int) AlongZ(int[] r) => (r[1], r[3]);
        return
        [
            Edge(BoxEdge.Top,    boxW, boxH, r => r[1] == 0, AlongX),
            Edge(BoxEdge.Bottom, boxW, boxH, r => r[1] + r[3] == boxH, AlongX),
            Edge(BoxEdge.Left,   boxH, boxW, r => r[0] == 0, AlongZ),
            Edge(BoxEdge.Right,  boxH, boxW, r => r[0] + r[2] == boxW, AlongZ),
        ];
    }
}
