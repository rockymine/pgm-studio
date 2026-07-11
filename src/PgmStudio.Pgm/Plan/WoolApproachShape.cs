namespace PgmStudio.Pgm.Plan;

/// <summary>The base wool-approach families (the categorizer's read of a wool box — see the catalog in
/// <c>docs/contracts/layout-generation.md</c> §2). <see cref="I"/>/<see cref="L"/>/<see cref="Z"/>/
/// <see cref="Scythe"/> are the open path by turn count (an S with a fold wraps a bay = scythe);
/// <see cref="U"/> and <see cref="H"/> are the two-leg branch, split by how the wool docks the crossbar —
/// <see cref="U"/> flush against a bar wider than itself, <see cref="H"/> capping its own room-run stub;
/// <see cref="Clamp"/> catches the wool between two bars (it bridges them); <see cref="Donut"/> encloses a
/// void; <see cref="Isolated"/> has no terrain approach.</summary>
public enum ApproachShape { Isolated, I, L, Z, Scythe, Clamp, U, H, Donut }

/// <summary>
/// The canonical wool-approach classifier — one decision tree over the terrain, read <b>width-independently</b>
/// (nothing keys off the absolute width of any piece; uniform scaling and per-piece thickness never change the
/// family). Order, strongest signal first: <b>isolated</b> (no terrain touches the wool) → <b>donut</b> (terrain
/// encloses a void) → <b>clamp</b> (the wool bridges two opposite bars — removing it disconnects the terrain) →
/// else the open path <b>I/L/Z/scythe</b> off the turn count, with the two-leg branch splitting into <b>U/H</b>.
///
/// <para><b>Every test is width-independent.</b> Turns are reflex corners of the terrain <em>outline</em> (the
/// approach minus the room), so a lane and the same lane widened uniformly turn the same number of times. A
/// <b>bay</b> is a concavity that indents from a single edge of the bounding box — a wrapped notch at any width,
/// never a reach-<c>W</c>-cells probe. A <b>branch</b> (U/H) is two terrain runs meeting a shared bounding-box
/// edge — two legs off a crossbar — so a leg stays one leg however thick it is drawn. A <b>bridge</b> (clamp) is
/// the wool being a cut cell: remove it and the terrain falls into two pieces (the wall between two bars). None
/// of these consult the reference width, so a thick leg, a box-shaped bar, or a wide bay never flips the family.</para>
///
/// <para><b>U vs H.</b> Both are the two-leg branch — legs off a crossbar, the wool on the crossbar's opposite
/// side. The split is the crossbar's <em>overhang</em>: <b>U</b> docks the wool flush on a bar wider than itself
/// (terrain reaches past the wool's footprint on the perpendicular diagonal); <b>H</b> caps the wool on a
/// room-run stub its own width, lifting it off the crossbar. One extra piece (the stub) is the whole difference.</para>
/// </summary>
public static class WoolApproachShape
{
    /// <summary>Classify the approach of the wool room piece <paramref name="woolPieceId"/> in
    /// <paramref name="plan"/>. <paramref name="laneWidth"/> is the reference corridor width in cells; it is kept
    /// for the width report only — the family read is width-independent.</summary>
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
    /// (both cell sets; <paramref name="filled"/> includes the room's own cells). The family read is
    /// width-independent; <paramref name="laneWidth"/> is retained only for call compatibility.</summary>
    public static (ApproachShape Shape, int Width) Classify(IReadOnlySet<(int, int)> filled, IReadOnlySet<(int, int)> room, int laneWidth)
    {
        _ = laneWidth;
        // the terrain component reachable from the room — so unrelated terrain in a multi-shape plan can't
        // contaminate the enclosed-void / branch tests.
        var comp = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        foreach (var r in room) if (comp.Add(r)) q.Enqueue(r);
        while (q.Count > 0) { var c = q.Dequeue(); foreach (var n in N4(c)) if (filled.Contains(n) && comp.Add(n)) q.Enqueue(n); }

        var terr = new HashSet<(int, int)>(comp); terr.ExceptWith(room);
        if (!terr.Any(c => N4(c).Any(room.Contains))) return (ApproachShape.Isolated, 0);

        // reported width from the terminal terrain cross-section (lane convention), orthogonal to the family.
        var seeds = new List<(int, int)>();
        foreach (var r in room) foreach (var n in N4(r)) if (terr.Contains(n)) seeds.Add(n);
        int Hrun((int, int) c) { int n = 1; for (var x = c.Item1 - 1; terr.Contains((x, c.Item2)); x--) n++; for (var x = c.Item1 + 1; terr.Contains((x, c.Item2)); x++) n++; return n; }
        int Vrun((int, int) c) { int n = 1; for (var z = c.Item2 - 1; terr.Contains((c.Item1, z)); z--) n++; for (var z = c.Item2 + 1; terr.Contains((c.Item1, z)); z++) n++; return n; }
        int width = Math.Clamp(Math.Max(1, seeds.Min(c => Math.Min(Hrun(c), Vrun(c)))), 2, 6);

        // 1. donut — terrain encloses a void. The strongest signal (a loop can carry a locally thick corner).
        if (HasEnclosedVoid(comp)) return (ApproachShape.Donut, width);

        // 2. clamp — the wool is caught between terrain on two OPPOSITE sides AND it bridges them: remove the
        //    wool and the two bars fall apart. (A dead-end cap has terrain on one side; a self-connected shape
        //    doesn't split, so neither is a clamp.)
        int rminx = room.Min(c => c.Item1), rmaxx = room.Max(c => c.Item1), rminz = room.Min(c => c.Item2), rmaxz = room.Max(c => c.Item2);
        bool Side(int x0, int x1, int z0, int z1) { for (var x = x0; x <= x1; x++) for (var z = z0; z <= z1; z++) if (terr.Contains((x, z))) return true; return false; }
        bool top = Side(rminx, rmaxx, rminz - 1, rminz - 1), bot = Side(rminx, rmaxx, rmaxz + 1, rmaxz + 1);
        bool left = Side(rminx - 1, rminx - 1, rminz, rmaxz), right = Side(rmaxx + 1, rmaxx + 1, rminz, rmaxz);
        if (((top && bot) || (left && right)) && Components(terr) >= 2) return (ApproachShape.Clamp, width);

        // 3. the open path — turns are reflex corners of the terrain outline (room excluded), so the count is
        //    width-invariant: a lane and the same lane widened uniformly read the same. 0 = I, 1 = L; ≥2 forks
        //    into branch (U/H) / scythe / Z.
        int bends = ReflexCount(terr);
        if (bends == 0) return (ApproachShape.I, width);
        if (bends == 1) return (ApproachShape.L, width);

        // ≥2 turns and two terrain legs sharing a bounding-box edge the wool does NOT sit on = a branch. Split
        // by how the wool docks the crossbar: flush against a bar wider than itself (the crossbar reaches past
        // the wool toward the legs) is a U; capping its own narrow room-run stub is an H. The wool's own edge is
        // excluded from the leg test — otherwise a fold's two path-ends (a scythe) read as a fork. Else a fold
        // that wraps a bay is a scythe; two opposing bends with no bay is a Z.
        if (ParallelArms(comp, terr, room))
            return (FlushOnBar(terr, rminx, rmaxx, rminz, rmaxz) ? ApproachShape.U : ApproachShape.H, width);
        return (HasBay(comp) ? ApproachShape.Scythe : ApproachShape.Z, width);
    }

    // U vs H: does the wool dock flush against a bar WIDER than itself (the crossbar reaching past the wool
    // toward the legs — a U), or cap its own narrow stub the width of the wool (an H)? For each side the wool
    // attaches to, the bar is flush if terrain continues perpendicular just past the wool's footprint (the
    // diagonal-adjacent cell) — a crossbar overhangs the wool, a stub does not. Width-independent: a thick stub
    // stays as wide as its wool, a crossbar always overhangs.
    private static bool FlushOnBar(HashSet<(int, int)> terr, int rminx, int rmaxx, int rminz, int rmaxz)
    {
        bool ColHas(int x) { for (var z = rminz; z <= rmaxz; z++) if (terr.Contains((x, z))) return true; return false; }
        bool RowHas(int z) { for (var x = rminx; x <= rmaxx; x++) if (terr.Contains((x, z))) return true; return false; }
        if (ColHas(rminx - 1) && (terr.Contains((rminx - 1, rminz - 1)) || terr.Contains((rminx - 1, rmaxz + 1)))) return true;  // attach left, bar overhangs vertically
        if (ColHas(rmaxx + 1) && (terr.Contains((rmaxx + 1, rminz - 1)) || terr.Contains((rmaxx + 1, rmaxz + 1)))) return true;  // attach right
        if (RowHas(rminz - 1) && (terr.Contains((rminx - 1, rminz - 1)) || terr.Contains((rmaxx + 1, rminz - 1)))) return true;  // attach top, bar overhangs horizontally
        if (RowHas(rmaxz + 1) && (terr.Contains((rminx - 1, rmaxz + 1)) || terr.Contains((rmaxx + 1, rmaxz + 1)))) return true;  // attach bottom
        return false;
    }

    // Number of connected components of a cell set (4-connectivity).
    private static int Components(HashSet<(int, int)> cells)
    {
        var seen = new HashSet<(int, int)>();
        int n = 0;
        foreach (var c in cells)
        {
            if (!seen.Add(c)) continue;
            n++;
            var q = new Queue<(int, int)>(); q.Enqueue(c);
            while (q.Count > 0) { var d = q.Dequeue(); foreach (var m in N4(d)) if (cells.Contains(m) && seen.Add(m)) q.Enqueue(m); }
        }
        return n;
    }

    // A branch: two separate terrain runs pressed against one edge of the bounding box — two legs meeting a
    // shared hub side. Width-independent: a thick leg is still one run, so a widened branch still reads as a
    // fork and a single (wide) lane, whose one run spans the whole edge, never does. An edge the WOOL sits on is
    // skipped — a folded path (scythe) ends both its runs on the wool's edge and must not read as a fork.
    private static bool ParallelArms(HashSet<(int, int)> comp, HashSet<(int, int)> terr, IReadOnlySet<(int, int)> room)
    {
        int mnx = comp.Min(c => c.Item1), mxx = comp.Max(c => c.Item1), mnz = comp.Min(c => c.Item2), mxz = comp.Max(c => c.Item2);
        bool TwoRuns(IEnumerable<(int, int)> line)
        {
            var cells = line.ToList();
            if (cells.Any(room.Contains)) return false;   // the wool's own edge — a fold, not a fork
            int runs = 0; bool inRun = false;
            foreach (var c in cells) { if (terr.Contains(c)) { if (!inRun) { runs++; inRun = true; } } else inRun = false; }
            return runs >= 2;
        }
        var north = Enumerable.Range(mnx, mxx - mnx + 1).Select(x => (x, mnz));
        var south = Enumerable.Range(mnx, mxx - mnx + 1).Select(x => (x, mxz));
        var west = Enumerable.Range(mnz, mxz - mnz + 1).Select(z => (mnx, z));
        var east = Enumerable.Range(mnz, mxz - mnz + 1).Select(z => (mxx, z));
        return TwoRuns(north) || TwoRuns(south) || TwoRuns(west) || TwoRuns(east);
    }

    // A bay: an open concavity that indents from a SINGLE bounding-box edge (a notch wrapped by terrain on its
    // other three sides). Width-independent — a wide bay is still one one-edge remainder. A corner notch touches
    // two edges and an enclosed hole touches none, so neither counts.
    private static bool HasBay(HashSet<(int, int)> comp)
    {
        int mnx = comp.Min(c => c.Item1), mxx = comp.Max(c => c.Item1), mnz = comp.Min(c => c.Item2), mxz = comp.Max(c => c.Item2);
        var seen = new HashSet<(int, int)>();
        for (var x = mnx; x <= mxx; x++)
            for (var z = mnz; z <= mxz; z++)
            {
                if (comp.Contains((x, z)) || seen.Contains((x, z))) continue;
                var edges = 0; var mask = 0;
                var q = new Queue<(int, int)>(); q.Enqueue((x, z)); seen.Add((x, z));
                var cells = new List<(int, int)>();
                while (q.Count > 0)
                {
                    var c = q.Dequeue(); cells.Add(c);
                    foreach (var n in N4(c)) if (n.Item1 >= mnx && n.Item1 <= mxx && n.Item2 >= mnz && n.Item2 <= mxz && !comp.Contains(n) && seen.Add(n)) q.Enqueue(n);
                }
                foreach (var c in cells)
                {
                    if (c.Item1 == mnx) mask |= 1; if (c.Item1 == mxx) mask |= 2;
                    if (c.Item2 == mnz) mask |= 4; if (c.Item2 == mxz) mask |= 8;
                }
                edges = ((mask & 1) != 0 ? 1 : 0) + ((mask & 2) != 0 ? 1 : 0) + ((mask & 4) != 0 ? 1 : 0) + ((mask & 8) != 0 ? 1 : 0);
                if (edges == 1) return true;
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

    // Reflex (concave) corners of the terrain outline = the bend count.
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
