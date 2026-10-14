using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The <b>observed facts</b> about one box edge, read off the compound filling the box (§4): which edge, its
/// length in cells, and its <see cref="Intervals"/> — the per-piece stretches on the edge, ordered along it, each
/// carrying the part (<see cref="CompoundParts"/>) of the piece presenting it, the room included. Whether an edge
/// may receive a dock is a rule over these facts, and the rules are the <see cref="DockingGate"/>'s. The facts
/// are read off the compound, so they move with it — a room at a different corner, an entry shift, a flip.
/// </summary>
public sealed record BoxEdgeInterface(BoxEdge Edge, int LengthCells, IReadOnlyList<EdgeInterval> Intervals)
{
    /// <summary>The parts on this edge, one per interval — the flat view the gate's per-part rules read.</summary>
    public IReadOnlyList<string> Parts => Intervals.Select(i => i.Part!).ToList();
}

/// <summary>
/// The valid-edges <b>data model</b>: read a box's four edges as
/// <see cref="BoxEdgeInterface"/> <b>facts</b> off the compound filling it. It <b>observes; it does not
/// judge</b> — each edge carries the per-piece intervals on it. The <b>rules</b> that turn these facts into a dockability verdict — which edges are legal docks, which
/// parts never connect, how many a family demands — are the <see cref="DockingGate"/> over this model.
/// The box-perimeter sibling of the shape-relative boundary read (<see cref="CompoundEdges"/>): this reads the
/// box's four edge lines, that reads the shape's whole classified outline — the two agree wherever the shape
/// reaches the box edge.
/// </summary>
public static class BoxInterfaces
{
    /// <summary>The four edges of a <paramref name="boxW"/>×<paramref name="boxH"/> box as
    /// <see cref="BoxEdgeInterface"/> facts for the <paramref name="compound"/> filling it (box-local cells), its
    /// room included where it has one. Every edge is returned — the gate decides which are dockable.</summary>
    public static IReadOnlyList<BoxEdgeInterface> Of(Compound compound, int boxW, int boxH)
    {
        BoxEdgeInterface Edge(BoxEdge e, int length, Func<CellRect, bool> on, Func<CellRect, (int Start, int Len)> along)
        {
            var intervals = new List<EdgeInterval>();
            foreach (var (r, part) in compound.Pieces)
                if (on(r)) { var (start, len) = along(r); intervals.Add(new EdgeInterval(e, start, len, part)); }
            intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
            return new(e, length, intervals);
        }
        (int, int) AlongX(CellRect r) => (r.X, r.Width);
        (int, int) AlongZ(CellRect r) => (r.Z, r.Height);
        return
        [
            Edge(BoxEdge.Top,    boxW, r => r.Z == 0, AlongX),
            Edge(BoxEdge.Bottom, boxW, r => r.Z + r.Height == boxH, AlongX),
            Edge(BoxEdge.Left,   boxH, r => r.X == 0, AlongZ),
            Edge(BoxEdge.Right,  boxH, r => r.X + r.Width == boxW, AlongZ),
        ];
    }

    /// <summary>Merge an edge's per-piece <see cref="EdgeInterval"/>s into contiguous <b>runs</b> — a ring's
    /// corner + leg + corner, or a spine + its arm, is one attachable surface, not one per piece; a genuine gap
    /// (a U's bay between its two legs) stays two runs. The offers a box publishes are runs, not raw piece
    /// intervals, and a run carries no part.</summary>
    public static IReadOnlyList<EdgeInterval> Runs(IReadOnlyList<EdgeInterval> intervals)
    {
        var runs = new List<EdgeInterval>();
        foreach (var iv in intervals.OrderBy(i => i.Start))
            if (runs.Count > 0 && iv.Start <= runs[^1].End)
            {
                var last = runs[^1];
                runs[^1] = last with { Length = Math.Max(last.End, iv.End) - last.Start };
            }
            else runs.Add(iv with { Part = null });
        return runs;
    }
}
