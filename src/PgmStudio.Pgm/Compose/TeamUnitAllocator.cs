using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>A hub side in unit-relative (u, v) terms, before the symmetry frame maps it to a real
/// <see cref="Box"/> edge: <see cref="Front"/> is toward the axis (−u, where the frontline meets the mid),
/// <see cref="Back"/> away from it (+u), <see cref="Left"/>/<see cref="Right"/> the two lateral (±v) sides. The
/// team unit hangs its neighbours off these four sides.</summary>
public enum UnitSide { Front, Back, Left, Right }

/// <summary>The frame-independent <b>placement plan</b> of a team unit (G63-C.2): which hub side each neighbour
/// sits on. <see cref="Frontline"/> is the front side or <c>null</c> (no frontline); <see cref="Spawn"/> is the
/// back or a lateral side; each of <see cref="Wools"/> names its side. Geometry (dims, Rects, the offer plan)
/// is layered on this by the allocator; this is the decision layer.</summary>
public sealed record UnitPlan(UnitSide? Frontline, UnitSide Spawn, IReadOnlyList<UnitSide> Wools);

/// <summary>
/// The partition-first team-unit allocator (G63-C.2) — a <b>clean box-model sampler</b> that decides the unit's
/// structure and lays out box footprints from the budget, replacing <see cref="TeamUnitGrower"/>'s grow-then-fill.
/// This layer is the frame-independent <b>placement plan</b> (<see cref="UnitPlan"/>): the wool count (kept from
/// the grower) and which hub side each neighbour takes. The <b>spawn may sit on the back or a lateral side</b>;
/// the wools are assigned <b>after</b> the spawn and around it — the two free (non-spawn, non-front) sides first,
/// back preferred, a third wool doubling up on the spawn's side (which reduces to the grower's "two side wools +
/// a back wool-c" exactly when the spawn is on the back).
/// </summary>
public static class TeamUnitAllocator
{
    /// <summary>The wool-box count — kept from the grower: 2–3 for a full team (3 two-in-five), one for a tiny
    /// board, else 1–2.</summary>
    public static int WoolCount(ComposeEnvelope env, ComposeRng rng) =>
        env.PlayersPerTeam >= 16 ? (rng.NextBool(0.4) ? 3 : 2)
        : env.LandPerTeam < 600 ? 1
        : rng.NextInt(1, 3);

    /// <summary>Assign each of <paramref name="woolCount"/> wools a hub side, given the <paramref name="spawn"/>'s
    /// side. The two free body sides (back and the sides, minus the spawn's, <b>back first</b>) take a wool each;
    /// a third wool doubles up on the spawn's side. Front is never a wool side (it is the frontline's).</summary>
    public static IReadOnlyList<UnitSide> AssignWools(UnitSide spawn, int woolCount)
    {
        var free = new[] { UnitSide.Back, UnitSide.Left, UnitSide.Right }.Where(s => s != spawn).ToArray();
        var wools = new UnitSide[woolCount];
        for (var i = 0; i < woolCount; i++) wools[i] = i < free.Length ? free[i] : spawn;
        return wools;
    }

    /// <summary>Sample a unit's placement plan: the wool count, the spawn's side (back or a lateral side), and
    /// the wools around it. <paramref name="hasFrontline"/> reserves the front side for the frontline.</summary>
    public static UnitPlan SamplePlan(ComposeEnvelope env, ComposeRng rng, bool hasFrontline)
    {
        var woolCount = WoolCount(env, rng);
        var spawn = new[] { UnitSide.Back, UnitSide.Left, UnitSide.Right }[rng.NextInt(0, 3)];
        return new UnitPlan(hasFrontline ? UnitSide.Front : null, spawn, AssignWools(spawn, woolCount));
    }

    /// <summary>The clearance kept between a docked neighbour and each hub <b>corner</b>, in cells — so no two
    /// neighbours on adjacent hub sides seat at a shared corner and only corner-touch (a diagonal pinch).</summary>
    private const int CornerClearanceCells = 1;

    /// <summary>A neighbour box to seat against the hub: the hub <see cref="Side"/> it docks, its box
    /// <see cref="Kind"/>, its outward <see cref="Depth"/> (perpendicular to the hub edge) and along-edge
    /// <see cref="Along"/> extent (cells), and its <see cref="Id"/>. Sizing is frame- and form-independent (it
    /// reads the budget); only the seat position is not — so the whole set is fixed before the form is chosen.</summary>
    private sealed record Demand(UnitSide Side, BoxKind Kind, int Depth, int Along, string Id);

    /// <summary>Allocate a team unit's <see cref="BoxPartition"/> from the <paramref name="env"/> budget — the
    /// geometry layer over <see cref="SamplePlan"/>. Positions the hub on the (u, v) grid, <b>owns the hub-form
    /// choice</b> (map-generation.md §5.5), and seats the spawn and wools on the chosen form's <b>real free-edge
    /// intervals</b> — the offerable surface the body actually presents (§1.13), not its bounding box. So a
    /// non-rectangular hub (L/U/Ring/Double-hole) never leaves a neighbour docking an empty bbox stretch and only
    /// corner-touching it (a <c>t*/*t</c> pinch); the four-full-edges rectangle is just the degenerate case. The
    /// chosen form rides on the hub <see cref="Box.Form"/> for the filler to re-emit; each hub↔neighbour joint
    /// carries the hub's per-edge <b>w-width offer</b> (the plan <see cref="TeamUnitFiller"/> consumes). The
    /// sampled form <b>falls back to the solid <see cref="Compound.Rectangle"/></b> when its free edges cannot host
    /// the plan. Returns the partition + the spawn facing (<see cref="Frame.TowardAxis"/>), or <c>null</c> when
    /// even the rectangle cannot host a neighbour (the box is too small — the directed "no shape fits" signal, §4).
    /// Wools and the frontline layer on next.</summary>
    public static (BoxPartition Partition, string SpawnFacing)? Allocate(ComposeEnvelope env, ComposeRng rng)
    {
        var frame = Frame.For(env.Symmetry);
        var w = env.LandPerTeam > 2500 ? 3 : 2;                       // lane width (LN1: 10; 15 on big maps)
        var plan = SamplePlan(env, rng, hasFrontline: false);         // frontline geometry lands in a later slice

        // hub dims — simplified floors/caps (the frontline/twin/wool-c clearance floors refine as those land)
        var cap = env.LandPerTeam >= 3000 ? 6 : env.LandPerTeam >= 1500 ? 5 : env.LandPerTeam >= 800 ? 4 : 3;
        var floor = w + 2;
        var hubU = rng.NextInt(floor, Math.Max(floor, cap) + 1);
        var hubV = rng.NextInt(floor, Math.Max(floor, cap) + 1);
        var hubUMin = Envelope.AxisMarginCells;                       // + the frontline's reach when it lands
        var hubVMin = -(hubV / 2);
        var hubRect = frame.ToRect(hubUMin, hubU, hubVMin, hubV);

        // the neighbour demands (spawn + wools), sized from the budget before the form is chosen
        var demands = Demands(env, rng, plan, w, hubU, hubV);

        // pick the hub form, seat the demands on its real free edges; fall back to the solid rectangle (four full
        // edges) when the sampled form's offerable surface cannot host every dock
        var sampled = rng.Pick(HubBoxEmitter.Forms);
        var seating = Seat(sampled, hubRect, frame, w, demands, rng);
        if (seating is null && sampled.Form != Compound.Rectangle)
            seating = Seat(new CompoundRead(Compound.Rectangle), hubRect, frame, w, demands, rng);
        if (seating is not { } s) return null;

        return (new BoxPartition(s.Boxes, s.Joints), frame.TowardAxis);
    }

    /// <summary>The neighbour boxes to seat: the spawn (a straight I for now — cross = entry width, seats
    /// cleanly; the L's overhanging foot lands next) plus the budget-share-sized wools, each on its planned side
    /// (the free sides first, a third doubling into the spawn's edge). The spawn size is the one RNG draw here;
    /// the wool sizes read the budget (generic, no per-family solve), so the whole set is fixed before the form
    /// is chosen and is identical across a fallback re-seat.</summary>
    private static IReadOnlyList<Demand> Demands(
        ComposeEnvelope env, ComposeRng rng, UnitPlan plan, int w, int hubU, int hubV)
    {
        var demands = new List<Demand>();

        var iSizes = FillProfiles.SpawnSizes.Where(sz => sz.Family == ShapeFamily.I).ToList();
        var size = iSizes[rng.NextInt(0, iSizes.Count)];
        var (spW, spH) = SpawnBoxEmitter.Box(size.Family, w, size.RunCells, size.TurnCells);
        demands.Add(new Demand(plan.Spawn, BoxKind.Spawn, spH, spW, "spawn"));

        var budgetCells = env.LandPerTeam / (env.Cell * (double)env.Cell);
        var flexible = Math.Max(0.0, budgetCells - hubU * hubV);
        const int laneChainBlocks = 50;                              // LN2 chain cap
        var depthCap = Math.Max(4, laneChainBlocks / env.Cell);
        var woolShare = flexible / (plan.Wools.Count + 1.0);         // the spawn takes a rough unit share too
        for (var i = 0; i < plan.Wools.Count; i++)
        {
            var side = plan.Wools[i];
            var edgeLen = side is UnitSide.Front or UnitSide.Back ? hubV : hubU;
            var along = Math.Clamp((int)Math.Round(Math.Sqrt(woolShare)), w, Math.Min(3 * w, edgeLen - 2 * CornerClearanceCells));
            var depth = Math.Clamp((int)Math.Round(woolShare / along), 4, depthCap);
            demands.Add(new Demand(side, BoxKind.Wool, depth, along, $"wool-{(char)('a' + i)}"));
        }
        return demands;
    }

    /// <summary>Seat every demand on <paramref name="form"/>'s real free-edge intervals, seated on the hub
    /// <paramref name="hubRect"/>. Builds the body once (<see cref="HubBoxEmitter"/>) — the same body the filler
    /// re-emits, so both read the same runs — and reads its per-edge free runs off the emitted offers (the
    /// offerable surface, §1.13). Returns the hub box (carrying <paramref name="form"/> for the filler) plus the
    /// seated neighbour boxes and their hub joints, or <c>null</c> when the box is too small for the form or a
    /// demand finds no free run to dock (the directed signal the caller answers by falling back / resampling).</summary>
    private static (List<Box> Boxes, List<BoxJoint> Joints)? Seat(
        CompoundRead form, int[] hubRect, Frame frame, int w, IReadOnlyList<Demand> demands, ComposeRng rng)
    {
        int boxW = hubRect[2], boxH = hubRect[3];
        var hubBox = new Box("hub", BoxKind.Hub, hubRect, boxW * boxH, form);
        if (HubBoxEmitter.Fill(hubBox, form, FillProfiles.HubWallCells) is not { } hub) return null;   // too small for the form

        // the offerable surface: the contiguous free runs on each hub edge (box-local along-coords), read off the
        // emitted body's per-edge offers — one offer per free run, so a bay simply yields no run over its stretch
        var runsByEdge = hub.Offers.GroupBy(o => o.Edge).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<(int Start, int Len)>)g.Select(o => (o.Interval.Start, o.Interval.LengthCells)).ToList());

        var boxes = new List<Box> { hubBox };
        var joints = new List<BoxJoint>();
        var occupied = new Dictionary<BoxEdge, List<(int Start, int Len)>>();   // per-edge docked intervals

        foreach (var d in demands)
        {
            var edge = SideEdge(frame, d.Side);
            var edgeLen = edge is BoxEdge.Top or BoxEdge.Bottom ? boxW : boxH;
            if (!runsByEdge.TryGetValue(edge, out var runs)) return null;      // the form leaves this edge empty
            occupied.TryAdd(edge, []);
            if (SeatInRuns(runs, occupied[edge], edgeLen, d.Along, CornerClearanceCells, rng) is not { } seat)
                return null;
            occupied[edge].Add((seat, d.Along));
            boxes.Add(new Box(d.Id, d.Kind, NeighbourRect(edge, seat, d.Depth, d.Along, hubRect), d.Along * d.Depth));
            joints.Add(HubJoint("hub", d.Id, edge, seat, d.Along, w));
        }
        return (boxes, joints);
    }

    /// <summary>The plan-cell rect of a <paramref name="depth"/>×<paramref name="along"/> box seated at box-local
    /// along-coord <paramref name="seat"/> on the hub's <paramref name="edge"/>: its depth reaches outward from
    /// that edge, its along-extent runs along it. Frame-free — the (u, v) frame chose the edge; the box then
    /// follows the edge's outward normal (Top −z, Bottom +z, Left −x, Right +x), so the seating needs no per-mode
    /// branch and stays correct where a (u, v)→box-local run mapping would reverse.</summary>
    private static int[] NeighbourRect(BoxEdge edge, int seat, int depth, int along, int[] hub)
    {
        int hx = hub[0], hz = hub[1], hw = hub[2], hh = hub[3];
        return edge switch
        {
            BoxEdge.Top => [hx + seat, hz - depth, along, depth],
            BoxEdge.Bottom => [hx + seat, hz + hh, along, depth],
            BoxEdge.Left => [hx - depth, hz + seat, depth, along],
            _ => [hx + hw, hz + seat, depth, along],                  // Right
        };
    }

    /// <summary>A free box-local along-position for an <paramref name="along"/>-wide dock among the edge's
    /// <paramref name="runs"/> (its offerable surface), avoiding the <paramref name="occupied"/> intervals and an
    /// <paramref name="inset"/>-cell clearance at each <b>box corner</b> — a run end coinciding with along-coord 0
    /// or <paramref name="edgeLen"/>, so no neighbour seats at a hub corner and corner-touches a neighbour on the
    /// adjacent side; an internal run end (a bay boundary) is no box corner and needs no inset. Sampled within a
    /// randomly chosen fitting gap, or null when no gap holds it.</summary>
    private static int? SeatInRuns(
        IReadOnlyList<(int Start, int Len)> runs, List<(int Start, int Len)> occupied,
        int edgeLen, int along, int inset, ComposeRng rng)
    {
        var gaps = new List<(int Lo, int Hi)>();
        foreach (var (rs, rl) in runs)
        {
            int lo = rs, hi = rs + rl;
            if (lo == 0) lo += inset;                                 // a box corner at the low end
            if (hi == edgeLen) hi -= inset;                          // a box corner at the high end
            var cursor = lo;
            foreach (var (os, ol) in occupied.Where(o => o.Start < hi && o.Start + o.Len > lo).OrderBy(o => o.Start))
            {
                if (os - cursor >= along) gaps.Add((cursor, os));
                cursor = Math.Max(cursor, os + ol);
            }
            if (hi - cursor >= along) gaps.Add((cursor, hi));
        }
        if (gaps.Count == 0) return null;
        var (glo, ghi) = gaps[rng.NextInt(0, gaps.Count)];
        return glo + rng.NextInt(0, ghi - glo - along + 1);
    }

    /// <summary>The hub↔neighbour joint carrying the hub's offer on <paramref name="edge"/>: the interface
    /// interval where they touch, and an <see cref="EdgeOffer"/> whose width is the lane width
    /// <paramref name="w"/> the neighbour reads as its <c>cw</c> (severally — each neighbour its own dock).</summary>
    private static BoxJoint HubJoint(string hubId, string nbId, BoxEdge edge, int alongStart, int along, int w)
    {
        var iface = new BoxInterface(edge, alongStart, along);
        var offer = new EdgeOffer(edge, new EdgeInterval(alongStart, along, ApproachSlots.Bar), w, OfferGrouping.Several, $"hub-{edge}");
        return new BoxJoint(hubId, nbId, iface, offer);
    }

    /// <summary>The hub's box edge facing <paramref name="side"/> — the (u, v) outward direction mapped through
    /// the <see cref="Frame"/> to a box-local edge (min-z Top, max-z Bottom, min-x Left, max-x Right).</summary>
    private static BoxEdge SideEdge(Frame frame, UnitSide side)
    {
        var (du, dv) = side switch
        {
            UnitSide.Front => (-1.0, 0.0),
            UnitSide.Back => (1.0, 0.0),
            UnitSide.Left => (0.0, -1.0),
            _ => (0.0, 1.0),                                          // Right
        };
        var (ox, oz) = frame.ToPoint(0, 0);
        var (px, pz) = frame.ToPoint(du, dv);
        double dx = px - ox, dz = pz - oz;
        if (dz < 0) return BoxEdge.Top;
        if (dz > 0) return BoxEdge.Bottom;
        return dx < 0 ? BoxEdge.Left : BoxEdge.Right;
    }
}
