using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The span class of a box edge relative to its box: the longer sides are <see cref="Long"/>, the
/// shorter <see cref="Short"/> (docs/contracts/map-generation.md §4). A square box has no short edge — its
/// edges all read <see cref="Long"/>.</summary>
public enum EdgeSpan { Long, Short }

/// <summary>
/// A box edge as a candidate docking <b>interface</b> — the valid-edges data model (§4). It records which
/// edge, its <see cref="Span"/> (long vs short), whether the wool room touches it (<see cref="TouchesRoom"/> —
/// a wool-touching edge <b>never docks</b>, else a dock would seal the wool), and whether terrain reaches it
/// (<see cref="HasTerrain"/> — something to dock <em>to</em>). <see cref="Dockable"/> is the derived verdict.
///
/// <para>It is <b>shape-relative, not box-relative</b>: every field is read off the emitted shape, so it moves
/// <em>with</em> the shape — a room at a different corner, an entry shifted down its edge — rather than naming
/// a fixed box coordinate. This is the vocabulary only: <em>which</em> of a box's dockable edges a given
/// family actually docks, and how many interfaces it demands, are the per-family docking modes (G80) that
/// execute over this model.</para>
/// </summary>
public sealed record BoxEdgeInterface(BoxEdge Edge, EdgeSpan Span, int LengthCells, bool TouchesRoom, bool HasTerrain)
{
    /// <summary>A neighbour may dock this edge: terrain reaches it and the wool room does not seal it (§4).</summary>
    public bool Dockable => HasTerrain && !TouchesRoom;
}

/// <summary>
/// The valid-edges derivation (G41-B): read a box's four edges as candidate <see cref="BoxEdgeInterface"/>s
/// off the emitted shape filling it. This is the data model every fill and pattern binds to — the
/// <b>multi-interface</b> set (a box exposes several edges, not one mouth, retiring the single-mouth
/// assumption) with each edge classified long/short and marked never-dock where the wool room sits. It is
/// universal and shape-relative; the per-family selection of which dockable edges to use, and the docking
/// modes that shift with an entry, are G80's content over this model.
/// </summary>
public static class BoxInterfaces
{
    /// <summary>The four edges of a <paramref name="boxW"/>×<paramref name="boxH"/> box as candidate
    /// interfaces for the emitted <paramref name="shape"/> filling it (box-local cells).</summary>
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

    /// <summary>The <b>dockable</b> subset of <see cref="Of"/> — the interfaces a neighbour may actually claim
    /// (terrain-reached, not wool-sealed). A box may expose more than one: the multi-interface set a family's
    /// docking modes (G80) draw from.</summary>
    public static IReadOnlyList<BoxEdgeInterface> Dockable(EmittedShape shape, int boxW, int boxH) =>
        Of(shape, boxW, boxH).Where(e => e.Dockable).ToList();
}
