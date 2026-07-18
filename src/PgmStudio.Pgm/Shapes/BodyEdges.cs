using PgmStudio.Geom;

namespace PgmStudio.Pgm.Shapes;

/// <summary>The class of a connected negative space around (or inside) a rectilinear body, by how many of the
/// four axis directions the body walls it: <see cref="Hole"/> is fully enclosed (the ring's void — four walls);
/// <see cref="Bay"/> is walled from three directions, open toward one (the staple's recess, the hook's bay);
/// <see cref="Notch"/> from two (the corner an L wraps); <see cref="Open"/> from at most one — plain outside
/// space along a flat side, not a feature of the shape. The escalation notch → bay → hole is the wall count
/// 2 → 3 → 4.</summary>
public enum NegativeSpaceKind { Open, Notch, Bay, Hole }

/// <summary>One rectangle of a negative space's slab decomposition, classed by its <b>own</b> body walls —
/// counting only real terrain, never sibling parts. The layer on top of the space class: a non-rectangular
/// space is itself a compound of rectangles (the uneven branch's six-edge bay is a U — a bar at the mouth plus
/// two legs), and classing each part separately is what lets a rule reach an inset feature — the bar part at
/// the mouth borders the shorter arm's end at notch grade, so "attach to the inset leg's tip" becomes
/// stateable while the space-level class stays correct.</summary>
public sealed record NegativeSpacePart(int[] Rect, NegativeSpaceKind Kind, bool Guarded = false);

/// <summary>One connected negative space read off a body: its <see cref="Kind"/>, its cells, the number of
/// distinct axis directions the body walls it from (<see cref="WallDirections"/> — the wall count behind the
/// kind; a hole is enclosed outright, whatever the count says), its <see cref="Parts"/> — the slab
/// decomposition into rectangles, each classed by its own body walls (a rectangular space is its own single
/// part) — and its <see cref="Form"/>: the space's <b>own compound identity</b> (the void is a body too — the
/// uneven branch's six-edge bay reads as a two-arm spine, the Π it is), null when the space classifies to no
/// compound. Cells are box-local grid cells.</summary>
public sealed record NegativeSpace(
    NegativeSpaceKind Kind, IReadOnlySet<(int X, int Z)> Cells, int WallDirections,
    IReadOnlyList<NegativeSpacePart> Parts, CompoundRead? Form);

/// <summary>A maximal straight run of the body's boundary, classified along two independent axes: what it
/// <see cref="Faces"/> (<see cref="NegativeSpaceKind.Open"/> for a free outward edge, else the class of the
/// notch/bay/hole it walls) and who owns it — <see cref="Terminal"/> marks a run on the terminal room's own
/// wall. The owner is a <b>fact</b>; the verdict over it is the docking gate's rule (a terminal wall never
/// receives a dock today — <c>SlotDockRole.NeverDock</c> — with the elevation-stage dock and the clamp's
/// designated room faces as the sanctioned exceptions), so the free offerable surface is exactly the
/// <see cref="NegativeSpaceKind.Open"/> runs with <see cref="Terminal"/> false. Runs split where ownership
/// changes: a room capping a lane splits the shared boundary line into a free terrain interval and a sealed
/// room interval. <see cref="Guarded"/> is the third axis — the run lies inside the terminal's <b>clearance
/// margin</b> (the room inflated by the corridor minimum): sealed by rule even on terrain, because a piece
/// docked there would sit too close to the room and alter the approach the emitter designed. Coordinates are
/// cell-corner coordinates (a vertical run has <c>X1 == X2</c>, a horizontal one <c>Z1 == Z2</c>);
/// <see cref="Length"/> is the run's extent in cells.</summary>
public sealed record ClassifiedEdge(
    int X1, int Z1, int X2, int Z2, NegativeSpaceKind Faces, int Length, bool Terminal = false, bool Guarded = false);

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

    /// <summary>The default clearance margin around a terminal room, in cells — two cells is the 10-block
    /// corridor minimum, the smallest gap that keeps a docked piece from crowding the room.</summary>
    public const int DefaultClearanceCells = 2;

    /// <summary>Classify an approach emission as one mass — terrain plus the terminal room. The room takes part
    /// in the walls it forms (a clamp's recess is a bay only because the room closes it; the same two bars
    /// without the room wrap a mere notch), and every boundary run on the room's own wall is marked
    /// <see cref="ClassifiedEdge.Terminal"/>.</summary>
    public static EdgeClassification Classify(EmittedShape shape)
    {
        var (cells, terminal) = EmissionCells(shape);
        return Classify(cells, terminal, clearance: null);
    }

    /// <summary>Classify an approach emission with the terminal's <b>clearance margin</b> applied — the third
    /// layer over spaces and parts. The room inflated by <paramref name="clearanceCells"/> is the guard region:
    /// boundary runs inside it are <see cref="ClassifiedEdge.Guarded"/> (splitting from the free remainder of
    /// their line), and every space part is split against it, the covered piece marked
    /// <see cref="NegativeSpacePart.Guarded"/>. The room and its immediate approach are final as the emitter
    /// designed them — nothing may dock or publish inside the margin, or the objective's approach changes out
    /// from under the design.</summary>
    public static EdgeClassification Classify(EmittedShape shape, int clearanceCells)
    {
        var (cells, terminal) = EmissionCells(shape);
        var d = clearanceCells;
        int[] clearance = [shape.Room[0] - d, shape.Room[1] - d, shape.Room[2] + 2 * d, shape.Room[3] + 2 * d];
        return Classify(cells, terminal, clearance);
    }

    private static (HashSet<(int, int)> Cells, HashSet<(int, int)> Terminal) EmissionCells(EmittedShape shape)
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
        return (cells, terminal);
    }

    /// <summary>Classify a cell set with no terminal.</summary>
    public static EdgeClassification Classify(IReadOnlySet<(int, int)> cells) =>
        Classify(cells, new HashSet<(int, int)>(), clearance: null);

    /// <summary>Classify a cell set, marking boundary runs whose inner cell lies in <paramref name="terminal"/>
    /// (the terminal room's own wall) — runs never merge across the terrain↔terminal ownership change.</summary>
    public static EdgeClassification Classify(IReadOnlySet<(int, int)> cells, IReadOnlySet<(int, int)> terminal) =>
        Classify(cells, terminal, clearance: null);

    private static EdgeClassification Classify(
        IReadOnlySet<(int, int)> cells, IReadOnlySet<(int, int)> terminal, int[]? clearance)
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
                var walled = new List<(int Dx, int Dz)>();
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (comp.Any(c => cells.Contains((c.Item1 + dx, c.Item2 + dz)))) walled.Add((dx, dz));
                var kind = enclosed ? NegativeSpaceKind.Hole
                    : walled.Count >= 3 ? NegativeSpaceKind.Bay
                    : walled.Count == 2 ? NegativeSpaceKind.Notch
                    : NegativeSpaceKind.Open;
                var parts = Decompose(comp, cells, walled);
                if (clearance is not null)
                    parts = parts.SelectMany(p => SplitByClearance(p, clearance)).ToList();
                spaces.Add(new NegativeSpace(kind, comp, walled.Count, parts, SpaceForm(comp)));
            }

        // boundary edges — every filled↔empty cell seam, classed by the space behind it (outside the bounding
        // box is open), the inner cell's ownership, and the clearance guard (the void cell just beyond the
        // seam lies in the margin — docking there crowds the room), then merged into maximal same-key straight
        // runs: a run never continues across a terrain↔terminal or free↔guarded change
        NegativeSpaceKind FacingKind((int, int) n) =>
            spaceOf.TryGetValue(n, out var s) ? spaces[s].Kind : NegativeSpaceKind.Open;
        bool Guarded((int, int) n) => clearance is not null
            && n.Item1 >= clearance[0] && n.Item1 < clearance[0] + clearance[2]
            && n.Item2 >= clearance[1] && n.Item2 < clearance[1] + clearance[3];
        var vertical = new Dictionary<(int Line, NegativeSpaceKind Kind, bool Terminal, bool Guarded), List<int>>();   // line x → unit spans z
        var horizontal = new Dictionary<(int Line, NegativeSpaceKind Kind, bool Terminal, bool Guarded), List<int>>(); // line z → unit spans x
        foreach (var (x, z) in cells)
        {
            var own = terminal.Contains((x, z));
            if (!cells.Contains((x + 1, z))) Add(vertical, (x + 1, FacingKind((x + 1, z)), own, Guarded((x + 1, z))), z);
            if (!cells.Contains((x - 1, z))) Add(vertical, (x, FacingKind((x - 1, z)), own, Guarded((x - 1, z))), z);
            if (!cells.Contains((x, z + 1))) Add(horizontal, (z + 1, FacingKind((x, z + 1)), own, Guarded((x, z + 1))), x);
            if (!cells.Contains((x, z - 1))) Add(horizontal, (z, FacingKind((x, z - 1)), own, Guarded((x, z - 1))), x);
        }
        var edges = new List<ClassifiedEdge>();
        foreach (var ((line, kind, own, grd), spans) in vertical
            .OrderBy(e => e.Key.Line).ThenBy(e => e.Key.Kind).ThenBy(e => e.Key.Terminal).ThenBy(e => e.Key.Guarded))
            foreach (var (lo, hi) in Runs(spans))
                edges.Add(new ClassifiedEdge(line, lo, line, hi, kind, hi - lo, own, grd));
        foreach (var ((line, kind, own, grd), spans) in horizontal
            .OrderBy(e => e.Key.Line).ThenBy(e => e.Key.Kind).ThenBy(e => e.Key.Terminal).ThenBy(e => e.Key.Guarded))
            foreach (var (lo, hi) in Runs(spans))
                edges.Add(new ClassifiedEdge(lo, line, hi, line, kind, hi - lo, own, grd));
        return new EdgeClassification(spaces, edges);
    }

    private static void Add(
        Dictionary<(int, NegativeSpaceKind, bool, bool), List<int>> lines,
        (int, NegativeSpaceKind, bool, bool) key, int at)
    {
        if (!lines.TryGetValue(key, out var l)) lines[key] = l = [];
        l.Add(at);
    }

    // the space's own compound identity — the void read as a body through the same classifier (null when it
    // classifies to no compound)
    private static CompoundRead? SpaceForm(HashSet<(int, int)> comp)
    {
        try { return ShapeClassifier.ClassifyBody(comp); }
        catch (ArgumentException) { return null; }
    }

    // split a part against the clearance rect: the covered piece is guarded (non-publishable), the guillotine
    // remainders keep the part's class and stay free
    private static IEnumerable<NegativeSpacePart> SplitByClearance(NegativeSpacePart part, int[] clearance)
    {
        int px0 = part.Rect[0], pz0 = part.Rect[1], px1 = px0 + part.Rect[2], pz1 = pz0 + part.Rect[3];
        int cx0 = Math.Max(px0, clearance[0]), cz0 = Math.Max(pz0, clearance[1]);
        int cx1 = Math.Min(px1, clearance[0] + clearance[2]), cz1 = Math.Min(pz1, clearance[1] + clearance[3]);
        if (cx0 >= cx1 || cz0 >= cz1) { yield return part; yield break; }
        if (cx0 > px0) yield return part with { Rect = [px0, pz0, cx0 - px0, pz1 - pz0] };
        if (px1 > cx1) yield return part with { Rect = [cx1, pz0, px1 - cx1, pz1 - pz0] };
        if (cz0 > pz0) yield return part with { Rect = [cx0, pz0, cx1 - cx0, cz0 - pz0] };
        if (pz1 > cz1) yield return part with { Rect = [cx0, cz1, cx1 - cx0, pz1 - cz1] };
        yield return part with { Rect = [cx0, cz0, cx1 - cx0, cz1 - cz0], Guarded = true };
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

    // slab-decompose a space into rectangles and class each by its own body walls. Both slab orientations are
    // tried and the coarser one wins; on a tie the cut runs perpendicular to a bay's mouth, so the part at the
    // mouth spans it (the U-bar of the uneven branch's bay) and the legs behind it separate.
    private static IReadOnlyList<NegativeSpacePart> Decompose(
        HashSet<(int, int)> comp, IReadOnlySet<(int, int)> body, List<(int Dx, int Dz)> walled)
    {
        var horizontal = Slabs(comp, horizontal: true);
        var vertical = Slabs(comp, horizontal: false);
        List<int[]> rects;
        if (horizontal.Count != vertical.Count) rects = horizontal.Count < vertical.Count ? horizontal : vertical;
        else
        {
            var openVertical = !walled.Contains((0, 1)) || !walled.Contains((0, -1));   // mouth up/down
            rects = openVertical ? horizontal : vertical;
        }
        return rects.Select(r => new NegativeSpacePart(r, PartKind(r, body))).ToList();
    }

    // group consecutive lines (rows for horizontal slabs, columns for vertical) with identical interval
    // structure; every interval × line-band is one rectangle
    private static List<int[]> Slabs(HashSet<(int, int)> comp, bool horizontal)
    {
        int Line((int, int) c) => horizontal ? c.Item2 : c.Item1;
        int Cross((int, int) c) => horizontal ? c.Item1 : c.Item2;

        var rects = new List<int[]>();
        List<(int Lo, int Hi)>? band = null;
        int bandStart = 0, prevLine = 0;
        void Flush()
        {
            if (band is null) return;
            foreach (var (lo, hi) in band)
                rects.Add(horizontal
                    ? [lo, bandStart, hi - lo, prevLine - bandStart + 1]
                    : [bandStart, lo, prevLine - bandStart + 1, hi - lo]);
        }
        foreach (var g in comp.GroupBy(Line).OrderBy(g => g.Key))
        {
            var intervals = Runs(g.Select(Cross).ToList()).ToList();
            if (band is null || g.Key != prevLine + 1 || !intervals.SequenceEqual(band))
            {
                Flush();
                band = intervals;
                bandStart = g.Key;
            }
            prevLine = g.Key;
        }
        Flush();
        return rects;
    }

    // a part's own class: walls against real body terrain only — sibling parts count as open, which is the
    // whole point (the bar part at a bay's mouth reads notch-grade even though the legs sit beside it)
    private static NegativeSpaceKind PartKind(int[] r, IReadOnlySet<(int, int)> body)
    {
        int x0 = r[0], z0 = r[1], x1 = r[0] + r[2] - 1, z1 = r[1] + r[3] - 1;
        var walls = 0;
        if (Enumerable.Range(z0, r[3]).Any(z => body.Contains((x1 + 1, z)))) walls++;
        if (Enumerable.Range(z0, r[3]).Any(z => body.Contains((x0 - 1, z)))) walls++;
        if (Enumerable.Range(x0, r[2]).Any(x => body.Contains((x, z1 + 1)))) walls++;
        if (Enumerable.Range(x0, r[2]).Any(x => body.Contains((x, z0 - 1)))) walls++;
        return walls >= 4 ? NegativeSpaceKind.Hole
            : walls == 3 ? NegativeSpaceKind.Bay
            : walls == 2 ? NegativeSpaceKind.Notch
            : NegativeSpaceKind.Open;
    }
}
