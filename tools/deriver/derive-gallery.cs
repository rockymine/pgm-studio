#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// build-cache bust: deriver v1
// The layout DERIVER, first cut (docs/contracts/layout-evaluator.md §5). Reads the authored seed corpus
// (tools/seeds/*.plan.json), fans each to the full board in CELL space, and computes structure from geometry +
// markers: islands + anchor role (team/objective/neutral/decorative), branch vs residual (morphological
// erosion — the "peel the lanes, the rest is the residual" split), per-wool approach count (arms attached to
// the room), the frontline EDGE (team land facing a build zone), and enclosed voids split declared/undeclared.
// Renders one annotated card per seed to a self-contained gallery so the author can eyeball whether the
// deriver's reading matches theirs — its disagreements are the cutoff test set (§5.4). The undeclared voids
// are the buffer worklist. Writes tools/deriver/out/derive-gallery.html.
using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

const string BgCanvas = "#080f1a";
const string AxisCol = "#a78bfa";
const string CWoolRoom = "#3fae74";   // AUTHORED wool-room piece (editor green) — intent, not derived
const string CSpawnRole = "#8f7bd6";  // AUTHORED spawn piece (editor purple) — intent, not derived
const string CBranch = "#aab6c2";     // DERIVED branch / lane (light slate)
const string CResidual = "#5b6b7a";   // DERIVED residual (dark slate)
const string CBuild = "#3b82f6";      // build zone (accent)
const string CFront = "#f59e0b";      // frontline edge (amber)
const string CVoidUndecl = "#ef4444"; // undeclared enclosed void — the buffer worklist (red)
const string CVoidDecl = "#60a5fa";   // declared void (blue)
const string MkWool = "#e6e6e6";
const string MkSpawn = "#e0b13c";
const string MkStroke = "#222222";
var RoleInk = new Dictionary<string, string> { ["team"] = "#93c5fd", ["objective"] = "#6ee7b7", ["neutral"] = "#fcd34d", ["decorative"] = "#94a3b8" };

var files = Directory.EnumerateFiles(Path.Combine("tools", "seeds"), "*.plan.json").OrderBy(p => p, StringComparer.Ordinal).ToList();
int cards = 0;
var sb = new StringBuilder();
var failures = new List<string>();

foreach (var path in files)
{
    var name = Path.GetFileName(path)[..^".plan.json".Length];
    try
    {
        var plan = PlanModel.Parse(File.ReadAllText(path))!;
        var d = Derive(plan);
        sb.Append(Card(name, plan, d));
        cards++;
        var roleCounts = string.Join(",", d.Roles.GroupBy(r => r).OrderBy(g => g.Key).Select(g => $"{g.Count()}{g.Key[..1]}"));
        var apps = string.Join("/", d.Approaches.Select(a => a.Count).OrderByDescending(x => x));
        var voidSizes = string.Join(",", d.Voids.Where(v => !v.Declared).Select(v => v.Cells.Count).OrderByDescending(x => x));
        Console.WriteLine($"  {name,-42} islands={d.Islands.Count} [{roleCounts}]  branch={d.Branch.Count} residual={d.Residual.Count}  frontEdges={d.FrontEdges.Count}  approaches={apps}  voids(cells): [{voidSizes}]");
    }
    catch (Exception ex) { failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}"); }
}

var html = Page(sb.ToString());
var outPath = Path.Combine("tools", "deriver", "out", "derive-gallery.html");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, html);
Console.WriteLine($"wrote {outPath}");
Console.WriteLine($"cards: {cards}  failures: {failures.Count}");
foreach (var f in failures) Console.WriteLine($"  FAIL {f}");

// ── the deriver ───────────────────────────────────────────────────────────────────────────────────────────

Derived Derive(PlanModel plan)
{
    int order = Symmetry.Order(plan.Globals.Symmetry);
    string[] axes = Symmetry.OrbitAxes(plan.Globals.Symmetry);
    var roleOf = plan.Pieces.ToDictionary(p => p.Id, p => p.Role);        // authored piece role (wool-room/spawn carry intent)

    var filled = new Dictionary<(int, int), (string PieceId, int K)>();   // generating-piece cells → hosting piece image
    var build = new HashSet<(int, int)>();                                // buildable (zone) cells
    var declaredVoid = new HashSet<(int, int)>();                         // buffer pieces + zone holes = declared empties

    foreach (var p in plan.Pieces)
        foreach (var c in FanCells(p.Rect, axes, order))
        {
            if (p.Role == PlanRoles.Buffer) declaredVoid.Add(c);
            else if (p.Role == PlanRoles.Connector) { /* annotation: no terrain */ }
            else filled[c] = (p.Id, 0);   // K unused post-fan; kept for clarity
        }
    // re-tag with image K so marker hosting resolves per image
    filled.Clear();
    foreach (var p in plan.Pieces)
    {
        if (p.Role is PlanRoles.Buffer or PlanRoles.Connector) continue;
        for (var k = 0; k < order; k++)
            foreach (var c in FanCellsK(p.Rect, axes, k)) filled[c] = (p.Id, k);
    }
    foreach (var z in plan.Zones)
    {
        foreach (var c in FanCells(z.Rect, axes, order)) build.Add(c);
        foreach (var h in z.Holes) foreach (var c in FanCells(h, axes, order)) declaredVoid.Add(c);
    }

    // islands — 4-connected components of filled cells
    var islandOf = new Dictionary<(int, int), int>();
    var islands = new List<HashSet<(int, int)>>();
    foreach (var start in filled.Keys)
    {
        if (islandOf.ContainsKey(start)) continue;
        var comp = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        q.Enqueue(start); islandOf[start] = islands.Count;
        while (q.Count > 0)
        {
            var cur = q.Dequeue(); comp.Add(cur);
            foreach (var nb in N4(cur))
                if (filled.ContainsKey(nb) && !islandOf.ContainsKey(nb)) { islandOf[nb] = islands.Count; q.Enqueue(nb); }
        }
        islands.Add(comp);
    }

    // anchor role per island — team (spawn) / objective (wool, no spawn) / neutral (in build) / decorative
    var spawnKeys = new HashSet<(string, int)>();
    var woolKeys = new HashSet<(string, int)>();
    for (var k = 0; k < order; k++)
    {
        foreach (var s in plan.Placements.Spawns) spawnKeys.Add((s.Piece, k));
        foreach (var w in plan.Placements.Wools) woolKeys.Add((w.Piece, k));
    }
    var roles = new string[islands.Count];
    for (var i = 0; i < islands.Count; i++)
    {
        // the authored wool-room / spawn ROLE is the strongest intent signal — use it alongside the markers
        bool hasSpawn = islands[i].Any(c => spawnKeys.Contains(filled[c]) || roleOf[filled[c].PieceId] == PlanRoles.Spawn),
             hasWool = islands[i].Any(c => woolKeys.Contains(filled[c]) || roleOf[filled[c].PieceId] == PlanRoles.WoolRoom),
             touchesBuild = islands[i].Any(c => N4(c).Any(build.Contains) || build.Contains(c));
        roles[i] = hasSpawn ? "team" : hasWool ? "objective" : touchesBuild ? "neutral" : "decorative";
    }

    // branch vs residual — erosion: a cell whose 4 neighbours are all same-island is a residual CORE; the
    // residual is the cores dilated back by one (restoring the thick region's rim); everything else is branch.
    var residual = new HashSet<(int, int)>();
    foreach (var isl in islands)
    {
        var core = isl.Where(c => N4(c).All(isl.Contains)).ToHashSet();
        foreach (var c in core) { residual.Add(c); foreach (var nb in N4(c)) if (isl.Contains(nb)) residual.Add(nb); }
    }
    var branch = new HashSet<(int, int)>(filled.Keys.Where(c => !residual.Contains(c)));

    // per-wool approach count — arms (connected filled clusters adjacent to the room) touching the wool's piece
    var approaches = new List<(int Island, double Bx, double Bz, int Count)>();
    for (var k = 0; k < order; k++)
        foreach (var w in plan.Placements.Wools)
        {
            var piece = plan.Pieces.FirstOrDefault(p => p.Id == w.Piece);
            if (piece is null) continue;
            var room = FanCellsK(piece.Rect, axes, k).Where(filled.ContainsKey).ToHashSet();
            if (room.Count == 0) continue;
            int isl = islandOf[room.First()];
            var arm = new HashSet<(int, int)>();
            foreach (var c in room) foreach (var nb in N4(c)) if (filled.ContainsKey(nb) && !room.Contains(nb)) arm.Add(nb);
            approaches.Add((isl, MarkerBlock(piece.Rect, w.At, k, axes).X, MarkerBlock(piece.Rect, w.At, k, axes).Z, ComponentCount(arm)));
        }

    // frontline edges — the void-facing OUTSIDE edge only: a team-island cell side shared with a cell that is
    // buildable AND empty (the crossing void). The neighbour must be unfilled — an interior seam between two
    // pieces is never a frontline, even where a big build rectangle overlaps the terrain on both sides.
    var frontEdges = new List<(int X1, int Z1, int X2, int Z2)>();
    foreach (var c in filled.Keys)
    {
        if (roles[islandOf[c]] != "team") continue;
        foreach (var (nb, seg) in N4Seg(c)) if (build.Contains(nb) && !filled.ContainsKey(nb)) frontEdges.Add(seg);
    }

    // enclosed voids — a hole is TRUE void (empty terrain, non-buildable) that the border can't reach without
    // crossing terrain OR a build region: both terrain and build are walls for this flood. That is what lets a
    // rotation pocket ("rotary device") near the frontline — walled by twin frontlines on some sides and the
    // mid build band on the others — register as enclosed, instead of leaking to the border through the band.
    // Declared when the pocket overlaps a buffer / zone-hole; else an undeclared void (the buffer worklist).
    var all = filled.Keys.Concat(build).Concat(declaredVoid).ToList();
    int minX = all.Min(c => c.Item1) - 1, maxX = all.Max(c => c.Item1) + 1;
    int minZ = all.Min(c => c.Item2) - 1, maxZ = all.Max(c => c.Item2) + 1;
    bool TrueVoid((int, int) c) => !filled.ContainsKey(c) && !build.Contains(c)
        && c.Item1 >= minX && c.Item1 <= maxX && c.Item2 >= minZ && c.Item2 <= maxZ;
    var outside = new HashSet<(int, int)>();
    var oq = new Queue<(int, int)>();
    for (var x = minX; x <= maxX; x++) foreach (var c in new[] { (x, minZ), (x, maxZ) }) if (TrueVoid(c) && outside.Add(c)) oq.Enqueue(c);
    for (var z = minZ; z <= maxZ; z++) foreach (var c in new[] { (minX, z), (maxX, z) }) if (TrueVoid(c) && outside.Add(c)) oq.Enqueue(c);
    while (oq.Count > 0) { var cur = oq.Dequeue(); foreach (var nb in N4(cur)) if (TrueVoid(nb) && outside.Add(nb)) oq.Enqueue(nb); }

    var voids = new List<(HashSet<(int, int)> Cells, bool Declared)>();
    var seenVoid = new HashSet<(int, int)>();
    for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
        {
            var s = (x, z);
            if (!TrueVoid(s) || outside.Contains(s) || seenVoid.Contains(s)) continue;
            var comp = new HashSet<(int, int)>(); var q = new Queue<(int, int)>(); q.Enqueue(s); seenVoid.Add(s);
            while (q.Count > 0) { var cur = q.Dequeue(); comp.Add(cur); foreach (var nb in N4(cur)) if (TrueVoid(nb) && !outside.Contains(nb) && seenVoid.Add(nb)) q.Enqueue(nb); }
            bool declared = comp.Any(declaredVoid.Contains);   // a buffer / zone-hole marks this pocket deliberate
            // report EVERY enclosed void, any size — the authored seeds are ground truth, and they carry
            // intended holes as small as 1x2 cells (mirror-tiny-map-cliff, rotate-wide-frontline). Never let a
            // size rule override the corpus.
            voids.Add((comp, declared));
        }

    return new Derived(plan.Globals.Cell, filled, build, residual, branch, islands, islandOf, roles, approaches, frontEdges, voids);
}

// ── geometry helpers ────────────────────────────────────────────────────────────────────────────────────────

IEnumerable<(int, int)> N4((int, int) c) { yield return (c.Item1 + 1, c.Item2); yield return (c.Item1 - 1, c.Item2); yield return (c.Item1, c.Item2 + 1); yield return (c.Item1, c.Item2 - 1); }

// neighbour + the shared cell-edge segment (in CELL units) between c and that neighbour
IEnumerable<((int, int) Nb, (int, int, int, int) Seg)> N4Seg((int, int) c)
{
    int x = c.Item1, z = c.Item2;
    yield return ((x + 1, z), (x + 1, z, x + 1, z + 1));
    yield return ((x - 1, z), (x, z, x, z + 1));
    yield return ((x, z + 1), (x, z + 1, x + 1, z + 1));
    yield return ((x, z - 1), (x, z, x + 1, z));
}

int ComponentCount(HashSet<(int, int)> cells)
{
    var seen = new HashSet<(int, int)>(); int n = 0;
    foreach (var s in cells)
    {
        if (!seen.Add(s)) continue; n++;
        var q = new Queue<(int, int)>(); q.Enqueue(s);
        while (q.Count > 0) { var cur = q.Dequeue(); foreach (var nb in N4(cur)) if (cells.Contains(nb) && seen.Add(nb)) q.Enqueue(nb); }
    }
    return n;
}

// fan a cell rect to EVERY orbit image, yielding all cells
IEnumerable<(int, int)> FanCells(int[] rect, string[] axes, int order)
{
    for (var k = 0; k < order; k++) foreach (var c in FanCellsK(rect, axes, k)) yield return c;
}

// fan a cell rect to the k-th orbit image (identity at k=0), yielding its cells
IEnumerable<(int, int)> FanCellsK(int[] rect, string[] axes, int k)
{
    int x = rect[0], z = rect[1], w = rect[2], h = rect[3];
    int x1, z1, x2, z2;
    if (k == 0) { x1 = x; z1 = z; x2 = x + w; z2 = z + h; }
    else
    {
        var axis = axes[k - 1];
        var pts = new[] { (x, z), (x + w, z), (x, z + h), (x + w, z + h) }
            .Select(p => Symmetry.Apply(p.Item1, p.Item2, axis, 0, 0)).ToList();
        x1 = (int)Math.Round(pts.Min(p => p.X)); z1 = (int)Math.Round(pts.Min(p => p.Z));
        x2 = (int)Math.Round(pts.Max(p => p.X)); z2 = (int)Math.Round(pts.Max(p => p.Z));
    }
    for (var cx = x1; cx < x2; cx++) for (var cz = z1; cz < z2; cz++) yield return (cx, cz);
}

// a marker's block coordinate at image k (piece origin + half-cell offset, fanned)
(double X, double Z) MarkerBlock(int[] rect, double[] at, int k, string[] axes)
{
    double cx = rect[0] + at[0], cz = rect[1] + at[1];   // in cells
    if (k > 0) { var (fx, fz) = Symmetry.Apply(cx, cz, axes[k - 1], 0, 0); cx = fx; cz = fz; }
    return (cx, cz);   // in cells; caller scales by cell
}

// ── render ────────────────────────────────────────────────────────────────────────────────────────────────

string Card(string name, PlanModel plan, Derived d)
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

    // build zones (under terrain)
    foreach (var c in d.Build) CellRect(c.Item1, c.Item2, CBuild, 0.14, CBuild, 0.4);
    // terrain cells — AUTHORED wool-room (green) / spawn (purple) take their editor colour so the intent reads;
    // everything else is coloured by the DERIVED branch (light slate) / residual (dark slate) split
    var roleOf = plan.Pieces.ToDictionary(p => p.Id, p => p.Role);
    foreach (var c in d.Filled.Keys)
    {
        var role = roleOf[d.Filled[c].PieceId];
        string fill = role == PlanRoles.WoolRoom ? CWoolRoom : role == PlanRoles.Spawn ? CSpawnRole
            : d.Residual.Contains(c) ? CResidual : CBranch;
        CellRect(c.Item1, c.Item2, fill, 0.75, BgCanvas, 0.5);
    }
    // enclosed voids — undeclared (red, the worklist) vs declared (blue)
    foreach (var (vc, isDecl) in d.Voids) foreach (var c in vc) CellRect(c.Item1, c.Item2, isDecl ? CVoidDecl : CVoidUndecl, isDecl ? 0.2 : 0.28, isDecl ? CVoidDecl : CVoidUndecl, 0.9);
    // axis
    svg.Append($"<line x1=\"{N(PX(0))}\" y1=\"{N(PY(minZ))}\" x2=\"{N(PX(0))}\" y2=\"{N(PY(maxZ))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.4\" stroke-width=\"1\"/>");
    svg.Append($"<line x1=\"{N(PX(minX))}\" y1=\"{N(PY(0))}\" x2=\"{N(PX(maxX))}\" y2=\"{N(PY(0))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.4\" stroke-width=\"1\"/>");
    // frontline edges (amber, thick)
    foreach (var (x1, z1, x2, z2) in d.FrontEdges)
        svg.Append($"<line x1=\"{N(PX(x1 * cell))}\" y1=\"{N(PY(z1 * cell))}\" x2=\"{N(PX(x2 * cell))}\" y2=\"{N(PY(z2 * cell))}\" stroke=\"{CFront}\" stroke-width=\"2.4\" stroke-linecap=\"round\"/>");
    // markers + per-wool approach count
    for (var k = 0; k < Symmetry.Order(plan.Globals.Symmetry); k++)
    {
        foreach (var w in plan.Placements.Wools)
        {
            var pc = plan.Pieces.FirstOrDefault(p => p.Id == w.Piece); if (pc is null) continue;
            var (bx, bz) = MarkerBlock(pc.Rect, w.At, k, Symmetry.OrbitAxes(plan.Globals.Symmetry));
            double sq = cell * 0.5 * s;
            svg.Append($"<rect x=\"{N(PX(bx * cell) - sq / 2)}\" y=\"{N(PY(bz * cell) - sq / 2)}\" width=\"{N(sq)}\" height=\"{N(sq)}\" fill=\"{MkWool}\" stroke=\"{MkStroke}\" stroke-width=\"1\"/>");
        }
        foreach (var sp in plan.Placements.Spawns)
        {
            var pc = plan.Pieces.FirstOrDefault(p => p.Id == sp.Piece); if (pc is null) continue;
            var (bx, bz) = MarkerBlock(pc.Rect, sp.At, k, Symmetry.OrbitAxes(plan.Globals.Symmetry));
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
    string roleStr = string.Join(" ", new[] { "team", "objective", "neutral", "decorative" }.Where(byRole.ContainsKey).Select(r => $"{byRole[r]} {r}"));
    var appCounts = d.Approaches.Select(a => a.Count).ToList();
    string appStr = appCounts.Count == 0 ? "—" : string.Join("/", appCounts.OrderByDescending(x => x));
    string stat(string v, string l) => $"<span class=\"stat\"><span class=\"stat-v\">{v}</span> {l}</span>";
    var stats = string.Join("<span class=\"dot\">·</span>",
        stat(d.Islands.Count.ToString(), "islands"), stat(roleStr, ""),
        stat(appStr, "approaches"), stat($"{undecl}", "undeclared voids") + (decl > 0 ? $" <span class=\"stat\">/ {decl} declared</span>" : ""));

    return $"""
          <article class="card">
            <div class="card-head"><span class="card-id">{Esc(name)}</span><span class="card-sub">{Esc(plan.Globals.Symmetry)}</span></div>
            <div class="svg-wrap">{svg}</div>
            <div class="card-stats">{stats}</div>
          </article>

    """;
}

static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

string Page(string cardsHtml)
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
    .svg-wrap{ background:var(--bg-canvas); border:1px solid var(--border); border-radius:6px; overflow:hidden; line-height:0; }
    .svg-wrap svg.map{ display:block; width:100%; height:auto; }
    .card-stats{ display:flex; flex-wrap:wrap; align-items:center; gap:3px 6px; font-family:var(--mono); font-size:11px; color:var(--text-muted); border-top:1px solid var(--border); padding-top:8px; }
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
          <span class="lg"><span class="sw" style="background:{CBranch}"></span>branch / lane</span>
          <span class="lg"><span class="sw" style="background:{CResidual}"></span>residual</span>
          <span class="lg"><span class="sw sw--edge" style="border-color:{CFront}"></span>frontline edge</span>
          <span class="sep"></span>
          <span class="legend-lbl">Voids</span>
          <span class="lg"><span class="sw" style="background:{CVoidUndecl}55;border:1.3px solid {CVoidUndecl}"></span>undeclared (buffer worklist)</span>
          <span class="lg"><span class="sw" style="background:{CVoidDecl}55;border:1.3px solid {CVoidDecl}"></span>declared</span>
          <span class="sep"></span>
          <span class="legend-lbl">Markers</span>
          <span class="lg"><span class="sw" style="background:{MkWool}"></span>wool (n× = approaches)</span>
          <span class="lg"><span class="sw" style="background:{MkSpawn};border-radius:50%"></span>spawn</span>
          <span class="sep"></span>
          <span class="legend-lbl">Build</span>
          <span class="lg"><span class="sw" style="background:{CBuild}33;border:1.2px solid {CBuild}"></span>zone</span>
        </div>
    """;

    string body = $"""
    <div class="wrap">
      <header class="top">
        <p class="eyebrow">Layout evaluator · deriver v1 · seed corpus</p>
        <h1>What the deriver reads in the authored seeds</h1>
        <p class="lede">Each card is a seed from <code>tools/seeds/</code>, fanned to the full board, with the
        <strong>derived</strong> structure drawn on — nothing here is authored beyond geometry + the wool/spawn
        markers. Review target: does the deriver's reading match yours? The <b>branch/residual</b> split (green
        vs slate) is the peel-the-lanes cutoff (§5.3) and the <b>v1 approximation</b> most worth eyeballing; the
        <b>n×</b> beside each wool is its approach count (green ≥2 = multi-access, red = lone dead-end); the
        <strong style="color:{CVoidUndecl}">red voids</strong> are enclosed empties nobody has declared yet —
        <b>the buffer worklist</b> (add a hole-mark/buffer to each deliberate one). Disagreements are the cutoff
        test set. Per <code>docs/contracts/layout-evaluator.md</code> §5.</p>
        {legend}
      </header>

      <div class="grid">
    {cardsHtml}  </div>

      <footer>Deriver v1 — first cut for visual review, not the final algorithm. Authored <b>wool-room</b> /
      <b>spawn</b> pieces keep their editor colour (intent); other terrain is the DERIVED branch / residual split
      (morphological erosion). approach count = arms at the room; frontline = team land's OUTSIDE edge facing a
      build void (no interior seams); voids = true void (empty, non-buildable) walled by terrain OR build (the
      terrain+build encasing catches the frontline rotary devices) — EVERY enclosed void reported, any size, the
      seeds are ground truth. Static SVG, self-contained, cell = 5 blocks.</footer>
    </div>
    """;

    return $"""
    <!doctype html>
    <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Deriver v1 — seed corpus</title><style>{css}</style></head>
    <body>{body}</body></html>
    """;
}

// derived-structure bundle for one seed
record Derived(
    int Cell,
    Dictionary<(int, int), (string PieceId, int K)> Filled,
    HashSet<(int, int)> Build,
    HashSet<(int, int)> Residual,
    HashSet<(int, int)> Branch,
    List<HashSet<(int, int)>> Islands,
    Dictionary<(int, int), int> IslandOf,
    string[] Roles,
    List<(int Island, double Bx, double Bz, int Count)> Approaches,
    List<(int X1, int Z1, int X2, int Z2)> FrontEdges,
    List<(HashSet<(int, int)> Cells, bool Declared)> Voids);
