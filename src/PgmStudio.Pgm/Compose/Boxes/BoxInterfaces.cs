using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The span class of a box edge relative to its box: the longer sides are <see cref="Long"/>, the
/// shorter <see cref="Short"/> (docs/contracts/map-generation.md §4). A square box has no short edge — its
/// edges all read <see cref="Long"/>.</summary>
public enum EdgeSpan { Long, Short }

/// <summary>
/// The <b>observed facts</b> about one box edge, read off the emitted shape (§4): which edge, its
/// <see cref="Span"/> (long vs short), and the <see cref="Slots"/> the fill puts on it — the template roles
/// (<see cref="ApproachSlots"/>) of the pieces whose rects reach the edge, the room included as
/// <see cref="ApproachSlots.Room"/>. These are neutral observations — <b>no policy</b>. <see cref="TouchesRoom"/>
/// and <see cref="HasTerrain"/> are convenience reads over <see cref="Slots"/>.
///
/// <para>Whether an edge may actually <em>receive a dock</em> is a <b>rule, not a fact</b>: it must land on an
/// entry (docking) slot, must not seal the wool, and must satisfy the family's span/count demand. Those rules
/// are the <b>G80 docking gate</b> (<see cref="DockingGate"/>), which maps these slots to dock roles and
/// applies the demand — deliberately not baked in here, so every docking rule lives in one place (a room edge
/// is legally docked at the elevation stage, G81, which is exactly why "room ⇒ never-dock" is a policy, not a
/// fact). It is <b>shape-relative</b>: the slots are read off the shape, so the facts move with it — a room at
/// a different corner, an entry shift, a flipped handedness.</para>
/// </summary>
public sealed record BoxEdgeInterface(BoxEdge Edge, EdgeSpan Span, int LengthCells, IReadOnlyList<string> Slots)
{
    /// <summary>The wool room reaches this edge (its rect is flush with it). A convenience read over
    /// <see cref="Slots"/>; the "room ⇒ never-dock" verdict over it is the gate's, not this fact's.</summary>
    public bool TouchesRoom => Slots.Contains(ApproachSlots.Room);

    /// <summary>Walkable terrain (any non-room slot) reaches this edge. A convenience read over
    /// <see cref="Slots"/>.</summary>
    public bool HasTerrain => Slots.Any(s => s != ApproachSlots.Room);
}

/// <summary>
/// The valid-edges <b>data model</b> (G41-B): read a box's four edges as <see cref="BoxEdgeInterface"/>
/// <b>facts</b> off the emitted shape filling it. It <b>observes; it does not judge</b> — the multi-interface
/// vocabulary a box exposes (four edges, each classified long/short and carrying the template slots on it),
/// retiring the single-mouth assumption. The <b>rules</b> that turn these facts into a dockability verdict —
/// which edges are legal docks, which slots never connect, how many a family demands — are the
/// <see cref="DockingGate"/> (G80) over this model.
/// </summary>
public static class BoxInterfaces
{
    /// <summary>The four edges of a <paramref name="boxW"/>×<paramref name="boxH"/> box as
    /// <see cref="BoxEdgeInterface"/> facts for the emitted <paramref name="shape"/> filling it (box-local
    /// cells). Every edge is returned — the gate (G80) decides which are dockable.</summary>
    public static IReadOnlyList<BoxEdgeInterface> Of(EmittedShape shape, int boxW, int boxH)
    {
        var room = shape.Room;
        var terrain = shape.Terrain;
        BoxEdgeInterface Edge(BoxEdge e, int length, int perpendicular, Func<int[], bool> on)
        {
            var slots = terrain.Where(t => on(t.Rect)).Select(t => t.Slot).ToList();
            if (on(room)) slots.Add(ApproachSlots.Room);
            return new(e, length >= perpendicular ? EdgeSpan.Long : EdgeSpan.Short, length, slots);
        }
        return
        [
            Edge(BoxEdge.Top,    boxW, boxH, r => r[1] == 0),
            Edge(BoxEdge.Bottom, boxW, boxH, r => r[1] + r[3] == boxH),
            Edge(BoxEdge.Left,   boxH, boxW, r => r[0] == 0),
            Edge(BoxEdge.Right,  boxH, boxW, r => r[0] + r[2] == boxW),
        ];
    }
}
