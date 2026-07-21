#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// board gallery: the composed FULL board (map completion v0) in two modes. The CORPUS mode runs the real
// pipeline (Composer.ComposeBoxStages: derived envelope, corpus land budget, one threaded RNG) — the true
// output. The PRESET mode reuses the unit gallery's handcrafted envelopes with per-stage fresh RNGs, so board
// card N contains exactly the unit shown on unit-gallery card N, completed with the band and its fanned image.
// Each card runs the loop-closed check — a flood from the spawn over land + band must reach every fanned spawn
// image — and reports the closure holes (emergent only in v0).
using System.Text;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;
using Sym = PgmStudio.Geom.Symmetry;

const int seeds = 16;
var disconnected = 0;
var sections = new StringBuilder();

// ── corpus mode: the real pipeline ─────────────────────────────────────────────────────────────────────
foreach (var (label, players) in new[] { ("Small", 6), ("Mid", 8), ("Big", 12), ("Huge", 20), ("Giant", 30) })
{
    var cards = new StringBuilder();
    for (var seed = 0; seed < seeds; seed++)
    {
        try
        {
            var stages = Composer.ComposeBoxStages(new ComposeRequest(players, seed: (ulong)seed));
            cards.Append(BoardCard(seed, stages.Plan, stages.Envelope.Symmetry));
        }
        catch (ComposeException) { cards.Append(Fail(seed, "no acceptable plan")); }
    }
    sections.Append($"<section><header><h2>{label} board — corpus budget</h2>"
        + $"<p>{players} players/team · derived envelope (corpus land anchors) · rot_180 default</p>"
        + $"<div class=gallery>{cards}</div></section>");
}

// ── preset mode: the unit gallery's handcrafted envelopes, per-stage fresh RNGs — card N holds exactly the
// unit of unit-gallery card N, completed with the band ────────────────────────────────────────────────────
foreach (var (label, players, land) in new[]
    { ("Small", 6, 700.0), ("Mid", 8, 1600.0), ("Big", 12, 2800.0), ("Huge", 20, 3800.0), ("Giant", 30, 6000.0) })
{
    var env = new ComposeEnvelope("mirror_z", Teams: 2, players, Cell: 5, Surface: 9, Headroom: 11,
        BoardWidthBlocks: 300, BoardLengthBlocks: 300, land, UnitMinX: 0, UnitMinZ: 0, UnitMaxX: 60, UnitMaxZ: 60);
    var crossing = MidCarver.BandOnly(env);
    var cards = new StringBuilder();
    for (var seed = 0; seed < seeds; seed++)
    {
        if (TeamUnitAllocator.Allocate(env, new ComposeRng((ulong)seed), crossing) is not { } a)
        { cards.Append(Fail(seed, "no allocation")); continue; }
        if (TeamUnitFiller.Fill(a.Partition, a.SpawnFacing, new ComposeRng((ulong)seed)) is not { } filled)
        { cards.Append(Fail(seed, "no fill")); continue; }
        if (MidCarver.TryCarve(env, new ComposeRng((ulong)seed), crossing, filled.Unit, flushOnly: true) is not { } mid)
        { cards.Append(Fail(seed, "no band — contact discipline (a wool within BZ6's clearance of the axis); the corpus path resamples such units")); continue; }

        var plan = new PlanModel
        {
            Meta = new PlanMeta { Name = $"preset {label} s{seed}" },
            Globals = new PlanGlobals
            {
                Cell = env.Cell, Symmetry = env.Symmetry, MaxPlayers = players,
                Surface = env.Surface, Headroom = env.Headroom,
            },
        };
        foreach (var piece in filled.Unit.Pieces)
            plan.Pieces.Add(new PlanPiece { Id = piece.Id, Role = piece.Role, Rect = piece.Rect });
        plan.Zones.Add(new PlanZone { Id = "mid-band", Rect = mid.BandRect });
        plan.Placements.Spawns.Add(new SpawnPlacement
        { Piece = filled.Unit.Spawn.Piece, At = filled.Unit.Spawn.At, Facing = filled.Unit.Spawn.Facing });
        foreach (var wool in filled.Unit.Wools)
            plan.Placements.Wools.Add(new WoolPlacement { Piece = wool.Piece, At = wool.At });

        cards.Append(BoardCard(seed, plan, env.Symmetry));
    }
    sections.Append($"<section><header><h2>{label} board — preset envelope</h2>"
        + $"<p>{players} players · {land:0} land (the unit gallery's envelope) · mirror_z · card N = unit-gallery card N + the band</p>"
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
    + ".terms{border:1px solid var(--line);border-radius:8px;padding:14px 18px;margin:0 0 24px;background:var(--panel)}"
    + ".terms h3{font-size:13px;font-weight:600;margin:0 0 4px}.terms h4{font-size:12px;font-weight:600;margin:8px 0 4px}"
    + ".termcols{display:flex;flex-wrap:wrap;gap:26px}.termcols>div{flex:1;min-width:340px}"
    + ".terms ul{margin:0;padding-left:16px;font-size:11.5px;line-height:1.55}.terms i{color:var(--dim);font-style:normal}"
    + "svg{background:var(--canvas);border-radius:4px;display:block}</style>");
html.Append("<h1>Composed boards — map completion v0</h1>"
    + "<p class=tag>Two modes. <b>Corpus budget</b>: the real pipeline (ComposeBoxStages — derived envelope, "
    + "rot_180, one threaded RNG, hard-terms gate, parallel-fronts law). <b>Preset envelope</b>: the unit "
    + "gallery's handcrafted budgets with per-stage fresh RNGs — card N is exactly unit-gallery card N plus the "
    + "band and its mirror. Both use the band-only mid (uniform 20-block gap, flush dock, no stones).</p>");

// ── what gets scored: the evaluator's full term catalogue, hard and soft, straight off LayoutEvaluator ──
var glosses = new Dictionary<string, string>
{
    ["structural-integrity"] = "the plan is geometrically well-formed — no overlapping or malformed pieces",
    ["lint-pc-c"] = "the cell-level corner law — full land edges, ¾-solid corners, no narrow seams or diagonal pinches",
    ["lint-g2"] = "every land interface is a full readable edge, never a sliver",
    ["gap-hop-band"] = "every void hop between islands stays in the 10–20-block band",
    ["band-wool-clearance"] = "the mid band keeps two full cells from every wool piece",
    ["wool-ringed-hole"] = "no closure hole is ringed by a wool plateau (a second approach around the wool)",
    ["spawn-wool-floor"] = "wool↔spawn surface distance keeps the 20-block floor",
    ["fill-ratio"] = "land area vs the per-team budget",
    ["enclosed-void-count"] = "closure holes on the board",
    ["neutral-stepping-count"] = "neutral stepping stones in the mid",
    ["team-stepping-count"] = "team-side stepping stones",
    ["band-count"] = "build bands (the clean form is one merged band)",
    ["isolation-cut-count"] = "isolation cuts across the unit",
    ["uncrossed-middle-void"] = "middle void left without a crossing",
    ["frontline-count"] = "frontlines per team side",
    ["frontline-width"] = "the frontline face width",
    ["max-chain-length"] = "the longest collinear lane chain (the 50-block cap)",
    ["lane-width"] = "lane width vs the board's lane class",
    ["wool-wool-distance"] = "separation between a team's wools",
    ["spawn-wool-distance"] = "spawn↔wool marker distance",
    ["spawn-wool-spread"] = "balance: the spread of the per-wool spawn distances (one wool guarded, another abandoned)",
    ["wool-front-distance"] = "the most exposed wool's distance to the frontline edge (where the band meets the land)",
    ["wool-front-balance"] = "the triangle: spread of per-wool defence deficits (spawn distance − frontline distance)",
};
string TermRow(ILayoutTerm t) => $"<li><b>{t.Id}</b> <i>({t.RuleId}"
    + (t.Kind == TermKind.Soft ? $" · w {EvaluationProfile.Default.Weight(t.Id):0.##}" : "")
    + $")</i> — {(glosses.TryGetValue(t.Id, out var g) ? g : "")}</li>";
var hardTerms = LayoutEvaluator.AllTerms.Where(t => t.Kind == TermKind.Hard).ToList();
var softTerms = LayoutEvaluator.AllTerms.Where(t => t.Kind == TermKind.Soft).ToList();
html.Append("<div class=terms><h3>What gets scored</h3>"
    + "<p class=tag>Score = Σ violated hard terms × 1000 + Σ soft weight·distance — lower is better, 0 perfect. "
    + "The composed pipeline gates on the hard terms only; the per-card score here runs the FULL evaluation. "
    + "Soft distances are measured against envelopes from the authored seeds, so box-path scores read as "
    + "distance-from-that-era's-feel until the envelopes are re-anchored.</p>"
    + $"<div class=termcols><div><h4>Hard — the acceptance gate ({hardTerms.Count})</h4><ul>"
    + string.Concat(hardTerms.Select(TermRow)) + "</ul></div>"
    + $"<div><h4>Soft — the feel metrics ({softTerms.Count})</h4><ul>"
    + string.Concat(softTerms.Select(TermRow)) + "</ul></div></div></div>");
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

// one board card: fan the plan's pieces + band over the symmetry orbit, run the loop-closed flood, score the
// plan with the full evaluator (hard + soft — the composed path itself only gates the hard terms), render
string BoardCard(int seed, PlanModel plan, string sym)
{
    var profile = EvaluationProfile.Default;
    var eval = LayoutEvaluator.Evaluate(plan, profile);
    var hardIds = eval.Terms
        .Where(t => t.Kind == TermKind.Hard && t.Violation is not null).Select(t => t.TermId).ToList();
    var topSoft = eval.Terms
        .Where(t => t.Kind == TermKind.Soft && t.Distance > 0)
        .Select(t => (t.TermId, Contribution: profile.Weight(t.TermId) * t.Distance))
        .OrderByDescending(t => t.Contribution).Take(2).ToList();
    var scoreLabel = $"score {eval.Score:0.0}"
        + (topSoft.Count > 0 ? $" ({string.Join(", ", topSoft.Select(t => $"{t.TermId} {t.Contribution:0.0}"))})" : "");

    var order = Sym.Order(sym);
    var axes = Sym.OrbitAxes(sym);

    var fanned = new List<(int[] Rect, string Id, string Role, int K)>();
    foreach (var p in plan.Pieces.Where(p => !PlanRoles.Annotations.Contains(p.Role)))
        for (var k = 0; k < order; k++)
            fanned.Add((Fan(p.Rect, axes, k), p.Id, p.Role, k));
    var band = plan.Zones.First(z => z.Id == "mid-band");
    var bandImages = Enumerable.Range(0, order).Select(k => Fan(band.Rect, axes, k)).ToList();

    var walk = new HashSet<(int, int)>();
    foreach (var (r, _, _, _) in fanned) Rasterize(r, walk);
    foreach (var b in bandImages) Rasterize(b, walk);
    var spawnPiece = plan.Pieces.First(p => p.Role == PlanRoles.Spawn);
    var spawnCells = Enumerable.Range(0, order).Select(k => Center(Fan(spawnPiece.Rect, axes, k))).ToList();
    var reached = Flood(walk, spawnCells[0]);
    var connected = spawnCells.All(reached.Contains);
    if (!connected) disconnected++;

    var holes = ClosureAnalysis.HoleSizes(plan);
    return Card(seed, fanned, bandImages, spawnCells, connected, holes, scoreLabel, hardIds);
}

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
    IReadOnlyList<int[]> bandImages, IReadOnlyList<(int X, int Z)> spawnCells, bool connected, IReadOnlyList<int> holes,
    string scoreLabel, IReadOnlyList<string> hardIds)
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
    var hardBadge = hardIds.Count > 0 ? $" <span class=badge>⚠ hard: {string.Join(",", hardIds)}</span>" : "";
    var bad = !connected || hardIds.Count > 0;
    return $"<div class='card{(bad ? " warn" : "")}'><div class=title>seed {seed} · {w}×{h} "
        + $"· holes {holesLabel} · {scoreLabel}{badge}{hardBadge}</div>{svg}</div>";
}
