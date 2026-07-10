namespace PgmStudio.Pgm.Plan;

/// <summary>The base wool-approach families (the categorizer's read of a wool box — see the catalog in
/// <c>docs/contracts/layout-generation.md</c> §2). <see cref="I"/>/<see cref="L"/>/<see cref="Z"/>/
/// <see cref="Scythe"/> are the thin open path by bend count; <see cref="H"/> branches; <see cref="Donut"/>
/// encloses a void; <see cref="Plug"/> is a solid body; <see cref="Isolated"/> has no terrain approach.</summary>
public enum ApproachShape { Isolated, I, L, Z, Scythe, H, Donut, Plug }

/// <summary>
/// The canonical wool-approach skeleton classifier — the four-way decision tree of the base-shape catalog,
/// promoted so both the shape fixtures and the composer's emitter read shapes the same way. Order (the earlier
/// tests are the stronger signals): <b>isolated</b> (no terrain touches the wool) → <b>donut</b> (terrain
/// encloses a void) → <b>plug</b> (the wool docks a solid body — its approach lane is all thick) →
/// <b>H</b> (the corridor branches) → else the thin open path <b>I/L/Z/scythe</b> by bend count.
///
/// <para><b>Branch test — rectilinear, width-robust, no thinning.</b> The corridor is a union of axis-aligned
/// rects. At every fully-filled <c>W×W</c> block (<c>W</c> = the actual corridor width) count the sides whose
/// full outward <c>W</c>-strip is also filled — a "full-width arm". A straight lane has 2 (the two ends), an
/// L/Z corner has 2 (two adjacent arms), a T/+ has ≥3. Because arms must be the full corridor width, a
/// <em>wide</em> straight still counts 2 (its narrow sides are void), so the test needs no medial axis.</para>
/// </summary>
public static class WoolApproachShape
{
    /// <summary>Classify the approach of the wool room piece <paramref name="woolPieceId"/> in
    /// <paramref name="plan"/>. <paramref name="laneWidth"/> is the reference corridor width in cells (the
    /// composer's <c>cw</c>): the four-way test is relative to it — a body wider than a lane is a plug, a
    /// junction is where lane-width arms meet — so the caller passes the width it built the shape at.</summary>
    public static (ApproachShape Shape, int Width) Classify(PlanModel plan, string woolPieceId, int laneWidth)
    {
        var filled = new HashSet<(int, int)>();
        foreach (var p in plan.Pieces)
        {
            if (p.Role is PlanRoles.Buffer or PlanRoles.Connector) continue;
            for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++) filled.Add((x, z));
        }
        var wp = plan.Pieces.FirstOrDefault(p => p.Id == woolPieceId);
        if (wp is null) return (ApproachShape.Isolated, 0);
        var room = new HashSet<(int, int)>();
        for (var x = wp.Rect[0]; x < wp.Rect[0] + wp.Rect[2]; x++)
            for (var z = wp.Rect[1]; z < wp.Rect[1] + wp.Rect[3]; z++) room.Add((x, z));
        return Classify(filled, room, laneWidth);
    }

    /// <summary>Classify the approach a <paramref name="room"/> caps within <paramref name="filled"/> terrain
    /// (both cell sets; <paramref name="filled"/> includes the room's own cells). <paramref name="laneWidth"/>
    /// is the reference corridor width in cells — the plug/branch tests are relative to it.</summary>
    public static (ApproachShape Shape, int Width) Classify(IReadOnlySet<(int, int)> filled, IReadOnlySet<(int, int)> room, int laneWidth)
    {
        // the terrain component reachable from the room — so unrelated terrain in a multi-shape plan can't
        // contaminate the enclosed-void / branch tests.
        var comp = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        foreach (var r in room) if (comp.Add(r)) q.Enqueue(r);
        while (q.Count > 0) { var c = q.Dequeue(); foreach (var n in N4(c)) if (filled.Contains(n) && comp.Add(n)) q.Enqueue(n); }

        var seeds = new List<(int, int)>();
        foreach (var r in room) foreach (var n in N4(r)) if (comp.Contains(n) && !room.Contains(n)) seeds.Add(n);
        if (seeds.Count == 0) return (ApproachShape.Isolated, 0);

        // corridor width from the TERRAIN cross-section (the room would inflate it and make a 1-wide lane's
        // branch block 2×2, hiding real junctions).
        var terr = new HashSet<(int, int)>(comp); terr.ExceptWith(room);
        int Hrun((int, int) c) { int n = 1; for (var x = c.Item1 - 1; terr.Contains((x, c.Item2)); x--) n++; for (var x = c.Item1 + 1; terr.Contains((x, c.Item2)); x++) n++; return n; }
        int Vrun((int, int) c) { int n = 1; for (var z = c.Item2 - 1; terr.Contains((c.Item1, z)); z--) n++; for (var z = c.Item2 + 1; terr.Contains((c.Item1, z)); z++) n++; return n; }
        int cross = Math.Max(1, seeds.Min(c => Math.Min(Hrun(c), Vrun(c))));  // measured terminal cross-section
        int width = Math.Clamp(cross, 2, 6);                                 // reported width (lane convention)
        int w = Math.Max(1, laneWidth);                                      // reference lane width for the structural tests

        if (HasEnclosedVoid(comp)) return (ApproachShape.Donut, width);

        // plug: the wool docks a solid body — every approach cell sits in a (w+1)² block, so the thin lane is empty.
        int k = w + 1;
        bool Blk((int, int) o) { for (var dx = 0; dx < k; dx++) for (var dz = 0; dz < k; dz++) if (!comp.Contains((o.Item1 + dx, o.Item2 + dz))) return false; return true; }
        bool Thick((int, int) c) { for (var x = c.Item1 - k + 1; x <= c.Item1; x++) for (var z = c.Item2 - k + 1; z <= c.Item2; z++) if (Blk((x, z))) return true; return false; }
        bool Narrow((int, int) c) => comp.Contains(c) && !room.Contains(c) && !Thick(c);
        var lane = new HashSet<(int, int)>();
        var lq = new Queue<(int, int)>();
        foreach (var s in seeds) if (Narrow(s) && lane.Add(s)) lq.Enqueue(s);
        while (lq.Count > 0) { var c = lq.Dequeue(); foreach (var n in N4(c)) if (Narrow(n) && lane.Add(n)) lq.Enqueue(n); }
        if (lane.Count == 0) return (ApproachShape.Plug, width);

        if (HasBranch(comp, w)) return (ApproachShape.H, width);

        // thin open path: I/L by bend count; ≥2 bends split by whether the lane wraps a bay (an open
        // concavity, void on ≥3 lane sides = scythe/U) or not (an S = Z).
        int reflex = ReflexCount(lane);
        if (reflex == 0) return (ApproachShape.I, width);
        if (reflex == 1) return (ApproachShape.L, width);
        return (HasBay(lane, comp, w) ? ApproachShape.Scythe : ApproachShape.Z, width);
    }

    // A fully-filled w×w block with ≥3 sides whose full outward w-strip is also filled — a branch (T/+/Y).
    private static bool HasBranch(HashSet<(int, int)> walk, int w)
    {
        int minx = walk.Min(c => c.Item1), maxx = walk.Max(c => c.Item1), minz = walk.Min(c => c.Item2), maxz = walk.Max(c => c.Item2);
        bool Block(int ox, int oz) { for (var dx = 0; dx < w; dx++) for (var dz = 0; dz < w; dz++) if (!walk.Contains((ox + dx, oz + dz))) return false; return true; }
        bool Strip(Func<int, (int, int)> cell) { for (var i = 0; i < w; i++) if (!walk.Contains(cell(i))) return false; return true; }
        for (var ox = minx; ox <= maxx - w + 1; ox++)
            for (var oz = minz; oz <= maxz - w + 1; oz++)
            {
                if (!Block(ox, oz)) continue;
                int arms = 0;
                if (Strip(i => (ox + i, oz - 1))) arms++;      // north
                if (Strip(i => (ox + i, oz + w))) arms++;      // south
                if (Strip(i => (ox - 1, oz + i))) arms++;      // west
                if (Strip(i => (ox + w, oz + i))) arms++;      // east
                if (arms >= 3) return true;
            }
        return false;
    }

    // A true-void cell with lane within w cells on ≥3 cardinal sides = an open concavity (a bay): the
    // U/scythe signature that tells a same-handed pair of bends (a bay) from an S (a Z), which share the
    // reflex-corner count. Width-aware (searches w cells out) so a w-wide notch is a bay, not just a 1-cell one.
    private static bool HasBay(HashSet<(int, int)> lane, HashSet<(int, int)> comp, int w)
    {
        bool Reaches((int, int) v, int dx, int dz) { for (var i = 1; i <= w; i++) if (lane.Contains((v.Item1 + dx * i, v.Item2 + dz * i))) return true; return false; }
        var seen = new HashSet<(int, int)>();
        foreach (var c in lane)
            foreach (var v in N4(c))
                if (!comp.Contains(v) && seen.Add(v))
                {
                    int d = 0;
                    if (Reaches(v, 1, 0)) d++; if (Reaches(v, -1, 0)) d++; if (Reaches(v, 0, 1)) d++; if (Reaches(v, 0, -1)) d++;
                    if (d >= 3) return true;
                }
        return false;
    }

    // A background cell enclosed by the footprint (unreachable from outside its bbox) = a hole.
    private static bool HasEnclosedVoid(HashSet<(int, int)> fill)
    {
        int mnx = fill.Min(c => c.Item1) - 1, mxx = fill.Max(c => c.Item1) + 1, mnz = fill.Min(c => c.Item2) - 1, mxz = fill.Max(c => c.Item2) + 1;
        var outside = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>(); q.Enqueue((mnx, mnz)); outside.Add((mnx, mnz));
        while (q.Count > 0) { var c = q.Dequeue(); foreach (var n in N4(c)) if (n.Item1 >= mnx && n.Item1 <= mxx && n.Item2 >= mnz && n.Item2 <= mxz && !fill.Contains(n) && outside.Add(n)) q.Enqueue(n); }
        for (var x = mnx; x <= mxx; x++) for (var z = mnz; z <= mxz; z++) if (!fill.Contains((x, z)) && !outside.Contains((x, z))) return true;
        return false;
    }

    // Reflex (concave) corners of the lane = the bend count.
    private static int ReflexCount(HashSet<(int, int)> lane)
    {
        int mnx = lane.Min(c => c.Item1), mxx = lane.Max(c => c.Item1) + 1, mnz = lane.Min(c => c.Item2), mxz = lane.Max(c => c.Item2) + 1, r = 0;
        for (var x = mnx; x <= mxx; x++)
            for (var z = mnz; z <= mxz; z++)
            {
                int cnt = 0;
                if (lane.Contains((x, z))) cnt++; if (lane.Contains((x - 1, z))) cnt++;
                if (lane.Contains((x, z - 1))) cnt++; if (lane.Contains((x - 1, z - 1))) cnt++;
                if (cnt == 3) r++;
            }
        return r;
    }

    private static IEnumerable<(int, int)> N4((int, int) c)
    {
        yield return (c.Item1 + 1, c.Item2); yield return (c.Item1 - 1, c.Item2);
        yield return (c.Item1, c.Item2 + 1); yield return (c.Item1, c.Item2 - 1);
    }
}
