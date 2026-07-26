#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Sweep composed boards and emit, as JSON, the ones that show a particular hub body or frontline face — the
// data behind the seed showcase. Each board is classified by the producibility read (which already names the
// hub's form and, when a wall is widened, its exact wall vector) rather than by re-deriving the geometry, and
// its frontline face is measured against the hub's front edge.
using System.Text.Json;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using Sym = PgmStudio.Geom.Symmetry;

var players = args.Length > 0 ? int.Parse(args[0]) : 20;
var maxSeed = args.Length > 1 ? int.Parse(args[1]) : 400;

const int PerCategory = 12;                    // how many drawn examples each body / face type keeps

var boards = new List<object>();
var tally = new Dictionary<string, int>();
var kept = new Dictionary<string, int>();

for (ulong seed = 0; seed <= (ulong)maxSeed; seed++)
{
    ComposedStages stages;
    try { stages = Composer.ComposeStages(new ComposeRequest(players, 2, "rot_180", seed)); }
    catch (ComposeException) { continue; }

    var plan = stages.Plan;
    PlanBoxAnnotation.Apply(plan, stages.Unit);
    var read = Producibility.ReadPlan(plan);

    var hubRead = read.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Hub);
    if (hubRead is null) continue;
    var hubBox = plan.Boxes.First(b => b.Kind == PlanBoxKinds.Hub);
    var frontBox = plan.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Frontline);

    // the hub body, off the identity the classifier reads; the wall vector off the tuple that reproduced it
    var form = hubRead.Identity.Split(' ')[0].Split('(')[0];
    var label = hubRead.Producible?.Label ?? "";
    var walls = label.Contains("walls ") ? label[(label.IndexOf("walls ") + 6)..].Split(' ')[0] : null;

    // the face: rot_180 grows along z, so the hub's front edge runs along x. A face narrower than that edge is
    // partial; one whose centre is off the hub's is shifted; one reaching past either end overhangs.
    string? face = null;
    double offset = 0;
    if (frontBox is not null)
    {
        int hx = hubBox.Rect[0], hw = hubBox.Rect[2], fx = frontBox.Rect[0], fw = frontBox.Rect[2];
        var partial = fw < hw;
        var overhang = fx < hx || fx + fw > hx + hw;
        offset = (fx + fw / 2.0) - (hx + hw / 2.0);
        var shifted = Math.Abs(offset) >= 1;
        face = overhang ? "overhang" : shifted ? "shifted" : partial ? "partial" : "full";
    }

    var tag = form + (walls is null ? "" : "-widened");
    tally[tag] = tally.GetValueOrDefault(tag) + 1;
    if (face is not null) tally["face-" + face] = tally.GetValueOrDefault("face-" + face) + 1;

    // the census counts every board; only a capped sample of each category is drawn, so the rarer bodies are
    // all kept while the common Ring does not swamp the page
    kept[tag] = kept.GetValueOrDefault(tag) + 1;
    var faceTag = "face-" + face;
    var faceRoom = face is not null && kept.GetValueOrDefault(faceTag) < PerCategory;
    if (kept[tag] > PerCategory && !faceRoom) { kept[tag]--; continue; }
    if (face is not null) kept[faceTag] = kept.GetValueOrDefault(faceTag) + 1;

    // the drawable board: every piece fanned over the symmetry orbit, plus the mid band's images
    var axes = Sym.OrbitAxes(plan.Globals.Symmetry);
    var order = Sym.Order(plan.Globals.Symmetry);
    var rects = new List<object>();
    foreach (var p in plan.Pieces.Where(p => !PlanRoles.Annotations.Contains(p.Role)))
        for (var k = 0; k < order; k++)
        {
            var r = Fan(p.Rect, axes, k);
            rects.Add(new { r, kind = KindOf(p.Id), room = p.Role != PlanRoles.Piece, k });
        }
    var band = plan.Zones.FirstOrDefault(z => z.Id == "mid-band");
    var bands = band is null ? [] : Enumerable.Range(0, order).Select(k => Fan(band.Rect, axes, k)).ToList();

    boards.Add(new
    {
        seed,
        players,
        form,
        walls,
        face,
        offset = Math.Round(offset, 1),
        hubRect = hubBox.Rect,
        frontRect = frontBox?.Rect,
        wools = plan.Boxes.Count(b => b.Kind == PlanBoxKinds.Wool),
        producible = read.IsProducible,
        hubLabel = label,
        rects,
        bands,
    });
}

Directory.CreateDirectory("tools/compose/out");
File.WriteAllText($"tools/compose/out/seed-showcase-{players}.json",
    JsonSerializer.Serialize(new { players, maxSeed, tally, boards }));
Console.WriteLine($"{boards.Count} boards at {players}p over seeds 0..{maxSeed}");
foreach (var (k, v) in tally.OrderByDescending(t => t.Value)) Console.WriteLine($"  {k,-22} {v}");

static string KindOf(string id) =>
    id.StartsWith("hub") ? "hub" : id.StartsWith("spawn") ? "spawn"
    : id.StartsWith("wool") ? "wool" : id.StartsWith("frontline") ? "frontline" : "other";

static int[] Fan(int[] r, string[] axes, int k)
{
    if (k == 0) return r;
    (double x, double z)[] corners =
        [(r[0], r[1]), (r[0], r[1] + r[3]), (r[0] + r[2], r[1]), (r[0] + r[2], r[1] + r[3])];
    var pts = corners.Select(c => Sym.Apply(c.x, c.z, axes[k - 1], 0, 0)).ToList();
    int x1 = (int)Math.Round(pts.Min(p => p.X)), z1 = (int)Math.Round(pts.Min(p => p.Z));
    return [x1, z1, (int)Math.Round(pts.Max(p => p.X)) - x1, (int)Math.Round(pts.Max(p => p.Z)) - z1];
}
