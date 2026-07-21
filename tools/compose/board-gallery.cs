#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// board gallery: the composed FULL board (map completion v0) — Composer.ComposeBoxStages per preset × seed:
// the box-path team unit, its fanned orbit image(s), and the band-only mid connecting them. Each card runs the
// loop-closed check — a flood from the spawn over land + band must reach every fanned spawn image — and reports
// the closure holes (emergent only in v0: e.g. a staple frontline's bay the band seals).
using System.Text;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using Sym = PgmStudio.Geom.Symmetry;

var presets = new[]
{
    ("Small board", 6), ("Mid board", 8), ("Big board", 12), ("Huge board", 20), ("Giant board", 30),
};
const int seeds = 16;

var disconnected = 0;
var sections = new StringBuilder();
foreach (var (label, players) in presets)
{
    var cards = new StringBuilder();
    for (var seed = 0; seed < seeds; seed++)
    {
        ComposedStages stages;
        try { stages = Composer.ComposeBoxStages(new ComposeRequest(players, seed: (ulong)seed)); }
        catch (ComposeException) { cards.Append(Fail(seed, "no acceptable plan")); continue; }

        var plan = stages.Plan;
        var sym = stages.Envelope.Symmetry;
        var order = Sym.Order(sym);
        var axes = Sym.OrbitAxes(sym);

        // every fanned rect: (rect, id, role, image index) over land pieces and the band zone
        var fanned = new List<(int[] Rect, string Id, string Role, int K)>();
        foreach (var p in plan.Pieces.Where(p => !PlanRoles.Annotations.Contains(p.Role)))
            for (var k = 0; k < order; k++)
                fanned.Add((Fan(p.Rect, axes, k), p.Id, p.Role, k));
        var band = plan.Zones.First(z => z.Id == "mid-band");
        var bandImages = Enumerable.Range(0, order).Select(k => Fan(band.Rect, axes, k)).ToList();

        // the loop-closed check: land + band cells, flooded from the spawn; every fanned spawn image reachable
        var land = new HashSet<(int, int)>();
        foreach (var (r, _, _, _) in fanned) Rasterize(r, land);
        var walk = new HashSet<(int, int)>(land);
        foreach (var b in bandImages) Rasterize(b, walk);
        var spawnPiece = plan.Pieces.First(p => p.Role == PlanRoles.Spawn);
        var spawnCells = Enumerable.Range(0, order).Select(k => Center(Fan(spawnPiece.Rect, axes, k))).ToList();
        var reached = Flood(walk, spawnCells[0]);
        var connected = spawnCells.All(reached.Contains);
        if (!connected) disconnected++;

        var holes = ClosureAnalysis.HoleSizes(plan);
        cards.Append(Card(seed, fanned, bandImages, spawnCells, connected, holes));
    }
    sections.Append($"<section><header><h2>{label}</h2><p>{players} players/team · full fanned board · band-only mid</p></header>"
        + $"<div class=gallery>{cards}</div></section>");
}

var html = new StringBuilder();
html.Append("<style>"
    + ":root{--bg:#0a1120;--panel:#111b2e;--line:#1e2b45;--ink:#e2e8f5;--dim:#8595b4;--canvas:#070d18}"
    + "body{background:var(--bg);color:var(--ink);font-family:ui-sans-serif,system-ui,sans-serif;margin:0;padding:28px;line-height:1.5}"
    + "h1{font-size:20px;font-weight:600;margin:0 0 4px;letter-spacing:-0.01em}"
    + ".tag{font-size:12px;color:var(--dim);margin:0 0 18px}"
    + ".legend{display:flex;flex-wrap:wrap;gap:16px;font-size:12.5px;margin:0 0 26px;padding-bottom:18px;border-bottom:1px solid var(--line)}"
    + ".legend b{font-weight:500}.legend i{font-style:normal;color:var(--dim)}"
    + "section{margin-bottom:30px}header h2{font-size:14px;font-weight:600;margin:0}"
    + "header p{font-size:12px;color:var(--dim);margin:1px 0 12px;font-variant-numeric:tabular-nums}"
    + ".gallery{display:flex;flex-wrap:wrap;gap:12px}"
    + ".card{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:9px}"
    + ".card.warn{border-color:#f87171;box-shadow:0 0 0 1px #f8717155}"
    + ".badge{color:#f87171;font-size:9px;letter-spacing:0}.ok{color:#34d399;font-size:9px}"
    + ".title{font-size:10.5px;color:var(--dim);margin-bottom:6px;letter-spacing:0.03em}"
    + ".fail{color:#f87171;font-size:11px;padding:26px;text-align:center}"
    + "svg{background:var(--canvas);border-radius:4px;display:block}</style>");
html.Append("<h1>Composed boards — map completion v0</h1>"
    + "<p class=tag>Composer.ComposeBoxStages: allocator → filler → the band-only mid (uniform 20-block gap, no "
    + "stones, no centre island). Both fanned team sides drawn; the loop-closed check floods from the spawn over "
    + "land + band and must reach the opposing spawn.</p>");
html.Append("<div class=legend>"
    + "<b style='color:#a78bfa'>■ hub</b><b style='color:#34d399'>■ spawn</b>"
    + "<b style='color:#fbbf24'>■ wool</b><b style='color:#fb923c'>■ frontline</b>"
    + "<b style='color:#38bdf8'>■ mid band (build zone)</b>"
    + "<i>solid = own side · dimmed = fanned image · ● spawn centres</i>"
    + "<i>title: board W×H cells · holes = closure hole sizes (cells)</i></div>");
html.Append(sections.ToString());

Directory.CreateDirectory("tools/compose/out");
File.WriteAllText("tools/compose/out/board-gallery.html", html.ToString());
Console.WriteLine($"wrote tools/compose/out/board-gallery.html — {disconnected} disconnected board(s)");

// a rect's k-th orbit image: identity for k=0, else the symmetry op about the axis line (rect in, rect out —
// the ops are axis-aligned)
static int[] Fan(int[] r, string[] axes, int k)
{
    if (k == 0) return r;
    (double x, double z)[] corners = [(r[0], r[1]), (r[0], r[1] + r[3]), (r[0] + r[2], r[1]), (r[0] + r[2], r[1] + r[3])];
    var pts = corners.Select(c => Sym.Apply(c.x, c.z, axes[k - 1], 0, 0)).ToList();
    var x1 = (int)Math.Round(pts.Min(p => p.X));
    var z1 = (int)Math.Round(pts.Min(p => p.Z));
    return [x1, z1, (int)Math.Round(pts.Max(p => p.X)) - x1, (int)Math.Round(pts.Max(p => p.Z)) - z1];
}

static void Rasterize(int[] r, HashSet<(int, int)> into)
{
    for (var x = r[0]; x < r[0] + r[2]; x++)
        for (var z = r[1]; z < r[1] + r[3]; z++)
            into.Add((x, z));
}

static (int X, int Z) Center(int[] r) => (r[0] + r[2] / 2, r[1] + r[3] / 2);

static HashSet<(int, int)> Flood(HashSet<(int, int)> walk, (int X, int Z) start)
{
    var seen = new HashSet<(int, int)>();
    if (!walk.Contains(start)) return seen;
    var q = new Queue<(int, int)>();
    seen.Add(start); q.Enqueue(start);
    while (q.Count > 0)
    {
        var (x, z) = q.Dequeue();
        foreach (var n in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            if (walk.Contains(n) && seen.Add(n)) q.Enqueue(n);
    }
    return seen;
}

static string PieceColor(string id) =>
    id.StartsWith("hub") ? "#a78bfa"
    : id.StartsWith("spawn") ? "#34d399"
    : id.StartsWith("wool") ? "#fbbf24"
    : id.StartsWith("frontline") ? "#fb923c"
    : "#64748b";

static string Fail(int seed, string why) =>
    $"<div class='card warn'><div class=title>seed {seed}</div><div class=fail>{why}</div></div>";

static string Card(int seed, IReadOnlyList<(int[] Rect, string Id, string Role, int K)> fanned,
    IReadOnlyList<int[]> bandImages, IReadOnlyList<(int X, int Z)> spawnCells, bool connected, IReadOnlyList<int> holes)
{
    var all = fanned.Select(f => f.Rect).Concat(bandImages).ToList();
    int minX = all.Min(r => r[0]), minZ = all.Min(r => r[1]);
    int maxX = all.Max(r => r[0] + r[2]), maxZ = all.Max(r => r[1] + r[3]);
    int w = maxX - minX, h = maxZ - minZ;
    const int scale = 9, pad = 10;
    int vw = w * scale + 2 * pad, vh = h * scale + 2 * pad;

    var svg = new StringBuilder($"<svg viewBox='0 0 {vw} {vh}' width='{vw}' height='{vh}'>");
    foreach (var b in bandImages)
        svg.Append($"<rect x='{(b[0] - minX) * scale + pad}' y='{(b[1] - minZ) * scale + pad}' "
            + $"width='{b[2] * scale}' height='{b[3] * scale}' fill='#38bdf8' fill-opacity='0.18' "
            + "stroke='#38bdf8' stroke-opacity='0.5' stroke-width='1' stroke-dasharray='3 2'/>");
    foreach (var (r, id, role, k) in fanned)
    {
        var col = PieceColor(id);
        var room = role != PlanRoles.Piece;
        var op = (k == 0 ? (room ? 0.95 : 0.4) : (room ? 0.55 : 0.22)).ToString("0.##");
        svg.Append($"<rect x='{(r[0] - minX) * scale + pad}' y='{(r[1] - minZ) * scale + pad}' "
            + $"width='{r[2] * scale}' height='{r[3] * scale}' rx='1' fill='{col}' fill-opacity='{op}' "
            + $"stroke='{col}' stroke-opacity='{(k == 0 ? "1" : "0.4")}' stroke-width='0.8'/>");
    }
    foreach (var (sx, sz) in spawnCells)
        svg.Append($"<circle cx='{(sx - minX) * scale + pad + scale / 2}' cy='{(sz - minZ) * scale + pad + scale / 2}' "
            + "r='2.5' fill='#e2e8f5'/>");
    svg.Append("</svg>");

    var holesLabel = holes.Count == 0 ? "none" : string.Join(",", holes);
    var badge = connected ? " <span class=ok>✔ connected</span>" : " <span class=badge>⚠ disconnected</span>";
    return $"<div class='card{(connected ? "" : " warn")}'><div class=title>seed {seed} · {w}×{h} "
        + $"· holes {holesLabel}{badge}</div>{svg}</div>";
}
