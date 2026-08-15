#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Sweep composed boards for the two-legged (U) hub and emit, as JSON, the boards that carry one plus the leg
// census behind them. The hub's form and its exact leg layout are read off the producibility label — the tuple
// that reproduced the box — rather than re-derived from the geometry, so the numbers shown are the numbers the
// composer drew.
using System.Text.Json;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using Sym = PgmStudio.Geom.Symmetry;
using PgmStudio.Geom;

var maxSeed = args.Length > 0 ? int.Parse(args[0]) : 600;
int[] cohorts = [8, 12, 20];

const int PerCohort = 12;                       // how many drawn U boards each player count keeps

var boards = new List<object>();
var census = new List<object>();

foreach (var players in cohorts)
{
    int composed = 0, branch = 0, oneLeg = 0, twoLeg = 0, kept = 0;
    var legWidths = new SortedDictionary<int, int>();
    var bays = new SortedDictionary<int, int>();
    var spines = new SortedDictionary<int, int>();

    for (ulong seed = 0; seed <= (ulong)maxSeed; seed++)
    {
        ComposedStages stages;
        try { stages = Composer.ComposeStages(new ComposeRequest(players, 2, "rot_180", seed)); }
        catch (ComposeException) { continue; }
        composed++;

        var plan = stages.Plan;
        PlanBoxAnnotation.Apply(plan, stages.Unit);
        var read = Producibility.ReadPlan(plan);
        var hubRead = read.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Hub);
        if (hubRead?.Producible is not { } made) continue;
        if (!made.Label.StartsWith("SpineArms(")) continue;
        branch++;
        var arms = int.Parse(made.Label[10..made.Label.IndexOf(')')]);
        var hubSpine = plan.Boxes.First(b => b.Kind == PlanBoxKinds.Hub).Rect.Width;
        spines[hubSpine] = spines.GetValueOrDefault(hubSpine) + 1;
        if (arms == 1) oneLeg++;
        if (arms != 2) continue;

        // a hub whose legs are the plain default reproduces at the default tuple, whose label carries no explicit
        // layout — so the layout is the emitter's own default, not a missing reading
        var legs = ParseLegs(made.Label);
        if (legs.Count == 0)
            legs = HubBoxEmitter.DefaultArms(hubSpine, 2, FillProfiles.HubWallCells)?.ToList() ?? [];
        if (legs.Count != 2) continue;
        twoLeg++;
        foreach (var (_, w) in legs) legWidths[w] = legWidths.GetValueOrDefault(w) + 1;
        var bay = legs[1].Start - (legs[0].Start + legs[0].Width);
        bays[bay] = bays.GetValueOrDefault(bay) + 1;

        if (kept >= PerCohort) continue;
        kept++;

        var hubBox = plan.Boxes.First(b => b.Kind == PlanBoxKinds.Hub);
        var frontBox = plan.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Frontline);

        var axes = Sym.OrbitAxes(plan.Globals.Symmetry);
        var order = Sym.Order(plan.Globals.Symmetry);
        var rects = new List<object>();
        foreach (var p in plan.Pieces.Where(p => !PlanRoles.Annotations.Contains(p.Role)))
            for (var k = 0; k < order; k++)
                rects.Add(new { r = Fan(p.Rect, axes, k), kind = KindOf(p.Id), room = p.Role != PlanRoles.Piece, k });
        var band = plan.Zones.FirstOrDefault(z => z.Id == "mid-band");
        var bands = band is null ? [] : Enumerable.Range(0, order).Select(k => Fan(band.Rect, axes, k)).ToList();

        boards.Add(new
        {
            seed,
            players,
            spine = hubBox.Rect.Width,
            depth = hubBox.Rect.Height,
            legs = legs.Select(l => new { start = l.Start, width = l.Width }).ToList(),
            bay,
            widest = legs.Max(l => l.Width),
            hubRect = hubBox.Rect,
            frontRect = frontBox?.Rect,
            wools = plan.Boxes.Count(b => b.Kind == PlanBoxKinds.Wool),
            label = made.Label,
            rects,
            bands,
        });
    }

    census.Add(new { players, composed, branch, oneLeg, twoLeg, legWidths, bays, spines });
    Console.WriteLine($"{players,2}p: {composed} composed, {branch} branch ({oneLeg} L, {twoLeg} U)");
    Console.WriteLine($"      branch spines {string.Join("  ", spines.Select(kv => $"{kv.Key}:{kv.Value}"))}");
    Console.WriteLine($"      leg widths    {string.Join("  ", legWidths.Select(kv => $"{kv.Key}:{kv.Value}"))}");
    Console.WriteLine($"      bays          {string.Join("  ", bays.Select(kv => $"{kv.Key}:{kv.Value}"))}");
}

Directory.CreateDirectory("tools/compose/out");
File.WriteAllText("tools/compose/out/u-hub-showcase.json",
    JsonSerializer.Serialize(new
    {
        maxSeed,
        cap = new { legNumerator = HubBoxEmitter.LegWidthCapNumerator, legDenominator = HubBoxEmitter.LegWidthCapDenominator,
                    bayCorridors = HubBoxEmitter.BayCapCorridors, cw = FillProfiles.HubWallCells },
        census,
        boards,
    }));
Console.WriteLine($"{boards.Count} boards drawn");

// "SpineArms(2) legs 0:4+7:3 flipped" -> [(0,4),(7,3)]
static List<(int Start, int Width)> ParseLegs(string label)
{
    var at = label.IndexOf("legs ", StringComparison.Ordinal);
    if (at < 0) return [];
    var body = label[(at + 5)..].Split(' ')[0];
    return body.Split('+')
        .Select(p => p.Split(':'))
        .Where(p => p.Length == 2)
        .Select(p => (int.Parse(p[0]), int.Parse(p[1])))
        .ToList();
}

static string KindOf(string id) =>
    id.StartsWith("hub") ? "hub" : id.StartsWith("spawn") ? "spawn"
    : id.StartsWith("wool") ? "wool" : id.StartsWith("frontline") ? "frontline" : "other";

static CellRect Fan(CellRect r, string[] axes, int k)
{
    if (k == 0) return r;
    (double x, double z)[] corners =
        [(r.X, r.Z), (r.X, r.Z + r.Height), (r.X + r.Width, r.Z), (r.X + r.Width, r.Z + r.Height)];
    var pts = corners.Select(c => Sym.Apply(c.x, c.z, axes[k - 1], 0, 0)).ToList();
    int x1 = (int)Math.Round(pts.Min(p => p.X)), z1 = (int)Math.Round(pts.Min(p => p.Z));
    return new CellRect(x1, z1, (int)Math.Round(pts.Max(p => p.X)) - x1, (int)Math.Round(pts.Max(p => p.Z)) - z1);
}
