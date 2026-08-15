#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Find the composed seed whose board is structurally closest to an authored plan.
//
// Similarity is scored on a signature both sides can produce the same way: each box read back through
// Producibility (the hub's body and wall vector, the frontline's form and arm count, each approach's family),
// the frontline face measured against the hub's front edge, and the placement plan — which side of the hub each
// neighbour sits on, taken relative to the frontline rather than to absolute coordinates, since an authored
// plan may be drawn on either side of the symmetry axis.
//
// Usage: nearest-seed.cs <plan.json> [maxSeed] [players...]
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Geom;

var planPath = args.Length > 0 ? args[0] : "tools/seeds/shifted-u-frontline-attach-hole-hub.plan.json";
var maxSeed = args.Length > 1 ? int.Parse(args[1]) : 600;
var cohorts = args.Length > 2 ? args[2..].Select(int.Parse).ToArray() : [12, 16, 20, 24, 30];

var target = Signature.Of(PlanModel.Parse(File.ReadAllText(planPath))!, resolveWalls: true)!;
Console.WriteLine($"target: {planPath}");
Console.WriteLine($"  {target}");
Console.WriteLine();

var scored = new List<(double Score, int Players, ulong Seed, Signature Sig, string Breakdown)>();
foreach (var players in cohorts)
    for (ulong seed = 0; seed <= (ulong)maxSeed; seed++)
    {
        ComposedStages stages;
        try { stages = Composer.ComposeStages(new ComposeRequest(players, 2, "rot_180", seed)); }
        catch (ComposeException) { continue; }
        PlanBoxAnnotation.Apply(stages.Plan, stages.Unit);
        var sig = Signature.Of(stages.Plan);
        if (sig is null) continue;
        var (score, why) = Similarity.Score(target, sig);
        scored.Add((score, players, seed, sig, why));
    }

Console.WriteLine($"scored {scored.Count} composed boards over {cohorts.Length} cohort(s), seeds 0..{maxSeed}");
Console.WriteLine();

// the exemplar's essence is three things AT ONCE — a hole hub, a two-arm frontline off centre, and a donut
// attached. Report that intersection on its own: a high weighted score can hide a board that has none of them
// together, and "most similar" is only meaningful next to "does an exact structural match exist at all".
bool HoleHub(Signature s) => s.HubForm is "Ring" or "P" or "G" or "DoubleHole";
bool OffCentreU(Signature s) => s.FrontArms == 2 && s.Face is "shifted" or "overhang";
bool Attached(Signature s) => s.WoolFamilies.Contains("Donut");
var triple = scored.Where(s => HoleHub(s.Sig) && OffCentreU(s.Sig) && Attached(s.Sig))
    .OrderByDescending(s => s.Score).ToList();

Console.WriteLine($"— boards with ALL THREE (hole hub + 2-arm off-centre face + donut): {triple.Count}");
foreach (var (score, players, seed, sig, _) in triple.Take(8))
    Console.WriteLine($"  {score:0.000}  {players}p seed {seed}  {Resolve(players, seed, sig)}");
Console.WriteLine();
Console.WriteLine($"— pairwise: hole hub + off-centre U = "
    + $"{scored.Count(s => HoleHub(s.Sig) && OffCentreU(s.Sig))}, "
    + $"hole hub + donut = {scored.Count(s => HoleHub(s.Sig) && Attached(s.Sig))}, "
    + $"off-centre U + donut = {scored.Count(s => OffCentreU(s.Sig) && Attached(s.Sig))}");
Console.WriteLine();

Console.WriteLine("— best by weighted score");
foreach (var (score, players, seed, sig, why) in scored.OrderByDescending(s => s.Score).Take(10))
{
    Console.WriteLine($"{score,6:0.000}  {players}p seed {seed}");
    Console.WriteLine($"         {Resolve(players, seed, sig)}");
    Console.WriteLine($"         {why}");
}

// re-read one board with the full producibility search, so the reported wall vector is the tuple that
// reproduces it rather than the cheap classification used for the sweep
string Resolve(int players, ulong seed, Signature cheap)
{
    try
    {
        var st = Composer.ComposeStages(new ComposeRequest(players, 2, "rot_180", seed));
        PlanBoxAnnotation.Apply(st.Plan, st.Unit);
        return Signature.Of(st.Plan, resolveWalls: true)?.ToString() ?? cheap.ToString();
    }
    catch (ComposeException) { return cheap.ToString(); }
}

// how often each component matched at all — so a component nothing can satisfy is visible rather than silently
// dragging every score down by the same amount
Console.WriteLine();
Console.WriteLine("component reachability (share of boards matching the target exactly):");
foreach (var (name, hits) in Similarity.Reach(target!, scored.Select(s => s.Sig)))
    Console.WriteLine($"  {name,-18} {100.0 * hits / Math.Max(1, scored.Count),5:0.0}%  ({hits})");

// ── the signature ──────────────────────────────────────────────────────────────────────────────────────────
sealed record Signature(
    string HubForm, string? HubWalls, string FrontForm, int FrontArms, string? Face, double Offset,
    string SpawnFamily, IReadOnlyList<string> WoolFamilies, string SpawnSide, IReadOnlyList<string> WoolSides,
    int HubW, int HubH)
{
    /// Build from the classifiers directly — the same reads Producibility bases its Identity on, but without the
    /// candidate enumeration behind them, so a wide seed space is affordable. Pass resolveWalls to additionally
    /// run the full search for the hub's wall vector; that part is only worth paying for on the finalists.
    public static Signature? Of(PlanModel plan, bool resolveWalls = false)
    {
        if (plan.Boxes.Count == 0) return null;
        var hub = plan.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Hub);
        if (hub is null) return null;
        var front = plan.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Frontline);

        string? walls = null;
        if (resolveWalls)
        {
            var hubRead = Producibility.Read(plan).FirstOrDefault(b => b.BoxId == hub.Id);
            var label = hubRead?.Producible?.Label ?? "";
            walls = label.Contains("walls ") ? label[(label.IndexOf("walls ") + 6)..].Split(' ')[0] : null;
        }
        var hubBody = ShapeClassifier.ClassifyBody(Cells(plan, hub));

        // the frontline decides which way is "front"; every other side is named relative to it, so a plan drawn
        // on either side of the axis reads the same
        var (frontAxis, frontSign) = Facing(hub.Rect, front?.Rect);
        string Side(CellRect r) => SideOf(hub.Rect, r, frontAxis, frontSign);

        // an approach's family reads off its terrain plus its terminal room — the emit↔derive mirror
        string Family(PlanBox b)
        {
            var members = PlanBoxes.MembersOf(plan, b);
            var terminal = new HashSet<(int, int)>();
            var filled = new HashSet<(int, int)>();
            foreach (var p in members)
            {
                Raster(p.Rect, filled);
                if (p.Role != PlanRoles.Piece) Raster(p.Rect, terminal);
            }
            return filled.Count == 0 ? "?" : ShapeClassifier.Classify(filled, terminal).Family.ToString();
        }

        var wools = plan.Boxes.Where(b => b.Kind == PlanBoxKinds.Wool).ToList();
        var spawn = plan.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Spawn);

        string? face = null;
        double offset = 0;
        if (front is not null)
        {
            // measure along the hub's front edge — the axis perpendicular to the facing direction
            var along = frontAxis == 1 ? 0 : 1;
            var (hs, hl) = OnAxis(hub.Rect, along);
            var (fs, fl) = OnAxis(front.Rect, along);
            offset = (fs + fl / 2.0) - (hs + hl / 2.0);
            face = (fs < hs || fs + fl > hs + hl) ? "overhang"
                 : Math.Abs(offset) >= 1 ? "shifted"
                 : fl < hl ? "partial" : "full";
        }

        var frontBody = front is null ? null : ShapeClassifier.ClassifyBody(Cells(plan, front));

        return new Signature(
            hubBody.Form.ToString(), walls,
            frontBody?.Form.ToString() ?? "", frontBody?.Arms ?? 0, face, Math.Round(offset, 1),
            spawn is null ? "none" : Family(spawn),
            wools.Select(Family).OrderBy(f => f).ToList(),
            spawn is null ? "none" : Side(spawn.Rect),
            wools.Select(w => Side(w.Rect)).OrderBy(s => s).ToList(),
            hub.Rect.Width, hub.Rect.Height);
    }

    private static HashSet<(int, int)> Cells(PlanModel plan, PlanBox box)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var p in PlanBoxes.MembersOf(plan, box)) Raster(p.Rect, cells);
        return cells;
    }

    private static void Raster(CellRect r, HashSet<(int, int)> into)
    {
        for (var x = r.X; x < r.X + r.Width; x++)
            for (var z = r.Z; z < r.Z + r.Height; z++) into.Add((x, z));
    }

    // A rect's start and extent on one axis. The rect used to be an int[] and this was r[axis]/r[axis + 2];
    // CellRect has no indexer on purpose, since reading [3] as a depth rather than a height compiled too.
    private static (int Start, int Extent) OnAxis(CellRect r, int axis) =>
        axis == 0 ? (r.X, r.Width) : (r.Z, r.Height);

    // which axis (0 = x, 1 = z) and direction the frontline sits from the hub; defaults to +z when there is none
    private static (int Axis, int Sign) Facing(CellRect hub, CellRect? front)
    {
        if (front is not { } face) return (1, 1);
        double dx = (face.X + face.Width / 2.0) - (hub.X + hub.Width / 2.0);
        double dz = (face.Z + face.Height / 2.0) - (hub.Z + hub.Height / 2.0);
        return Math.Abs(dx) > Math.Abs(dz) ? (0, Math.Sign(dx)) : (1, Math.Sign(dz) == 0 ? 1 : Math.Sign(dz));
    }

    private static string SideOf(CellRect hub, CellRect r, int axis, int sign)
    {
        var (rs, re) = OnAxis(r, axis);
        var (hubStart, hubExtent) = OnAxis(hub, axis);
        double d = (rs + re / 2.0) - (hubStart + hubExtent / 2.0);
        var other = axis == 0 ? 1 : 0;
        var (rOther, rOtherExtent) = OnAxis(r, other);
        var (hubOther, hubOtherExtent) = OnAxis(hub, other);
        double o = (rOther + rOtherExtent / 2.0) - (hubOther + hubOtherExtent / 2.0);
        if (Math.Abs(d) >= Math.Abs(o)) return Math.Sign(d) == sign ? "front" : "back";
        return o < 0 ? "left" : "right";
    }

    public override string ToString() =>
        $"hub {HubForm}{(HubWalls is null ? "" : " walls " + HubWalls)} {HubW}x{HubH} · "
        + $"front {(FrontForm.Length == 0 ? "none" : FrontForm + (FrontArms > 0 ? $"({FrontArms})" : ""))}"
        + $"{(Face is null ? "" : " " + Face + " " + Offset)} · "
        + $"spawn {SpawnFamily}@{SpawnSide} · wools [{string.Join(",", WoolFamilies)}]@[{string.Join(",", WoolSides)}]";
}

// ── the metric ─────────────────────────────────────────────────────────────────────────────────────────────
static class Similarity
{
    // named, weighted components — the whole point is that a score can be read back as "what matched"
    private static readonly (string Name, double Weight, Func<Signature, Signature, double> F)[] Parts =
    [
        ("hub body",    3.0, (a, b) => a.HubForm == b.HubForm ? 1 : 0),
        ("hub walls",   2.0, (a, b) => a.HubWalls == b.HubWalls ? 1 : a.HubWalls is not null && b.HubWalls is not null ? 0.5 : 0),
        ("wool set",    3.0, (a, b) => Jaccard(a.WoolFamilies, b.WoolFamilies)),
        ("face class",  2.5, (a, b) => a.Face == b.Face ? 1 : Offish(a.Face) && Offish(b.Face) ? 0.6 : 0),
        ("face offset", 1.0, (a, b) => a.Face is null || b.Face is null ? 0
            : Math.Sign(a.Offset) == Math.Sign(b.Offset) ? Math.Max(0, 1 - Math.Abs(a.Offset - b.Offset) / 4.0) : 0),
        ("front form",  1.5, (a, b) => a.FrontForm == b.FrontForm ? (a.FrontArms == b.FrontArms ? 1 : 0.6) : 0),
        ("wool count",  1.0, (a, b) => a.WoolFamilies.Count == b.WoolFamilies.Count ? 1 : 0),
        ("placement",   1.5, (a, b) => (a.SpawnSide == b.SpawnSide ? 0.5 : 0) + 0.5 * Jaccard(a.WoolSides, b.WoolSides)),
        ("spawn family",0.5, (a, b) => a.SpawnFamily == b.SpawnFamily ? 1 : 0),
        ("hub size",    1.0, (a, b) => Math.Max(0, 1 - (Math.Abs(a.HubW - b.HubW) + Math.Abs(a.HubH - b.HubH)) / 8.0)),
    ];

    private static bool Offish(string? f) => f is "shifted" or "overhang";

    public static (double Score, string Breakdown) Score(Signature a, Signature b)
    {
        double sum = 0, total = 0;
        var hits = new List<string>();
        foreach (var (name, w, f) in Parts)
        {
            var v = f(a, b);
            sum += w * v; total += w;
            if (v > 0) hits.Add($"{name} {v:0.##}");
        }
        return (sum / total, string.Join(" · ", hits));
    }

    public static IEnumerable<(string Name, int Hits)> Reach(Signature a, IEnumerable<Signature> all)
    {
        var list = all.ToList();
        foreach (var (name, _, f) in Parts)
            yield return (name, list.Count(s => f(a, s) >= 1));
    }

    private static double Jaccard(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 1;
        var union = a.Concat(b).Distinct().Count();
        var inter = a.Distinct().Count(x => b.Contains(x));
        return union == 0 ? 0 : (double)inter / union;
    }
}
