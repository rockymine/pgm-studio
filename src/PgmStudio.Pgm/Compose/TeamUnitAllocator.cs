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

    /// <summary>Allocate a team unit's <see cref="BoxPartition"/> from the <paramref name="env"/> budget — the
    /// geometry layer over <see cref="SamplePlan"/>. Positions the hub on the (u, v) grid and seats the spawn on
    /// its assigned side, mapping both to plan-cell Rects through the symmetry <see cref="Frame"/>, and emits the
    /// hub↔spawn joint carrying the hub's per-edge <b>w-width offer</b> (the plan <see cref="TeamUnitFiller"/>
    /// consumes). Returns the partition + the spawn facing (<see cref="Frame.TowardAxis"/>), or <c>null</c> when a
    /// box does not fit its hub edge. Wools and the frontline layer on next.</summary>
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

        var boxes = new List<Box> { new("hub", BoxKind.Hub, frame.ToRect(hubUMin, hubU, hubVMin, hubV), hubU * hubV) };
        var joints = new List<BoxJoint>();
        var occupied = new Dictionary<UnitSide, List<(int Start, int Len)>>();   // per-side docked intervals

        (int Min, int Len) Edge(UnitSide s) => s is UnitSide.Front or UnitSide.Back ? (hubVMin, hubV) : (hubUMin, hubU);

        // seat a depth×along box on `side` in a free gap of that hub edge, tracking occupancy so two boxes on one
        // edge (a third wool beside the spawn) do not collide; the box-local alongStart for the joint, or null
        int? SeatOn(UnitSide side, int depth, int along, out int[] rect)
        {
            rect = [];
            var (edgeMin, edgeLen) = Edge(side);
            if (along > edgeLen) return null;
            occupied.TryAdd(side, []);
            if (SeatInGap(occupied[side], edgeLen, along, rng) is not { } local) return null;
            occupied[side].Add((local, along));
            rect = RectFor(frame, side, hubUMin, hubU, hubVMin, hubV, edgeMin + local, depth, along);
            return local;
        }

        // spawn — the straight I for now (cross = entry width, seats cleanly); the L's overhanging foot lands next
        var iSizes = FillProfiles.SpawnSizes.Where(s => s.Family == ShapeFamily.I).ToList();
        var size = iSizes[rng.NextInt(0, iSizes.Count)];
        var (spW, spH) = SpawnBoxEmitter.Box(size.Family, w, size.RunCells, size.TurnCells);
        if (SeatOn(plan.Spawn, spH, spW, out var spawnRect) is not { } spawnStart) return null;
        boxes.Add(new Box("spawn", BoxKind.Spawn, spawnRect, spW * spH));
        joints.Add(HubJoint("hub", "spawn", SideEdge(frame, plan.Spawn), spawnStart, spW, w));

        // wools — budget-share sized (generic, no per-family solve), seated around the spawn: the free sides
        // first, a third doubling into the spawn's edge (the gap-seating keeps them off the spawn)
        var budgetCells = env.LandPerTeam / (env.Cell * (double)env.Cell);
        var flexible = Math.Max(0.0, budgetCells - hubU * hubV);
        const int laneChainBlocks = 50;                              // LN2 chain cap
        var depthCap = Math.Max(4, laneChainBlocks / env.Cell);
        var woolShare = flexible / (plan.Wools.Count + 1.0);         // the spawn takes a rough unit share too
        for (var i = 0; i < plan.Wools.Count; i++)
        {
            var side = plan.Wools[i];
            var (_, edgeLen) = Edge(side);
            var along = Math.Clamp((int)Math.Round(Math.Sqrt(woolShare)), w, Math.Min(3 * w, edgeLen));
            var depth = Math.Clamp((int)Math.Round(woolShare / along), 4, depthCap);
            if (SeatOn(side, depth, along, out var woolRect) is not { } woolStart) return null;
            var id = $"wool-{(char)('a' + i)}";
            boxes.Add(new Box(id, BoxKind.Wool, woolRect, along * depth));
            joints.Add(HubJoint("hub", id, SideEdge(frame, side), woolStart, along, w));
        }

        return (new BoxPartition(boxes, joints), frame.TowardAxis);
    }

    /// <summary>The plan-cell Rect of a depth×along box seated at <paramref name="seat"/> (absolute along-coord)
    /// on <paramref name="side"/>: its depth reaches outward from the hub edge, its along-extent runs along it.</summary>
    private static int[] RectFor(
        Frame frame, UnitSide side, int hubUMin, int hubU, int hubVMin, int hubV, int seat, int depth, int along)
    {
        int hubUMax = hubUMin + hubU, hubVMax = hubVMin + hubV;
        var (uMin, uSpan, vMin, vSpan) = side switch
        {
            UnitSide.Front => (hubUMin - depth, depth, seat, along),
            UnitSide.Back => (hubUMax, depth, seat, along),
            UnitSide.Left => (seat, along, hubVMin - depth, depth),
            _ => (seat, along, hubVMax, depth),                       // Right
        };
        return frame.ToRect(uMin, uSpan, vMin, vSpan);
    }

    /// <summary>A free box-local along-position for an <paramref name="along"/>-wide dock in an
    /// <paramref name="edgeLen"/>-cell edge, avoiding the <paramref name="occupied"/> intervals — sampled within
    /// a randomly chosen fitting gap, or null when no gap holds it.</summary>
    private static int? SeatInGap(List<(int Start, int Len)> occupied, int edgeLen, int along, ComposeRng rng)
    {
        var gaps = new List<(int Lo, int Hi)>();
        var cursor = 0;
        foreach (var (s, l) in occupied.OrderBy(o => o.Start))
        {
            if (s - cursor >= along) gaps.Add((cursor, s));
            cursor = Math.Max(cursor, s + l);
        }
        if (edgeLen - cursor >= along) gaps.Add((cursor, edgeLen));
        if (gaps.Count == 0) return null;
        var (lo, hi) = gaps[rng.NextInt(0, gaps.Count)];
        return lo + rng.NextInt(0, hi - lo - along + 1);
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
