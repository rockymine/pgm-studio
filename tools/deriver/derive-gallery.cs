#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// build-cache bust: deriver v1
// The layout DERIVER, first cut (docs/contracts/layout-evaluator.md §5). Reads the authored seed corpus
// (tools/seeds/*.plan.json), fans each to the full board in CELL space, and computes structure from geometry +
// markers: islands + anchor role (team/objective/neutral/decorative), stepping stones (neutral/team), wool
// lanes (the terrain stacked from a wool room's redstone interface), residual (whatever terrain remains), per-
// wool approach count (arms at the room), the frontline EDGE (team land facing a build zone), the intra-team /
// self bridges, and enclosed voids classified by position (encased/gap/frontline/middle) + declared/undeclared.
// Renders one annotated card per seed to a self-contained gallery so the author can eyeball whether the
// deriver's reading matches theirs — its disagreements are the cutoff test set (§5.4). The undeclared voids
// are the buffer worklist. Writes tools/deriver/out/derive-gallery.html.
using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;

const string BgCanvas = "#080f1a";
const string AxisCol = "#a78bfa";
const string CWoolRoom = "#3fae74";   // AUTHORED wool-room piece (editor green) — intent, not derived
const string CSpawnRole = "#8f7bd6";  // AUTHORED spawn piece (editor purple) — intent, not derived
const string CResidual = "#5b6b7a";   // DERIVED residual — terrain not claimed by any specific label (dark slate)
const string CStoneNeutral = "#78716c"; // DERIVED neutral stepping stone — a contested island (warm stone gray)
const string CStoneTeam = "#d946ef";  // DERIVED team stepping stone — captive, on a spawn<->wool route (fuchsia)
const string CBuild = "#3b82f6";      // build zone (fallback accent)
// build-zone kinds — typed by what the zone links
const string CZFrontFront = "#3b82f6";   // front<->front — the crossing / direct team link (blue)
const string CZFrontNeut = "#14b8a6";    // front<->neutral — a team's bridge toward the mid (teal)
const string CZNeutNeut = "#a855f7";     // neutral<->neutral — mid-internal link between neutral islands (violet)
const string CZIntra = "#f472b6";        // intra — a team's own isolation cut (pink, matches the bridge edge)
const string CZSelf = "#22d3ee";         // self — a one-island notch (cyan, matches the self edge)
const string CFront = "#f59e0b";      // frontline edge (amber)
const string CIntra = "#f472b6";      // intra-team spawn<->wool interface / isolation-cut bridge (pink)
const string CSelf = "#22d3ee";       // self-bridge — a notch within ONE island (cyan, dotted)
// enclosed-void (hole) classes, interior → contested spectrum
const string CHoleEncased = "#818cf8";   // one team's terrain, no build — a bubble deep inside a landmass (indigo)
const string CHoleGap = "#f472b6";       // one team, intra/self build — a team's isolation-cut gap (pink)
const string CHoleFront = "#fbbf24";     // one team touching frontline build — the team's exposed edge (amber)
const string CHoleMiddle = "#ef4444";    // >=2 teams / pure build — the contested crossing arena (red)
const string CWoolLane = "#f97316";   // wool lane — the terrain the wool room stacks into (orange, a solid label)
const string CRedstone = "#ff2d2d";   // the wool-room interface (redstone line the generator stamps)
const string MkWool = "#e6e6e6";
const string MkSpawn = "#e0b13c";
const string MkStroke = "#222222";
var RoleInk = new Dictionary<string, string> { ["team"] = "#93c5fd", ["objective"] = "#6ee7b7", ["neutral"] = "#fcd34d", ["decorative"] = "#94a3b8" };

var files = Directory.EnumerateFiles(Path.Combine("tools", "seeds"), "*.plan.json").OrderBy(p => p, StringComparer.Ordinal).ToList();
int cards = 0;
var sb = new StringBuilder();
var sbGen = new StringBuilder();
var failures = new List<string>();

void Emit(string name, PlanModel plan, StringBuilder buf)
{
    var d = BoardDeriver.Derive(plan);
    buf.Append(Card(name, plan, d));
    cards++;
    string ifaceRange(IEnumerable<(string Kind, int Neutrals, int Width, int IfaceMin, int IfaceMax)> g)
    { int lo = g.Min(z => z.IfaceMin), hi = g.Max(z => z.IfaceMax); return lo == hi ? $"{lo}" : $"{lo}-{hi}"; }
    var zoneStr = string.Join(", ", d.Zones.GroupBy(z => z.Kind).OrderBy(g => g.Key)
        .Select(g => $"{g.Count()}{g.Key}(w{string.Join("/", g.Select(z => z.Width).Distinct().OrderBy(x => x))} if{ifaceRange(g)})"));
    // BZ3 buckets: 1 cell = 5-block choke, 2 = 10-block bridge (dominant), >=3 = 15+ open band
    var bz3 = string.Join("/", d.Zones.GroupBy(z => z.Width <= 1 ? "choke" : z.Width == 2 ? "bridge" : "band")
        .OrderBy(g => g.Key).Select(g => $"{g.Count()}{g.Key}"));
    var pw = string.Join("/", d.Voids.Where(v => v.Class == "middle" && v.CrossRoutes > 0).Select(v => v.CrossRoutes).OrderByDescending(x => x));
    var shapes = string.Join(",", d.WoolShapes.GroupBy(s => s.Shape).OrderBy(g => g.Key).Select(g => $"{g.Count()}{g.Key}"));
    Console.WriteLine($"  {name,-42} mid={d.MidForm,-10} lanes=[{shapes,-12}] bz3={bz3,-16} zones=[{zoneStr}]{(pw.Length > 0 ? $"  pWays={pw}" : "")}");
}

foreach (var path in files)
{
    var name = Path.GetFileName(path)[..^".plan.json".Length];
    try { Emit(name, PlanModel.Parse(File.ReadAllText(path))!, sb); }
    catch (Exception ex) { failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}"); }
}

// candidates from the CURRENT composer — a spread of P/team/symmetry/seed. These are what the composer builds
// today (the long-lane era): the ready-made NEGATIVE set the scoring rules will eventually be calibrated against.
Console.WriteLine("-- generated (current composer) --");
var genCases = new List<(string Label, int P, int T, string S, ulong Seed)>();
void AddCases(int p, int t, string sym, string tag, params ulong[] seeds)
{
    foreach (var s in seeds) genCases.Add(($"p{p} {tag} s{s}", p, t, sym, s));
}
// 2-team rot_180 — the workhorse family, swept across seeds + player counts
AddCases(12, 2, "rot_180", "r180", 1, 3, 7, 13, 15, 17, 24, 33, 45);
AddCases(16, 2, "rot_180", "r180", 2, 8, 19, 42);
AddCases(20, 2, "rot_180", "r180", 1, 5, 7, 11, 53);
AddCases(30, 2, "rot_180", "r180", 1, 3);
// 2-team mirrors
AddCases(12, 2, "mirror_z", "mrz", 1, 2, 3, 5, 8, 11, 24);
AddCases(20, 2, "mirror_z", "mrz", 2, 8);
AddCases(12, 2, "mirror_x", "mrx", 2, 5, 9);
// 4-team rot_90
AddCases(10, 4, "rot_90", "r90", 1, 5, 9);
AddCases(16, 4, "rot_90", "r90", 1, 5, 13);
AddCases(20, 4, "rot_90", "r90", 3, 9);
foreach (var g in genCases)
{
    try { Emit(g.Label, Composer.Compose(new ComposeRequest(g.P, g.T, g.S, g.Seed, 5)), sbGen); }
    catch (Exception ex) { failures.Add($"{g.Label}: {ex.GetType().Name}: {ex.Message}"); }
}

var html = Page(sb.ToString(), sbGen.ToString());
var outPath = Path.Combine("tools", "deriver", "out", "derive-gallery.html");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, html);
Console.WriteLine($"wrote {outPath}");
Console.WriteLine($"cards: {cards}  failures: {failures.Count}");
foreach (var f in failures) Console.WriteLine($"  FAIL {f}");

// ── render ────────────────────────────────────────────────────────────────────────────────────────────────

string Card(string name, PlanModel plan, BoardStructure d)
{
    int cell = d.Cell;
    var allCells = d.Filled.Keys.Concat(d.Build).ToList();
    foreach (var v in d.Voids) allCells.AddRange(v.Cells);
    double minX = allCells.Min(c => c.Item1) * cell, minZ = allCells.Min(c => c.Item2) * cell;
    double maxX = (allCells.Max(c => c.Item1) + 1) * cell, maxZ = (allCells.Max(c => c.Item2) + 1) * cell;
    double mgn = cell * 1.2; minX -= mgn; minZ -= mgn; maxX += mgn; maxZ += mgn;
    double bw = maxX - minX, bh = maxZ - minZ;
    const double TW = 320; double s = TW / bw, vbw = bw * s, vbh = bh * s;
    double PX(double bx) => (bx - minX) * s;
    double PY(double bz) => (bz - minZ) * s;

    var svg = new StringBuilder();
    svg.Append($"<svg viewBox=\"0 0 {N(vbw)} {N(vbh)}\" xmlns=\"http://www.w3.org/2000/svg\" class=\"map\" role=\"img\">");
    svg.Append($"<rect width=\"{N(vbw)}\" height=\"{N(vbh)}\" fill=\"{BgCanvas}\"/>");

    void CellRect(int cx, int cz, string fill, double fo, string stroke, double sw) =>
        svg.Append($"<rect x=\"{N(PX(cx * cell))}\" y=\"{N(PY(cz * cell))}\" width=\"{N(cell * s)}\" height=\"{N(cell * s)}\" fill=\"{fill}\" fill-opacity=\"{N(fo)}\" stroke=\"{stroke}\" stroke-width=\"{N(sw)}\"/>");

    // build zones (under terrain) — tinted by ZONE KIND (what the zone links)
    string ZoneCol(string k) => k switch {
        "front-front" => CZFrontFront, "front-neutral" => CZFrontNeut, "neutral-neutral" => CZNeutNeut,
        "intra" => CZIntra, "self" => CZSelf, _ => CBuild };
    foreach (var c in d.Build)
    {
        var zc = d.BuildKindOf.TryGetValue(c, out var k) ? ZoneCol(k) : CBuild;
        CellRect(c.Item1, c.Item2, zc, 0.18, zc, 0.5);
    }
    // terrain cells — every tile gets exactly ONE label, by priority: AUTHORED wool-room (green) / spawn
    // (purple) keep their editor colour; a STEPPING-STONE island is coloured whole (neutral stone-gray / team
    // fuchsia); a WOOL-LANE tile is the wool room's approach (orange, a solid label, not an overlay); everything
    // that remains is RESIDUAL (dark slate) — no erosion split, residual is simply the unclaimed terrain.
    var roleOf = plan.Pieces.ToDictionary(p => p.Id, p => p.Role);
    foreach (var c in d.Filled.Keys)
    {
        var role = roleOf[d.Filled[c].PieceId];
        var kind = d.SteppingKind[d.IslandOf[c]];
        string fill = role == PlanRoles.WoolRoom ? CWoolRoom : role == PlanRoles.Spawn ? CSpawnRole
            : kind == "team" ? CStoneTeam : kind == "neutral" ? CStoneNeutral
            : d.LaneCells.Contains(c) ? CWoolLane : CResidual;
        CellRect(c.Item1, c.Item2, fill, 0.75, BgCanvas, 0.5);
    }
    // enclosed voids — coloured by position class; undeclared (the buffer worklist) pops, declared is muted
    foreach (var (vc, isDecl, cls, _) in d.Voids)
    {
        string hc = cls == "encased" ? CHoleEncased : cls == "gap" ? CHoleGap : cls == "frontline" ? CHoleFront : CHoleMiddle;
        foreach (var c in vc) CellRect(c.Item1, c.Item2, hc, isDecl ? 0.14 : 0.30, hc, isDecl ? 0.6 : 1.1);
    }
    // axis
    svg.Append($"<line x1=\"{N(PX(0))}\" y1=\"{N(PY(minZ))}\" x2=\"{N(PX(0))}\" y2=\"{N(PY(maxZ))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.4\" stroke-width=\"1\"/>");
    svg.Append($"<line x1=\"{N(PX(minX))}\" y1=\"{N(PY(0))}\" x2=\"{N(PX(maxX))}\" y2=\"{N(PY(0))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.4\" stroke-width=\"1\"/>");
    // intra-team spawn<->wool interfaces (pink, dashed) — the deliberate internal gap / isolation-cut bridge
    foreach (var (x1, z1, x2, z2) in d.IntraEdges)
        svg.Append($"<line x1=\"{N(PX(x1 * cell))}\" y1=\"{N(PY(z1 * cell))}\" x2=\"{N(PX(x2 * cell))}\" y2=\"{N(PY(z2 * cell))}\" stroke=\"{CIntra}\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-dasharray=\"3 2\"/>");
    // self-bridge notches (cyan, dotted) — a build pocket carved into ONE island, its two walls the same landmass
    foreach (var (x1, z1, x2, z2) in d.SelfEdges)
        svg.Append($"<line x1=\"{N(PX(x1 * cell))}\" y1=\"{N(PY(z1 * cell))}\" x2=\"{N(PX(x2 * cell))}\" y2=\"{N(PY(z2 * cell))}\" stroke=\"{CSelf}\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-dasharray=\"1 2.6\"/>");
    // redstone interface line — the wool-room edge the stack projects from (where the generator stamps redstone)
    foreach (var (x1, z1, x2, z2) in d.RedstoneEdges)
        svg.Append($"<line x1=\"{N(PX(x1 * cell))}\" y1=\"{N(PY(z1 * cell))}\" x2=\"{N(PX(x2 * cell))}\" y2=\"{N(PY(z2 * cell))}\" stroke=\"{CRedstone}\" stroke-width=\"3\" stroke-linecap=\"round\"/>");
    // frontline edges (amber, thick, solid)
    foreach (var (x1, z1, x2, z2) in d.FrontEdges)
        svg.Append($"<line x1=\"{N(PX(x1 * cell))}\" y1=\"{N(PY(z1 * cell))}\" x2=\"{N(PX(x2 * cell))}\" y2=\"{N(PY(z2 * cell))}\" stroke=\"{CFront}\" stroke-width=\"2.4\" stroke-linecap=\"round\"/>");
    // markers + per-wool approach count
    for (var k = 0; k < Symmetry.Order(plan.Globals.Symmetry); k++)
    {
        foreach (var w in plan.Placements.Wools)
        {
            var pc = plan.Pieces.FirstOrDefault(p => p.Id == w.Piece); if (pc is null) continue;
            var (bx, bz) = BoardDeriver.MarkerBlock(pc.Rect, w.At, k, Symmetry.OrbitAxes(plan.Globals.Symmetry));
            double sq = cell * 0.5 * s;
            svg.Append($"<rect x=\"{N(PX(bx * cell) - sq / 2)}\" y=\"{N(PY(bz * cell) - sq / 2)}\" width=\"{N(sq)}\" height=\"{N(sq)}\" fill=\"{MkWool}\" stroke=\"{MkStroke}\" stroke-width=\"1\"/>");
        }
        foreach (var sp in plan.Placements.Spawns)
        {
            var pc = plan.Pieces.FirstOrDefault(p => p.Id == sp.Piece); if (pc is null) continue;
            var (bx, bz) = BoardDeriver.MarkerBlock(pc.Rect, sp.At, k, Symmetry.OrbitAxes(plan.Globals.Symmetry));
            svg.Append($"<circle cx=\"{N(PX(bx * cell))}\" cy=\"{N(PY(bz * cell))}\" r=\"{N(cell * 0.3 * s)}\" fill=\"{MkSpawn}\" stroke=\"{MkStroke}\" stroke-width=\"1\"/>");
        }
    }
    // approach-count badge beside each wool
    svg.Append($"<g font-family=\"ui-monospace, Menlo, monospace\" font-weight=\"700\" font-size=\"10\" text-anchor=\"middle\" paint-order=\"stroke\" stroke=\"{BgCanvas}\" stroke-width=\"2.4\">");
    foreach (var a in d.Approaches)
        svg.Append($"<text x=\"{N(PX(a.Bx * cell))}\" y=\"{N(PY(a.Bz * cell) - cell * 0.5 * s)}\" fill=\"{(a.Count >= 2 ? "#6ee7b7" : "#fca5a5")}\">{a.Count}×</text>");
    svg.Append("</g></svg>");

    // stats
    var byRole = d.Roles.GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
    int undecl = d.Voids.Count(v => !v.Declared), decl = d.Voids.Count(v => v.Declared);
    int neutralStones = d.SteppingKind.Count(k => k == "neutral"), teamStones = d.SteppingKind.Count(k => k == "team");
    // anchor roles only (the anchorless islands are reported as stepping stones, not a "neutral" anchor role)
    var appCounts = d.Approaches.Select(a => a.Count).ToList();
    string appStr = appCounts.Count == 0 ? "—" : string.Join("/", appCounts.OrderByDescending(x => x));
    string stat(string v, string l) => l.Length == 0
        ? $"<span class=\"stat\"><span class=\"stat-v\">{v}</span></span>"
        : $"<span class=\"stat\"><span class=\"stat-v\">{v}</span> {l}</span>";
    // one labelled row; empty items dropped, whole row dropped if nothing to show
    string grp(string label, params string[] items)
    {
        var body = string.Concat(items.Where(s => !string.IsNullOrEmpty(s)));
        return body.Length == 0 ? "" : $"<div class=\"sgroup\"><span class=\"sglabel\">{label}</span><span class=\"sgbody\">{body}</span></div>";
    }

    string stoneStr = teamStones + neutralStones == 0 ? "" : teamStones > 0
        ? $"{neutralStones} neutral · {teamStones} team stones" : $"{neutralStones} neutral stones";
    var holeOrder = new[] { "encased", "gap", "frontline", "middle" };
    var holeByCls = d.Voids.GroupBy(v => v.Class).ToDictionary(g => g.Key, g => g.Count());
    var pw = d.Voids.Where(v => v.Class == "middle" && v.CrossRoutes > 1).Select(v => v.CrossRoutes).OrderByDescending(x => x).ToList();
    var zoneOrder = new[] { "front-front", "front-neutral", "neutral-neutral", "intra", "self" };
    var zoneBy = d.Zones.GroupBy(z => z.Kind).ToDictionary(g => g.Key, g => g.Count());
    int ffNeut = d.Zones.Where(z => z.Kind == "front-front").Sum(z => z.Neutrals);
    // BZ3 width buckets (cells → ×5 blocks): choke ≤1 (5), bridge 2 (10, dominant), band ≥3 (15+)
    var bzBy = d.Zones.GroupBy(z => z.Width <= 1 ? "choke" : z.Width == 2 ? "bridge" : "band").ToDictionary(g => g.Key, g => g.Count());

    var stats = string.Concat(
        grp("Islands",
            stat(d.Islands.Count.ToString(), "islands"),
            string.Concat(new[] { "team", "objective" }.Where(byRole.ContainsKey).Select(r => stat(byRole[r].ToString(), r))),
            stat(stoneStr, "")),
        grp("Wools",
            stat(appStr, "approaches"),
            d.WoolShapes.Count > 0 ? stat(string.Join(" ", d.WoolShapes
                .GroupBy(s => s.Width > 0 ? $"{s.Shape}·w{s.Width}" : s.Shape).OrderBy(g => g.Key)
                .Select(g => $"{g.Count()}×{g.Key}")), "lanes") : "",
            stat(d.LaneCells.Count.ToString(), "lane tiles")),
        grp("Zones",
            string.Concat(zoneOrder.Where(zoneBy.ContainsKey).Select(k =>
                stat(zoneBy[k].ToString(), k + (k == "front-front" && ffNeut > 0 ? $" +{ffNeut}◦" : "")))),
            new[] { "choke", "bridge", "band" }.Any(bzBy.ContainsKey)
                ? "<span class=\"dot\">·</span>" + string.Concat(new[] { "choke", "bridge", "band" }.Where(bzBy.ContainsKey).Select(b => stat(bzBy[b].ToString(), b))) : ""),
        grp("Edges",
            d.FrontEdges.Count > 0 ? stat(d.FrontEdges.Count.ToString(), "frontline") : "",
            d.IntraEdges.Count > 0 ? stat(d.IntraEdges.Count.ToString(), "intra-team") : "",
            d.SelfEdges.Count > 0 ? stat(d.SelfEdges.Count.ToString(), "self-bridge") : ""),
        grp("Holes",
            string.Concat(holeOrder.Where(holeByCls.ContainsKey).Select(c => stat(holeByCls[c].ToString(), c))),
            pw.Count > 0 ? "<span class=\"dot\">·</span>" + stat(string.Join("/", pw), "parallel ways") : "",
            decl > 0 ? "<span class=\"dot\">·</span>" + stat($"{undecl}/{d.Voids.Count}", "undeclared") : ""));

    return $"""
          <article class="card">
            <div class="card-head"><span class="card-id">{Esc(name)}</span><span class="card-sub"><span class="midform midform--{d.MidForm}">{Esc(d.MidForm)}</span> · {Esc(plan.Globals.Symmetry)}</span></div>
            <div class="svg-wrap">{svg}</div>
            <div class="card-stats">{stats}</div>
          </article>

    """;
}

static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

string Page(string cardsHtml, string genHtml)
{
    const string css = """
    :root{ --bg-base:#0f172a; --bg-panel:#1e293b; --bg-canvas:#080f1a; --border:#334155; --text-muted:#8397b0;
      --text-secondary:#94a3b8; --text-primary:#cbd5e1; --text-bright:#e2e8f0; --text-strong:#fff; --accent-light:#60a5fa; --warn:#f59e0b;
      --mono:ui-monospace, SFMono-Regular, Menlo, monospace; --sans:-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }
    @media (prefers-color-scheme: light){ :root{ --bg-base:#f1f5f9; --bg-panel:#fff; --border:#cbd5e1; --text-muted:#586780;
      --text-secondary:#475569; --text-primary:#1e293b; --text-bright:#0f172a; --text-strong:#0f172a; --accent-light:#2563eb; } }
    :root[data-theme="dark"]{ --bg-base:#0f172a; --bg-panel:#1e293b; --border:#334155; --text-muted:#8397b0; --text-secondary:#94a3b8; --text-primary:#cbd5e1; --text-bright:#e2e8f0; --text-strong:#fff; --accent-light:#60a5fa; }
    :root[data-theme="light"]{ --bg-base:#f1f5f9; --bg-panel:#fff; --border:#cbd5e1; --text-muted:#586780; --text-secondary:#475569; --text-primary:#1e293b; --text-bright:#0f172a; --text-strong:#0f172a; --accent-light:#2563eb; }
    *{ box-sizing:border-box; } html,body{ margin:0; padding:0; }
    body{ background:var(--bg-base); color:var(--text-primary); font-family:var(--sans); font-size:14px; line-height:1.5; overflow-x:hidden; -webkit-font-smoothing:antialiased; }
    .wrap{ max-width:1320px; margin:0 auto; padding:30px 24px 64px; }
    header.top{ border-bottom:1px solid var(--border); padding-bottom:20px; }
    .eyebrow{ font-family:var(--mono); font-size:11px; letter-spacing:.14em; text-transform:uppercase; color:var(--accent-light); margin:0 0 6px; }
    h1{ font-size:24px; line-height:1.2; margin:0 0 10px; color:var(--text-strong); font-weight:660; text-wrap:balance; }
    h2.section{ font-size:16px; margin:34px 0 4px; padding-top:20px; border-top:1px solid var(--border); color:var(--text-bright); font-weight:640; }
    .section-sub{ font-family:var(--mono); font-size:12px; font-weight:400; color:var(--text-muted); }
    .section-note{ margin:0 0 4px; max-width:80ch; color:var(--text-secondary); font-size:12.5px; }
    .lede{ margin:0; max-width:80ch; color:var(--text-secondary); font-size:13.5px; }
    .lede code,.lede b{ font-family:var(--mono); font-size:12px; } .lede b{ color:var(--text-bright); }
    .lede code{ color:var(--text-bright); background:var(--bg-panel); padding:1px 5px; border-radius:3px; }
    .legend{ display:flex; flex-wrap:wrap; gap:7px 16px; margin-top:16px; padding:12px 14px; background:var(--bg-panel); border:1px solid var(--border); border-radius:6px; align-items:center; }
    .legend-lbl{ font-family:var(--mono); font-size:10px; letter-spacing:.1em; text-transform:uppercase; color:var(--text-muted); }
    .lg{ display:inline-flex; align-items:center; gap:6px; font-size:12px; color:var(--text-secondary); font-family:var(--mono); }
    .sw{ width:13px; height:13px; border-radius:2px; flex:none; } .sw--edge{ width:15px; height:0; border-top:3px solid; }
    .sep{ width:1px; align-self:stretch; background:var(--border); }
    .grid{ display:grid; grid-template-columns:repeat(auto-fill, minmax(340px, 1fr)); gap:18px; align-items:start; margin-top:24px; }
    .card{ background:var(--bg-panel); border:1px solid var(--border); border-radius:10px; padding:12px 12px 10px; display:flex; flex-direction:column; gap:9px; }
    .card-head{ display:flex; align-items:baseline; justify-content:space-between; gap:8px; }
    .card-id{ font-family:var(--mono); font-size:12.5px; color:var(--text-bright); font-weight:600; }
    .card-sub{ font-family:var(--mono); font-size:11px; color:var(--text-muted); }
    .midform{ font-weight:700; text-transform:uppercase; letter-spacing:.06em; padding:1px 5px; border-radius:3px; color:#0f172a; }
    .midform--channelled{ background:#7dd3fc; } .midform--parallel{ background:#fca5a5; } .midform--hash{ background:#c4b5fd; }
    .svg-wrap{ background:var(--bg-canvas); border:1px solid var(--border); border-radius:6px; overflow:hidden; line-height:0; }
    .svg-wrap svg.map{ display:block; width:100%; height:auto; }
    .card-stats{ display:flex; flex-direction:column; gap:3px; font-family:var(--mono); font-size:11px; color:var(--text-muted); border-top:1px solid var(--border); padding-top:8px; }
    .sgroup{ display:grid; grid-template-columns:52px 1fr; gap:9px; align-items:baseline; }
    .sglabel{ font-size:8.5px; letter-spacing:.11em; text-transform:uppercase; color:var(--text-muted); opacity:.65; text-align:right; }
    .sgbody{ display:flex; flex-wrap:wrap; gap:2px 10px; }
    .stat-v{ color:var(--text-bright); font-weight:600; } .dot{ color:var(--border); }
    footer{ margin-top:40px; padding-top:16px; border-top:1px solid var(--border); font-family:var(--mono); font-size:11px; color:var(--text-muted); max-width:90ch; }
    """;

    string legend = $"""
        <div class="legend">
          <span class="legend-lbl">Authored</span>
          <span class="lg"><span class="sw" style="background:{CWoolRoom}"></span>wool-room piece</span>
          <span class="lg"><span class="sw" style="background:{CSpawnRole}"></span>spawn piece</span>
          <span class="sep"></span>
          <span class="legend-lbl">Derived</span>
          <span class="lg"><span class="sw" style="background:{CWoolLane}"></span>wool lane</span>
          <span class="lg"><span class="sw" style="background:{CStoneNeutral}"></span>neutral stepping stone</span>
          <span class="lg"><span class="sw" style="background:{CStoneTeam}"></span>team stepping stone</span>
          <span class="lg"><span class="sw" style="background:{CResidual}"></span>residual (what remains)</span>
          <span class="lg"><span class="sw sw--edge" style="border-color:{CFront}"></span>frontline edge</span>
          <span class="lg"><span class="sw sw--edge" style="border-top-style:dashed;border-color:{CIntra}"></span>intra-team bridge</span>
          <span class="lg"><span class="sw sw--edge" style="border-top-style:dotted;border-color:{CSelf}"></span>self-bridge notch</span>
          <span class="lg"><span class="sw sw--edge" style="border-color:{CRedstone};border-top-width:3px"></span>redstone interface</span>
          <span class="sep"></span>
          <span class="legend-lbl">Holes</span>
          <span class="lg"><span class="sw" style="background:{CHoleEncased}55;border:1.3px solid {CHoleEncased}"></span>encased (in one team)</span>
          <span class="lg"><span class="sw" style="background:{CHoleGap}55;border:1.3px solid {CHoleGap}"></span>gap (isolation cut)</span>
          <span class="lg"><span class="sw" style="background:{CHoleFront}55;border:1.3px solid {CHoleFront}"></span>frontline pocket</span>
          <span class="lg"><span class="sw" style="background:{CHoleMiddle}55;border:1.3px solid {CHoleMiddle}"></span>middle (contested)</span>
          <span class="sep"></span>
          <span class="legend-lbl">Markers</span>
          <span class="lg"><span class="sw" style="background:{MkWool}"></span>wool (n× = approaches)</span>
          <span class="lg"><span class="sw" style="background:{MkSpawn};border-radius:50%"></span>spawn</span>
          <span class="sep"></span>
          <span class="legend-lbl">Build zones</span>
          <span class="lg"><span class="sw" style="background:{CZFrontFront}55;border:1.2px solid {CZFrontFront}"></span>front↔front (crossing)</span>
          <span class="lg"><span class="sw" style="background:{CZFrontNeut}55;border:1.2px solid {CZFrontNeut}"></span>front↔neutral (bridge to mid)</span>
          <span class="lg"><span class="sw" style="background:{CZNeutNeut}55;border:1.2px solid {CZNeutNeut}"></span>neutral↔neutral (mid-internal)</span>
          <span class="lg"><span class="sw" style="background:{CZIntra}55;border:1.2px solid {CZIntra}"></span>intra / self (isolation cut)</span>
        </div>
    """;

    string body = $"""
    <div class="wrap">
      <header class="top">
        <p class="eyebrow">Layout evaluator · deriver v1 · seed corpus</p>
        <h1>What the deriver reads in the authored seeds</h1>
        <p class="lede">Each card is a seed from <code>tools/seeds/</code>, fanned to the full board, with the
        <strong>derived</strong> structure drawn on — nothing here is authored beyond geometry + the wool/spawn
        markers. Review target: does the deriver's reading match yours? Every terrain tile gets exactly one label:
        a <strong style="color:{CStoneNeutral}">stone-gray</strong> or <strong style="color:{CStoneTeam}">fuchsia</strong>
        island is a <b>stepping stone</b> (whole island — <b>team</b> fuchsia if captive on one team's spawn↔wool
        route, else <b>neutral</b> gray, a contested centre island); the <strong style="color:{CWoolLane}">orange</strong>
        tiles are the <b>wool lane</b> — the terrain stacked out from the wool room's
        <strong style="color:{CRedstone}">redstone interface</strong> (bright red line) until void, build, or a
        <b>T</b> (a crossbar reaching beyond the band on both sides; a one-sided jut is just a side branch), a
        two-sided room stacking both ways and a side-docked room stacking along the docked lane's axis (a lane may
        run to a frontline; spawns never stack a lane); and everything that remains is
        <strong style="color:{CResidual}">residual</strong> — unclaimed terrain (no erosion split; residual is
        simply what is left). The <b>n×</b> beside each wool is its approach count (green ≥2 = multi-access, red =
        lone dead-end). The
        <b>holes</b> (enclosed voids) are coloured by <b>what their boundary touches</b>, interior→contested:
        <strong style="color:{CHoleEncased}">encased</strong> (deep in one team), <strong style="color:{CHoleGap}">gap</strong>
        (a team's isolation-cut), <strong style="color:{CHoleFront}">frontline pocket</strong> (a team's exposed edge),
        <strong style="color:{CHoleMiddle}">middle</strong> (contested crossing) — and any still <b>undeclared</b>
        is the buffer worklist. The <b>build zones</b> are tinted by what they link:
        <strong style="color:{CZFrontFront}">front↔front</strong> (the crossing, may carry stepping stones between it),
        <strong style="color:{CZFrontNeut}">front↔neutral</strong> (a team's bridge to the mid),
        <strong style="color:{CZNeutNeut}">neutral↔neutral</strong> (a mid-internal link, often across the axis), and
        <strong style="color:{CZIntra}">intra/self</strong> (a team's isolation cut) — from which the
        <b>CT mid-form</b> in the card header (<b>channelled</b> / <b>parallel</b> / <b>hash</b>) is derived, and a
        middle hole's <b>parallel ways</b> = the crossings ringing it (big-board's two front↔front lanes). A
        <strong style="color:{CIntra}">pink dashed edge</strong> is an <b>intra-team bridge</b> — a build region on
        a team's own internal spawn↔wool route (direct, or a chain through a <b>captive</b> stepping stone only that
        team can reach): it marks a deliberate internal gap where a piece was chopped off and bridged back to slow
        attackers (the isolation cut). A <strong style="color:{CSelf}">cyan dotted edge</strong> is a
        <b>self-bridge notch</b> — a build pocket carved into a <em>single</em> island (its two walls the same
        landmass); it is internal like the bridge but shapes one piece rather than gapping two, so it reads as its
        own signal. Captivity also separates a <b>team</b> stepping stone (on the spawn↔wool path) from a
        <b>neutral</b> one (a contested centre island).
        Disagreements are the cutoff test set. Per <code>docs/contracts/layout-evaluator.md</code> §5.</p>
        {legend}
      </header>

      <h2 class="section">Authored seeds <span class="section-sub">— ground truth</span></h2>
      <div class="grid">
    {cardsHtml}  </div>

      <h2 class="section">Generated <span class="section-sub">— current composer, candidate negatives</span></h2>
      <p class="section-note">What the composer builds <b>today</b> (the long-lane era), run through the same
      deriver. Not ground truth — these are the <b>bad samples</b> the scoring rules will be calibrated against:
      read the derived structure and note where it goes wrong (marathon wool lanes, missing multi-access, dead
      residual, absent or malformed mid). The deriver stays neutral; the divergence is the signal.</p>
      <div class="grid">
    {genHtml}  </div>

      <footer>Deriver v1 — first cut for visual review, not the final algorithm. Every terrain tile gets ONE
      label by priority: authored <b>wool-room</b> / <b>spawn</b> pieces keep their editor colour (intent), then
      stepping-stone islands, then wool-lane tiles, then <b>residual</b> = whatever remains (no erosion split —
      residual is simply unclaimed terrain). approach count = arms at the room; frontline = OUTSIDE edge facing a
      build void, but only on a VOID-DOMINANT island (more void-border than build-border — exposed territory); a
      build-dominant island (embedded in the crossing) or a pure-void one is a stepping stone with no frontline
      (corpus-wide, void-dominant == holds a spawn/wool), labelled as a whole island (team = fuchsia if captive on
      a spawn&lt;-&gt;wool route, else neutral = stone-gray); an edge facing an intra-team spawn&lt;-&gt;wool bridge
      (a build region on a team's own internal route — direct, or through a captive stepping stone only that team
      can reach) is re-tagged intra, not frontline, and a build pocket carved into a SINGLE island is split off as
      a self-bridge notch (cyan dotted); wool lane (orange) = terrain stacked from the wool room's redstone
      interface (red line) out to void/build/T (a crossbar reaching beyond the band on both sides stops it; a
      one-sided jut is a side branch), both ways for two-sided rooms, and along the docked lane's own axis for a
      side-dock — wools only, never spawns, and a lane may reach a frontline; holes = true void walled by terrain
      OR build (the terrain+build encasing catches the frontline rotary devices), EVERY one reported at any size
      (the seeds are ground truth), classified by what the boundary touches — encased (one team, no build), gap
      (one team + intra/self build, an isolation cut), frontline pocket (one team + frontline build), middle (>=2
      teams / pure build); still-undeclared holes are the buffer worklist. Build zones are typed by what they link
      (front↔front / front↔neutral / neutral↔neutral / intra / self), read off the island incidence — from which
      the CT mid-form (channelled / parallel / hash) and a middle hole's parallel-ways are derived. Static SVG,
      self-contained, cell = 5 blocks.</footer>
    </div>
    """;

    return $"""
    <!doctype html>
    <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Deriver v1 — seed corpus</title><style>{css}</style></head>
    <body>{body}</body></html>
    """;
}

