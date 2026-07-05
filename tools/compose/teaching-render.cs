#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

// Renders ONE authored teaching plan to a single wide, self-contained HTML figure.
// Fans the authored (z>=0) unit by its mirror_z symmetry (order 2): base drawn solid + labelled,
// mirror drawn solid-but-dimmed + unlabelled. Colours pieces/zones by id-prefix (role is uniform here).

// ── palette (editor visual language: plan-doc.js / tokens.css) ──
const string BgCanvas = "#080f1a";   // --bg-canvas (SVG ground)
const string AxisCol  = "#a78bfa";   // --canvas-axis
const string Ink      = "#ffffff";   // --canvas-ink
// piece prefixes (reuse editor role hexes as four distinct fills)
const string CFrontline = "#e0b13c"; // amber   (MARKER_COLORS.spawn)
const string CHub       = "#8f7bd6"; // purple  (ROLE_COLORS.spawn)
const string CStep      = "#3fae74"; // green   (ROLE_COLORS.wool-room)
const string CPieceGen  = "#7c8899"; // grey    (ROLE_COLORS.piece)
const string CBuffer    = "#f2792b"; // orange  (ROLE_COLORS.buffer — reserved-gap annotation)
// zone prefixes (two distinct translucent tints)
const string CBand   = "#3b82f6";    // blue    (--accent)
const string CBridge = "#f472b6";    // pink

var planPath = args.Length > 0 ? args[0] : Path.Combine("tools", "seeds", "teaching", "frontline-dos-and-donts-rot-180-mirror.plan.json");
var json = File.ReadAllText(planPath);

var plan = PlanModel.Parse(json);
if (plan is null) { Console.Error.WriteLine("PARSE FAILED: PlanModel.Parse returned null"); return; }

int cell = plan.Globals.Cell;
string sym = plan.Globals.Symmetry;
int order = Symmetry.Order(sym);
string[] axes = Symmetry.OrbitAxes(sym);

// Buffers colour by their non-generating role first (robust), then by the `buffer*` id-prefix convention.
var bufferIds = plan.Pieces.Where(p => p.Role == PlanRoles.Buffer || p.Id.StartsWith("buffer")).Select(p => p.Id).ToHashSet();

// ── prefix classifiers ──
string PiecePrefix(string id) =>
    id.StartsWith("frontline") ? "frontline" :
    id.StartsWith("hub") ? "hub" :
    id.StartsWith("step") ? "step" : "piece";
string PieceColor(string id) => bufferIds.Contains(id) ? CBuffer : PiecePrefix(id) switch
{
    "frontline" => CFrontline, "hub" => CHub, "step" => CStep, _ => CPieceGen,
};
string ZonePrefix(string id) => id.StartsWith("bridge") ? "bridge" : "band";
string ZoneColor(string id) => ZonePrefix(id) == "bridge" ? CBridge : CBand;

// ── fan into orbit images (block coords). k=0 base, k>=1 mirror image(s) ──
var pieceImgs = new List<(double X1, double Z1, double X2, double Z2, string Id, int K)>();
var zoneImgs = new List<(double X1, double Z1, double X2, double Z2, string Id, int K)>();

foreach (var p in plan.Pieces)
{
    double x1 = p.Rect[0] * cell, z1 = p.Rect[1] * cell;
    double x2 = (p.Rect[0] + p.Rect[2]) * cell, z2 = (p.Rect[1] + p.Rect[3]) * cell;
    for (int k = 0; k < order; k++)
    {
        var (a, b, cc, d) = Fan(x1, z1, x2, z2, axes, k);
        pieceImgs.Add((a, b, cc, d, p.Id, k));
    }
}
foreach (var z in plan.Zones)
{
    double x1 = z.Rect[0] * cell, z1 = z.Rect[1] * cell;
    double x2 = (z.Rect[0] + z.Rect[2]) * cell, z2 = (z.Rect[1] + z.Rect[3]) * cell;
    for (int k = 0; k < order; k++)
    {
        var (a, b, cc, d) = Fan(x1, z1, x2, z2, axes, k);
        zoneImgs.Add((a, b, cc, d, z.Id, k));
    }
}

// ── content bounds over ALL images (base + mirror) for the viewBox ──
double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
void Bound(double a, double b, double cc, double d)
{ minX = Math.Min(minX, a); minZ = Math.Min(minZ, b); maxX = Math.Max(maxX, cc); maxZ = Math.Max(maxZ, d); }
foreach (var p in pieceImgs) Bound(p.X1, p.Z1, p.X2, p.Z2);
foreach (var z in zoneImgs) Bound(z.X1, z.Z1, z.X2, z.Z2);

double mgn = 1.5 * cell;   // blocks of breathing room each side
minX -= mgn; minZ -= mgn; maxX += mgn; maxZ += mgn;

const double S = 9.0;      // px per block  (cell=5 blocks -> 45px/cell; a 2-cell piece = 90px, roomy for ids)
double vbw = (maxX - minX) * S, vbh = (maxZ - minZ) * S;
double PX(double bx) => (bx - minX) * S;
double PY(double bz) => (bz - minZ) * S;

double idFont = 12.0, zoneFont = 10.0;

// ── build SVG ──
var svg = new StringBuilder();
svg.Append($"<svg width=\"{N(vbw)}\" height=\"{N(vbh)}\" viewBox=\"0 0 {N(vbw)} {N(vbh)}\" xmlns=\"http://www.w3.org/2000/svg\" class=\"fig\" role=\"img\" aria-label=\"teaching set\">");
// buffer (reserved-gap) diagonal-hatch pattern, in device pixels
svg.Append($"<defs><pattern id=\"buffer-hatch\" patternUnits=\"userSpaceOnUse\" width=\"6\" height=\"6\" patternTransform=\"rotate(45)\">" +
           $"<rect width=\"6\" height=\"6\" fill=\"{CBuffer}\" fill-opacity=\"0.12\"/>" +
           $"<line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" stroke=\"{CBuffer}\" stroke-width=\"1.2\"/></pattern></defs>");
svg.Append($"<rect x=\"0\" y=\"0\" width=\"{N(vbw)}\" height=\"{N(vbh)}\" fill=\"{BgCanvas}\"/>");

// dim the mirror side (z<0) with a faint wash so base/mirror read apart at a glance
svg.Append($"<rect x=\"0\" y=\"0\" width=\"{N(vbw)}\" height=\"{N(PY(0))}\" fill=\"#ffffff\" fill-opacity=\"0.015\"/>");

// faint cell grid
int gx0 = (int)Math.Floor(minX / cell), gx1 = (int)Math.Ceiling(maxX / cell);
int gz0 = (int)Math.Floor(minZ / cell), gz1 = (int)Math.Ceiling(maxZ / cell);
svg.Append("<g stroke-linecap=\"butt\">");
for (int gc = gx0; gc <= gx1; gc++)
    svg.Append($"<line x1=\"{N(PX(gc * cell))}\" y1=\"{N(PY(gz0 * cell))}\" x2=\"{N(PX(gc * cell))}\" y2=\"{N(PY(gz1 * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.10\" stroke-width=\"0.6\"/>");
for (int gc = gz0; gc <= gz1; gc++)
    svg.Append($"<line x1=\"{N(PX(gx0 * cell))}\" y1=\"{N(PY(gc * cell))}\" x2=\"{N(PX(gx1 * cell))}\" y2=\"{N(PY(gc * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.10\" stroke-width=\"0.6\"/>");
svg.Append("</g>");

// x=0 axis (light) + z=0 mirror line (prominent, dashed, labelled)
svg.Append($"<line x1=\"{N(PX(0))}\" y1=\"{N(PY(gz0 * cell))}\" x2=\"{N(PX(0))}\" y2=\"{N(PY(gz1 * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.35\" stroke-width=\"1.0\"/>");
svg.Append($"<line x1=\"{N(PX(gx0 * cell))}\" y1=\"{N(PY(0))}\" x2=\"{N(PX(gx1 * cell))}\" y2=\"{N(PY(0))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.85\" stroke-width=\"2.0\" stroke-dasharray=\"10 6\"/>");
svg.Append($"<text x=\"{N(PX(gx0 * cell) + 8)}\" y=\"{N(PY(0) - 6)}\" font-family=\"ui-monospace, Menlo, monospace\" font-size=\"12\" fill=\"{AxisCol}\" paint-order=\"stroke\" stroke=\"{BgCanvas}\" stroke-width=\"3\">z = 0 · mirror line</text>");

// zones under pieces — mirror first (dim), then base. translucent fill, dashed same-colour stroke.
void EmitZone(in (double X1, double Z1, double X2, double Z2, string Id, int K) z, bool mir)
{
    var col = ZoneColor(z.Id);
    double fo = mir ? 0.08 : 0.20, so = mir ? 0.45 : 1.0, sw = mir ? 1.0 : 1.4;
    svg.Append($"<rect x=\"{N(PX(z.X1))}\" y=\"{N(PY(z.Z1))}\" width=\"{N((z.X2 - z.X1) * S)}\" height=\"{N((z.Z2 - z.Z1) * S)}\" " +
               $"fill=\"{col}\" fill-opacity=\"{N(fo)}\" stroke=\"{col}\" stroke-opacity=\"{N(so)}\" stroke-width=\"{N(sw)}\" stroke-dasharray=\"7 4\"/>");
}
foreach (var z in zoneImgs.Where(z => z.K != 0)) EmitZone(z, true);
foreach (var z in zoneImgs.Where(z => z.K == 0)) EmitZone(z, false);

// pieces — solid role-by-prefix colour; mirror dimmed.
void EmitPiece(in (double X1, double Z1, double X2, double Z2, string Id, int K) p, bool mir)
{
    var col = PieceColor(p.Id);
    if (bufferIds.Contains(p.Id))
    {
        // reserved-gap annotation: hatched fill + dashed stroke (no solid terrain); mirror image dimmed.
        double bfo = mir ? 0.30 : 0.90, bso = mir ? 0.45 : 0.85, bsw = mir ? 1.0 : 1.4;
        svg.Append($"<rect x=\"{N(PX(p.X1))}\" y=\"{N(PY(p.Z1))}\" width=\"{N((p.X2 - p.X1) * S)}\" height=\"{N((p.Z2 - p.Z1) * S)}\" " +
                   $"fill=\"url(#buffer-hatch)\" fill-opacity=\"{N(bfo)}\" stroke=\"{col}\" stroke-opacity=\"{N(bso)}\" stroke-width=\"{N(bsw)}\" stroke-dasharray=\"5 4\"/>");
        return;
    }
    double fo = mir ? 0.30 : 0.78, sw = mir ? 1.0 : 1.5, so = mir ? 0.5 : 1.0;
    svg.Append($"<rect x=\"{N(PX(p.X1))}\" y=\"{N(PY(p.Z1))}\" width=\"{N((p.X2 - p.X1) * S)}\" height=\"{N((p.Z2 - p.Z1) * S)}\" " +
               $"fill=\"{col}\" fill-opacity=\"{N(fo)}\" stroke=\"{col}\" stroke-opacity=\"{N(so)}\" stroke-width=\"{N(sw)}\"/>");
}
foreach (var p in pieceImgs.Where(p => p.K != 0)) EmitPiece(p, true);
foreach (var p in pieceImgs.Where(p => p.K == 0)) EmitPiece(p, false);

// id labels — BASE image only. zone ids near their top edge (near the mirror line), piece ids centred.
svg.Append($"<g font-family=\"ui-monospace, SFMono-Regular, Menlo, monospace\" font-weight=\"600\" text-anchor=\"middle\" paint-order=\"stroke\" stroke=\"{BgCanvas}\" stroke-width=\"3\" stroke-linejoin=\"round\">");
foreach (var z in zoneImgs.Where(z => z.K == 0))
{
    string zlbl = ZonePrefix(z.Id) == "bridge" ? "#f9a8d4" : "#93c5fd";
    svg.Append($"<text x=\"{N((PX(z.X1) + PX(z.X2)) / 2)}\" y=\"{N(PY(z.Z1) + zoneFont * 1.15)}\" font-size=\"{N(zoneFont)}\" fill=\"{zlbl}\">{Esc(z.Id)}</text>");
}
foreach (var p in pieceImgs.Where(p => p.K == 0))
    svg.Append($"<text x=\"{N((PX(p.X1) + PX(p.X2)) / 2)}\" y=\"{N((PY(p.Z1) + PY(p.Z2)) / 2 + idFont * 0.34)}\" font-size=\"{N(idFont)}\" fill=\"{Ink}\">{Esc(p.Id)}</text>");
svg.Append("</g>");
svg.Append("</svg>");

// ── correctness cross-checks (stdout report) ──
var validate = PlanValidator.Validate(plan);
int errCount = validate.Count(f => f.Severity == PlanSeverity.Error);
int lintCount = validate.Count(f => f.Severity == PlanSeverity.Lint);

// base-image bounds in CELLS (author's unit)
int bMinX = plan.Pieces.Select(p => p.Rect[0]).Concat(plan.Zones.Select(z => z.Rect[0])).Min();
int bMaxX = plan.Pieces.Select(p => p.Rect[0] + p.Rect[2]).Concat(plan.Zones.Select(z => z.Rect[0] + z.Rect[2])).Max();
int bMinZ = plan.Pieces.Select(p => p.Rect[1]).Concat(plan.Zones.Select(z => z.Rect[1])).Min();
int bMaxZ = plan.Pieces.Select(p => p.Rect[1] + p.Rect[3]).Concat(plan.Zones.Select(z => z.Rect[1] + z.Rect[3])).Max();

// overlaps among base-image rects (cell units). categorised: piece∩piece, zone∩zone, piece∩zone.
static double OvArea(int[] a, int[] b)
{
    double ix = Math.Min(a[0] + a[2], b[0] + b[2]) - Math.Max(a[0], b[0]);
    double iz = Math.Min(a[1] + a[3], b[1] + b[3]) - Math.Max(a[1], b[1]);
    return ix > 0 && iz > 0 ? ix * iz : 0;
}
var ppOv = new List<string>(); var zzOv = new List<string>(); var pzOv = new List<string>();
for (int i = 0; i < plan.Pieces.Count; i++)
    for (int j = i + 1; j < plan.Pieces.Count; j++)
        if (OvArea(plan.Pieces[i].Rect, plan.Pieces[j].Rect) > 0)
            ppOv.Add($"{plan.Pieces[i].Id} ∩ {plan.Pieces[j].Id}");
for (int i = 0; i < plan.Zones.Count; i++)
    for (int j = i + 1; j < plan.Zones.Count; j++)
        if (OvArea(plan.Zones[i].Rect, plan.Zones[j].Rect) > 0)
            zzOv.Add($"{plan.Zones[i].Id} ∩ {plan.Zones[j].Id}");
foreach (var p in plan.Pieces)
    foreach (var z in plan.Zones)
        if (OvArea(p.Rect, z.Rect) > 0)
            pzOv.Add($"{p.Id} ⊂ {z.Id}");

Console.WriteLine("── frontline teaching render — cross-check ──");
Console.WriteLine($"plan.name           : {plan.Meta?.Name}");
Console.WriteLine($"PlanModel.Parse     : loaded ok (version {plan.Version}), symmetry={sym} order={order}");
Console.WriteLine($"pieces / zones      : {plan.Pieces.Count} / {plan.Zones.Count}");
Console.WriteLine($"placements          : spawns={plan.Placements.Spawns.Count} wools={plan.Placements.Wools.Count} iron={plan.Placements.Iron.Count}");
Console.WriteLine($"bbox (base, cells)  : x [{bMinX}..{bMaxX}]  z [{bMinZ}..{bMaxZ}]");
Console.WriteLine($"bbox (base, blocks) : x [{bMinX * cell}..{bMaxX * cell}]  z [{bMinZ * cell}..{bMaxZ * cell}]");
Console.WriteLine($"figure viewBox (px) : {N(vbw)} x {N(vbh)}  (S={S}px/block, incl. mirror + {mgn}-block margin)");
Console.WriteLine($"PlanValidator       : {errCount} error(s), {lintCount} lint");
foreach (var f in validate)
    Console.WriteLine($"    [{f.Severity}]{(f.Rule is null ? "" : " " + f.Rule)}: {f.Message}");
Console.WriteLine($"piece∩piece overlaps: {ppOv.Count}");
foreach (var s in ppOv) Console.WriteLine($"    {s}");
Console.WriteLine($"zone∩zone overlaps  : {zzOv.Count}");
foreach (var s in zzOv) Console.WriteLine($"    {s}");
Console.WriteLine($"piece⊂zone overlaps : {pzOv.Count}");
foreach (var s in pzOv) Console.WriteLine($"    {s}");

// ── page ──
string legendPieces = string.Join("", new[]
{
    ("frontline-*", CFrontline), ("hub-*", CHub), ("step-*", CStep), ("piece-*", CPieceGen),
}.Select(t => $"<span class=\"lg\"><span class=\"sw\" style=\"background:{t.Item2}\"></span>{Esc(t.Item1)}</span>"));
legendPieces += $"<span class=\"lg\"><span class=\"sw\" style=\"background:repeating-linear-gradient(45deg,{CBuffer} 0 1.4px,transparent 1.4px 4px),{CBuffer}22;border:1px dashed {CBuffer}\"></span>buffer-*</span>";
string legendZones = string.Join("", new[]
{
    ("band-*", CBand), ("bridge-*", CBridge),
}.Select(t => $"<span class=\"lg\"><span class=\"sw sw--zone\" style=\"border-color:{t.Item2};background:{t.Item2}33\"></span>{Esc(t.Item1)}</span>"));

var overlapNote = pzOv.Count > 0
    ? $"{pzOv.Count} step/piece-in-band overlaps (intentional — steps sit inside bands); {ppOv.Count} piece∩piece, {zzOv.Count} zone∩zone."
    : $"{ppOv.Count} piece∩piece, {zzOv.Count} zone∩zone overlaps.";

var html = $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Frontline teaching set — mirror_z</title>
<style>
  :root{
    --bg-base:#0f172a; --bg-panel:#1e293b; --bg-canvas:#080f1a; --border:#334155;
    --text-muted:#8397b0; --text-secondary:#94a3b8; --text-primary:#cbd5e1;
    --text-bright:#e2e8f0; --text-strong:#ffffff; --accent-light:#60a5fa;
    --mono:ui-monospace, SFMono-Regular, Menlo, "Cascadia Mono", monospace;
    --sans:-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
  }
  @media (prefers-color-scheme: light){
    :root{ --bg-base:#f1f5f9; --bg-panel:#ffffff; --border:#cbd5e1; --text-muted:#586780;
      --text-secondary:#475569; --text-primary:#1e293b; --text-bright:#0f172a; --text-strong:#0f172a; --accent-light:#2563eb; }
  }
  *{ box-sizing:border-box; }
  html,body{ margin:0; padding:0; overflow-x:hidden; }
  body{ background:var(--bg-base); color:var(--text-primary); font-family:var(--sans); font-size:14px; line-height:1.5;
    -webkit-font-smoothing:antialiased; }
  .wrap{ max-width:100%; margin:0 auto; padding:26px 24px 48px; }
  header.top{ border-bottom:1px solid var(--border); padding-bottom:18px; margin-bottom:20px; }
  .eyebrow{ font-family:var(--mono); font-size:11px; letter-spacing:.14em; text-transform:uppercase; color:var(--accent-light); margin:0 0 6px; }
  h1{ font-size:22px; line-height:1.25; margin:0 0 8px; color:var(--text-strong); font-weight:650; letter-spacing:-.01em; }
  .lede{ margin:0; max-width:90ch; color:var(--text-secondary); font-size:13.5px; }
  .lede code{ font-family:var(--mono); color:var(--text-bright); background:var(--bg-panel); padding:1px 5px; border-radius:3px; font-size:12px; }
  .legend{ display:flex; flex-wrap:wrap; gap:8px 18px; margin-top:16px; padding:12px 14px; background:var(--bg-panel);
    border:1px solid var(--border); border-radius:6px; align-items:center; }
  .legend-lbl{ font-family:var(--mono); font-size:10px; letter-spacing:.1em; text-transform:uppercase; color:var(--text-muted); }
  .lg{ display:inline-flex; align-items:center; gap:6px; font-size:12px; color:var(--text-secondary); font-family:var(--mono); }
  .sw{ width:13px; height:13px; border-radius:2px; flex:none; }
  .sw--zone{ border:1.4px dashed; }
  .legend-sep{ width:1px; align-self:stretch; background:var(--border); }
  .figscroll{ margin-top:22px; overflow-x:auto; overflow-y:hidden; border:1px solid var(--border); border-radius:8px;
    background:var(--bg-canvas); }
  .figscroll .fig{ display:block; }
  .meta{ margin-top:14px; font-family:var(--mono); font-size:11.5px; color:var(--text-muted); display:flex; flex-wrap:wrap; gap:4px 16px; }
  .meta b{ color:var(--text-bright); font-weight:600; }
  footer{ margin-top:26px; padding-top:14px; border-top:1px solid var(--border); font-family:var(--mono); font-size:11px; color:var(--text-muted); }
</style>
</head>
<body>
<div class="wrap">
  <header class="top">
    <p class="eyebrow">Composer · teaching corpus · structural sketch</p>
    <h1>{{Esc(plan.Meta?.Name ?? "frontline teaching set")}}</h1>
    <p class="lede">Frontline teaching set — <code>mirror_z</code>, mirror line <code>z = 0</code>; base side
    labelled, mirror side dimmed. ~16 frontline / band / hub / step / bridge examples laid out along x. Colour is by
    <strong>id-prefix</strong> (every piece is <code>role:"piece"</code>, so role is uniform). No spawns or wools — a
    pure structural sketch.</p>
    <div class="legend">
      <span class="legend-lbl">Pieces</span>{{legendPieces}}
      <span class="legend-sep"></span>
      <span class="legend-lbl">Zones</span>{{legendZones}}
    </div>
  </header>

  <div class="figscroll">{{svg}}</div>

  <div class="meta">
    <span><b>{{plan.Pieces.Count}}</b> pieces</span>
    <span><b>{{plan.Zones.Count}}</b> zones</span>
    <span>base bbox x <b>[{{bMinX}}..{{bMaxX}}]</b> z <b>[{{bMinZ}}..{{bMaxZ}}]</b> cells</span>
    <span>cell = <b>{{cell}}</b> blocks</span>
    <span>fanned by <b>{{sym}}</b> (order {{order}})</span>
    <span>validator: <b>{{errCount}}</b> err · <b>{{lintCount}}</b> lint (markers absent — expected)</span>
    <span>{{Esc(overlapNote)}}</span>
  </div>

  <footer>Static SVG · self-contained · fanned via PgmStudio.Geom.Symmetry · scroll horizontally to pan the full x span.</footer>
</div>
</body>
</html>
""";

string stem = Path.GetFileName(planPath);
stem = stem.EndsWith(".plan.json") ? stem[..^".plan.json".Length] : Path.GetFileNameWithoutExtension(stem);
var outPath = args.Length > 0
    ? Path.Combine("tools", "compose", "out", $"{stem}-teaching.html")
    : Path.Combine("tools", "compose", "out", "frontline-teaching.html");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, html);
Console.WriteLine($"\nwrote {outPath}");
Console.WriteLine($"contains external url: {(html.Contains("http://") || html.Contains("https://") ? "YES" : "no")}");

// ── helpers ──
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
