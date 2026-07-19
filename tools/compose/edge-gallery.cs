#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// edge-taxonomy gallery: every shape's boundary edges colour-coded by the negative space they face
// (free / notch / bay / hole), the spaces tinted — the visual ground for offer/attachment rules.
using System.Globalization;
using System.Text;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

// ── edge/space palette: the wall-count escalation, cold → hot ────────────────────────────────────
var kindColor = new Dictionary<NegativeSpaceKind, string>
{
    [NegativeSpaceKind.Open] = "#34d399",    // free outward surface — the offer/dock candidates
    [NegativeSpaceKind.Notch] = "#fbbf24",   // 2 walls — a wrapped corner
    [NegativeSpaceKind.Bay] = "#fb923c",     // 3 walls — a recess, open one way
    [NegativeSpaceKind.Hole] = "#f87171",    // enclosed — the ring void
};
const string TerminalCol = "#ec4899";        // the terminal room's own wall — never attach (the gate's veto)
const string PieceFill = "#3a4a66";
const string RoomFill = "#3fae74";
const string BgCanvas = "#080f1a";
const string GridCol = "#a78bfa";

const int Cw = 3;
var failures = new List<string>();
var cards = new List<(string Group, string Title, string Sub, string Svg, string Counts)>();

void Add(string group, string title, string sub, Func<(IReadOnlyList<(int[] Rect, bool Room)> Pieces, EdgeClassification Read)> build)
{
    try
    {
        var (pieces, read) = build();
        cards.Add((group, title, sub, Render(pieces, read), CountChips(read)));
    }
    catch (Exception ex) { failures.Add($"{title}: {ex.GetType().Name}: {ex.Message}"); }
}

(IReadOnlyList<(int[], bool)>, EdgeClassification) Emission(ShapeFamily fam, int w, int h)
{
    var e = ShapeEmitter.Emit(fam, w, h, Cw);
    var pieces = e.Terrain.Select(p => (p.Rect, false)).Append((e.Room, true)).ToList();
    return (pieces, BodyEdges.Classify(e, BodyEdges.DefaultClearanceCells));
}

(IReadOnlyList<(int[], bool)>, EdgeClassification) Body(ShapeBody b) =>
    (b.Pieces.Select(p => (p.Rect, false)).ToList(), BodyEdges.Classify(b));

// (1) the approach families as full emissions — the room takes part in the walls it forms
Add("approach", "I", "no negative space; the lane's sides split where the sealed room caps the end", () => Emission(ShapeFamily.I, 9, 15));
Add("approach", "L", "one notch; the band's end line splits into free terrain and the sealed room", () => Emission(ShapeFamily.L, 15, 18));
Add("approach", "Z", "two opposing bends — a notch each", () => Emission(ShapeFamily.Z, 15, 21));
Add("approach", "Scythe", "the fold's bay + the notch behind the entry tail", () => Emission(ShapeFamily.Scythe, 18, 15));
Add("approach", "Clamp", "two legs down to the mouth; the wool clamped between them, its bottom wall facing the bay", () => Emission(ShapeFamily.Clamp, 12, 15));
Add("approach", "U", "the bay between the legs; the room wraps a notch each side", () => Emission(ShapeFamily.U, 15, 18));
Add("approach", "H / Y", "the U's bay + the notches beside the room stub", () => Emission(ShapeFamily.H, 15, 21));
Add("approach", "Donut", "the enclosed hole; stub and room wrap notches", () => Emission(ShapeFamily.Donut, 18, 15));

// (2) the terminal-free compounds — the hub/frontline form menu candidates
Add("compound", "Rectangle", "the solid hub today: four free edges are its entire attachment rule", () => Body(BodyEmitter.Rectangle(6 * Cw, 4 * Cw)));
Add("compound", "T · 1 arm", "one middle arm: two notches", () => Body(BodyEmitter.SpineArms(Cw, 1)));
Add("compound", "Π · 2 arms (ends)", "arms at the ends: one bay between the legs", () => Body(BodyEmitter.SpineArms(Cw, [0, 4 * Cw], 5 * Cw)));
Add("compound", "F · 2 arms (end + mid)", "same K, different placement: a bay AND a notch — placement decides the negative spaces", () => Body(BodyEmitter.SpineArms(Cw, [0, 2 * Cw], 5 * Cw)));
Add("compound", "E · 3 arms", "three arms: two bays", () => Body(BodyEmitter.SpineArms(Cw, [0, 2 * Cw, 4 * Cw], 5 * Cw)));
Add("compound", "F · uneven atoms", "the six-edge bay is itself a U: its slab parts split into the mouth bar (notch-grade — the short arm's tip is reachable through it), a bay leg and a notch leg", () => Body(BodyEmitter.SpineArms(spineLen: 7 * Cw, barThickness: 2 * Cw, arms: [(0, Cw, 6 * Cw), (3 * Cw, 2 * Cw, 3 * Cw)])));
Add("compound", "Ring", "one enclosed hole, all outer edges free", () => Body(BodyEmitter.Ring(Cw, 5 * Cw, 5 * Cw)));
Add("compound", "Double-hole · equal", "a ring + a full-height U: two holes", () => Body(BodyEmitter.DoubleHole(Cw, 4 * Cw, 5 * Cw, uW: 3 * Cw, uH: 5 * Cw, uz: 0)));
Add("compound", "Double-hole · variant", "a shorter U slid down the edge: two holes + the notches the slide opens", () => Body(BodyEmitter.DoubleHole(Cw, 4 * Cw, 7 * Cw, uW: 2 * Cw, uH: 3 * Cw, uz: 2 * Cw)));
Add("compound", "P", "the loop's hole + the notches where the bar overhangs", () => Body(BodyEmitter.P(Cw, 4 * Cw, 5 * Cw)));
Add("compound", "Two-U-on-I", "two holes with an open channel between — the channel is a bay", () => Body(BodyEmitter.TwoUOnI(Cw, 5 * Cw)));

var html = Page(cards, failures);
var outPath = Path.Combine("tools", "compose", "out", "edge-gallery.html");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, html);
Console.WriteLine($"wrote {outPath}");
Console.WriteLine($"cards: {cards.Count} · failures: {failures.Count}");
foreach (var f in failures) Console.WriteLine($"  FAIL {f}");

string CountChips(EdgeClassification read)
{
    var sb = new StringBuilder();
    foreach (var kind in new[] { NegativeSpaceKind.Notch, NegativeSpaceKind.Bay, NegativeSpaceKind.Hole })
    {
        var n = read.Spaces.Count(s => s.Kind == kind);
        if (n > 0) sb.Append($"<span class=\"cnt\" style=\"color:{kindColor[kind]}\">{n} {kind.ToString().ToLowerInvariant()}{(n > 1 ? (kind == NegativeSpaceKind.Notch ? "es" : "s") : "")}</span>");
    }
    var free = read.Edges.Count(e => e.Faces == NegativeSpaceKind.Open && !e.Terminal && !e.Guarded);
    sb.Append($"<span class=\"cnt\" style=\"color:{kindColor[NegativeSpaceKind.Open]}\">{free} free edge{(free == 1 ? "" : "s")}</span>");
    var sealedRuns = read.Edges.Count(e => e.Terminal);
    if (sealedRuns > 0)
        sb.Append($"<span class=\"cnt\" style=\"color:{TerminalCol}\">{sealedRuns} sealed</span>");
    var guardedRuns = read.Edges.Count(e => e.Guarded && !e.Terminal);
    if (guardedRuns > 0)
        sb.Append($"<span class=\"cnt\" style=\"color:{TerminalCol}\">{guardedRuns} guarded</span>");
    return sb.ToString();
}

// one shape, box-local, scaled to fit: pieces as muted fills, negative spaces tinted by class, and the
// boundary edges stroked in their class colour — the read itself is the picture
string Render(IReadOnlyList<(int[] Rect, bool Room)> pieces, EdgeClassification read)
{
    var rects = pieces.Select(p => p.Rect).ToList();
    int minX = rects.Min(r => r[0]) - 1, minZ = rects.Min(r => r[1]) - 1;
    int maxX = rects.Max(r => r[0] + r[2]) + 1, maxZ = rects.Max(r => r[1] + r[3]) + 1;
    int cellsW = maxX - minX, cellsH = maxZ - minZ;
    const double TargetW = 240, TargetH = 240;
    double px = Math.Min(TargetW / cellsW, TargetH / cellsH);
    double vbw = cellsW * px, vbh = cellsH * px;
    double PX(double x) => (x - minX) * px;
    double PY(double z) => (z - minZ) * px;

    var svg = new StringBuilder();
    svg.Append($"<svg viewBox=\"0 0 {N(vbw)} {N(vbh)}\" xmlns=\"http://www.w3.org/2000/svg\" class=\"body\" role=\"img\">");
    svg.Append($"<rect x=\"0\" y=\"0\" width=\"{N(vbw)}\" height=\"{N(vbh)}\" fill=\"{BgCanvas}\"/>");

    svg.Append("<g stroke-linecap=\"butt\">");
    for (int gx = minX; gx <= maxX; gx++)
        svg.Append($"<line x1=\"{N(PX(gx))}\" y1=\"{N(PY(minZ))}\" x2=\"{N(PX(gx))}\" y2=\"{N(PY(maxZ))}\" stroke=\"{GridCol}\" stroke-opacity=\"0.10\" stroke-width=\"0.6\"/>");
    for (int gz = minZ; gz <= maxZ; gz++)
        svg.Append($"<line x1=\"{N(PX(minX))}\" y1=\"{N(PY(gz))}\" x2=\"{N(PX(maxX))}\" y2=\"{N(PY(gz))}\" stroke=\"{GridCol}\" stroke-opacity=\"0.10\" stroke-width=\"0.6\"/>");
    svg.Append("</g>");

    // negative spaces: a rectangular space tints as one region; a decomposed space tints PER PART, each in
    // its own class colour with a dashed outline — the layer that lets a rule reach an inset feature. The
    // publish policy's verdict rides the labels: ✓ offered onward, ✗ vetoed wholesale.
    var capped = read.Edges.Any(e => e.Terminal);
    foreach (var s in read.Spaces)
    {
        if (s.Kind == NegativeSpaceKind.Open) continue;
        var pub = PublishPolicy.PublishableParts(s, capped);
        var vetoed = PublishPolicy.Space(s, capped) == PublishVerdict.Veto;
        if (s.Parts.Count > 1)
        {
            foreach (var p in s.Parts)
            {
                var pc = p.Guarded ? TerminalCol : kindColor[p.Kind];
                var label = (p.Guarded ? "guard" : p.Kind.ToString().ToLowerInvariant())
                    + (pub.Contains(p) ? " ✓" : vetoed ? " ✗" : "");
                svg.Append($"<rect x=\"{N(PX(p.Rect[0]))}\" y=\"{N(PY(p.Rect[1]))}\" width=\"{N(p.Rect[2] * px)}\" height=\"{N(p.Rect[3] * px)}\" " +
                           $"fill=\"{pc}\" fill-opacity=\"0.16\" stroke=\"{pc}\" stroke-opacity=\"0.55\" stroke-width=\"0.9\" stroke-dasharray=\"3 3\"/>");
                double pcx = PX(p.Rect[0] + p.Rect[2] / 2.0), pcz = PY(p.Rect[1] + p.Rect[3] / 2.0);
                svg.Append($"<text x=\"{N(pcx)}\" y=\"{N(pcz + 3.4)}\" font-size=\"9\" text-anchor=\"middle\" fill=\"{pc}\" fill-opacity=\"0.9\">{label}</text>");
            }
            // the space's own compound identity — the void read as a body (a decomposed bay is a Π/U, not noise)
            if (s.Form is { } form && form.Form != Compound.Rectangle)
            {
                var fx = (s.Cells.Min(c => c.X) + s.Cells.Max(c => c.X) + 1) / 2.0;
                var fz = s.Cells.Min(c => c.Z);
                var name = form.Form == Compound.SpineArms ? $"spine+{form.Arms}" : form.Form.ToString().ToLowerInvariant();
                svg.Append($"<text x=\"{N(PX(fx))}\" y=\"{N(PY(fz) + 9)}\" font-size=\"8.5\" text-anchor=\"middle\" fill=\"#94a3b8\">≡ {name}</text>");
            }
            continue;
        }
        var col = kindColor[s.Kind];
        foreach (var (cx, cz) in s.Cells)
            svg.Append($"<rect x=\"{N(PX(cx))}\" y=\"{N(PY(cz))}\" width=\"{N(px)}\" height=\"{N(px)}\" fill=\"{col}\" fill-opacity=\"0.16\"/>");
        var lx = (s.Cells.Min(c => c.X) + s.Cells.Max(c => c.X) + 1) / 2.0;
        var lz = (s.Cells.Min(c => c.Z) + s.Cells.Max(c => c.Z) + 1) / 2.0;
        var mark = pub.Count > 0 ? " ✓" : vetoed ? " ✗" : "";
        svg.Append($"<text x=\"{N(PX(lx))}\" y=\"{N(PY(lz) + 3.4)}\" font-size=\"9.5\" text-anchor=\"middle\" fill=\"{col}\" fill-opacity=\"0.9\">{s.Kind.ToString().ToLowerInvariant()}{mark}</text>");
    }

    // bay mouths — a dotted bracket across each opening, labelled with its width class (the wN the mouth
    // tapers to: what may dock THROUGH the opening)
    foreach (var s in read.Spaces)
    {
        if (s.Kind != NegativeSpaceKind.Bay) continue;
        int minCz = s.Cells.Min(c => c.Z), maxCz = s.Cells.Max(c => c.Z);
        int minCx = s.Cells.Min(c => c.X), maxCx = s.Cells.Max(c => c.X);
        var col = kindColor[NegativeSpaceKind.Bay];
        foreach (var m in s.Mouths)
        {
            var (x1, y1, x2, y2) = m.Side switch
            {
                BoxEdge.Top => (PX(m.Start), PY(minCz), PX(m.Start + m.WidthCells), PY(minCz)),
                BoxEdge.Bottom => (PX(m.Start), PY(maxCz + 1), PX(m.Start + m.WidthCells), PY(maxCz + 1)),
                BoxEdge.Left => (PX(minCx), PY(m.Start), PX(minCx), PY(m.Start + m.WidthCells)),
                _ => (PX(maxCx + 1), PY(m.Start), PX(maxCx + 1), PY(m.Start + m.WidthCells)),
            };
            svg.Append($"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" " +
                       $"stroke=\"{col}\" stroke-width=\"1.6\" stroke-dasharray=\"2 2\"/>");
            svg.Append($"<text x=\"{N((x1 + x2) / 2)}\" y=\"{N((y1 + y2) / 2 + (m.Side is BoxEdge.Top ? -3 : m.Side is BoxEdge.Bottom ? 9 : 3))}\" " +
                       $"font-size=\"8.5\" text-anchor=\"middle\" fill=\"{col}\">w{m.WidthClass}</text>");
        }
    }

    // pieces — muted fill, the terminal room tinted its own colour
    foreach (var (rect, room) in pieces)
    {
        var col = room ? RoomFill : PieceFill;
        svg.Append($"<rect x=\"{N(PX(rect[0]))}\" y=\"{N(PY(rect[1]))}\" width=\"{N(rect[2] * px)}\" height=\"{N(rect[3] * px)}\" " +
                   $"fill=\"{col}\" fill-opacity=\"{(room ? "0.55" : "0.45")}\"/>");
    }

    // the classified boundary — thick strokes in the class colour; a terminal-owned run overrides with the
    // sealed colour whatever it faces (the never-attach wall), and a guarded terrain run (inside the room's
    // clearance margin) draws the same colour dashed — sealed by rule, not by ownership
    foreach (var e in read.Edges)
    {
        var col = e.Terminal || e.Guarded ? TerminalCol : kindColor[e.Faces];
        var dash = !e.Terminal && e.Guarded ? " stroke-dasharray=\"5 3\"" : "";
        svg.Append($"<line x1=\"{N(PX(e.X1))}\" y1=\"{N(PY(e.Z1))}\" x2=\"{N(PX(e.X2))}\" y2=\"{N(PY(e.Z2))}\" " +
                   $"stroke=\"{col}\" stroke-width=\"2.4\" stroke-linecap=\"square\"{dash}/>");
    }
    svg.Append("</svg>");
    return svg.ToString();
}

static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

string Page(List<(string Group, string Title, string Sub, string Svg, string Counts)> all, List<string> fails)
{
    string Chip(NegativeSpaceKind k, string label) =>
        $"<span class=\"chip\"><span class=\"chip-sw\" style=\"background:{kindColor[k]}\"></span>{Esc(label)}</span>";

    var groups = new (string Key, string Title, string Sub)[]
    {
        ("approach", "Approach emissions", "the eight families with their terminal — the room takes part in the walls it forms"),
        ("compound", "Terminal-free compounds", "the hub/frontline form-menu candidates — the solid rectangle is today's hub"),
    };
    var sections = new StringBuilder();
    foreach (var (key, title, sub) in groups)
    {
        var cs = all.Where(c => c.Group == key).ToList();
        var body = new StringBuilder();
        foreach (var (_, t, s, svg, counts) in cs)
            body.Append($"""
                  <article class="card">
                    <div class="card-head"><span class="card-id">{Esc(t)}</span><span class="counts">{counts}</span></div>
                    <div class="svg-wrap">{svg}</div>
                    <p class="card-sub">{Esc(s)}</p>
                  </article>

            """);
        sections.Append($"""
              <section class="grp">
                <div class="grp-head">
                  <h2 class="grp-title">{Esc(title)}</h2>
                  <span class="grp-sub">{Esc(sub)}</span>
                  <span class="grp-count">{cs.Count} shapes</span>
                </div>
                <div class="grid">
            {body}    </div>
              </section>

            """);
    }

    string failPanel = fails.Count == 0 ? "" : $"""
        <section class="panel panel--err">
          <h2 class="panel-title">Failures ({fails.Count})</h2>
          <ul class="panel-list">{string.Concat(fails.Select(f => $"<li><code>{Esc(f)}</code></li>"))}</ul>
        </section>
    """;

    const string css = """
    :root{
      --bg:#0d1524; --panel:#151f33; --panel-2:#101827; --canvas:#080f1a; --border:#2a3852;
      --muted:#8095b2; --secondary:#9fb2cc; --primary:#c6d4e6; --bright:#e6edf6; --strong:#ffffff;
      --accent:#6ea8ff; --err:#f87171;
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
    h1{ font-size:25px; line-height:1.22; margin:0 0 10px; color:var(--strong); font-weight:660; letter-spacing:-.012em; }
    .lede{ margin:0; max-width:76ch; color:var(--secondary); font-size:13.5px; }
    .lede code{ font-family:var(--mono); color:var(--bright); background:var(--panel); padding:1px 5px; border-radius:3px; font-size:12px; }
    .lede b{ color:var(--bright); font-weight:640; }
    .legend{ display:flex; flex-wrap:wrap; gap:10px 16px; align-items:center; margin-top:18px;
      padding:12px 14px; background:var(--panel); border:1px solid var(--border); border-radius:8px; }
    .chip{ display:inline-flex; align-items:center; gap:6px; font-family:var(--mono); font-size:11.5px; color:var(--secondary);
      background:var(--panel-2); border:1px solid var(--border); border-radius:999px; padding:3px 9px 3px 7px; }
    .chip-sw{ width:11px; height:11px; border-radius:3px; flex:none; }
    section.grp{ margin-top:34px; }
    .grp-head{ display:flex; align-items:baseline; gap:12px; flex-wrap:wrap; padding-bottom:9px; margin-bottom:16px; border-bottom:1px solid var(--border); }
    .grp-title{ font-size:14px; margin:0; color:var(--bright); font-weight:640; font-family:var(--mono); }
    .grp-sub{ font-size:12.5px; color:var(--muted); }
    .grp-count{ margin-left:auto; font-family:var(--mono); font-size:11px; color:var(--muted); }
    .grid{ display:grid; grid-template-columns:repeat(auto-fill,minmax(232px,1fr)); gap:16px; align-items:start; }
    .card{ background:var(--panel); border:1px solid var(--border); border-radius:10px; padding:11px 11px 12px; display:flex; flex-direction:column; gap:9px; }
    .card-head{ display:flex; align-items:center; gap:8px; flex-wrap:wrap; }
    .card-id{ font-family:var(--mono); font-size:12.5px; color:var(--bright); font-weight:640; }
    .counts{ margin-left:auto; display:flex; gap:7px; font-family:var(--mono); font-size:10px; }
    .cnt{ font-weight:640; }
    .svg-wrap{ background:var(--canvas); border:1px solid var(--border); border-radius:7px; overflow:hidden; line-height:0; aspect-ratio:1/1;
      display:flex; align-items:center; justify-content:center; }
    .svg-wrap svg.body{ display:block; width:100%; height:100%; }
    .svg-wrap svg.body text{ font-family:var(--mono); }
    .card-sub{ margin:0; font-size:11.5px; color:var(--muted); line-height:1.45; }
    .panel{ margin-top:28px; border:1px solid var(--border); border-radius:10px; padding:16px 18px; background:var(--panel); border-left:3px solid var(--err); }
    .panel-title{ font-size:13px; margin:0 0 6px; color:var(--bright); font-weight:640; font-family:var(--mono); }
    .panel-list{ margin:6px 0 0; padding-left:18px; color:var(--secondary); font-size:12.5px; }
    footer{ margin-top:44px; padding-top:16px; border-top:1px solid var(--border); font-family:var(--mono); font-size:11px; color:var(--muted); }
    """;

    return $"""
    <title>The edge taxonomy — every shape's edges, classified</title>
    <style>{css}</style>
    <div class="wrap">
      <header class="top">
        <p class="eyebrow">Map generation · the shape vocabulary · edge taxonomy</p>
        <h1>Every edge, classified by the negative space it faces</h1>
        <p class="lede">A body's negative spaces escalate by <b>wall count</b>: a <b>notch</b> is wrapped by two
        edges (the L's corner), a <b>bay</b> by three (the staple's recess), a <b>hole</b> is enclosed (the ring's
        void); anything less encased is plain outside. Every boundary edge is stroked in the colour of the space
        it faces — <b>green edges are free surface</b>, the candidates for docks, hub offers, and the mid band —
        and <b>pink edges are the terminal room's own wall</b>: sealed, never attached (the docking gate's
        never-dock veto; the clamp's designated seat and the elevation-stage dock are the sanctioned
        exceptions). <b>Dashed pink is the room's clearance margin</b> — terrain within the corridor minimum of
        the room, sealed by rule so nothing docks too close and alters the approach the emitter designed; the
        margin also splits the adjacent negative space into a guarded piece and free remainders. A boundary
        line splits where ownership or guard changes, so a room capping a lane leaves part of the line free and
        part sealed. Tinted cells are the spaces themselves; a decomposed space also names its own compound
        form (<code>≡ spine+2</code> — the void is a body too). The solid rectangle — today's
        hub — shows the degenerate case: four free edges, which is the whole current attachment rule. Computed
        by <code>BodyEdges.Classify</code> from geometry alone; regenerate with
        <code>dotnet run tools/compose/edge-gallery.cs</code>.</p>
        <div class="legend">
          {Chip(NegativeSpaceKind.Open, "free edge — offerable surface")}
          <span class="chip"><span class="chip-sw" style="background:{TerminalCol}"></span>sealed — the room's wall, never attach</span>
          <span class="chip"><span class="chip-sw" style="background:repeating-linear-gradient(90deg,{TerminalCol} 0 4px,transparent 4px 7px)"></span>guarded — room clearance (≥10 blocks), sealed by rule</span>
          {Chip(NegativeSpaceKind.Notch, "notch · 2 walls")}
          {Chip(NegativeSpaceKind.Bay, "bay · 3 walls")}
          {Chip(NegativeSpaceKind.Hole, "hole · enclosed")}
          <span class="chip">✓ published (an offer — filled later, maybe never) · ✗ vetoed</span>
        </div>
      </header>

    {sections}
      {failPanel}
      <footer>Static self-contained SVG · {all.Count} shapes · corridor width {Cw} cells.</footer>
    </div>
    """;
}
