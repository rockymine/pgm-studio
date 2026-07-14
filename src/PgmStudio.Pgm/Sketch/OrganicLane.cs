using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Organic lane growth — the engine behind the <see cref="LaneArchetype.Organic"/> board. A team's island is
/// grown outward from a spawn <b>hub</b> into a few <b>wool lanes</b>: a noise grid spreads the wool tips
/// across the far edge (farthest-point sampling), each lane is bent by noise-offset waypoints, and the lane
/// body is a variable-width jittered polygon <b>ribbon</b> (an interesting hull, not a clean rectangle) that
/// may carry a <b>diamond hole</b>. A lane is one hub→tip branch ending in a dead-end wool tip; the spawn
/// sits at the hub where the lanes meet. Pure + seeded (same <see cref="LaneLayoutOptions.Seed"/> → same
/// island).
/// </summary>
public static class OrganicLane
{
    public sealed record Unit(List<SketchShape> Shapes, (double X, double Z) Spawn,
        List<(double X, double Z)> Tips, List<(double X, double Z)> TrunkTips);

    /// <summary>A coherent value-noise field sampled on a regular grid, for the demonstration page. Row-major:
    /// the value at column c (x = X0 + c·Step) row r (z = Z0 + r·Step) is Values[r·Cols + c], each in [0,1].</summary>
    public sealed record NoiseGrid(double X0, double Z0, double Step, int Cols, int Rows, double[] Values);

    /// <summary>The spawn-hub plaza: centre, outer radius, style (0 round · 1 square · 2 organic blob) and the
    /// ring-hole radius (0 = no hole).</summary>
    public sealed record HubInfo(double X, double Z, double R, int Style, double HoleR);

    /// <summary>One lane spine — a Catmull-Rom centerline — tagged with the role it plays
    /// (<c>trunk</c> · <c>lane</c> · <c>spawn</c> · <c>fork-primary</c> · <c>fork-child</c>).</summary>
    public sealed record Spine(string Kind, List<double[]> Points);

    /// <summary>The Organic generator's per-stage intermediates, captured from a single real <see cref="Grow"/>
    /// run for the demonstration page: the value-noise field, the sampled anchors (hub + trunk + wool tips),
    /// the lane spines, the assembled ribbon shapes and the placed objectives. The board size + mirror settings
    /// let a viewer reproduce the symmetry mirror.</summary>
    public sealed record Stages(
        double Width, double Height, double LaneWidth, double Margin,
        NoiseGrid Noise, HubInfo Hub,
        List<(double X, double Z)> TrunkTips, List<(double X, double Z)> WoolTips,
        (double X, double Z) Spawn, List<(double X, double Z)> WoolObjs,
        List<Spine> Spines, List<SketchShape> Shapes,
        string MirrorMode, double Cx, double Cz);

    /// <summary>Mutable collector for the per-stage geometry; populated by <see cref="Grow"/> only when a
    /// trace is attached, so the normal generate path stays allocation-free. Sampling the noise + recording
    /// spines never touches the RNG, so a traced run produces an identical unit to an untraced one.</summary>
    internal sealed class StageTrace
    {
        public NoiseGrid? Noise;
        public HubInfo? Hub;
        public List<(double X, double Z)> WoolTips = [];
        public readonly List<Spine> Spines = [];
        public void AddSpine(string kind, List<double[]> pts) =>
            Spines.Add(new Spine(kind, pts.Select(p => new[] { p[0], p[1] }).ToList()));
    }

    /// <summary>Grow a unit AND capture every stage's geometry for the demonstration page. Runs the exact same
    /// <see cref="Grow"/> pipeline (deterministic for the seed) with a trace attached.</summary>
    public static Stages GrowStages(LaneLayoutOptions o)
    {
        var trace = new StageTrace();
        var unit = Grow(o, trace);
        var mode = string.IsNullOrEmpty(o.MirrorMode) ? "mirror_z" : o.MirrorMode;
        return new Stages(
            o.Width, o.Height, o.LaneWidth, o.Margin,
            trace.Noise!, trace.Hub!,
            unit.TrunkTips, trace.WoolTips,
            unit.Spawn, unit.Tips,
            trace.Spines, unit.Shapes,
            mode, o.Width / 2, o.Height / 2);
    }

    public static Unit Grow(LaneLayoutOptions o) => Grow(o, null);

    private static Unit Grow(LaneLayoutOptions o, StageTrace? trace)
    {
        var rng = new Rng(o.Seed);
        double W = o.Width, H = o.Height, m = o.Margin, lw = o.LaneWidth, midZ = H / 2;
        var noise = new NoiseField(o.Seed, Math.Max(6.0, lw));
        if (trace is not null) trace.Noise = SampleNoise(noise, o);
        var minAngle = o.MinHubAngle * Math.PI / 180.0;

        // hub junction: above the mid line, facing the foe — the trunks and wool lanes meet here
        var hub = (W / 2 + rng.Range(-W * 0.06, W * 0.06), midZ - lw * 2.0);

        // mid trunks: 1–2 short branches reaching toward the mid line so the island connects across the void.
        // The island stops VoidDistance/2 short of mid; two well-separated branches → two bridges → two angles
        // of attack across the centre instead of a single chokepoint.
        var reachZ = midZ - o.VoidDistance / 2;
        var branches = Math.Max(1, o.MidBranches);
        var trunkTips = new List<(double X, double Z)>();
        for (var k = 0; k < branches; k++)
        {
            var fx = branches == 1 ? 0.0 : k / (double)(branches - 1) - 0.5;     // −0.5 … 0.5
            trunkTips.Add((hub.Item1 + fx * lw * 2.4 + rng.Range(-lw * 0.2, lw * 0.2), reachZ));
        }
        var trunkDirs = trunkTips.Select(t => Math.Atan2(t.Z - hub.Item2, t.X - hub.Item1)).ToList();

        // wool tips: far-spread on the noise grid, but kept ≥ minAngle apart from each other AND the trunks —
        // the hub-fan minimum, so no two branches fan out tightly enough to pinch a sliver of land between them.
        var tips = FarthestTips(o, rng, noise, hub, Math.Max(1, o.Wools), minAngle, trunkDirs);
        if (trace is not null) trace.WoolTips = tips;

        var shapes = new List<SketchShape>();
        var id = 0;

        // hub plaza: a small open area where the branches meet, so they fan out of an AREA not a point (a point
        // hub pinches thin land wedges and gives no room). Its shape varies — round-ish blob, rotated square, or
        // an organic jittered polygon — and a big-enough hub may carry its own hole, making a ring plaza (the
        // corpus centre). Lanes attach by submerging two nodes inside it rather than colliding at its centre.
        var hubStyle = rng.Int(0, 3);                          // 0 round · 1 square · 2 organic blob
        var wantHole = rng.Chance(0.45);                       // a ring-plaza centre
        // size the hub off its style; grow it when it must hold a ring (squares/blobs have a tighter inradius)
        var baseR = wantHole ? rng.Range(1.5, 1.9) : rng.Range(1.1, 1.5);
        var hubR = lw * (hubStyle == 1 ? baseR * 1.15 : baseR);
        shapes.Add(Poly("hub", HubPolygon(hub, hubR, hubStyle, rng, noise), "add"));
        // radius guaranteed inside the hub for ANY direction (a square is the tightest) — lanes attach within it
        var hubInr = hubR * (hubStyle == 1 ? 0.68 : hubStyle == 2 ? 0.72 : 0.9);
        // hole sized to leave a ring the lanes can attach to (outside it); skip if the hub came out too small
        var holeR = wantHole && hubInr >= lw * 1.0 ? Math.Min(hubInr - lw * 0.55, hubInr * 0.6) : 0.0;
        if (trace is not null) trace.Hub = new HubInfo(hub.Item1, hub.Item2, hubR, hubStyle, holeR);

        foreach (var tt in trunkTips)
            shapes.Add(Poly($"trunk{id++}", GrowLane(rng, noise, hub, hubInr, holeR, tt, lw, o, allowHole: false, trace: trace, spineKind: "trunk").Ribbon, "add"));

        // one lane per wool tip: hub → tip, bent + varied, optional hole inside. The wool objective is inset off
        // the dead-end tip into the lane body (≈ half a lane width) so it carries cover; the lane caps beyond it.
        var woolObjs = new List<(double X, double Z)>();
        foreach (var tip in tips)
        {
            // a long enough wool lane may FORK into a child branch (the wool stays on the primary tip)
            if (o.BranchChance > 0 && Dist(hub, tip) > lw * 3.5 && rng.Chance(o.BranchChance))
            {
                var fork = GrowForkedLane(rng, noise, hub, hubInr, holeR, tip, lw, o, minAngle, id, trace);
                shapes.AddRange(fork.Shapes);
                woolObjs.Add(fork.WoolTip);
            }
            else
            {
                var lane = GrowLane(rng, noise, hub, hubInr, holeR, tip, lw, o, allowHole: true, trace: trace, spineKind: "lane");
                shapes.Add(Poly($"lane{id}", lane.Ribbon, "add"));
                if (lane.Hole is { } hole) shapes.Add(Poly($"hole{id}", hole, "subtract"));
                woolObjs.Add(lane.Obj);
            }
            id++;
        }

        // Spawn goes on its OWN spur off the hub, tucked into a pocket pointing AWAY from the mid line (not
        // toward the bridge) so its protection sits well off the crossing — an attacker reaching the hub fans
        // out to the wools with room, not squeezing a single corridor past the spawn. See the contract doc.
        var spawn = SpawnSpur(rng, noise, hub, hubInr, holeR, trunkDirs, tips, lw, o, minAngle, shapes, trace);

        // cut the hub's own hole last, so it stays open regardless of the lanes that submerge into the ring
        if (holeR > 0) shapes.Add(Poly("hubhole", HolePolygon(hub.Item1, hub.Item2, holeR, rng), "subtract"));
        return new Unit(shapes, spawn, woolObjs, trunkTips);
    }

    private static (double X, double Z) SpawnSpur(
        Rng rng, NoiseField noise, (double X, double Z) hub, double hubInr, double holeR, List<double> trunkDirs,
        List<(double X, double Z)> tips, double lw, LaneLayoutOptions o, double minAngle, List<SketchShape> shapes,
        StageTrace? trace = null)
    {
        var dirs = new List<double>(trunkDirs);
        dirs.AddRange(tips.Select(t => Math.Atan2(t.Z - hub.Z, t.X - hub.X)));
        dirs.Sort();
        (double Angle, double Width) widest = (0, -1), away = (0, -1);
        for (var i = 0; i < dirs.Count; i++)
        {
            var a = dirs[i];
            var b = dirs[(i + 1) % dirs.Count] + (i + 1 == dirs.Count ? 2 * Math.PI : 0);
            var mid = a + (b - a) / 2; var width = b - a;
            if (width > widest.Width) widest = (mid, width);
            // "away from the bridge": the spur should not point toward the mid line (+z). Prefer an upward gap.
            if (Math.Sin(mid) < 0 && width > away.Width) away = (mid, width);
        }
        var chosen = away.Width >= 2 * minAngle ? away.Angle : widest.Angle;     // prefer a roomy away-from-mid pocket
        var len = lw * 2.3;                                                       // deeper spur → spawn off the hub mouth
        var spawnTip = (hub.X + Math.Cos(chosen) * len, hub.Z + Math.Sin(chosen) * len);
        var spur = GrowLane(rng, noise, hub, hubInr, holeR, spawnTip, lw * 0.9, o, allowHole: false, trace: trace, spineKind: "spawn");
        shapes.Add(Poly("spawnspur", spur.Ribbon, "add"));
        return spur.Obj;   // inset off the spur tip, like the wools
    }

    // ── far-spread wool tips from the noise grid (toward the far edge), kept ≥ minAngle apart at the hub ──
    private static List<(double X, double Z)> FarthestTips(LaneLayoutOptions o, Rng rng, NoiseField noise,
        (double X, double Z) hub, int count, double minAngle, List<double> reserved)
    {
        double W = o.Width, H = o.Height, m = o.Margin, lw = o.LaneWidth;
        var step = Math.Max(4.0, lw * 0.5);
        var cands = new List<(double X, double Z, double N)>();
        var threshold = 0.42;
        while (cands.Count < count + 2 && threshold > -0.01)
        {
            cands.Clear();
            for (var x = m + lw; x <= W - m - lw; x += step)
                for (var z = m; z <= H * 0.24; z += step)
                {
                    var nz = noise.At(x, z);
                    if (nz > threshold) cands.Add((x + rng.Range(-step * 0.3, step * 0.3), z + rng.Range(-step * 0.3, step * 0.3), nz));
                }
            threshold -= 0.1;
        }
        if (cands.Count == 0)
            for (var i = 0; i < count; i++) cands.Add((m + lw + (W - 2 * (m + lw)) * (i + 0.5) / count, m + lw, 1));

        double Ang((double X, double Z) p) => Math.Atan2(p.Z - hub.Z, p.X - hub.X);
        var first = cands.OrderByDescending(c => c.N).First();
        var chosen = new List<(double X, double Z)> { (first.X, first.Z) };
        while (chosen.Count < count)
        {
            (double X, double Z) best = default;
            var bestScore = -1.0;
            // first pass requires ≥ minAngle from every chosen tip and the trunk dirs (the hub-fan minimum);
            // relax only if no candidate qualifies so a tip is always produced.
            for (var pass = 0; pass < 2 && bestScore < 0; pass++)
                foreach (var c in cands)
                {
                    if (pass == 0)
                    {
                        var ca = Ang((c.X, c.Z));
                        if (chosen.Any(ch => Math.Abs(AngleDiff(ca, Ang(ch))) < minAngle)
                            || reserved.Any(rd => Math.Abs(AngleDiff(ca, rd)) < minAngle)) continue;
                    }
                    var d = Math.Min(Dist((c.X, c.Z), hub), chosen.Min(ch => Dist((c.X, c.Z), ch)));
                    var score = d * (0.5 + c.N);             // far-spread, weighted by the noise grid
                    if (score > bestScore) { bestScore = score; best = (c.X, c.Z); }
                }
            chosen.Add(best);
        }
        return chosen;
    }

    // signed smallest angle a→b in (−π, π]
    private static double AngleDiff(double a, double b)
    {
        var d = (a - b) % (2 * Math.PI);
        if (d > Math.PI) d -= 2 * Math.PI;
        if (d < -Math.PI) d += 2 * Math.PI;
        return d;
    }

    // ── a lane's SPINE: attach into the hub (two submerged nodes, kept in the ring if it has a hole) → 1–2
    // noise-offset bend waypoints → tip. Centripetal smoothing means the short attach segment doesn't overshoot;
    // connectors (allowBend false) run straight, as a short bent strip self-intersects when offset. ───────────
    private static List<double[]> LaneCenterline(
        Rng rng, NoiseField noise, (double X, double Z) hub, double hubInr, double holeR,
        (double X, double Z) tip, double lw, bool allowBend)
    {
        double ax = tip.X - hub.X, az = tip.Z - hub.Z;
        var adist = Math.Sqrt(ax * ax + az * az);
        double ux = ax / Math.Max(1e-6, adist), uz = az / Math.Max(1e-6, adist);   // unit hub→tip
        // two attach nodes, both inside the hub's inradius (so they're submerged for any direction / hub shape).
        // Without a hole the deep node nearly reaches the centre so adjacent lanes overlap and FILL the wedges
        // between them (no thin slivers); with a hole it stays just outside the ring.
        var outer = hubInr * 0.9;
        var inner = holeR > 0 ? Math.Max(holeR + lw * 0.35, outer * 0.55) : outer * 0.2;
        if (inner >= outer) inner = outer * 0.6;
        var entry = (X: hub.X + ux * outer, Z: hub.Z + uz * outer);

        double dx = tip.X - entry.X, dz = tip.Z - entry.Z;
        var len = Math.Sqrt(dx * dx + dz * dz);
        double px = -dz / Math.Max(1e-6, len), pz = dx / Math.Max(1e-6, len);   // perpendicular to entry→tip
        var bend = allowBend ? lw * 0.55 : 0.0;

        var ctrl = new List<double[]>
        {
            new[] { hub.X + ux * inner, hub.Z + uz * inner },
            new[] { entry.X, entry.Z },
        };
        var waypoints = allowBend ? rng.Int(1, 3) : 1;
        for (var k = 1; k <= waypoints; k++)
        {
            var t = k / (double)(waypoints + 1);
            double bx = entry.X + dx * t, bz = entry.Z + dz * t;
            var off = (noise.At(bx, bz) - 0.5) * 2 * bend;                       // gentle bends; 0 for connectors
            ctrl.Add([bx + px * off, bz + pz * off]);
        }
        ctrl.Add([tip.X, tip.Z]);
        return CatmullRom.Spline(ctrl, 16);
    }

    // ── a SPINE → variable-width ribbon polygon (+ optional organic hole), plus the inset tip objective ───────
    private static (List<double[]> Ribbon, List<double[]>? Hole, (double X, double Z) Obj) RibbonFromCenterline(
        List<double[]> center, double lw, LaneLayoutOptions o, Rng rng, NoiseField noise, bool allowHole)
    {
        var n = center.Count;
        var half = new double[n];                                                // base half-width, tapering to the tip
        for (var i = 0; i < n; i++) half[i] = lw * 0.5 * (1 - 0.25 * (i / (double)(n - 1)));
        double total = 0;
        for (var i = 1; i < n; i++) total += Dist((center[i][0], center[i][1]), (center[i - 1][0], center[i - 1][1]));

        // optional hole: bulge the ribbon so a path of at least 0.7·laneWidth remains on EACH side of the hole
        // (the corpus keeps holes inside wide lanes, never as thin necks), then cut an organic 4–6-gon.
        List<double[]>? hole = null;
        if (allowHole && rng.Chance(o.HoleChance) && total > lw * 4)
        {
            var hi = Math.Clamp(n / 2 + rng.Int(-n / 6, n / 6 + 1), 3, n - 4);
            var window = Math.Max(2, n / 6);
            var hr = lw * rng.Range(0.35, 0.55);
            var needHalf = hr + lw * 0.85;
            for (var i = Math.Max(0, hi - window); i <= Math.Min(n - 1, hi + window); i++)
            {
                var falloff = 1 - Math.Abs(i - hi) / (double)window;
                half[i] = Math.Max(half[i], lw * 0.5 + (needHalf - lw * 0.5) * falloff);
            }
            hole = HolePolygon(center[hi][0], center[hi][1], hr, rng);
        }

        // jitter the two sides independently for an organic outline
        var left = new List<double>(n);
        var right = new List<double>(n);
        for (var i = 0; i < n; i++)
        {
            var p = center[i];
            var jl = (noise.At(p[0] + 13.7, p[1] + 5.1) - 0.5) * lw * 0.3;
            var jr = (noise.At(p[0] - 7.3, p[1] + 19.4) - 0.5) * lw * 0.3;
            left.Add(Math.Max(2, half[i] + jl));
            right.Add(Math.Max(2, half[i] + jr));
        }
        return (Lane.Ribbon(center, left, right), hole, InsetAlong(center, lw * 0.5));
    }

    // one straightforward lane = spine + ribbon (the inset tip objective is the wool). The optional trace
    // records the spine under spineKind, for the demonstration page.
    private static (List<double[]> Ribbon, List<double[]>? Hole, (double X, double Z) Obj) GrowLane(
        Rng rng, NoiseField noise, (double X, double Z) hub, double hubInr, double holeR,
        (double X, double Z) tip, double lw, LaneLayoutOptions o, bool allowHole,
        StageTrace? trace = null, string? spineKind = null)
    {
        var center = LaneCenterline(rng, noise, hub, hubInr, holeR, tip, lw, allowBend: allowHole);
        if (trace is not null && spineKind is not null) trace.AddSpine(spineKind, center);
        return RibbonFromCenterline(center, lw, o, rng, noise, allowHole);
    }

    // ── a FORKED lane: a wool lane that splits. A primary hub→tip strip, then a CHILD branch grows off a point
    // partway along it — the child anchors back along the parent so the two ribbons overlap and the junction
    // unions cleanly (the same submerge-attach idea as the hub). Returns every shape plus the wool tip; which
    // leaves carry wools is policy left to the caller — today the wool stays on the primary tip, child = terrain.
    private static (List<SketchShape> Shapes, (double X, double Z) WoolTip) GrowForkedLane(
        Rng rng, NoiseField noise, (double X, double Z) hub, double hubInr, double holeR,
        (double X, double Z) primaryTip, double lw, LaneLayoutOptions o, double minAngle, int id,
        StageTrace? trace = null)
    {
        var shapes = new List<SketchShape>();
        var pc = LaneCenterline(rng, noise, hub, hubInr, holeR, primaryTip, lw, allowBend: true);
        if (trace is not null) trace.AddSpine("fork-primary", pc);
        var prim = RibbonFromCenterline(pc, lw, o, rng, noise, allowHole: true);
        shapes.Add(Poly($"lane{id}", prim.Ribbon, "add"));
        if (prim.Hole is { } h) shapes.Add(Poly($"hole{id}", h, "subtract"));

        // fork point partway along the primary spine; child heads off by ≥ minAngle to one side
        var fi = Math.Clamp((int)(pc.Count * rng.Range(0.45, 0.62)), 3, pc.Count - 4);
        (double X, double Z) fp = (pc[fi][0], pc[fi][1]);
        double tdx = pc[fi + 1][0] - pc[fi - 1][0], tdz = pc[fi + 1][1] - pc[fi - 1][1];
        var tl = Math.Sqrt(tdx * tdx + tdz * tdz);
        (double X, double Z) pd = (tdx / Math.Max(1e-6, tl), tdz / Math.Max(1e-6, tl));   // parent direction at the fork
        var sign = rng.Chance(0.5) ? 1.0 : -1.0;
        var cang = Math.Atan2(pd.Z, pd.X) + sign * (minAngle + rng.Range(0.2, 0.6));
        var clen = lw * rng.Range(2.2, 3.6);
        (double X, double Z) childTip = (fp.X + Math.Cos(cang) * clen, fp.Z + Math.Sin(cang) * clen);

        // child spine: an anchor back along the parent (overlap at the junction), the fork point, the child tip
        (double X, double Z) anchor = (fp.X - pd.X * lw * 0.8, fp.Z - pd.Z * lw * 0.8);
        var cc = CatmullRom.Spline([[anchor.X, anchor.Z], [fp.X, fp.Z], [childTip.X, childTip.Z]], 16);
        if (trace is not null) trace.AddSpine("fork-child", cc);
        shapes.Add(Poly($"branch{id}", RibbonFromCenterline(cc, lw, o, rng, noise, allowHole: false).Ribbon, "add"));
        return (shapes, prim.Obj);
    }

    /// <summary>A non-mirrored catalogue layout for documenting the primitives: the top row is one example of
    /// every hub-plaza style (round · square · organic blob · round+hole · blob+hole); the bottom row is every
    /// lane behaviour (straight connector · bend · hole · fork) attached to a small hub. Deterministic.</summary>
    public static SketchLayout StyleCatalog()
    {
        var rng = new Rng(7);
        var noise = new NoiseField(7, 12);
        double lw = 12, cell = 110, hubR = lw * 1.7;
        var minAngle = 35 * Math.PI / 180.0;
        var noHole = new LaneLayoutOptions { HoleChance = 0 };
        var allHole = new LaneLayoutOptions { HoleChance = 1 };
        var shapes = new List<SketchShape>();

        var hubs = new (string Label, int Style, bool Hole)[]
            { ("round", 0, false), ("square", 1, false), ("blob", 2, false), ("round+hole", 0, true), ("blob+hole", 2, true) };
        for (var c = 0; c < hubs.Length; c++)
        {
            var (label, style, hole) = hubs[c];
            (double X, double Z) ctr = (c * cell + cell / 2, cell / 2);
            shapes.Add(Poly($"hub:{label}", HubPolygon(ctr, hubR, style, rng, noise), "add"));
            if (hole) shapes.Add(Poly($"hubhole:{label}", HolePolygon(ctr.X, ctr.Z, hubR * 0.42, rng), "subtract"));
        }

        for (var c = 0; c < 4; c++)
        {
            (double X, double Z) hub = (c * cell + cell / 2, cell + cell * 0.95);
            var hr = lw * 1.1;
            var hubInr = hr * 0.9;
            shapes.Add(Poly($"lhub:{c}", HubPolygon(hub, hr, 0, rng, noise), "add"));
            (double X, double Z) tip = (hub.X, hub.Z - cell * 0.82);
            switch (c)
            {
                case 0:  // straight connector
                    shapes.Add(Poly("lane:straight", RibbonFromCenterline(LaneCenterline(rng, noise, hub, hubInr, 0, tip, lw, allowBend: false), lw, noHole, rng, noise, allowHole: false).Ribbon, "add"));
                    break;
                case 1:  // bend, no hole
                    shapes.Add(Poly("lane:bent", RibbonFromCenterline(LaneCenterline(rng, noise, hub, hubInr, 0, tip, lw, allowBend: true), lw, noHole, rng, noise, allowHole: false).Ribbon, "add"));
                    break;
                case 2:  // hole
                {
                    var l = RibbonFromCenterline(LaneCenterline(rng, noise, hub, hubInr, 0, tip, lw, allowBend: true), lw, allHole, rng, noise, allowHole: true);
                    shapes.Add(Poly("lane:hole", l.Ribbon, "add"));
                    if (l.Hole is { } hh) shapes.Add(Poly("lanehole:hole", hh, "subtract"));
                    break;
                }
                default:  // fork (the new style)
                    shapes.AddRange(GrowForkedLane(rng, noise, hub, hubInr, 0, tip, lw, noHole, minAngle, 0).Shapes);
                    break;
            }
        }

        return new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "none", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Layout = new SketchShapes { Shapes = shapes, Islands = [] },
        };
    }

    /// <summary>Walk back from a centerline's tip (last point) by <paramref name="d"/> blocks and return that
    /// point — used to inset an objective off the dead-end into the lane body.</summary>
    private static (double X, double Z) InsetAlong(List<double[]> center, double d)
    {
        for (var i = center.Count - 1; i > 0; i--)
        {
            double dx = center[i][0] - center[i - 1][0], dz = center[i][1] - center[i - 1][1];
            var seg = Math.Sqrt(dx * dx + dz * dz);
            if (seg >= d) return (center[i][0] - dx / seg * d, center[i][1] - dz / seg * d);
            d -= seg;
        }
        return (center[0][0], center[0][1]);
    }

    // hub plaza outline: a rotated square (style 1), a round-ish 12-gon (0), or an organic noise-jittered blob (2)
    private static List<double[]> HubPolygon((double X, double Z) c, double r, int style, Rng rng, NoiseField noise)
    {
        if (style == 1)
        {
            var rot = rng.Range(0, Math.PI / 2);
            var sq = new List<double[]>(4);
            for (var i = 0; i < 4; i++) { var a = rot + i * Math.PI / 2; sq.Add([c.X + r * Math.Cos(a), c.Z + r * Math.Sin(a)]); }
            return sq;
        }
        var n = style == 0 ? 12 : rng.Int(7, 11);
        var rotO = rng.Range(0, 2 * Math.PI / n);
        var ring = new List<double[]>(n);
        for (var i = 0; i < n; i++)
        {
            var a = rotO + i * 2 * Math.PI / n;
            var rr = style == 2
                ? r * (0.78 + 0.4 * noise.At(c.X + Math.Cos(a) * r, c.Z + Math.Sin(a) * r))   // organic, noise-driven
                : r * (0.95 + 0.1 * rng.NextDouble());                                          // round, a little life
            ring.Add([c.X + rr * Math.Cos(a), c.Z + rr * Math.Sin(a)]);
        }
        return ring;
    }

    // an organic hole: a 4–6-gon with jittered radii (≤ r, so the lane's 0.7·lw side-path guarantee still holds)
    private static List<double[]> HolePolygon(double cx, double cz, double r, Rng rng)
    {
        var n = rng.Int(4, 7);
        var rot = rng.Range(0, 2 * Math.PI / n);
        var ring = new List<double[]>(n);
        for (var i = 0; i < n; i++)
        {
            var a = rot + i * 2 * Math.PI / n;
            var rr = r * rng.Range(0.75, 1.0);
            ring.Add([cx + rr * Math.Cos(a), cz + rr * Math.Sin(a)]);
        }
        return ring;
    }

    private static double Dist((double X, double Z) a, (double X, double Z) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    // Sample the noise field over the whole board on a regular grid (rounded for a compact wire payload), so
    // the demonstration page can paint the field the tip-spread + bends read from.
    private static NoiseGrid SampleNoise(NoiseField noise, LaneLayoutOptions o)
    {
        var step = Math.Max(2.0, o.LaneWidth / 4);
        var cols = (int)(o.Width / step) + 1;
        var rows = (int)(o.Height / step) + 1;
        var values = new double[cols * rows];
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                values[r * cols + c] = Math.Round(noise.At(c * step, r * step), 3);
        return new NoiseGrid(0, 0, step, cols, rows, values);
    }

    private static SketchShape Poly(string id, List<double[]> ring, string op) =>
        new() { Id = id, Type = "polygon", Operation = op, Vertices = [.. ring] };

    /// <summary>Smooth 2-D value noise from a seed — a coherent field in [0,1] sampled on a grid of cell
    /// size <c>cell</c>, used to spread the wool tips and bend/jitter the lanes.</summary>
    private sealed class NoiseField(long seed, double cell)
    {
        private double Hash(int gx, int gz)
        {
            var h = (ulong)(gx * 374761393L + gz * 668265263L) ^ ((ulong)seed * 0x9E3779B97F4A7C15UL);
            h = (h ^ (h >> 13)) * 1274126177UL;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (double)0xFFFFFF;
        }

        public double At(double x, double z)
        {
            double fx = x / cell, fz = z / cell;
            int gx = (int)Math.Floor(fx), gz = (int)Math.Floor(fz);
            double tx = Smooth(fx - gx), tz = Smooth(fz - gz);
            double a = Hash(gx, gz), b = Hash(gx + 1, gz), c = Hash(gx, gz + 1), d = Hash(gx + 1, gz + 1);
            return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), tz);
        }

        private static double Smooth(double t) => t * t * (3 - 2 * t);
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
