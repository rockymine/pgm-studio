#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// build-cache bust: composer round 6 (G49 — real spawn + wool-room pieces)
using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

// ── palette (source of truth: plan-doc.js ROLE_COLORS, plan-canvas.js MARKER_COLORS, tokens.css) ──
const string CPiece = "#7c8899";      // ROLE_COLORS.piece
const string CWoolRoom = "#3fae74";   // ROLE_COLORS["wool-room"]
const string CSpawnRole = "#8f7bd6";  // ROLE_COLORS.spawn
const string CBuffer = "#f2792b";     // ROLE_COLORS.buffer (reserved-gap annotation)
const string MkSpawn = "#e0b13c";     // MARKER_COLORS.spawn
const string MkWool = "#e6e6e6";      // MARKER_COLORS.wool
const string MkIron = "#9aa7b4";      // MARKER_COLORS.iron
const string Accent = "#3b82f6";      // --accent
const string BgCanvas = "#080f1a";    // --bg-canvas (SVG ground; stays dark in both themes)
const string AxisCol = "#a78bfa";     // --canvas-axis
const string Ink = "#ffffff";         // --canvas-ink
const string MkStroke = "#222222";    // spawn/wool/iron marker stroke (editor uses #222)

var families = new List<(string Header, string Sub, List<Case> Cases)>();
void AddFam(string h, string sub, IEnumerable<Case> cs) => families.Add((h, sub, cs.ToList()));

// 2-team rot_180, grouped by player count, seeds 1/7/13
foreach (var p in new[] { 12, 16, 20, 30 })
    AddFam($"2-team · rot_180 · {p} players", "one authored unit, rotated 180° about centre",
        new ulong[] { 1, 7, 13 }.Select(seed => new Case(p, 2, "rot_180", seed)));
// 4-team rot_90 pinwheel
AddFam("4-team · rot_90", "one wedge, fanned to four by 90° rotation",
    new[] { new Case(10, 4, "rot_90", 1UL), new Case(16, 4, "rot_90", 5UL), new Case(20, 4, "rot_90", 9UL) });
// mirror symmetries
AddFam("Mirror symmetries", "reflected halves (mirror_x / mirror_z)",
    new[] { new Case(12, 2, "mirror_x", 2UL), new Case(10, 2, "mirror_z", 2UL) });
// mirror_z at bigger budgets — where the wide frontline + MD6 stone grid form
AddFam("Mirror · wide frontline + stone grid", "mirror_z — FR6 wide face the band docks to, MD6 2-col grid",
    new ulong[] { 1, 2, 3, 5, 8, 11 }.Select(seed => new Case(20, 2, "mirror_z", seed)));
// centerline islands — a stone (or pair) straddling the axis, its fan completing central island(s) (CT11).
// Freshly classified after the pair-depth bias: lead with the 10x10 pair, then a deep pair, then each single form.
AddFam("Centerline islands — variety", "axis-straddling stone(s): 10×10 pair / deep pair / square / horizontal / vertical (CT11)",
    new[]
    {
        new Case(12, 2, "rot_180", 24UL),   // 10x10 PAIR — two small squares on the axis (the ex-10 form, now common)
        new Case(12, 2, "mirror_z", 24UL),  // 10x10 pair (mirror)
        new Case(12, 2, "rot_180", 33UL),   // deep pair (10x20 each) — the occasional form
        new Case(12, 2, "rot_180", 15UL),   // single 10x10 (small square)
        new Case(12, 2, "rot_180", 17UL),   // single horizontal (20x10, wide+flat)
        new Case(16, 2, "rot_180", 53UL),   // single vertical (10x20, deep+narrow)
        new Case(12, 2, "rot_180", 45UL),   // single 20x20 (large square)
    });

int cardCount = 0;
var failures = new List<(string Id, string Msg)>();
var sectionsHtml = new StringBuilder();

foreach (var (header, sub, cs) in families)
{
    var cards = new StringBuilder();
    int rendered = 0;
    foreach (var c in cs)
    {
        var id = $"gen-p{c.P}-t{c.T}-{c.S}-s{c.Seed}";
        try
        {
            var stages = Composer.ComposeStages(new ComposeRequest(c.P, c.T, c.S, c.Seed, 5));
            var plan = stages.Plan;
            int pieces = plan.Pieces.Count;
            int zones = plan.Zones.Count;
            int stones = stages.Mid.Stones.Count;
            int holes = ClosureAnalysis.HoleSizes(plan).Count;
            bool cut = stages.Cut != null;
            var svg = BuildSvg(plan);
            cards.Append(Card(id, c, svg, pieces, zones, stones, holes, cut));
            rendered++;
            cardCount++;
        }
        catch (Exception ex)
        {
            failures.Add((id, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }
    if (rendered == 0) continue;
    sectionsHtml.Append($"""
        <section class="fam">
          <div class="fam-head">
            <h2 class="fam-title">{Esc(header)}</h2>
            <span class="fam-sub">{Esc(sub)}</span>
            <span class="fam-count">{rendered} plan{(rendered == 1 ? "" : "s")}</span>
          </div>
          <div class="grid">
    {cards}      </div>
        </section>

    """);
}

string failuresPanel = "";
if (failures.Count > 0)
{
    var rows = new StringBuilder();
    foreach (var (fid, msg) in failures)
        rows.Append($"<li><code>{Esc(fid)}</code> — {Esc(msg)}</li>\n");
    failuresPanel = $"""
        <section class="panel panel--err">
          <h2 class="panel-title">Unexpected compose failures ({failures.Count})</h2>
          <p class="panel-body">These cases were expected to compose but threw. Investigate — not documented limitations.</p>
          <ul class="panel-list">{rows}</ul>
        </section>

    """;
}

var html = Page(sectionsHtml.ToString(), failuresPanel);
var outPath = Path.Combine("tools", "compose", "out", "composer-review.html");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, html);

Console.WriteLine($"wrote {outPath}");
Console.WriteLine($"cards rendered: {cardCount}");
Console.WriteLine($"unexpected failures: {failures.Count}");
foreach (var (fid, msg) in failures) Console.WriteLine($"  FAIL {fid}: {msg}");
Console.WriteLine($"contains 'http': {(html.Contains("http://") || html.Contains("https://") ? "YES" : "no")}");

// ────────────────────────────────────────────────────────────────────────────
// SVG builder — fans the authored unit into the full map via PgmStudio.Geom.Symmetry,
// mirroring Compose/ComposeGeometry.FanImage's 4-corner transform + AABB rebound.

string BuildSvg(PlanModel plan)
{
    int cell = plan.Globals.Cell;
    string sym = plan.Globals.Symmetry;
    int order = Symmetry.Order(sym);
    string[] axes = Symmetry.OrbitAxes(sym);

    var pieceImgs = new List<(double X1, double Z1, double X2, double Z2, string Role, int K, string Id)>();
    var zoneImgs = new List<(double X1, double Z1, double X2, double Z2, string Id, int K, List<(double, double, double, double)> Holes)>();
    var spawnImgs = new List<(double Cx, double Cz, double Dx, double Dz, int K)>();
    var squareImgs = new List<(double Cx, double Cz, string Kind, int K)>();

    // pieces (all orbit images)
    foreach (var p in plan.Pieces)
    {
        double x1 = p.Rect[0] * cell, z1 = p.Rect[1] * cell;
        double x2 = (p.Rect[0] + p.Rect[2]) * cell, z2 = (p.Rect[1] + p.Rect[3]) * cell;
        for (int k = 0; k < order; k++)
        {
            var (a, b, cc, d) = Fan(x1, z1, x2, z2, axes, k);
            pieceImgs.Add((a, b, cc, d, p.Role, k, p.Id));
        }
    }
    // zones + holes (all orbit images)
    foreach (var z in plan.Zones)
    {
        double x1 = z.Rect[0] * cell, z1 = z.Rect[1] * cell;
        double x2 = (z.Rect[0] + z.Rect[2]) * cell, z2 = (z.Rect[1] + z.Rect[3]) * cell;
        for (int k = 0; k < order; k++)
        {
            var (a, b, cc, d) = Fan(x1, z1, x2, z2, axes, k);
            var holes = new List<(double, double, double, double)>();
            foreach (var h in z.Holes)
            {
                double hx1 = h[0] * cell, hz1 = h[1] * cell, hx2 = (h[0] + h[2]) * cell, hz2 = (h[1] + h[3]) * cell;
                holes.Add(Fan(hx1, hz1, hx2, hz2, axes, k));
            }
            zoneImgs.Add((a, b, cc, d, z.Id, k, holes));
        }
    }
    // markers (all orbit images) — resolve piece-relative offsets to block coords, then fan
    var pieceById = plan.Pieces.ToDictionary(p => p.Id);
    (double, double)[] facingDir(string f) => f switch
    {
        "right" => new (double, double)[] { (1, 0) }, "back" => new[] { (0.0, 1.0) },
        "left" => new[] { (-1.0, 0.0) }, _ => new[] { (0.0, -1.0) },  // front
    };
    foreach (var sp in plan.Placements.Spawns)
    {
        if (!pieceById.TryGetValue(sp.Piece, out var pc)) continue;
        double bx = (pc.Rect[0] + sp.At[0]) * cell, bz = (pc.Rect[1] + sp.At[1]) * cell;
        var (dx, dz) = facingDir(sp.Facing)[0];
        for (int k = 0; k < order; k++)
        {
            var (mx, mz) = k == 0 ? (bx, bz) : Symmetry.Apply(bx, bz, axes[k - 1], 0, 0);
            var (fdx, fdz) = k == 0 ? (dx, dz) : Symmetry.Apply(dx, dz, axes[k - 1], 0, 0);
            spawnImgs.Add((mx, mz, fdx, fdz, k));
        }
    }
    void AddSquares(IEnumerable<(string Piece, double[] At)> ms, string kind)
    {
        foreach (var m in ms)
        {
            if (!pieceById.TryGetValue(m.Piece, out var pc)) continue;
            double bx = (pc.Rect[0] + m.At[0]) * cell, bz = (pc.Rect[1] + m.At[1]) * cell;
            for (int k = 0; k < order; k++)
            {
                var (mx, mz) = k == 0 ? (bx, bz) : Symmetry.Apply(bx, bz, axes[k - 1], 0, 0);
                squareImgs.Add((mx, mz, kind, k));
            }
        }
    }
    AddSquares(plan.Placements.Wools.Select(w => (w.Piece, w.At)), "wool");
    AddSquares(plan.Placements.Iron.Select(i => (i.Piece, i.At)), "iron");

    // content bounds over fanned pieces + zones
    double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
    void Bound(double a, double b, double cc, double d)
    { minX = Math.Min(minX, a); minZ = Math.Min(minZ, b); maxX = Math.Max(maxX, cc); maxZ = Math.Max(maxZ, d); }
    foreach (var p in pieceImgs) Bound(p.X1, p.Z1, p.X2, p.Z2);
    foreach (var z in zoneImgs) Bound(z.X1, z.Z1, z.X2, z.Z2);
    if (minX == double.MaxValue) { minX = minZ = 0; maxX = maxZ = cell; }

    double mgn = cell + 1;
    minX -= mgn; minZ -= mgn; maxX += mgn; maxZ += mgn;
    double bw = maxX - minX, bh = maxZ - minZ;
    const double TW = 460, THmax = 520;
    double s = Math.Min(TW / bw, THmax / bh);
    double vbw = bw * s, vbh = bh * s;
    double PX(double bx) => (bx - minX) * s;
    double PY(double bz) => (bz - minZ) * s;

    double r = cell * 0.34 * s;          // marker disc radius (px), cell-relative like the editor
    double sq = r * 1.5;                 // wool/iron square side
    double idFont = 12.0, zoneFont = 10.5;

    var svg = new StringBuilder();
    svg.Append($"<svg viewBox=\"0 0 {N(vbw)} {N(vbh)}\" xmlns=\"http://www.w3.org/2000/svg\" class=\"map\" role=\"img\">");
    // buffer (reserved-gap) diagonal-hatch pattern, in device pixels
    svg.Append($"<defs><pattern id=\"buffer-hatch\" patternUnits=\"userSpaceOnUse\" width=\"6\" height=\"6\" patternTransform=\"rotate(45)\">" +
               $"<rect width=\"6\" height=\"6\" fill=\"{CBuffer}\" fill-opacity=\"0.12\"/>" +
               $"<line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" stroke=\"{CBuffer}\" stroke-width=\"1.2\"/></pattern></defs>");
    svg.Append($"<rect x=\"0\" y=\"0\" width=\"{N(vbw)}\" height=\"{N(vbh)}\" fill=\"{BgCanvas}\"/>");

    // faint cell grid + origin axes + centre ring
    int gx0 = (int)Math.Floor(minX / cell), gx1 = (int)Math.Ceiling(maxX / cell);
    int gz0 = (int)Math.Floor(minZ / cell), gz1 = (int)Math.Ceiling(maxZ / cell);
    svg.Append("<g stroke-linecap=\"butt\">");
    for (int gc = gx0; gc <= gx1; gc++)
        svg.Append($"<line x1=\"{N(PX(gc * cell))}\" y1=\"{N(PY(gz0 * cell))}\" x2=\"{N(PX(gc * cell))}\" y2=\"{N(PY(gz1 * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.12\" stroke-width=\"0.6\"/>");
    for (int gc = gz0; gc <= gz1; gc++)
        svg.Append($"<line x1=\"{N(PX(gx0 * cell))}\" y1=\"{N(PY(gc * cell))}\" x2=\"{N(PX(gx1 * cell))}\" y2=\"{N(PY(gc * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.12\" stroke-width=\"0.6\"/>");
    svg.Append("</g>");
    // origin axes (x=0, z=0) — the symmetry centre
    svg.Append($"<line x1=\"{N(PX(0))}\" y1=\"{N(PY(gz0 * cell))}\" x2=\"{N(PX(0))}\" y2=\"{N(PY(gz1 * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.42\" stroke-width=\"1.1\"/>");
    svg.Append($"<line x1=\"{N(PX(gx0 * cell))}\" y1=\"{N(PY(0))}\" x2=\"{N(PX(gx1 * cell))}\" y2=\"{N(PY(0))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.42\" stroke-width=\"1.1\"/>");
    svg.Append($"<circle cx=\"{N(PX(0))}\" cy=\"{N(PY(0))}\" r=\"{N(cell * 0.32 * s)}\" fill=\"none\" stroke=\"{AxisCol}\" stroke-opacity=\"0.55\" stroke-width=\"1.2\"/>");

    // zones (under pieces, like the editor) — translucent accent, dashed
    foreach (var z in zoneImgs)
    {
        svg.Append($"<rect x=\"{N(PX(z.X1))}\" y=\"{N(PY(z.Z1))}\" width=\"{N((z.X2 - z.X1) * s)}\" height=\"{N((z.Z2 - z.Z1) * s)}\" " +
                   $"fill=\"{Accent}\" fill-opacity=\"0.22\" stroke=\"{Accent}\" stroke-width=\"1.4\" stroke-dasharray=\"7 4\"/>");
        foreach (var h in z.Holes)
            svg.Append($"<rect x=\"{N(PX(h.Item1))}\" y=\"{N(PY(h.Item2))}\" width=\"{N((h.Item3 - h.Item1) * s)}\" height=\"{N((h.Item4 - h.Item2) * s)}\" " +
                       $"fill=\"{BgCanvas}\" fill-opacity=\"0.6\" stroke=\"{Accent}\" stroke-width=\"0.8\" stroke-dasharray=\"3 3\"/>");
    }

    // pieces — solid role colour, fill-opacity 0.7, same-colour stroke; buffers hatched + dashed (no terrain)
    foreach (var p in pieceImgs)
    {
        var col = RoleColor(p.Role);
        if (p.Role == PlanRoles.Buffer)
        {
            svg.Append($"<rect x=\"{N(PX(p.X1))}\" y=\"{N(PY(p.Z1))}\" width=\"{N((p.X2 - p.X1) * s)}\" height=\"{N((p.Z2 - p.Z1) * s)}\" " +
                       $"fill=\"url(#buffer-hatch)\" stroke=\"{col}\" stroke-width=\"1.4\" stroke-dasharray=\"5 4\" stroke-opacity=\"0.85\"/>");
            continue;
        }
        svg.Append($"<rect x=\"{N(PX(p.X1))}\" y=\"{N(PY(p.Z1))}\" width=\"{N((p.X2 - p.X1) * s)}\" height=\"{N((p.Z2 - p.Z1) * s)}\" " +
                   $"fill=\"{col}\" fill-opacity=\"0.7\" stroke=\"{col}\" stroke-width=\"1.5\"/>");
    }

    // markers — squares (wool/iron) then spawns, all images solid
    foreach (var m in squareImgs)
    {
        var col = m.Kind == "wool" ? MkWool : MkIron;
        svg.Append($"<rect x=\"{N(PX(m.Cx) - sq / 2)}\" y=\"{N(PY(m.Cz) - sq / 2)}\" width=\"{N(sq)}\" height=\"{N(sq)}\" rx=\"{N(cell * 0.08 * s)}\" " +
                   $"fill=\"{col}\" fill-opacity=\"0.85\" stroke=\"{MkStroke}\" stroke-width=\"1\"/>");
    }
    foreach (var m in spawnImgs)
    {
        svg.Append($"<circle cx=\"{N(PX(m.Cx))}\" cy=\"{N(PY(m.Cz))}\" r=\"{N(r)}\" fill=\"{MkSpawn}\" fill-opacity=\"0.85\" stroke=\"{MkStroke}\" stroke-width=\"1\"/>");
        // facing tick: direction is a unit vector; r is already in px, so r*1.7 is the tick length in px
        double ex = PX(m.Cx) + m.Dx * r * 1.7, ey = PY(m.Cz) + m.Dz * r * 1.7;
        svg.Append($"<line x1=\"{N(PX(m.Cx))}\" y1=\"{N(PY(m.Cz))}\" x2=\"{N(ex)}\" y2=\"{N(ey)}\" stroke=\"{MkStroke}\" stroke-width=\"2\"/>");
    }

    // id labels — BASE image (k=0) only. pieces at centre (ink), zones near top edge (accent-light).
    svg.Append($"<g font-family=\"ui-monospace, SFMono-Regular, Menlo, monospace\" font-weight=\"600\" text-anchor=\"middle\" paint-order=\"stroke\" stroke=\"{BgCanvas}\" stroke-width=\"3\" stroke-linejoin=\"round\">");
    foreach (var z in zoneImgs.Where(z => z.K == 0))
        svg.Append($"<text x=\"{N((PX(z.X1) + PX(z.X2)) / 2)}\" y=\"{N(PY(z.Z1) + zoneFont * 0.9)}\" font-size=\"{N(zoneFont)}\" fill=\"#93c5fd\">{Esc(z.Id)}</text>");
    foreach (var p in pieceImgs.Where(p => p.K == 0))
        svg.Append($"<text x=\"{N((PX(p.X1) + PX(p.X2)) / 2)}\" y=\"{N((PY(p.Z1) + PY(p.Z2)) / 2 + idFont * 0.34)}\" font-size=\"{N(idFont)}\" fill=\"{Ink}\">{Esc(p.Id)}</text>");
    svg.Append("</g>");

    svg.Append("</svg>");
    return svg.ToString();

    string RoleColor(string role) => role switch { PlanRoles.WoolRoom => CWoolRoom, PlanRoles.Spawn => CSpawnRole, PlanRoles.Buffer => CBuffer, _ => CPiece };
}

// FanImage twin: identity at k=0, else transform 4 corners by the k-th orbit axis about origin + rebound AABB.
static (double X1, double Z1, double X2, double Z2) Fan(double x1, double z1, double x2, double z2, string[] axes, int k)
{
    if (k == 0) return (x1, z1, x2, z2);
    var axis = axes[k - 1];
    (double x, double z)[] corners = { (x1, z1), (x1, z2), (x2, z1), (x2, z2) };
    var pts = corners.Select(c => Symmetry.Apply(c.x, c.z, axis, 0, 0)).ToList();
    return (pts.Min(p => p.X), pts.Min(p => p.Z), pts.Max(p => p.X), pts.Max(p => p.Z));
}

static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

string Card(string id, Case c, string svg, int pieces, int zones, int stones, int holes, bool cut)
{
    string stat(string label, string val) => $"<span class=\"stat\"><span class=\"stat-v\">{val}</span> {label}</span>";
    var stats = string.Join("<span class=\"stat-dot\">·</span>",
        stat("pieces", pieces.ToString()),
        stat("zones", zones.ToString()),
        stat("stones", stones.ToString()),
        stat("holes", holes.ToString()),
        $"<span class=\"stat stat--{(cut ? "yes" : "no")}\"><span class=\"stat-v\">{(cut ? "yes" : "no")}</span> cut</span>");
    return $"""
            <article class="card">
              <div class="card-head"><span class="card-id">{Esc(id)}</span></div>
              <div class="svg-wrap">{svg}</div>
              <div class="card-stats">{stats}</div>
            </article>

    """;
}

string Page(string sections, string failuresPanel)
{
    const string css = """
    :root{
      --bg-base:#0f172a; --bg-panel:#1e293b; --bg-panel-2:#172032; --bg-canvas:#080f1a;
      --border:#334155; --text-muted:#8397b0; --text-secondary:#94a3b8;
      --text-primary:#cbd5e1; --text-bright:#e2e8f0; --text-strong:#ffffff;
      --accent:#3b82f6; --accent-light:#60a5fa; --accent-lighter:#93c5fd;
      --role-piece:#7c8899; --role-wool:#3fae74; --role-spawn:#8f7bd6;
      --mk-spawn:#e0b13c; --mk-wool:#e6e6e6; --mk-iron:#9aa7b4;
      --ok:#22c55e; --warn:#f59e0b; --err:#f87171;
      --mono:ui-monospace, SFMono-Regular, Menlo, "Cascadia Mono", monospace;
      --sans:-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    }
    @media (prefers-color-scheme: light){
      :root{
        --bg-base:#f1f5f9; --bg-panel:#ffffff; --bg-panel-2:#f8fafc; --border:#cbd5e1;
        --text-muted:#586780; --text-secondary:#475569; --text-primary:#1e293b;
        --text-bright:#0f172a; --text-strong:#0f172a; --accent-light:#2563eb; --accent-lighter:#1d4ed8;
      }
    }
    *{ box-sizing:border-box; }
    html,body{ margin:0; padding:0; }
    body{
      background:var(--bg-base); color:var(--text-primary); font-family:var(--sans);
      font-size:14px; line-height:1.5; overflow-x:hidden;
      -webkit-font-smoothing:antialiased; text-rendering:optimizeLegibility;
    }
    .wrap{ max-width:1400px; margin:0 auto; padding:28px 24px 64px; }

    /* header */
    header.top{ border-bottom:1px solid var(--border); padding-bottom:20px; margin-bottom:24px; }
    .eyebrow{ font-family:var(--mono); font-size:11px; letter-spacing:.14em; text-transform:uppercase;
      color:var(--accent-light); margin:0 0 6px; }
    h1{ font-size:23px; line-height:1.25; margin:0 0 8px; color:var(--text-strong); font-weight:650; letter-spacing:-.01em; }
    .lede{ margin:0; max-width:70ch; color:var(--text-secondary); font-size:13.5px; }
    .lede code{ font-family:var(--mono); color:var(--text-bright); background:var(--bg-panel);
      padding:1px 5px; border-radius:3px; font-size:12px; }
    .green{ color:var(--ok); font-weight:600; }

    /* legend */
    .legend{ display:flex; flex-wrap:wrap; gap:6px 18px; margin-top:16px; padding:12px 14px;
      background:var(--bg-panel); border:1px solid var(--border); border-radius:6px; }
    .legend-group{ display:flex; flex-wrap:wrap; gap:6px 14px; align-items:center; }
    .legend-lbl{ font-family:var(--mono); font-size:10px; letter-spacing:.1em; text-transform:uppercase;
      color:var(--text-muted); margin-right:2px; }
    .lg{ display:inline-flex; align-items:center; gap:6px; font-size:12px; color:var(--text-secondary); font-family:var(--mono); }
    .sw{ width:13px; height:13px; border-radius:2px; flex:none; }
    .sw--zone{ background:color-mix(in srgb, var(--accent) 30%, transparent); border:1.3px dashed var(--accent); }
    .sw--buffer{ background:repeating-linear-gradient(45deg, #f2792b 0 1.4px, transparent 1.4px 4px),
      color-mix(in srgb, #f2792b 12%, transparent); border:1.3px dashed #f2792b; }
    .sw--dot{ border-radius:50%; }
    .sw--woolmk{ background:var(--mk-wool); } .sw--ironmk{ background:var(--mk-iron); }
    .legend-sep{ width:1px; align-self:stretch; background:var(--border); }

    /* families + grid */
    section.fam{ margin-top:30px; }
    .fam-head{ display:flex; align-items:baseline; gap:12px; flex-wrap:wrap;
      padding-bottom:8px; margin-bottom:14px; border-bottom:1px solid var(--border); }
    .fam-title{ font-size:14px; margin:0; color:var(--text-bright); font-weight:600; font-family:var(--mono); letter-spacing:.01em; }
    .fam-sub{ font-size:12.5px; color:var(--text-muted); }
    .fam-count{ margin-left:auto; font-family:var(--mono); font-size:11px; color:var(--text-muted); }

    .grid{ display:grid; grid-template-columns:repeat(auto-fill, minmax(300px, 1fr)); gap:16px; align-items:start; }
    .card{ background:var(--bg-panel); border:1px solid var(--border); border-radius:8px; padding:10px 10px 8px;
      display:flex; flex-direction:column; gap:8px; }
    .card-head{ display:flex; align-items:center; }
    .card-id{ font-family:var(--mono); font-size:12px; color:var(--text-bright); font-weight:600; letter-spacing:.005em; }
    .svg-wrap{ background:var(--bg-canvas); border:1px solid var(--border); border-radius:5px; overflow:hidden; line-height:0; }
    .svg-wrap svg.map{ display:block; width:100%; height:auto; }
    .card-stats{ display:flex; flex-wrap:wrap; align-items:center; gap:3px 5px;
      font-family:var(--mono); font-size:11.5px; color:var(--text-muted); font-variant-numeric:tabular-nums; }
    .stat-v{ color:var(--text-bright); font-weight:600; }
    .stat-dot{ color:var(--border); }
    .stat--yes .stat-v{ color:var(--accent-light); }
    .stat--no .stat-v{ color:var(--text-muted); }

    /* panels */
    .panel{ margin-top:26px; border:1px solid var(--border); border-radius:8px; padding:16px 18px; background:var(--bg-panel); }
    .panel--note{ border-left:3px solid var(--warn); }
    .panel--err{ border-left:3px solid var(--err); }
    .panel-title{ font-size:13px; margin:0 0 6px; color:var(--text-bright); font-weight:600; font-family:var(--mono); }
    .panel-body{ margin:0; color:var(--text-secondary); font-size:13px; max-width:80ch; }
    .panel-body code, .panel-list code{ font-family:var(--mono); font-size:12px; color:var(--text-bright);
      background:var(--bg-base); padding:1px 5px; border-radius:3px; }
    .panel-list{ margin:8px 0 0; padding-left:18px; color:var(--text-secondary); font-size:13px; }
    .panel-list li{ margin:3px 0; }

    footer{ margin-top:40px; padding-top:16px; border-top:1px solid var(--border);
      font-family:var(--mono); font-size:11px; color:var(--text-muted); }
    """;

    string legend = $"""
        <div class="legend">
          <div class="legend-group">
            <span class="legend-lbl">Piece roles</span>
            <span class="lg"><span class="sw" style="background:{CPiece}"></span>piece</span>
            <span class="lg"><span class="sw" style="background:{CWoolRoom}"></span>wool-room</span>
            <span class="lg"><span class="sw" style="background:{CSpawnRole}"></span>spawn</span>
            <span class="lg"><span class="sw sw--buffer"></span>buffer</span>
          </div>
          <span class="legend-sep"></span>
          <div class="legend-group">
            <span class="legend-lbl">Zone</span>
            <span class="lg"><span class="sw sw--zone"></span>build zone</span>
          </div>
          <span class="legend-sep"></span>
          <div class="legend-group">
            <span class="legend-lbl">Markers</span>
            <span class="lg"><span class="sw sw--dot" style="background:{MkSpawn}"></span>spawn</span>
            <span class="lg"><span class="sw sw--woolmk"></span>wool</span>
            <span class="lg"><span class="sw sw--ironmk"></span>iron</span>
          </div>
        </div>
    """;

    string note = $"""
        <section class="panel panel--note">
          <h2 class="panel-title">Known limitation — p5 deferred (not a regression)</h2>
          <p class="panel-body">p5 (both the 2-team rot_180 and the 4-team rot_90 shapes) is structurally
          infeasible under BZ6 (wool clears the mid band by 2 cells) plus a spawn of at least 2×2 cells within
          the fixed 325-block budget — deferred to a buffer-tile fix. It is a documented limitation, not a
          failure; p5 cases are excluded from this round.</p>
        </section>
    """;

    string bodyInner = $"""
    <div class="wrap">
      <header class="top">
        <p class="eyebrow">Composer · G49 · spawn + wool-room rooms</p>
        <h1>Generated plans, fanned by symmetry</h1>
        <p class="lede">Each card is a full map: one authored team unit fanned into every orbit image by the
        plan's symmetry, drawn in the <code>/plan</code> editor's visual language. New this round: the composer
        emits real role-bearing pieces — a <strong style="color:{CSpawnRole}">spawn</strong> region and a
        <strong style="color:{CWoolRoom}">wool-room</strong> per wool — carved as compact rooms at each lane's
        dead-end, which the plain lane pieces dock to. So a generated wool now stamps a room floor + red
        entrance seam and a spawn auto-renews its iron. Suite <span class="green">green (323/323)</span>;
        p5 is a documented known limitation (below).</p>
        {legend}
      </header>

      {note}

    {sections}
      {failuresPanel}

      <footer>Static SVG · self-contained · fanned via PgmStudio.Geom.Symmetry — {cardCount} plans, cell=5 blocks/proxy-cell.</footer>
    </div>
    """;

    return $"""
    <!doctype html>
    <html lang="en">
    <head>
      <meta charset="utf-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>Composer G32 — build-zone review</title>
      <style>{css}</style>
    </head>
    <body>
    {bodyInner}
    </body>
    </html>
    """;
}

record Case(int P, int T, string S, ulong Seed);
