using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The span class of a box edge relative to its box: the longer sides are <see cref="Long"/>, the
/// shorter <see cref="Short"/> (docs/contracts/map-generation.md §4). A square box has no short edge — its
/// edges all read <see cref="Long"/>.</summary>
public enum EdgeSpan { Long, Short }

/// <summary>
/// The <b>observed facts</b> about one box edge, read off the emitted shape (§4): which edge, its
/// <see cref="Span"/> (long vs short), whether the wool room touches it (<see cref="TouchesRoom"/>), and
/// whether terrain reaches it (<see cref="HasTerrain"/>). These are neutral observations — <b>no policy</b>.
///
/// <para>Whether an edge may actually <em>receive a dock</em> is a <b>rule, not a fact</b>: it needs terrain
/// to dock to, must not seal the wool, and must be an entry edge the family exposes in the right count/span.
/// Those rules are the <b>G80 docking gate</b>, applied over these facts — deliberately not baked in here, so
/// every docking rule lives in one place (a room edge that is legally docked at the elevation stage, G81, is
/// exactly why "room ⇒ never-dock" is a policy, not a fact). It is <b>shape-relative</b>: every field is read
/// off the shape, so the facts move with it — a room at a different corner, a flipped handedness.</para>
/// </summary>
public sealed record BoxEdgeInterface(BoxEdge Edge, EdgeSpan Span, int LengthCells, bool TouchesRoom, bool HasTerrain);

/// <summary>
/// The valid-edges <b>data model</b> (G41-B): read a box's four edges as <see cref="BoxEdgeInterface"/>
/// <b>facts</b> off the emitted shape filling it. It <b>observes; it does not judge</b> — the multi-interface
/// vocabulary a box exposes (four edges, each classified long/short with wool-room contact and terrain reach),
/// retiring the single-mouth assumption. The <b>rules</b> that turn these facts into a dockability verdict —
/// which edges are legal docks, which slot edges never connect, how many a family demands — are the G80
/// docking gate over this model.
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
        BoxEdgeInterface Edge(BoxEdge e, int length, int perpendicular, Func<int[], bool> on) =>
            new(e, length >= perpendicular ? EdgeSpan.Long : EdgeSpan.Short, length,
                on(room), terrain.Any(t => on(t.Rect)));
        return
        [
            Edge(BoxEdge.Top,    boxW, boxH, r => r[1] == 0),
            Edge(BoxEdge.Bottom, boxW, boxH, r => r[1] + r[3] == boxH),
            Edge(BoxEdge.Left,   boxH, boxW, r => r[0] == 0),
            Edge(BoxEdge.Right,  boxH, boxW, r => r[0] + r[2] == boxW),
        ];
    }
}
