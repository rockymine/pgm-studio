#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// build-cache bust: wool-lane study v1
// Renders the hand-authored wool-lane TEMPLATES (tools/compose/wool-lane-study/*.plan.json) — each a
// `symmetry:"none"` freeform single unit (G46) using `connector` attachment points + `buffer` spacing (G47) —
// to one self-contained HTML gallery, lined up like an authored teaching plan. Draws in the /plan editor's
// visual language and overlays the derived land interfaces (green) and terrain↔wool-room entrance seams (red,
// ST1). Writes tools/compose/out/wool-lane-study.html.
using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

// ── palette (source of truth: plan-doc.js ROLE_COLORS / MARKER_COLORS, plan-canvas seam colours) ──
const string CPiece = "#7c8899";      // ROLE_COLORS.piece
const string CWoolRoom = "#3fae74";   // ROLE_COLORS["wool-room"]
const string CSpawnRole = "#8f7bd6";  // ROLE_COLORS.spawn
const string CBuffer = "#f2792b";     // ROLE_COLORS.buffer (reserved-gap annotation)
const string CConnector = "#2dd4bf";  // ROLE_COLORS.connector (attachment-point annotation)
const string MkWool = "#e6e6e6";      // MARKER_COLORS.wool
const string CZone = "#3b82f6";       // --accent (build zone)
const string CSeamLand = "#3fae74";   // land interface (green)
const string CSeamWool = "#ef4444";   // terrain↔wool-room entrance seam (red, ST1)
const string BgCanvas = "#080f1a";    // --bg-canvas (SVG ground; dark in both themes)
const string AxisCol = "#a78bfa";     // --canvas-axis
const string Ink = "#ffffff";         // --canvas-ink
const string MkStroke = "#222222";

// ── the study: each template + its authored intent (drawn from the seeds + layout-rules, NOT the composer) ──
var designs = new (string File, string Sub, string Note)[]
{
    ("01-single-access.plan.json", "one mouth · the weak baseline",
     "One <b>connector</b> mouth, a straight 2-wide lane, a wool-room at the back (WL1). The <b>buffer</b> cap reserves the dead space behind the room so nothing wraps past it. A lone chokepoint is weak — a single defender holds the mouth (G37); that is exactly why the designs below add routes."),
    ("02-dogleg.plan.json", "one mouth · L-turn hides the room",
     "An L-turn hides the room from the mouth-holder and lengthens the approach without a long straight (LN2). The <b>buffer</b> fills the crook void — a lane must not wrap a large empty square (G40). Still one attachment point, so still one chokepoint."),
    ("03-two-mouths.plan.json", "multi-access · two faces",
     "The multi-access answer: the room takes two land lanes on two faces, each with its own <b>connector</b>. An attacker can't be held at one mouth — the WL8 two-approaches device. Two red entrance seams stamp at export (ST1)."),
    ("04-build-zone-approach.plan.json", "multi-access · land + build route",
     "Two attachment <i>kinds</i>: a <b>connector</b> marks where the land lane feeds terrain; a build <b>zone</b> abutting the room is the second, build-it-yourself route. A zone may touch a wool-room for exactly this (BZ5, retired as a prohibition)."),
    ("05-parallel-spacing.plan.json", "spacing · reserved gap",
     "Two wool lanes side by side. The <b>buffer</b> strip between them reserves the lane-to-lane gap so their approaches and build zones don't merge — spacing you author explicitly rather than hope for. Each lane keeps its own connector mouth."),
    ("06-loop-two-entries.plan.json", "multi-access · U with a held void",
     "A U wraps two arms down to a shared base + room — two <b>connector</b> entries again. The <b>buffer</b> fills the enclosed square so the loop <i>reserves</i> that void as a hole rather than paving a giant dead-end (G40). Attackers pour in from both arms."),
};

int cards = 0;
var failures = new List<string>();
var sb = new StringBuilder();

foreach (var (file, sub, note) in designs)
{
    var path = Path.Combine("tools", "compose", "wool-lane-study", file);
    try
    {
        var plan = PlanModel.Parse(File.ReadAllText(path))!;
        var svg = BuildSvg(plan);
        var v = PlanValidator.Validate(plan);
        int err = v.Count(f => f.Severity == PlanSeverity.Error), lint = v.Count(f => f.Severity == PlanSeverity.Lint);
        var (_, intent) = PlanCompiler.Compile(plan);
        int conn = plan.Pieces.Count(p => p.Role == PlanRoles.Connector);
        int buf = plan.Pieces.Count(p => p.Role == PlanRoles.Buffer);
        int floors = intent.Structures?.RoomFloors.Count ?? 0;
        int seams = intent.Structures?.RedstoneLines.Count ?? 0;
        var stat = string.Join("<span class=\"dot\">·</span>",
            Stat(conn.ToString(), "connectors"), Stat(buf.ToString(), "buffers"),
            Stat(seams.ToString(), "entrance seams"), Stat($"{err}e/{lint}l", "validator"));
        sb.Append($$"""
              <article class="card">
                <div class="card-head"><span class="card-id">{{cards + 1:00}}</span><h2 class="card-title">{{Esc(plan.Meta?.Name ?? file)}}</h2><span class="card-sub">{{Esc(sub)}}</span></div>
                <div class="svg-wrap">{{svg}}</div>
                <p class="card-note">{{note}}</p>
                <div class="card-stats">{{stat}}</div>
              </article>

        """);
        cards++;
    }
    catch (Exception ex) { failures.Add($"{file}: {ex.GetType().Name}: {ex.Message}"); }
}

var html = Page(sb.ToString());
var outPath = Path.Combine("tools", "compose", "out", "wool-lane-study.html");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, html);
Console.WriteLine($"wrote {outPath}");
Console.WriteLine($"cards: {cards}   failures: {failures.Count}");
foreach (var f in failures) Console.WriteLine($"  FAIL {f}");

string Stat(string v, string l) => $"<span class=\"stat\"><span class=\"stat-v\">{v}</span> {l}</span>";

// ── SVG for one order-1 (none) plan: grid, zones, pieces (roles), interface seams, wool markers, ids ──
string BuildSvg(PlanModel plan)
{
    int cell = plan.Globals.Cell;
    var d = PlanDerived.Build(plan);

    double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
    void Bound(double a, double b, double c, double e)
    { minX = Math.Min(minX, a); minZ = Math.Min(minZ, b); maxX = Math.Max(maxX, c); maxZ = Math.Max(maxZ, e); }
    foreach (var p in plan.Pieces) Bound(p.Rect[0] * cell, p.Rect[1] * cell, (p.Rect[0] + p.Rect[2]) * cell, (p.Rect[1] + p.Rect[3]) * cell);
    foreach (var z in plan.Zones) Bound(z.Rect[0] * cell, z.Rect[1] * cell, (z.Rect[0] + z.Rect[2]) * cell, (z.Rect[1] + z.Rect[3]) * cell);
    if (minX > maxX) { minX = minZ = 0; maxX = maxZ = cell; }

    double mgn = 1.4 * cell;
    minX -= mgn; minZ -= mgn; maxX += mgn; maxZ += mgn;
    double bw = maxX - minX, bh = maxZ - minZ;
    const double TW = 300;
    double s = TW / bw, vbw = bw * s, vbh = bh * s;
    double PX(double bx) => (bx - minX) * s;
    double PY(double bz) => (bz - minZ) * s;

    double mkSq = cell * 0.5 * s;
    var svg = new StringBuilder();
    svg.Append($"<svg viewBox=\"0 0 {N(vbw)} {N(vbh)}\" xmlns=\"http://www.w3.org/2000/svg\" class=\"map\" role=\"img\">");
    // annotation hatch patterns (device px): buffer = single diagonal, connector = crossed
    svg.Append($"<defs><pattern id=\"buffer-hatch\" patternUnits=\"userSpaceOnUse\" width=\"6\" height=\"6\" patternTransform=\"rotate(45)\">" +
               $"<rect width=\"6\" height=\"6\" fill=\"{CBuffer}\" fill-opacity=\"0.12\"/><line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" stroke=\"{CBuffer}\" stroke-width=\"1.2\"/></pattern>" +
               $"<pattern id=\"connector-hatch\" patternUnits=\"userSpaceOnUse\" width=\"6\" height=\"6\" patternTransform=\"rotate(45)\">" +
               $"<rect width=\"6\" height=\"6\" fill=\"{CConnector}\" fill-opacity=\"0.14\"/><line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" stroke=\"{CConnector}\" stroke-width=\"1.2\"/><line x1=\"0\" y1=\"0\" x2=\"6\" y2=\"0\" stroke=\"{CConnector}\" stroke-width=\"1.2\"/></pattern></defs>");
    svg.Append($"<rect x=\"0\" y=\"0\" width=\"{N(vbw)}\" height=\"{N(vbh)}\" fill=\"{BgCanvas}\"/>");

    // faint cell grid
    int gx0 = (int)Math.Floor(minX / cell), gx1 = (int)Math.Ceiling(maxX / cell);
    int gz0 = (int)Math.Floor(minZ / cell), gz1 = (int)Math.Ceiling(maxZ / cell);
    for (int gc = gx0; gc <= gx1; gc++)
        svg.Append($"<line x1=\"{N(PX(gc * cell))}\" y1=\"{N(PY(gz0 * cell))}\" x2=\"{N(PX(gc * cell))}\" y2=\"{N(PY(gz1 * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.10\" stroke-width=\"0.6\"/>");
    for (int gc = gz0; gc <= gz1; gc++)
        svg.Append($"<line x1=\"{N(PX(gx0 * cell))}\" y1=\"{N(PY(gc * cell))}\" x2=\"{N(PX(gx1 * cell))}\" y2=\"{N(PY(gc * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.10\" stroke-width=\"0.6\"/>");

    // zones (under pieces) — translucent accent, dashed
    foreach (var z in plan.Zones)
        svg.Append($"<rect x=\"{N(PX(z.Rect[0] * cell))}\" y=\"{N(PY(z.Rect[1] * cell))}\" width=\"{N(z.Rect[2] * cell * s)}\" height=\"{N(z.Rect[3] * cell * s)}\" " +
                   $"fill=\"{CZone}\" fill-opacity=\"0.20\" stroke=\"{CZone}\" stroke-width=\"1.4\" stroke-dasharray=\"7 4\"/>");

    // pieces — generating roles solid; annotations hatched
    foreach (var p in plan.Pieces)
    {
        double x = PX(p.Rect[0] * cell), y = PY(p.Rect[1] * cell), w = p.Rect[2] * cell * s, h = p.Rect[3] * cell * s;
        if (p.Role == PlanRoles.Buffer || p.Role == PlanRoles.Connector)
        {
            var col = p.Role == PlanRoles.Buffer ? CBuffer : CConnector;
            var pat = p.Role == PlanRoles.Buffer ? "buffer-hatch" : "connector-hatch";
            svg.Append($"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" fill=\"url(#{pat})\" fill-opacity=\"0.9\" stroke=\"{col}\" stroke-opacity=\"0.85\" stroke-width=\"1.4\" stroke-dasharray=\"5 4\"/>");
            continue;
        }
        var pc = RoleColor(p.Role);
        svg.Append($"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" fill=\"{pc}\" fill-opacity=\"0.7\" stroke=\"{pc}\" stroke-width=\"1.5\"/>");
    }

    // interface seams from the derivation: land = green, terrain↔wool-room entrance = red (ST1), corner skipped
    foreach (var seg in d.InterfaceSegments)
    {
        if (seg.Kind == ContactKind.Corner) continue;
        var col = seg.WoolRoom ? CSeamWool : CSeamLand;
        svg.Append($"<line x1=\"{N(PX(seg.X1))}\" y1=\"{N(PY(seg.Z1))}\" x2=\"{N(PX(seg.X2))}\" y2=\"{N(PY(seg.Z2))}\" stroke=\"{col}\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-opacity=\"0.95\"/>");
    }

    // wool markers (white squares)
    foreach (var wl in plan.Placements.Wools)
    {
        var pc = plan.Pieces.FirstOrDefault(p => p.Id == wl.Piece);
        if (pc is null) continue;
        double bx = (pc.Rect[0] + wl.At[0]) * cell, bz = (pc.Rect[1] + wl.At[1]) * cell;
        svg.Append($"<rect x=\"{N(PX(bx) - mkSq / 2)}\" y=\"{N(PY(bz) - mkSq / 2)}\" width=\"{N(mkSq)}\" height=\"{N(mkSq)}\" rx=\"{N(cell * 0.08 * s)}\" fill=\"{MkWool}\" fill-opacity=\"0.9\" stroke=\"{MkStroke}\" stroke-width=\"1\"/>");
    }

    // id labels (piece + zone) centred
    svg.Append($"<g font-family=\"ui-monospace, SFMono-Regular, Menlo, monospace\" font-weight=\"600\" text-anchor=\"middle\" paint-order=\"stroke\" stroke=\"{BgCanvas}\" stroke-width=\"2.6\" stroke-linejoin=\"round\" font-size=\"10\">");
    foreach (var z in plan.Zones)
        svg.Append($"<text x=\"{N(PX((z.Rect[0] + z.Rect[2] / 2.0) * cell))}\" y=\"{N(PY(z.Rect[1] * cell) + 12)}\" fill=\"#93c5fd\">{Esc(z.Id)}</text>");
    foreach (var p in plan.Pieces)
        svg.Append($"<text x=\"{N(PX((p.Rect[0] + p.Rect[2] / 2.0) * cell))}\" y=\"{N(PY((p.Rect[1] + p.Rect[3] / 2.0) * cell) + 3.4)}\" fill=\"{(p.Role == PlanRoles.Buffer ? "#fdba74" : p.Role == PlanRoles.Connector ? "#99f6e4" : Ink)}\">{Esc(p.Id)}</text>");
    svg.Append("</g></svg>");
    return svg.ToString();

    string RoleColor(string role) => role switch { PlanRoles.WoolRoom => CWoolRoom, PlanRoles.Spawn => CSpawnRole, _ => CPiece };
}

static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

string Page(string cardsHtml)
{
    const string css = """
    :root{
      --bg-base:#0f172a; --bg-panel:#1e293b; --bg-canvas:#080f1a; --border:#334155;
      --text-muted:#8397b0; --text-secondary:#94a3b8; --text-primary:#cbd5e1;
      --text-bright:#e2e8f0; --text-strong:#ffffff; --accent-light:#60a5fa; --ok:#22c55e;
      --mono:ui-monospace, SFMono-Regular, Menlo, "Cascadia Mono", monospace;
      --sans:-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    }
    @media (prefers-color-scheme: light){
      :root{ --bg-base:#f1f5f9; --bg-panel:#ffffff; --border:#cbd5e1; --text-muted:#586780;
        --text-secondary:#475569; --text-primary:#1e293b; --text-bright:#0f172a; --text-strong:#0f172a; --accent-light:#2563eb; }
    }
    :root[data-theme="dark"]{ --bg-base:#0f172a; --bg-panel:#1e293b; --border:#334155; --text-muted:#8397b0;
      --text-secondary:#94a3b8; --text-primary:#cbd5e1; --text-bright:#e2e8f0; --text-strong:#ffffff; --accent-light:#60a5fa; }
    :root[data-theme="light"]{ --bg-base:#f1f5f9; --bg-panel:#ffffff; --border:#cbd5e1; --text-muted:#586780;
      --text-secondary:#475569; --text-primary:#1e293b; --text-bright:#0f172a; --text-strong:#0f172a; --accent-light:#2563eb; }
    *{ box-sizing:border-box; }
    html,body{ margin:0; padding:0; }
    body{ background:var(--bg-base); color:var(--text-primary); font-family:var(--sans); font-size:14px; line-height:1.5;
      overflow-x:hidden; -webkit-font-smoothing:antialiased; }
    .wrap{ max-width:1320px; margin:0 auto; padding:30px 24px 64px; }
    header.top{ border-bottom:1px solid var(--border); padding-bottom:20px; margin-bottom:6px; }
    .eyebrow{ font-family:var(--mono); font-size:11px; letter-spacing:.14em; text-transform:uppercase; color:var(--accent-light); margin:0 0 6px; }
    h1{ font-size:24px; line-height:1.2; margin:0 0 10px; color:var(--text-strong); font-weight:660; letter-spacing:-.01em; text-wrap:balance; }
    .lede{ margin:0; max-width:78ch; color:var(--text-secondary); font-size:13.5px; }
    .lede code, .lede b{ font-family:var(--mono); font-size:12px; }
    .lede code{ color:var(--text-bright); background:var(--bg-panel); padding:1px 5px; border-radius:3px; }
    .lede b{ color:var(--text-bright); font-weight:600; }
    .green{ color:var(--ok); font-weight:600; }
    .legend{ display:flex; flex-wrap:wrap; gap:7px 16px; margin-top:16px; padding:12px 14px; background:var(--bg-panel);
      border:1px solid var(--border); border-radius:6px; align-items:center; }
    .legend-lbl{ font-family:var(--mono); font-size:10px; letter-spacing:.1em; text-transform:uppercase; color:var(--text-muted); }
    .lg{ display:inline-flex; align-items:center; gap:6px; font-size:12px; color:var(--text-secondary); font-family:var(--mono); }
    .sw{ width:13px; height:13px; border-radius:2px; flex:none; }
    .sw--seam{ width:15px; height:0; border-radius:0; border-top:3px solid; }
    .legend-sep{ width:1px; align-self:stretch; background:var(--border); }
    .grid{ display:grid; grid-template-columns:repeat(auto-fill, minmax(340px, 1fr)); gap:18px; align-items:start; margin-top:24px; }
    .card{ background:var(--bg-panel); border:1px solid var(--border); border-radius:10px; padding:14px 14px 12px;
      display:flex; flex-direction:column; gap:10px; }
    .card-head{ display:flex; align-items:baseline; flex-wrap:wrap; gap:4px 9px; }
    .card-id{ font-family:var(--mono); font-size:12px; color:var(--accent-light); font-weight:700; }
    .card-title{ font-size:15px; margin:0; color:var(--text-strong); font-weight:640; letter-spacing:-.005em; }
    .card-sub{ font-family:var(--mono); font-size:11px; color:var(--text-muted); }
    .svg-wrap{ background:var(--bg-canvas); border:1px solid var(--border); border-radius:6px; overflow:hidden; line-height:0; padding:4px; }
    .svg-wrap svg.map{ display:block; width:100%; height:auto; }
    .card-note{ margin:0; font-size:12.5px; color:var(--text-secondary); line-height:1.5; }
    .card-note b{ font-family:var(--mono); font-size:11.5px; color:var(--text-bright); font-weight:600; }
    .card-stats{ display:flex; flex-wrap:wrap; align-items:center; gap:3px 6px; font-family:var(--mono); font-size:11px;
      color:var(--text-muted); font-variant-numeric:tabular-nums; border-top:1px solid var(--border); padding-top:9px; }
    .stat-v{ color:var(--text-bright); font-weight:600; }
    .dot{ color:var(--border); }
    footer{ margin-top:40px; padding-top:16px; border-top:1px solid var(--border); font-family:var(--mono); font-size:11px; color:var(--text-muted); }
    """;

    string legend = $"""
        <div class="legend">
          <span class="legend-lbl">Pieces</span>
          <span class="lg"><span class="sw" style="background:{CPiece}"></span>lane</span>
          <span class="lg"><span class="sw" style="background:{CWoolRoom}"></span>wool-room</span>
          <span class="legend-sep"></span>
          <span class="legend-lbl">Technical</span>
          <span class="lg"><span class="sw" style="background:repeating-linear-gradient(45deg,{CConnector} 0 1.4px,transparent 1.4px 4px),{CConnector}22;border:1px dashed {CConnector}"></span>connector</span>
          <span class="lg"><span class="sw" style="background:repeating-linear-gradient(45deg,{CBuffer} 0 1.4px,transparent 1.4px 4px),{CBuffer}22;border:1px dashed {CBuffer}"></span>buffer</span>
          <span class="legend-sep"></span>
          <span class="legend-lbl">Build</span>
          <span class="lg"><span class="sw" style="background:{CZone}33;border:1.3px dashed {CZone}"></span>zone</span>
          <span class="legend-sep"></span>
          <span class="legend-lbl">Seams</span>
          <span class="lg"><span class="sw sw--seam" style="border-color:{CSeamLand}"></span>land</span>
          <span class="lg"><span class="sw sw--seam" style="border-color:{CSeamWool}"></span>wool entrance</span>
          <span class="lg"><span class="sw" style="background:{MkWool}"></span>wool</span>
        </div>
    """;

    string body = $"""
    <div class="wrap">
      <header class="top">
        <p class="eyebrow">Plan authoring · G46–G48 · wool-lane study</p>
        <h1>Authoring wool lanes with the new plan tools</h1>
        <p class="lede">Six wool-lane <strong>templates</strong>, each a <code>symmetry:"none"</code> freeform single
        unit (<b>G46</b>) — no mirror fanning fighting the shape — using the <b>connector</b> attachment mark and
        <b>buffer</b> spacing mark (<b>G47</b>), which the resorted palette groups as <em>technical</em> pieces
        (<b>G48</b>). They are hand-designed from the seed corpus and <code>layout-rules.md</code> — <em>not</em> the
        composer's current budget-stretched long lanes. Read across for the three things a real wool lane needs: an
        <strong style="color:{CConnector}">attachment point</strong> where the lane feeds terrain or a build zone,
        <strong style="color:{CBuffer}">buffers</strong> where spacing/voids must be reserved, and
        <strong>multi-access</strong> so a defender can't just hold the mouth. Every template validates
        <span class="green">0 errors / 0 lint</span> and compiles to real wool-room floors + red entrance seams.</p>
        {legend}
      </header>

      <div class="grid">
    {cardsHtml}  </div>

      <footer>Static SVG · self-contained · order-1 (none) plans, cell = 5 blocks · seams from PlanDerived · authored by hand, validated by PlanValidator.</footer>
    </div>
    """;

    return $"""
    <!doctype html>
    <html lang="en">
    <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Wool-lane study — G46–G48</title><style>{css}</style></head>
    <body>{body}</body></html>
    """;
}
