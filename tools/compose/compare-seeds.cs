#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Emit an authored plan and a set of named composed seeds in one drawable JSON set, so they can be put side by
// side. Same output shape as the seed sweep, so the same viewer renders both.
//
// Usage: compare-seeds.cs <plan.json> <players:seed> [players:seed ...]
using System.Text.Json;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using Sym = PgmStudio.Geom.Symmetry;
using PgmStudio.Geom;

var planPath = args[0];
var boards = new List<object>();

var authored = PlanModel.Parse(File.ReadAllText(planPath))!;
boards.Add(Drawable(authored, Path.GetFileNameWithoutExtension(planPath).Replace(".plan", ""), "authored", 0));

foreach (var spec in args[1..])
{
    var parts = spec.Split(':');
    int players = int.Parse(parts[0]);
    ulong seed = ulong.Parse(parts[1]);
    var stages = Composer.ComposeStages(new ComposeRequest(players, 2, "rot_180", seed));
    PlanBoxAnnotation.Apply(stages.Plan, stages.Unit);
    boards.Add(Drawable(stages.Plan, $"{players}p seed {seed}", "composed", players));
}

Directory.CreateDirectory("tools/compose/out");
File.WriteAllText("tools/compose/out/compare.json",
    JsonSerializer.Serialize(new { boards }));
Console.WriteLine($"wrote tools/compose/out/compare.json — {boards.Count} boards");

object Drawable(PlanModel plan, string label, string origin, int players)
{
    var read = Producibility.Read(plan);
    var hubRead = read.FirstOrDefault(b => b.Kind == PlanBoxKinds.Hub);
    var hub = plan.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Hub);
    var front = plan.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Frontline);
    var lbl = hubRead?.Producible?.Label ?? "";
    var walls = lbl.Contains("walls ") ? lbl[(lbl.IndexOf("walls ") + 6)..].Split(' ')[0] : null;

    string? face = null;
    double offset = 0;
    if (hub is not null && front is not null)
    {
        int hx = hub.Rect.X, hw = hub.Rect.Width, fx = front.Rect.X, fw = front.Rect.Width;
        offset = (fx + fw / 2.0) - (hx + hw / 2.0);
        face = (fx < hx || fx + fw > hx + hw) ? "overhang" : Math.Abs(offset) >= 1 ? "shifted"
             : fw < hw ? "partial" : "full";
    }

    // colour by which box owns the piece, not by its id prefix: an authored plan names its pieces freely, so an
    // id-based read would grey out every hand-drawn approach
    var owner = new Dictionary<string, string>();
    foreach (var box in plan.Boxes)
        foreach (var p in PlanBoxes.MembersOf(plan, box))
            owner[p.Id] = box.Kind;

    var axes = Sym.OrbitAxes(plan.Globals.Symmetry);
    var order = Sym.Order(plan.Globals.Symmetry);
    var rects = new List<object>();
    foreach (var p in plan.Pieces.Where(p => !PlanRoles.Annotations.Contains(p.Role)))
        for (var k = 0; k < order; k++)
            rects.Add(new
            {
                r = Fan(p.Rect, axes, k),
                kind = owner.TryGetValue(p.Id, out var ok) ? ok : KindOf(p.Id),
                room = p.Role != PlanRoles.Piece,
                k,
            });

    var band = plan.Zones.FirstOrDefault(z => z.Id == "mid-band");
    var bands = band is null ? [] : Enumerable.Range(0, order).Select(k => Fan(band.Rect, axes, k)).ToList();

    return new
    {
        label,
        origin,
        players,
        form = hubRead?.Identity.Split(' ')[0].Split('(')[0] ?? "?",
        walls,
        face,
        offset = Math.Round(offset, 1),
        hubRect = hub?.Rect ?? default,
        wools = plan.Boxes.Count(b => b.Kind == PlanBoxKinds.Wool),
        // every box's derived family, so the comparison can be read box by box rather than as one number
        boxes = plan.Boxes.Select(b => new
        {
            b.Id,
            b.Kind,
            identity = read.FirstOrDefault(r => r.BoxId == b.Id)?.Identity ?? "?",
            producible = read.FirstOrDefault(r => r.BoxId == b.Id)?.IsProducible ?? false,
        }).ToList(),
        rects,
        bands,
    };
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
