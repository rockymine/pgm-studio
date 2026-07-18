using PgmStudio.Geom;

namespace PgmStudio.Pgm.Shapes;

/// <summary>The class of a connected negative space around (or inside) a rectilinear body, by how many of the
/// four axis directions the body walls it: <see cref="Hole"/> is fully enclosed (the ring's void — four walls);
/// <see cref="Bay"/> is walled from three directions, open toward one (the staple's recess, the hook's bay);
/// <see cref="Notch"/> from two (the corner an L wraps); <see cref="Open"/> from at most one — plain outside
/// space along a flat side, not a feature of the shape. The escalation notch → bay → hole is the wall count
/// 2 → 3 → 4.</summary>
public enum NegativeSpaceKind { Open, Notch, Bay, Hole }

/// <summary>One connected negative space read off a body: its <see cref="Kind"/>, its cells, and the number of
/// distinct axis directions the body walls it from (<see cref="WallDirections"/> — the wall count behind the
/// kind; a hole is enclosed outright, whatever the count says). Cells are box-local grid cells.</summary>
public sealed record NegativeSpace(NegativeSpaceKind Kind, IReadOnlySet<(int X, int Z)> Cells, int WallDirections);

/// <summary>A maximal straight run of the body's boundary, classified along two independent axes: what it
/// <see cref="Faces"/> (<see cref="NegativeSpaceKind.Open"/> for a free outward edge, else the class of the
/// notch/bay/hole it walls) and who owns it — <see cref="Terminal"/> marks a run on the terminal room's own
/// wall. The owner is a <b>fact</b>; the verdict over it is the docking gate's rule (a terminal wall never
/// receives a dock today — <c>SlotDockRole.NeverDock</c> — with the elevation-stage dock and the clamp's
/// designated room faces as the sanctioned exceptions), so the free offerable surface is exactly the
/// <see cref="NegativeSpaceKind.Open"/> runs with <see cref="Terminal"/> false. Runs split where ownership
/// changes: a room capping a lane splits the shared boundary line into a free terrain interval and a sealed
/// room interval. Coordinates are cell-corner coordinates (a vertical run has <c>X1 == X2</c>, a horizontal one
/// <c>Z1 == Z2</c>); <see cref="Length"/> is the run's extent in cells.</summary>
public sealed record ClassifiedEdge(
    int X1, int Z1, int X2, int Z2, NegativeSpaceKind Faces, int Length, bool Terminal = false);

/// <summary>Everything the edge read of one body yields: its negative <see cref="Spaces"/> and its classified
/// boundary <see cref="Edges"/>.</summary>
public sealed record EdgeClassification(IReadOnlyList<NegativeSpace> Spaces, IReadOnlyList<ClassifiedEdge> Edges);

/// <summary>
/// Reads a rectilinear body's <b>edge taxonomy</b> off its geometry alone: every connected negative space
/// within the body's bounding box, classed by wall count (<see cref="NegativeSpaceKind"/> — notch 2, bay 3,
/// hole enclosed), and every boundary edge classed by the space it faces plus its ownership —
/// <see cref="NegativeSpaceKind.Open"/> runs that are not <see cref="ClassifiedEdge.Terminal"/> are the free
/// outward surface. This is the derive-side generalization of the emit-time
/// <see cref="ShapeVacancy"/> publication: vacancies are exact but box-relative remainders a specific emitter
/// declares; this read is shape-relative and total — it works on any rectangle set (an emitted approach, a
/// terminal-free compound, a future hub body) with no emitter cooperation. The two deliberately disagree on
/// one case: the box remainder beside a straight lane is a published <c>notch</c> vacancy (box-relative), but
/// here it lies outside the shape's own bounding box and is no negative space at all — nothing encases it, so
/// the lane's side is free surface.
///
/// <para>The wall count is read per space, over all its cells: a direction counts as walled when any cell of
/// the space has body terrain as its neighbour in that direction. A space touching no side of the bounding box
/// is enclosed and reads <see cref="NegativeSpaceKind.Hole"/> regardless of count.</para>
/// </summary>
public static class BodyEdges
{
    /// <summary>Classify the union of <paramref name="rects"/> (<c>[x, z, w, h]</c> cell rects).</summary>
    public static EdgeClassification Classify(IEnumerable<int[]> rects)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var r in rects)
            for (var x = r[0]; x < r[0] + r[2]; x++)
                for (var z = r[1]; z < r[1] + r[3]; z++) cells.Add((x, z));
        return Classify(cells);
    }

    /// <summary>Classify a terminal-free body (its structural pieces).</summary>
    public static EdgeClassification Classify(ShapeBody body) => Classify(body.Pieces.Select(p => p.Rect));

    /// <summary>Classify an approach emission as one mass — terrain plus the terminal room. The room takes part
    /// in the walls it forms (a clamp's recess is a bay only because the room closes it; the same two bars
    /// without the room wrap a mere notch), and every boundary run on the room's own wall is marked
    /// <see cref="ClassifiedEdge.Terminal"/>.</summary>
    public static EdgeClassification Classify(EmittedShape shape)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var r in shape.Terrain.Select(p => p.Rect))
            for (var x = r[0]; x < r[0] + r[2]; x++)
                for (var z = r[1]; z < r[1] + r[3]; z++) cells.Add((x, z));
        var terminal = new HashSet<(int, int)>();
        for (var x = shape.Room[0]; x < shape.Room[0] + shape.Room[2]; x++)
            for (var z = shape.Room[1]; z < shape.Room[1] + shape.Room[3]; z++)
            {
                cells.Add((x, z));
                terminal.Add((x, z));
            }
        return Classify(cells, terminal);
    }

    /// <summary>Classify a cell set with no terminal.</summary>
    public static EdgeClassification Classify(IReadOnlySet<(int, int)> cells) =>
        Classify(cells, new HashSet<(int, int)>());

    /// <summary>Classify a cell set, marking boundary runs whose inner cell lies in <paramref name="terminal"/>
    /// (the terminal room's own wall) — runs never merge across the terrain↔terminal ownership change.</summary>
    public static EdgeClassification Classify(IReadOnlySet<(int, int)> cells, IReadOnlySet<(int, int)> terminal)
    {
        if (cells.Count == 0) return new EdgeClassification([], []);
        var (minX, minZ, maxX, maxZ) = Cells.BoundingBox(cells);

        // negative spaces — 4-connected components of the bounding box's complement, each classed by the axis
        // directions the body walls it from (or enclosed outright)
        var spaces = new List<NegativeSpace>();
        var spaceOf = new Dictionary<(int, int), int>();
        for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
            {
                var seed = (x, z);
                if (cells.Contains(seed) || spaceOf.ContainsKey(seed)) continue;
                var comp = new HashSet<(int, int)> { seed };
                var q = new Queue<(int, int)>();
                q.Enqueue(seed);
                spaceOf[seed] = spaces.Count;
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    foreach (var n in Cells.N4(c))
                    {
                        if (n.Item1 < minX || n.Item1 > maxX || n.Item2 < minZ || n.Item2 > maxZ) continue;
                        if (cells.Contains(n) || !comp.Add(n)) continue;
                        spaceOf[n] = spaces.Count;
                        q.Enqueue(n);
                    }
                }
                var enclosed = comp.All(c => c.Item1 > minX && c.Item1 < maxX && c.Item2 > minZ && c.Item2 < maxZ);
                var walls = 0;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (comp.Any(c => cells.Contains((c.Item1 + dx, c.Item2 + dz)))) walls++;
                var kind = enclosed ? NegativeSpaceKind.Hole
                    : walls >= 3 ? NegativeSpaceKind.Bay
                    : walls == 2 ? NegativeSpaceKind.Notch
                    : NegativeSpaceKind.Open;
                spaces.Add(new NegativeSpace(kind, comp, walls));
            }

        // boundary edges — every filled↔empty cell seam, classed by the space behind it (outside the bounding
        // box is open) and by the inner cell's ownership, then merged into maximal same-class same-owner
        // straight runs (a run never continues across the terrain↔terminal change)
        NegativeSpaceKind FacingKind((int, int) n) =>
            spaceOf.TryGetValue(n, out var s) ? spaces[s].Kind : NegativeSpaceKind.Open;
        var vertical = new Dictionary<(int Line, NegativeSpaceKind Kind, bool Terminal), List<int>>();   // line x → unit spans z
        var horizontal = new Dictionary<(int Line, NegativeSpaceKind Kind, bool Terminal), List<int>>(); // line z → unit spans x
        foreach (var (x, z) in cells)
        {
            var own = terminal.Contains((x, z));
            if (!cells.Contains((x + 1, z))) Add(vertical, (x + 1, FacingKind((x + 1, z)), own), z);
            if (!cells.Contains((x - 1, z))) Add(vertical, (x, FacingKind((x - 1, z)), own), z);
            if (!cells.Contains((x, z + 1))) Add(horizontal, (z + 1, FacingKind((x, z + 1)), own), x);
            if (!cells.Contains((x, z - 1))) Add(horizontal, (z, FacingKind((x, z - 1)), own), x);
        }
        var edges = new List<ClassifiedEdge>();
        foreach (var ((line, kind, own), spans) in vertical.OrderBy(e => e.Key.Line).ThenBy(e => e.Key.Kind).ThenBy(e => e.Key.Terminal))
            foreach (var (lo, hi) in Runs(spans))
                edges.Add(new ClassifiedEdge(line, lo, line, hi, kind, hi - lo, own));
        foreach (var ((line, kind, own), spans) in horizontal.OrderBy(e => e.Key.Line).ThenBy(e => e.Key.Kind).ThenBy(e => e.Key.Terminal))
            foreach (var (lo, hi) in Runs(spans))
                edges.Add(new ClassifiedEdge(lo, line, hi, line, kind, hi - lo, own));
        return new EdgeClassification(spaces, edges);
    }

    private static void Add(
        Dictionary<(int, NegativeSpaceKind, bool), List<int>> lines, (int, NegativeSpaceKind, bool) key, int at)
    {
        if (!lines.TryGetValue(key, out var l)) lines[key] = l = [];
        l.Add(at);
    }

    // merge sorted unit spans [at, at+1) into maximal contiguous runs [lo, hi)
    private static IEnumerable<(int Lo, int Hi)> Runs(List<int> spans)
    {
        spans.Sort();
        int lo = spans[0], hi = spans[0] + 1;
        for (var i = 1; i < spans.Count; i++)
        {
            if (spans[i] == hi) { hi++; continue; }
            yield return (lo, hi);
            lo = spans[i]; hi = spans[i] + 1;
        }
        yield return (lo, hi);
    }
}
