#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// build-cache bust: terminal-free ShapeBody gallery — slots labelled per piece, body mirror closed (G91) r4 — atoms free to differ in size (uneven F)
using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

// ── slot palette: coloured by the G90 layer a piece belongs to (structural slot vs designation mark) ──
var slotColor = new Dictionary<string, string>
{
    // designation marks — what a designation stamps (warm)
    [ApproachSlots.Entry] = "#f2b134",       // docking mark
    [ApproachSlots.Room] = "#3fae74",        // terminal (never in a body; here for the legend)
    // structural slots — the rectangle's role in the compound (cool)
    [ApproachSlots.Run] = "#38bdf8",
    [ApproachSlots.Bar] = "#6366f1",
    [ApproachSlots.Leg] = "#2dd4bf",
    // composites — a structural slot carrying a mark (mid)
    [ApproachSlots.EntryRun] = "#fb923c",
    [ApproachSlots.RoomRun] = "#f472b6",
    [ApproachSlots.EntryBar] = "#a78bfa",
    [ApproachSlots.RoomBar] = "#f87171",
};
string SlotColor(string s) => slotColor.TryGetValue(s, out var c) ? c : "#94a3b8";

const string BgCanvas = "#080f1a";
const string AxisCol = "#a78bfa";
const string Ink = "#ffffff";
const string VoidStroke = "#5b6b86";

// ── the two catalogs ──────────────────────────────────────────────────────────────────────────────
var failures = new List<string>();

// (1) the eight approach families, terminal-free — ShapeEmitter.Body, the G90 stage. Their pieces still carry
//     the approach slots (entry / run / bar / leg / entry-run / room-run / …); the terminal room is withheld.
const int Cw = 3;
var familyCards = new List<(string Title, string Sub, string Svg)>();
(string, ShapeFamily, int, int, string)[] families =
{
    ("I", ShapeFamily.I, 6, 15, "a spine — one bar"),
    ("L", ShapeFamily.L, 15, 18, "spine + one arm at the end (one bend)"),
    ("Z", ShapeFamily.Z, 15, 21, "a spine with two opposing bends — a zig"),
    ("Scythe", ShapeFamily.Scythe, 18, 15, "a bar folding back over a bay — a hook"),
    ("U", ShapeFamily.U, 15, 18, "the staple: a crossbar + two legs"),
    ("H", ShapeFamily.H, 15, 21, "the staple with a room-run stub (the Y body)"),
    ("Donut", ShapeFamily.Donut, 18, 15, "four bars around a void + a hub stub — a ring"),
    ("Clamp", ShapeFamily.Clamp, 12, 15, "two legs, the wool clamped between them as a cut cell (§7)"),
};
foreach (var (name, fam, w, h, sub) in families)
{
    try
    {
        var body = ShapeEmitter.Body(fam, w, h, Cw);
        familyCards.Add(($"{name} · {fam}", sub, RenderBody(body, Cw)));
    }
    catch (Exception ex) { failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}"); }
}

// (2) the new compounds — BodyEmitter, the shapes G91 adds. Each is classified back through ClassifyBody and the
//     derived form is shown beside the requested one: requested == derived is the body mirror closing.
var compoundCards = new List<(string Title, string Sub, string Svg, string Requested, string Derived, bool Ok)>();
void AddCompound(string title, string sub, string requested, Func<ShapeBody> emit)
{
    try
    {
        var body = emit();
        var read = ShapeClassifier.ClassifyBody(CellsOf(body));
        var derived = read.Form == Compound.SpineArms ? $"SpineArms({read.Arms})" : read.Form.ToString();
        compoundCards.Add((title, sub, RenderBody(body, Cw), requested, derived, requested == derived));
    }
    catch (Exception ex) { failures.Add($"{title}: {ex.GetType().Name}: {ex.Message}"); }
}
AddCompound("Rectangle", "one rectangle — the base of the ladder (the solid hub)", "Rectangle", () => BodyEmitter.Rectangle(6 * Cw, 4 * Cw));
AddCompound("T · 1 arm", "the branch family at K=1 — an arm off the middle", "SpineArms(1)", () => BodyEmitter.SpineArms(Cw, 1));
AddCompound("Π · 2 arms (ends)", "K=2, arms at both ends — the generalized staple", "SpineArms(2)", () => BodyEmitter.SpineArms(Cw, [0, 4 * Cw], 5 * Cw));
AddCompound("F · 2 arms (end + mid)", "K=2, a different placement — the F glyph; still SpineArms(2)", "SpineArms(2)", () => BodyEmitter.SpineArms(Cw, [0, 2 * Cw], 5 * Cw));
AddCompound("E · 3 arms", "K=3 — the E/comb; three arms is the maximum supported", "SpineArms(3)", () => BodyEmitter.SpineArms(Cw, [0, 2 * Cw, 4 * Cw], 5 * Cw));
AddCompound("F · uneven atoms", "the atoms are free to differ — a fat bar, a long thin leg + a short fat leg; identity is width-independent, so still SpineArms(2)", "SpineArms(2)", () => BodyEmitter.SpineArms(spineLen: 7 * Cw, barThickness: 2 * Cw, arms: [(0, Cw, 6 * Cw), (3 * Cw, 2 * Cw, 3 * Cw)]));
AddCompound("Ring", "four bars around one void", "Ring", () => BodyEmitter.Ring(Cw, 5 * Cw, 5 * Cw));
AddCompound("Double-hole · equal", "a ring + a full-height U — two equal voids, a solid ring leg between", "DoubleHole", () => BodyEmitter.DoubleHole(Cw, 4 * Cw, 5 * Cw, uW: 3 * Cw, uH: 5 * Cw, uz: 0));
AddCompound("Double-hole · variant", "a shorter U slid down the edge — variant voids; both holes kept", "DoubleHole", () => BodyEmitter.DoubleHole(Cw, 4 * Cw, 7 * Cw, uW: 2 * Cw, uH: 3 * Cw, uz: 2 * Cw));
AddCompound("P", "a ring on a longer bar — one void; the loop slides along the overhang", "P", () => BodyEmitter.P(Cw, 4 * Cw, 5 * Cw));
AddCompound("Two-U-on-I", "two loops on a shared baseline — two voids, an open channel between", "TwoUOnI", () => BodyEmitter.TwoUOnI(Cw, 5 * Cw));

var html = Page(familyCards, compoundCards, failures);
var outPath = Path.Combine("tools", "compose", "out", "body-gallery.html");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, html);
Console.WriteLine($"wrote {outPath}");
Console.WriteLine($"family bodies: {familyCards.Count} · new compounds: {compoundCards.Count} · mirror-ok: {compoundCards.Count(c => c.Ok)}/{compoundCards.Count}");
Console.WriteLine($"failures: {failures.Count}");
foreach (var f in failures) Console.WriteLine($"  FAIL {f}");
Console.WriteLine($"contains http link: {(html.Contains("http://") || html.Contains("https://") ? "YES" : "no")}");

// ── geometry helpers ────────────────────────────────────────────────────────────────────────────
static HashSet<(int, int)> CellsOf(ShapeBody body)
{
    var cells = new HashSet<(int, int)>();
    foreach (var (r, _) in body.Pieces)
        for (var x = r[0]; x < r[0] + r[2]; x++)
            for (var z = r[1]; z < r[1] + r[3]; z++) cells.Add((x, z));
    return cells;
}

// one body, box-local, scaled to fit — pieces filled + labelled by slot, vacancies drawn as dashed voids
string RenderBody(ShapeBody body, int cw)
{
    var rects = body.Pieces.Select(p => p.Rect).Concat(body.Vacancies.Select(v => v.Rect)).ToList();
    int minX = rects.Min(r => r[0]), minZ = rects.Min(r => r[1]);
    int maxX = rects.Max(r => r[0] + r[2]), maxZ = rects.Max(r => r[1] + r[3]);
    // one-cell margin
    minX -= 1; minZ -= 1; maxX += 1; maxZ += 1;
    int cellsW = maxX - minX, cellsH = maxZ - minZ;
    const double TargetW = 240, TargetH = 240;
    double px = Math.Min(TargetW / cellsW, TargetH / cellsH);
    double vbw = cellsW * px, vbh = cellsH * px;
    double PX(double x) => (x - minX) * px;
    double PY(double z) => (z - minZ) * px;

    var svg = new StringBuilder();
    svg.Append($"<svg viewBox=\"0 0 {N(vbw)} {N(vbh)}\" xmlns=\"http://www.w3.org/2000/svg\" class=\"body\" role=\"img\">");
    svg.Append($"<rect x=\"0\" y=\"0\" width=\"{N(vbw)}\" height=\"{N(vbh)}\" fill=\"{BgCanvas}\"/>");

    // faint cell grid
    svg.Append("<g stroke-linecap=\"butt\">");
    for (int gx = minX; gx <= maxX; gx++)
        svg.Append($"<line x1=\"{N(PX(gx))}\" y1=\"{N(PY(minZ))}\" x2=\"{N(PX(gx))}\" y2=\"{N(PY(maxZ))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.12\" stroke-width=\"0.6\"/>");
    for (int gz = minZ; gz <= maxZ; gz++)
        svg.Append($"<line x1=\"{N(PX(minX))}\" y1=\"{N(PY(gz))}\" x2=\"{N(PX(maxX))}\" y2=\"{N(PY(gz))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.12\" stroke-width=\"0.6\"/>");
    svg.Append("</g>");

    // vacancies (enclosed voids) — dashed outline on the dark ground
    foreach (var v in body.Vacancies)
    {
        var r = v.Rect;
        svg.Append($"<rect x=\"{N(PX(r[0]))}\" y=\"{N(PY(r[1]))}\" width=\"{N(r[2] * px)}\" height=\"{N(r[3] * px)}\" " +
                   $"fill=\"none\" stroke=\"{VoidStroke}\" stroke-width=\"1.2\" stroke-dasharray=\"4 3\"/>");
        double vcx = (PX(r[0]) + PX(r[0] + r[2])) / 2, vcz = (PY(r[1]) + PY(r[1] + r[3])) / 2;
        svg.Append($"<text x=\"{N(vcx)}\" y=\"{N(vcz + 3.4)}\" font-size=\"9.5\" text-anchor=\"middle\" fill=\"{VoidStroke}\">{Esc(v.Kind)}</text>");
    }

    // pieces — filled by slot colour, slot name centred with a dark halo
    foreach (var (rect, slot) in body.Pieces)
    {
        var col = SlotColor(slot);
        svg.Append($"<rect x=\"{N(PX(rect[0]))}\" y=\"{N(PY(rect[1]))}\" width=\"{N(rect[2] * px)}\" height=\"{N(rect[3] * px)}\" " +
                   $"fill=\"{col}\" fill-opacity=\"0.72\" stroke=\"{col}\" stroke-width=\"1.6\"/>");
    }
    svg.Append($"<g font-weight=\"600\" text-anchor=\"middle\" paint-order=\"stroke\" stroke=\"{BgCanvas}\" stroke-width=\"3\" stroke-linejoin=\"round\">");
    foreach (var (rect, slot) in body.Pieces)
    {
        double cx = (PX(rect[0]) + PX(rect[0] + rect[2])) / 2, cz = (PY(rect[1]) + PY(rect[1] + rect[3])) / 2;
        svg.Append($"<text x=\"{N(cx)}\" y=\"{N(cz + 3.6)}\" font-size=\"10.5\" fill=\"{Ink}\">{Esc(slot)}</text>");
    }
    svg.Append("</g></svg>");
    return svg.ToString();
}

static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

string Page(
    List<(string Title, string Sub, string Svg)> fam,
    List<(string Title, string Sub, string Svg, string Requested, string Derived, bool Ok)> comp,
    List<string> fails)
{
    string SlotChip(string slot) =>
        $"<span class=\"chip\"><span class=\"chip-sw\" style=\"background:{SlotColor(slot)}\"></span>{Esc(slot)}</span>";

    var famCards = new StringBuilder();
    foreach (var (title, sub, svg) in fam)
        famCards.Append($"""
              <article class="card">
                <div class="card-head"><span class="card-id">{Esc(title)}</span></div>
                <div class="svg-wrap">{svg}</div>
                <p class="card-sub">{Esc(sub)}</p>
              </article>

        """);

    var compCards = new StringBuilder();
    foreach (var (title, sub, svg, req, der, ok) in comp)
    {
        var badge = ok
            ? $"<span class=\"mirror mirror--ok\" title=\"emit → derive → same\">{Esc(der)} ✓</span>"
            : $"<span class=\"mirror mirror--bad\">{Esc(req)} → {Esc(der)}</span>";
        compCards.Append($"""
              <article class="card">
                <div class="card-head"><span class="card-id">{Esc(title)}</span>{badge}</div>
                <div class="svg-wrap">{svg}</div>
                <p class="card-sub">{Esc(sub)}</p>
              </article>

        """);
    }

    string legend = $"""
        <div class="legend">
          <div class="lg-group"><span class="lg-lbl">Designation marks</span>{SlotChip(ApproachSlots.Entry)}{SlotChip(ApproachSlots.Room)}</div>
          <span class="lg-sep"></span>
          <div class="lg-group"><span class="lg-lbl">Structural slots</span>{SlotChip(ApproachSlots.Run)}{SlotChip(ApproachSlots.Bar)}{SlotChip(ApproachSlots.Leg)}</div>
          <span class="lg-sep"></span>
          <div class="lg-group"><span class="lg-lbl">Composite (slot + mark)</span>{SlotChip(ApproachSlots.EntryRun)}{SlotChip(ApproachSlots.RoomRun)}{SlotChip(ApproachSlots.EntryBar)}{SlotChip(ApproachSlots.RoomBar)}</div>
        </div>
    """;

    string failPanel = fails.Count == 0 ? "" : $"""
        <section class="panel panel--err">
          <h2 class="panel-title">Emit failures ({fails.Count})</h2>
          <ul class="panel-list">{string.Concat(fails.Select(f => $"<li><code>{Esc(f)}</code></li>"))}</ul>
        </section>
    """;

    int mirrorOk = comp.Count(c => c.Ok);

    const string css = """
    :root{
      --bg:#0d1524; --panel:#151f33; --panel-2:#101827; --canvas:#080f1a; --border:#2a3852;
      --muted:#8095b2; --secondary:#9fb2cc; --primary:#c6d4e6; --bright:#e6edf6; --strong:#ffffff;
      --accent:#6ea8ff; --ok:#34d399; --err:#f87171;
      --mono:ui-monospace,SFMono-Regular,Menlo,"Cascadia Mono",monospace;
      --sans:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;
    }
    @media (prefers-color-scheme:light){
      :root{ --bg:#eef2f8; --panel:#ffffff; --panel-2:#f6f9fd; --border:#d3dcea;
        --muted:#5a6b86; --secondary:#425068; --primary:#1f2b40; --bright:#111c30; --strong:#0b1220; --accent:#2563eb; }
    }
    :root[data-theme="dark"]{ --bg:#0d1524; --panel:#151f33; --panel-2:#101827; --border:#2a3852;
      --muted:#8095b2; --secondary:#9fb2cc; --primary:#c6d4e6; --bright:#e6edf6; --strong:#ffffff; --accent:#6ea8ff; }
    :root[data-theme="light"]{ --bg:#eef2f8; --panel:#ffffff; --panel-2:#f6f9fd; --border:#d3dcea;
      --muted:#5a6b86; --secondary:#425068; --primary:#1f2b40; --bright:#111c30; --strong:#0b1220; --accent:#2563eb; }
    *{ box-sizing:border-box; }
    body{ margin:0; background:var(--bg); color:var(--primary); font-family:var(--sans);
      font-size:14px; line-height:1.5; -webkit-font-smoothing:antialiased; }
    .wrap{ max-width:1360px; margin:0 auto; padding:30px 24px 72px; }
    header.top{ border-bottom:1px solid var(--border); padding-bottom:22px; margin-bottom:8px; }
    .eyebrow{ font-family:var(--mono); font-size:11px; letter-spacing:.16em; text-transform:uppercase; color:var(--accent); margin:0 0 8px; }
    h1{ font-size:25px; line-height:1.22; margin:0 0 10px; color:var(--strong); font-weight:660; letter-spacing:-.012em; text-wrap:balance; }
    .lede{ margin:0; max-width:74ch; color:var(--secondary); font-size:13.5px; }
    .lede code{ font-family:var(--mono); color:var(--bright); background:var(--panel); padding:1px 5px; border-radius:3px; font-size:12px; }
    .lede b{ color:var(--bright); font-weight:640; }

    .legend{ display:flex; flex-wrap:wrap; gap:10px 16px; align-items:center; margin-top:18px;
      padding:12px 14px; background:var(--panel); border:1px solid var(--border); border-radius:8px; }
    .lg-group{ display:flex; flex-wrap:wrap; gap:6px 8px; align-items:center; }
    .lg-lbl{ font-family:var(--mono); font-size:10px; letter-spacing:.1em; text-transform:uppercase; color:var(--muted); margin-right:4px; }
    .lg-sep{ width:1px; align-self:stretch; background:var(--border); }
    .chip{ display:inline-flex; align-items:center; gap:6px; font-family:var(--mono); font-size:11.5px; color:var(--secondary);
      background:var(--panel-2); border:1px solid var(--border); border-radius:999px; padding:3px 9px 3px 7px; }
    .chip-sw{ width:11px; height:11px; border-radius:3px; flex:none; }

    section.grp{ margin-top:34px; }
    .grp-head{ display:flex; align-items:baseline; gap:12px; flex-wrap:wrap; padding-bottom:9px; margin-bottom:16px; border-bottom:1px solid var(--border); }
    .grp-title{ font-size:14px; margin:0; color:var(--bright); font-weight:640; font-family:var(--mono); letter-spacing:.01em; }
    .grp-sub{ font-size:12.5px; color:var(--muted); }
    .grp-count{ margin-left:auto; font-family:var(--mono); font-size:11px; color:var(--muted); }

    .grid{ display:grid; grid-template-columns:repeat(auto-fill,minmax(232px,1fr)); gap:16px; align-items:start; }
    .card{ background:var(--panel); border:1px solid var(--border); border-radius:10px; padding:11px 11px 12px; display:flex; flex-direction:column; gap:9px; }
    .card-head{ display:flex; align-items:center; gap:8px; }
    .card-id{ font-family:var(--mono); font-size:12.5px; color:var(--bright); font-weight:640; }
    .mirror{ margin-left:auto; font-family:var(--mono); font-size:10px; letter-spacing:.04em; padding:2px 7px; border-radius:999px; font-weight:640; }
    .mirror--ok{ color:#062b1b; background:var(--ok); }
    .mirror--bad{ color:#fff; background:var(--err); }
    .svg-wrap{ background:var(--canvas); border:1px solid var(--border); border-radius:7px; overflow:hidden; line-height:0; aspect-ratio:1/1;
      display:flex; align-items:center; justify-content:center; }
    .svg-wrap svg.body{ display:block; width:100%; height:100%; }
    .svg-wrap svg.body text{ font-family:var(--mono); }
    .card-sub{ margin:0; font-size:11.5px; color:var(--muted); line-height:1.45; }

    .panel{ margin-top:28px; border:1px solid var(--border); border-radius:10px; padding:16px 18px; background:var(--panel); border-left:3px solid var(--err); }
    .panel-title{ font-size:13px; margin:0 0 6px; color:var(--bright); font-weight:640; font-family:var(--mono); }
    .panel-list{ margin:6px 0 0; padding-left:18px; color:var(--secondary); font-size:12.5px; }
    .panel-list code{ font-family:var(--mono); color:var(--bright); }

    footer{ margin-top:44px; padding-top:16px; border-top:1px solid var(--border); font-family:var(--mono); font-size:11px; color:var(--muted); }
    footer b{ color:var(--ok); }
    """;

    return $"""
    <title>The terminal-free shape bodies</title>
    <style>{css}</style>
    <div class="wrap">
      <header class="top">
        <p class="eyebrow">Map generation · the shape vocabulary · G90 · G91</p>
        <h1>The terminal-free bodies, piece by piece</h1>
        <p class="lede">A <b>body</b> is a pure rectilinear compound — rectangles recombined along shared edges,
        identified by topology alone (voids, arms, bends), with <b>no terminal</b>. A <em>designation</em> finishes
        it: an approach stamps a wool/spawn room, a hub its edge interfaces, a frontline its face. <b>G90</b> split
        <code>ShapeEmitter.Emit</code> into <code>Body</code> + that designation; every piece a body carries names
        its <b>slot</b> — the structural role (<code>bar</code>, <code>leg</code>, <code>run</code>) or, on the
        approach families, a designation mark (<code>entry</code>) and the composites that pair the two. Below:
        first the eight approach families with their terminal withheld (<code>ShapeEmitter.Body</code>), then the
        new compounds <b>G91</b> adds (<code>BodyEmitter</code>), each read back to itself by
        <code>ShapeClassifier.ClassifyBody</code> — the body mirror, <b>{mirrorOk}/{comp.Count} closed</b>. Every
        atom rectangle is <b>free to differ in size</b> (a fat bar, a long and a short leg) — identity is the
        topology (voids · arms · bends), never the sizes. Pieces are coloured by slot; dashed rectangles are
        enclosed voids.</p>
        {legend}
      </header>

      <section class="grp">
        <div class="grp-head">
          <h2 class="grp-title">Approach-family bodies</h2>
          <span class="grp-sub">the eight families with the terminal removed — the G90 Body of each; pieces keep their approach slots</span>
          <span class="grp-count">{fam.Count} bodies</span>
        </div>
        <div class="grid">
    {famCards}    </div>
      </section>

      <section class="grp">
        <div class="grp-head">
          <h2 class="grp-title">New compounds — standalone (G91)</h2>
          <span class="grp-sub">the shapes the vocabulary named but the emitter couldn't build; each classifies back to itself</span>
          <span class="grp-count">{comp.Count} shapes</span>
        </div>
        <div class="grid">
    {compCards}    </div>
      </section>

      {failPanel}
      <footer>Static self-contained SVG · {fam.Count + comp.Count} bodies · corridor width {Cw} cells · body mirror <b>{mirrorOk}/{comp.Count}</b>.</footer>
    </div>
    """;
}
