using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Shapes;

/// <summary>The open (terminal-less) read of a corridor by rectilinear topology: <see cref="I"/> straight,
/// <see cref="L"/> one bend, <see cref="Z"/> two bends, <see cref="Complex"/> ≥3 bends; <see cref="Plaza"/> a
/// chunk right at the terminal, <see cref="None"/> no terrain corridor.</summary>
public enum LaneRead { I, L, Z, Complex, Plaza, None }

/// <summary>
/// The canonical shape classifier — one decision tree over the terrain, read <b>width-independently</b> (nothing
/// keys off the absolute width of any piece; uniform scaling and per-piece thickness never change the family).
/// Order, strongest signal first: <b>isolated</b> (no terrain touches the terminal) → <b>donut</b> (terrain
/// encloses a void) → <b>clamp</b> (the terminal bridges two opposite bars — removing it disconnects the terrain)
/// → else the open path <b>I/L/Z/scythe</b> off the turn count, with the two-leg branch splitting into <b>U/H</b>.
/// Nothing here is wool-specific: the <b>terminal</b> is whatever piece the shape caps (a wool room, a spawn
/// room, …).
///
/// <para><b>Every test is width-independent.</b> Turns are reflex corners of the terrain <em>outline</em> (the
/// approach minus the terminal), so a lane and the same lane widened uniformly turn the same number of times. A
/// <b>fold</b> (scythe) is terrain that doubles back — some row or column crosses it in two runs, the lines
/// through the wrapped bay — read on the terrain itself, so a shifted endpoint or a docked neighbour changes
/// the verdict only by genuinely adding or filling a fold, never by moving the bounding box. A <b>branch</b>
/// (U/H) is two terrain runs meeting a shared bounding-box
/// edge — two legs off a crossbar — so a leg stays one leg however thick it is drawn. A <b>bridge</b> (clamp) is
/// the terminal being a cut cell: remove it and the terrain falls into two pieces (the wall between two bars).</para>
///
/// <para><b>U vs H.</b> Both are the two-leg branch — legs off a crossbar, the terminal on the crossbar's
/// opposite side. The split is the crossbar's <em>overhang</em>: <b>U</b> docks the terminal flush on a bar wider
/// than itself (terrain reaches past the terminal's footprint on the perpendicular diagonal); <b>H</b> caps the
/// terminal on a room-run stub its own width, lifting it off the crossbar. One extra piece (the stub) is the
/// whole difference.</para>
///
/// <para><b>Open read.</b> <see cref="ClassifyOpen"/> is the terminal-less variant — the same reflex-corner read
/// on the thin corridor the terminal caps, self-delimited at any hub/plaza, returning a <see cref="LaneRead"/>.</para>
/// </summary>
public static class ShapeClassifier
{
    /// <summary>Classify the approach the piece <paramref name="terminalPieceId"/> caps in <paramref name="plan"/>
    /// (buffers/connectors excluded from the terrain).</summary>
    public static (ShapeFamily Family, int Width) Classify(PlanModel plan, string terminalPieceId)
    {
        var (filled, terminal) = CellSets(plan, terminalPieceId);
        return terminal is null ? (ShapeFamily.Isolated, 0) : Classify(filled, terminal);
    }

    /// <summary>Classify the approach a <paramref name="terminal"/> caps within <paramref name="filled"/> terrain
    /// (both cell sets; <paramref name="filled"/> includes the terminal's own cells). The family read is
    /// width-independent; the reported width is the terminal terrain cross-section (lane convention).</summary>
    public static (ShapeFamily Family, int Width) Classify(IReadOnlySet<(int, int)> filled, IReadOnlySet<(int, int)> terminal)
    {
        // the terrain component reachable from the terminal — so unrelated terrain in a multi-shape plan can't
        // contaminate the enclosed-void / branch tests.
        var comp = Cells.Flood(terminal, filled);

        var terr = new HashSet<(int, int)>(comp); terr.ExceptWith(terminal);
        if (!terr.Any(c => Cells.N4(c).Any(terminal.Contains))) return (ShapeFamily.Isolated, 0);

        // reported width from the terminal terrain cross-section (lane convention), orthogonal to the family.
        var seeds = new List<(int, int)>();
        foreach (var r in terminal) foreach (var n in Cells.N4(r)) if (terr.Contains(n)) seeds.Add(n);
        int width = Cells.MinRunWidth(terr, seeds);

        // 1. donut — terrain encloses a void. The strongest signal (a loop can carry a locally thick corner).
        if (Cells.HasEnclosedVoid(comp)) return (ShapeFamily.Donut, width);

        // 2. clamp — the terminal is a cut cell caught between terrain on TWO OR MORE distinct faces: remove it
        //    and the terrain falls into two pieces. Two faces suffice — opposite (the centered clamp) or adjacent
        //    (the corner clamp). One face bridging two stubs is a wool perched on a bar, not clamped (§7), so
        //    require two distinct faces; a dead-end cap grips one face and a self-connected shape doesn't split,
        //    so neither is a clamp.
        int rminx = terminal.Min(c => c.Item1), rmaxx = terminal.Max(c => c.Item1), rminz = terminal.Min(c => c.Item2), rmaxz = terminal.Max(c => c.Item2);
        bool Side(int x0, int x1, int z0, int z1) { for (var x = x0; x <= x1; x++) for (var z = z0; z <= z1; z++) if (terr.Contains((x, z))) return true; return false; }
        bool top = Side(rminx, rmaxx, rminz - 1, rminz - 1), bot = Side(rminx, rmaxx, rmaxz + 1, rmaxz + 1);
        bool left = Side(rminx - 1, rminx - 1, rminz, rmaxz), right = Side(rmaxx + 1, rmaxx + 1, rminz, rmaxz);
        int grippedFaces = (top ? 1 : 0) + (bot ? 1 : 0) + (left ? 1 : 0) + (right ? 1 : 0);
        if (grippedFaces >= 2 && Cells.Components(terr) >= 2) return (ShapeFamily.Clamp, width);

        // 3. the open path — turns are reflex corners of the terrain outline (terminal excluded), so the count is
        //    width-invariant: a lane and the same lane widened uniformly read the same. 0 = I, 1 = L; ≥2 forks
        //    into branch (U/H) / scythe / Z.
        int bends = Cells.ReflexCorners(terr);
        if (bends == 0) return (ShapeFamily.I, width);
        if (bends == 1) return (ShapeFamily.L, width);

        // ≥2 turns and two terrain legs sharing a bounding-box edge the terminal does NOT sit on = a branch. Split
        // by how the terminal docks the crossbar: flush against a bar wider than itself (the crossbar reaches past
        // the terminal toward the legs) is a U; capping its own narrow room-run stub is an H. The terminal's own
        // edge is excluded from the leg test — otherwise a fold's two path-ends (a scythe) read as a fork. Else a
        // terrain that doubles back (some row/column crosses it in two runs — the fold wrapping the scythe's bay)
        // is a scythe; a staircase of opposing bends is a Z. The fold is read on the terrain cells, not the
        // bounding box, so the verdict only changes when cells actually add or fill a fold — never because a
        // shifted endpoint or a docked neighbour merely moved the bounding box.
        if (ParallelArms(comp, terr, terminal))
            return (FlushOnBar(terr, rminx, rmaxx, rminz, rmaxz) ? ShapeFamily.U : ShapeFamily.H, width);
        return (Cells.HasFold(terr) ? ShapeFamily.Scythe : ShapeFamily.Z, width);
    }

    /// <summary>The open (terminal-less) read of the corridor the piece <paramref name="terminalPieceId"/> caps in
    /// <paramref name="plan"/> (one team unit's terrain — the k=0 image).</summary>
    public static (LaneRead Read, int Width) ClassifyOpen(PlanModel plan, string terminalPieceId)
    {
        var (filled, terminal) = CellSets(plan, terminalPieceId);
        return terminal is null ? (LaneRead.None, 0) : ClassifyOpen(filled, terminal);
    }

    /// <summary>Classify the open corridor a <paramref name="terminal"/> caps within a single unit's
    /// <paramref name="filled"/> terrain. The corridor width <c>W</c> is the first cross-section; a cell is a
    /// JUNCTION if it sits in a filled <c>(W+1)×(W+1)</c> block (wider than the corridor); flood the non-junction
    /// terrain from the terminal = the corridor; its reflex corners are the bends.</summary>
    public static (LaneRead Read, int Width) ClassifyOpen(IReadOnlySet<(int, int)> filled, IReadOnlySet<(int, int)> terminal)
    {
        var seeds = new List<(int, int)>();
        foreach (var r in terminal) foreach (var nb in Cells.N4(r)) if (filled.Contains(nb) && !terminal.Contains(nb)) seeds.Add(nb);
        if (seeds.Count == 0) return (LaneRead.None, 0);

        int w = Cells.MinRunWidth(filled, seeds), k = w + 1;
        bool Blk((int, int) o) { for (var dx = 0; dx < k; dx++) for (var dz = 0; dz < k; dz++) if (!filled.Contains((o.Item1 + dx, o.Item2 + dz))) return false; return true; }
        bool Thick((int, int) c) { for (var x = c.Item1 - k + 1; x <= c.Item1; x++) for (var z = c.Item2 - k + 1; z <= c.Item2; z++) if (Blk((x, z))) return true; return false; }
        bool Narrow((int, int) c) => filled.Contains(c) && !terminal.Contains(c) && !Thick(c);

        var lane = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        foreach (var s in seeds) if (Narrow(s) && lane.Add(s)) q.Enqueue(s);
        while (q.Count > 0) { var cur = q.Dequeue(); foreach (var nb in Cells.N4(cur)) if (Narrow(nb) && lane.Add(nb)) q.Enqueue(nb); }
        if (lane.Count == 0) return (LaneRead.Plaza, w);

        int reflex = Cells.ReflexCorners(lane);
        return (reflex == 0 ? LaneRead.I : reflex == 1 ? LaneRead.L : reflex == 2 ? LaneRead.Z : LaneRead.Complex, w);
    }

    /// <summary>Read a <b>terminal-free body</b> (a <see cref="ShapeBody"/>'s cells) back to its
    /// <see cref="Compound"/> — the derive side of the body mirror, by topology alone (§4 axes: void · branch ·
    /// bends), no terminal. Void count is the strongest signal. <b>Two voids</b> split on whether an <em>open
    /// channel</em> comes between them (two-U-on-I) or a solid wall does (double-hole); <b>one void</b> on whether
    /// an outer concavity — the loop overhanging its longer bar — adds reflex corners past the four a rectangular
    /// void contributes (P) or not (a clean ring). Void-free bodies are the solid <see cref="Compound.Rectangle"/>
    /// or a <see cref="Compound.SpineArms"/> whose arm count is the number of runs hanging off the spine.
    /// Width-independent, matching the approach classifier's philosophy.</summary>
    public static CompoundRead ClassifyBody(IReadOnlySet<(int, int)> cells)
    {
        if (cells.Count == 0) throw new ArgumentException("cannot classify an empty body.");
        var voids = VoidComponents(Cells.EnclosedVoid(cells));

        if (voids.Count == 2)
            return new(ChannelBetween(cells, voids[0], voids[1]) ? Compound.TwoUOnI : Compound.DoubleHole);
        if (voids.Count == 1)
            return new(Cells.ReflexCorners(cells) > 4 ? Compound.P : Compound.Ring);
        if (voids.Count != 0) throw new ArgumentException($"no body form has {voids.Count} voids.");

        var (mnx, mnz, mxx, mxz) = Cells.BoundingBox(cells);
        if (cells.Count == (long)(mxx - mnx + 1) * (mxz - mnz + 1)) return new(Compound.Rectangle);
        return new(Compound.SpineArms, ArmCount(cells, mnx, mnz, mxx, mxz));
    }

    // the enclosed void split into its 4-connected components (each a cell set) — two ⇒ a double-hole or two-U-on-I.
    private static List<HashSet<(int, int)>> VoidComponents(IReadOnlySet<(int, int)> voidCells)
    {
        var seen = new HashSet<(int, int)>();
        var comps = new List<HashSet<(int, int)>>();
        foreach (var c in voidCells)
        {
            if (!seen.Add(c)) continue;
            var comp = new HashSet<(int, int)> { c };
            var q = new Queue<(int, int)>(); q.Enqueue(c);
            while (q.Count > 0) { var d = q.Dequeue(); foreach (var n in Cells.N4(d)) if (voidCells.Contains(n) && seen.Add(n)) { comp.Add(n); q.Enqueue(n); } }
            comps.Add(comp);
        }
        return comps;
    }

    // two voids kept apart by an OPEN channel (⇒ two-U-on-I) rather than a solid wall (⇒ double-hole): the band
    // strictly between them, over their overlapping cross-range, holds a non-terrain (background) cell. The voids
    // sit side by side (separated on one axis); the wall between a ring and a docked-U bay is solid, the channel
    // between twin loops is open.
    private static bool ChannelBetween(IReadOnlySet<(int, int)> cells, HashSet<(int, int)> a, HashSet<(int, int)> b)
    {
        var (ax0, az0, ax1, az1) = Cells.BoundingBox(a);
        var (bx0, bz0, bx1, bz1) = Cells.BoundingBox(b);
        if (ax1 < bx0 || bx1 < ax0)                              // separated in x — scan the column band between
        {
            int gx0 = Math.Min(ax1, bx1) + 1, gx1 = Math.Max(ax0, bx0) - 1;
            int zlo = Math.Max(az0, bz0), zhi = Math.Min(az1, bz1);
            for (var x = gx0; x <= gx1; x++) for (var z = zlo; z <= zhi; z++) if (!cells.Contains((x, z))) return true;
            return false;
        }
        if (az1 < bz0 || bz1 < az0)                              // separated in z — scan the row band between
        {
            int gz0 = Math.Min(az1, bz1) + 1, gz1 = Math.Max(az0, bz0) - 1;
            int xlo = Math.Max(ax0, bx0), xhi = Math.Min(ax1, bx1);
            for (var z = gz0; z <= gz1; z++) for (var x = xlo; x <= xhi; x++) if (!cells.Contains((x, z))) return true;
            return false;
        }
        return false;
    }

    // the arm count of a spine-with-arms: strip the spine — a contiguous full-span band against one side of the
    // bounding box — and count the runs left hanging off it. All four sides are tried and the BRANCHIEST read
    // wins: a body can arrive in any orientation (a negative space's U carries its bar at the bottom), and a
    // full-height end arm can masquerade as a side spine, whose strip merges the true arms into one run — the
    // true spine's strip always separates every arm, so it yields the maximum. Emitted canonical bodies keep
    // their top-spine read (it is the maximal one).
    private static int ArmCount(IReadOnlySet<(int, int)> cells, int mnx, int mnz, int mxx, int mxz)
    {
        bool RowFull(int z) { for (var x = mnx; x <= mxx; x++) if (!cells.Contains((x, z))) return false; return true; }
        bool ColFull(int x) { for (var z = mnz; z <= mxz; z++) if (!cells.Contains((x, z))) return false; return true; }

        var best = 0;
        int r = mnz; while (r <= mxz && RowFull(r)) r++;                 // spine = top full-width rows
        if (r > mnz) best = Math.Max(best, Cells.Components(cells.Where(c => c.Item2 >= r).ToHashSet()));
        int c = mnx; while (c <= mxx && ColFull(c)) c++;                 // spine = left full-height cols
        if (c > mnx) best = Math.Max(best, Cells.Components(cells.Where(p => p.Item1 >= c).ToHashSet()));
        int rb = mxz; while (rb >= mnz && RowFull(rb)) rb--;             // spine = bottom full-width rows
        if (rb < mxz) best = Math.Max(best, Cells.Components(cells.Where(p => p.Item2 <= rb).ToHashSet()));
        int cr = mxx; while (cr >= mnx && ColFull(cr)) cr--;             // spine = right full-height cols
        if (cr < mxx) best = Math.Max(best, Cells.Components(cells.Where(p => p.Item1 <= cr).ToHashSet()));
        return best;
    }

    /// <summary>The lower-case string name of a <see cref="LaneRead"/> (<c>I</c>/<c>L</c>/<c>Z</c>/
    /// <c>complex</c>/<c>plaza</c>/<c>none</c>) — the board deriver's wool-lane measurable, for the derive
    /// gallery and the lane-audit harness.</summary>
    public static string LaneName(LaneRead read) => read switch
    {
        LaneRead.I => "I",
        LaneRead.L => "L",
        LaneRead.Z => "Z",
        LaneRead.Complex => "complex",
        LaneRead.Plaza => "plaza",
        _ => "none",
    };

    // filled terrain (buffers/connectors excluded) + the terminal piece's cells (null when the piece is absent).
    private static (HashSet<(int, int)> Filled, HashSet<(int, int)>? Terminal) CellSets(PlanModel plan, string terminalPieceId)
    {
        var filled = new HashSet<(int, int)>();
        foreach (var p in plan.Pieces)
        {
            if (p.Role is PlanRoles.Buffer or PlanRoles.Connector) continue;
            for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++) filled.Add((x, z));
        }
        var wp = plan.Pieces.FirstOrDefault(p => p.Id == terminalPieceId);
        if (wp is null) return (filled, null);
        var terminal = new HashSet<(int, int)>();
        for (var x = wp.Rect[0]; x < wp.Rect[0] + wp.Rect[2]; x++)
            for (var z = wp.Rect[1]; z < wp.Rect[1] + wp.Rect[3]; z++) terminal.Add((x, z));
        return (filled, terminal);
    }

    // U vs H: does the terminal dock flush against a bar WIDER than itself (the crossbar reaching past the terminal
    // toward the legs — a U), or cap its own narrow stub the width of the terminal (an H)? For each side the
    // terminal attaches to, the bar is flush if terrain continues perpendicular just past the terminal's footprint
    // (the diagonal-adjacent cell) — a crossbar overhangs the terminal, a stub does not. Width-independent.
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

    // A branch: two separate terrain runs pressed against one edge of the bounding box — two legs meeting a
    // shared hub side. Width-independent. An edge the TERMINAL sits on is skipped — a folded path (scythe) ends
    // both its runs on the terminal's edge and must not read as a fork.
    private static bool ParallelArms(HashSet<(int, int)> comp, HashSet<(int, int)> terr, IReadOnlySet<(int, int)> terminal)
    {
        var (mnx, mnz, mxx, mxz) = Cells.BoundingBox(comp);
        bool TwoRuns(IEnumerable<(int, int)> line)
        {
            var cells = line.ToList();
            if (cells.Any(terminal.Contains)) return false;   // the terminal's own edge — a fold, not a fork
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
}
