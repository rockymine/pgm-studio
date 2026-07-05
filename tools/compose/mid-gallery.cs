#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

// Renders the authored mid teaching plans to ONE self-contained HTML gallery for visual review:
//   • 8 rot_90 mid examples — a responsive grid, each fanned to 4 orbit images (order 4) about the origin,
//     all images solid, base image labelled; x=0 and z=0 axes drawn.
//   • the wide mirror_z mid taxonomy — a horizontally-scrollable fold, base labelled / mirror dimmed.
// Colour is by type: frontline / hub / step / generic piece · band / bridge zones · buffer = orange hatch
// (classified role=="buffer" first, else by id-prefix). Prints a per-plan parse/validate/counts cross-check.

// ── palette (editor visual language: plan-doc.js ROLE_COLORS / MARKER_COLORS, tokens.css) ──
const string BgCanvas = "#080f1a";   // --bg-canvas (SVG ground; dark in both themes)
const string AxisCol  = "#a78bfa";   // --canvas-axis
const string Ink      = "#ffffff";   // --canvas-ink
const string CFrontline = "#e0b13c"; // amber
const string CHub       = "#8f7bd6"; // purple
const string CStep      = "#3fae74"; // green
const string CPieceGen  = "#7c8899"; // grey
const string CBuffer    = "#f2792b"; // orange (buffer — reserved-gap annotation, diagonal hatch)
const string CBand   = "#3b82f6";    // blue
const string CBridge = "#f472b6";    // pink

string teachDir = Path.Combine("tools", "seeds", "teaching");
string[] rotStems =
[
    "rot-90-mid-example-1", "rot-90-mid-example-2", "rot-90-mid-example-3", "rot-90-mid-example-4",
    "rot-90-mid-example-5", "rot-90-mid-example-6", "rot-90-mid-example-7", "rot-90-mid-example-8",
];
const string mirrorStem = "mirror-mid-examples";

// ── type classifiers (role first, then id-prefix) ──
string PiecePrefix(string id) =>
    id.StartsWith("frontline") ? "frontline" :
    id.StartsWith("hub") ? "hub" :
    id.StartsWith("step") ? "step" : "piece";
string PieceColor(PlanPiece p) => p.Role == PlanRoles.Buffer ? CBuffer : PiecePrefix(p.Id) switch
{
    "frontline" => CFrontline, "hub" => CHub, "step" => CStep, _ => CPieceGen,
};
string ZonePrefix(string id) => id.StartsWith("bridge") ? "bridge" : "band";
string ZoneColor(string id) => ZonePrefix(id) == "bridge" ? CBridge : CBand;

var report = new List<Row>();
bool anyBufferHatch = false;

// ── render one plan to an inline SVG. dimMirror=false → every orbit image solid (rot_90 cards);
//    dimMirror=true → base solid + labelled, non-base images dimmed (mirror fold). ──
string BuildSvg(PlanModel plan, string patId, bool dimMirror, double pxPerBlock, double marginCells)
{
    int cell = plan.Globals.Cell;
    string sym = plan.Globals.Symmetry;
    int order = Symmetry.Order(sym);
    string[] axes = Symmetry.OrbitAxes(sym);

    var pieceImgs = new List<(double X1, double Z1, double X2, double Z2, PlanPiece P, int K)>();
    var zoneImgs = new List<(double X1, double Z1, double X2, double Z2, PlanZone Z, int K, List<(double, double, double, double)> Holes)>();

    foreach (var p in plan.Pieces)
    {
        double x1 = p.Rect[0] * cell, z1 = p.Rect[1] * cell;
        double x2 = (p.Rect[0] + p.Rect[2]) * cell, z2 = (p.Rect[1] + p.Rect[3]) * cell;
        for (int k = 0; k < order; k++)
        {
            var (a, b, cc, d) = Fan(x1, z1, x2, z2, axes, k);
            pieceImgs.Add((a, b, cc, d, p, k));
        }
    }
    foreach (var z in plan.Zones)
    {
        double x1 = z.Rect[0] * cell, z1 = z.Rect[1] * cell;
        double x2 = (z.Rect[0] + z.Rect[2]) * cell, z2 = (z.Rect[1] + z.Rect[3]) * cell;
        for (int k = 0; k < order; k++)
        {
            var (a, b, cc, d) = Fan(x1, z1, x2, z2, axes, k);
            var holes = new List<(double, double, double, double)>();
            foreach (var h in z.Holes)
                holes.Add(Fan(h[0] * cell, h[1] * cell, (h[0] + h[2]) * cell, (h[1] + h[3]) * cell, axes, k));
            zoneImgs.Add((a, b, cc, d, z, k, holes));
        }
    }

    // bounds over all orbit images
    double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
    void Bound(double a, double b, double cc, double d)
    { minX = Math.Min(minX, a); minZ = Math.Min(minZ, b); maxX = Math.Max(maxX, cc); maxZ = Math.Max(maxZ, d); }
    foreach (var p in pieceImgs) Bound(p.X1, p.Z1, p.X2, p.Z2);
    foreach (var z in zoneImgs) Bound(z.X1, z.Z1, z.X2, z.Z2);
    if (minX == double.MaxValue) { minX = minZ = 0; maxX = maxZ = cell; }

    double mgn = marginCells * cell;
    minX -= mgn; minZ -= mgn; maxX += mgn; maxZ += mgn;
    // symmetric span for rot_90 so the origin sits centred and both axes read
    if (!dimMirror)
    {
        double ext = Math.Max(Math.Max(-minX, maxX), Math.Max(-minZ, maxZ));
        minX = minZ = -ext; maxX = maxZ = ext;
    }

    double S = pxPerBlock;
    double vbw = (maxX - minX) * S, vbh = (maxZ - minZ) * S;
    double PX(double bx) => (bx - minX) * S;
    double PY(double bz) => (bz - minZ) * S;
    double idFont = dimMirror ? 12.0 : 11.0, zoneFont = dimMirror ? 10.0 : 9.5;

    var svg = new StringBuilder();
    svg.Append($"<svg viewBox=\"0 0 {N(vbw)} {N(vbh)}\" xmlns=\"http://www.w3.org/2000/svg\" class=\"fig\" role=\"img\" aria-label=\"{Esc(plan.Meta?.Name ?? "plan")}\">");
    // per-svg buffer hatch (unique id so multiple SVGs in one doc don't collide)
    svg.Append($"<defs><pattern id=\"{patId}\" patternUnits=\"userSpaceOnUse\" width=\"6\" height=\"6\" patternTransform=\"rotate(45)\">" +
               $"<rect width=\"6\" height=\"6\" fill=\"{CBuffer}\" fill-opacity=\"0.12\"/>" +
               $"<line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" stroke=\"{CBuffer}\" stroke-width=\"1.2\"/></pattern></defs>");
    svg.Append($"<rect x=\"0\" y=\"0\" width=\"{N(vbw)}\" height=\"{N(vbh)}\" fill=\"{BgCanvas}\"/>");

    // faint cell grid
    int gx0 = (int)Math.Floor(minX / cell), gx1 = (int)Math.Ceiling(maxX / cell);
    int gz0 = (int)Math.Floor(minZ / cell), gz1 = (int)Math.Ceiling(maxZ / cell);
    svg.Append("<g stroke-linecap=\"butt\">");
    for (int gc = gx0; gc <= gx1; gc++)
        svg.Append($"<line x1=\"{N(PX(gc * cell))}\" y1=\"{N(PY(gz0 * cell))}\" x2=\"{N(PX(gc * cell))}\" y2=\"{N(PY(gz1 * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.10\" stroke-width=\"0.6\"/>");
    for (int gc = gz0; gc <= gz1; gc++)
        svg.Append($"<line x1=\"{N(PX(gx0 * cell))}\" y1=\"{N(PY(gc * cell))}\" x2=\"{N(PX(gx1 * cell))}\" y2=\"{N(PY(gc * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.10\" stroke-width=\"0.6\"/>");
    svg.Append("</g>");

    if (dimMirror)
    {
        // dim the mirror side (z<0) with a faint wash
        svg.Append($"<rect x=\"0\" y=\"0\" width=\"{N(vbw)}\" height=\"{N(PY(0))}\" fill=\"#ffffff\" fill-opacity=\"0.015\"/>");
        // x=0 axis (light) + z=0 mirror line (prominent, dashed, labelled)
        svg.Append($"<line x1=\"{N(PX(0))}\" y1=\"{N(PY(gz0 * cell))}\" x2=\"{N(PX(0))}\" y2=\"{N(PY(gz1 * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.35\" stroke-width=\"1.0\"/>");
        svg.Append($"<line x1=\"{N(PX(gx0 * cell))}\" y1=\"{N(PY(0))}\" x2=\"{N(PX(gx1 * cell))}\" y2=\"{N(PY(0))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.85\" stroke-width=\"2.0\" stroke-dasharray=\"10 6\"/>");
        svg.Append($"<text x=\"{N(PX(gx0 * cell) + 8)}\" y=\"{N(PY(0) - 6)}\" font-family=\"ui-monospace, Menlo, monospace\" font-size=\"12\" fill=\"{AxisCol}\" paint-order=\"stroke\" stroke=\"{BgCanvas}\" stroke-width=\"3\">z = 0 · mirror line</text>");
    }
    else
    {
        // rot_90 pinwheel about the origin — both axes + centre ring prominent
        svg.Append($"<line x1=\"{N(PX(0))}\" y1=\"{N(PY(gz0 * cell))}\" x2=\"{N(PX(0))}\" y2=\"{N(PY(gz1 * cell))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.55\" stroke-width=\"1.3\"/>");
        svg.Append($"<line x1=\"{N(PX(gx0 * cell))}\" y1=\"{N(PY(0))}\" x2=\"{N(PX(gx1 * cell))}\" y2=\"{N(PY(0))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.55\" stroke-width=\"1.3\"/>");
        svg.Append($"<circle cx=\"{N(PX(0))}\" cy=\"{N(PY(0))}\" r=\"{N(cell * 0.28 * S)}\" fill=\"none\" stroke=\"{AxisCol}\" stroke-opacity=\"0.6\" stroke-width=\"1.2\"/>");
    }

    // zones (under pieces) — mirror first (dim), base last. translucent fill, dashed same-colour stroke.
    void EmitZone(in (double X1, double Z1, double X2, double Z2, PlanZone Z, int K, List<(double, double, double, double)> Holes) z)
    {
        bool dim = dimMirror && z.K != 0;
        var col = ZoneColor(z.Z.Id);
        double fo = dim ? 0.08 : 0.20, so = dim ? 0.45 : 1.0, sw = dim ? 1.0 : 1.4;
        svg.Append($"<rect x=\"{N(PX(z.X1))}\" y=\"{N(PY(z.Z1))}\" width=\"{N((z.X2 - z.X1) * S)}\" height=\"{N((z.Z2 - z.Z1) * S)}\" " +
                   $"fill=\"{col}\" fill-opacity=\"{N(fo)}\" stroke=\"{col}\" stroke-opacity=\"{N(so)}\" stroke-width=\"{N(sw)}\" stroke-dasharray=\"7 4\"/>");
        foreach (var h in z.Holes)
            svg.Append($"<rect x=\"{N(PX(h.Item1))}\" y=\"{N(PY(h.Item2))}\" width=\"{N((h.Item3 - h.Item1) * S)}\" height=\"{N((h.Item4 - h.Item2) * S)}\" " +
                       $"fill=\"{BgCanvas}\" fill-opacity=\"0.6\" stroke=\"{col}\" stroke-width=\"0.8\" stroke-dasharray=\"3 3\"/>");
    }
    foreach (var z in zoneImgs.Where(z => z.K != 0)) EmitZone(z);
    foreach (var z in zoneImgs.Where(z => z.K == 0)) EmitZone(z);

    // pieces — solid type colour; non-base dimmed only in mirror mode. buffers = hatch + dashed (no terrain).
    void EmitPiece(in (double X1, double Z1, double X2, double Z2, PlanPiece P, int K) p)
    {
        bool dim = dimMirror && p.K != 0;
        var col = PieceColor(p.P);
        if (p.P.Role == PlanRoles.Buffer)
        {
            double bfo = dim ? 0.30 : 0.95, bso = dim ? 0.45 : 0.9, bsw = dim ? 1.0 : 1.4;
            svg.Append($"<rect x=\"{N(PX(p.X1))}\" y=\"{N(PY(p.Z1))}\" width=\"{N((p.X2 - p.X1) * S)}\" height=\"{N((p.Z2 - p.Z1) * S)}\" " +
                       $"fill=\"url(#{patId})\" fill-opacity=\"{N(bfo)}\" stroke=\"{col}\" stroke-opacity=\"{N(bso)}\" stroke-width=\"{N(bsw)}\" stroke-dasharray=\"5 4\"/>");
            return;
        }
        double fo = dim ? 0.30 : 0.78, sw = dim ? 1.0 : 1.5, so = dim ? 0.5 : 1.0;
        svg.Append($"<rect x=\"{N(PX(p.X1))}\" y=\"{N(PY(p.Z1))}\" width=\"{N((p.X2 - p.X1) * S)}\" height=\"{N((p.Z2 - p.Z1) * S)}\" " +
                   $"fill=\"{col}\" fill-opacity=\"{N(fo)}\" stroke=\"{col}\" stroke-opacity=\"{N(so)}\" stroke-width=\"{N(sw)}\"/>");
    }
    foreach (var p in pieceImgs.Where(p => p.K != 0)) EmitPiece(p);
    foreach (var p in pieceImgs.Where(p => p.K == 0)) EmitPiece(p);

    // id labels — BASE image (k=0) only. zone ids near top edge, piece/buffer ids centred.
    svg.Append($"<g font-family=\"ui-monospace, SFMono-Regular, Menlo, monospace\" font-weight=\"600\" text-anchor=\"middle\" paint-order=\"stroke\" stroke=\"{BgCanvas}\" stroke-width=\"3\" stroke-linejoin=\"round\">");
    foreach (var z in zoneImgs.Where(z => z.K == 0))
    {
        string zlbl = ZonePrefix(z.Z.Id) == "bridge" ? "#f9a8d4" : "#93c5fd";
        svg.Append($"<text x=\"{N((PX(z.X1) + PX(z.X2)) / 2)}\" y=\"{N(PY(z.Z1) + zoneFont * 1.15)}\" font-size=\"{N(zoneFont)}\" fill=\"{zlbl}\">{Esc(z.Z.Id)}</text>");
    }
    foreach (var p in pieceImgs.Where(p => p.K == 0))
    {
        string flbl = p.P.Role == PlanRoles.Buffer ? "#fdba74" : Ink;
        svg.Append($"<text x=\"{N((PX(p.X1) + PX(p.X2)) / 2)}\" y=\"{N((PY(p.Z1) + PY(p.Z2)) / 2 + idFont * 0.34)}\" font-size=\"{N(idFont)}\" fill=\"{flbl}\">{Esc(p.P.Id)}</text>");
    }
    svg.Append("</g></svg>");
    return svg.ToString();
}

// ── overlap helper (cell units) ──
static double OvArea(int[] a, int[] b)
{
    double ix = Math.Min(a[0] + a[2], b[0] + b[2]) - Math.Max(a[0], b[0]);
    double iz = Math.Min(a[1] + a[3], b[1] + b[3]) - Math.Max(a[1], b[1]);
    return ix > 0 && iz > 0 ? ix * iz : 0;
}

Row CrossCheck(string stem, PlanModel? plan)
{
    var row = new Row { Stem = stem };
    if (plan is null) { row.Parsed = false; return row; }
    row.Parsed = true;
    row.Symmetry = plan.Globals.Symmetry;
    var validate = PlanValidator.Validate(plan);
    row.Errors = validate.Count(f => f.Severity == PlanSeverity.Error);
    row.Lint = validate.Count(f => f.Severity == PlanSeverity.Lint);
    row.Findings = validate.Select(f => $"[{f.Severity}]{(f.Rule is null ? "" : " " + f.Rule)} {f.Message}").ToList();
    row.Buffers = plan.Pieces.Count(p => p.Role == PlanRoles.Buffer);
    // findings that actually implicate a buffer id — these should be zero (buffers are non-generating annotations)
    var bufIds = plan.Pieces.Where(p => p.Role == PlanRoles.Buffer).Select(p => p.Id).ToHashSet();
    row.BufferComplaints = validate.Count(f => f.SubjectIds.Any(bufIds.Contains) || bufIds.Any(id => f.Message.Contains($"'{id}'")));
    row.Pieces = plan.Pieces.Count - row.Buffers;
    row.Zones = plan.Zones.Count;
    var xs0 = plan.Pieces.Select(p => p.Rect[0]).Concat(plan.Zones.Select(z => z.Rect[0]));
    var xs1 = plan.Pieces.Select(p => p.Rect[0] + p.Rect[2]).Concat(plan.Zones.Select(z => z.Rect[0] + z.Rect[2]));
    var zs0 = plan.Pieces.Select(p => p.Rect[1]).Concat(plan.Zones.Select(z => z.Rect[1]));
    var zs1 = plan.Pieces.Select(p => p.Rect[1] + p.Rect[3]).Concat(plan.Zones.Select(z => z.Rect[1] + z.Rect[3]));
    row.MinX = xs0.Min(); row.MaxX = xs1.Max(); row.MinZ = zs0.Min(); row.MaxZ = zs1.Max();

    // overlaps among base rects, split into categories; buffer involvement flagged separately
    for (int i = 0; i < plan.Pieces.Count; i++)
        for (int j = i + 1; j < plan.Pieces.Count; j++)
            if (OvArea(plan.Pieces[i].Rect, plan.Pieces[j].Rect) > 0)
            {
                bool buf = plan.Pieces[i].Role == PlanRoles.Buffer || plan.Pieces[j].Role == PlanRoles.Buffer;
                (buf ? row.BufferPieceOv : row.PieceOv).Add($"{plan.Pieces[i].Id} ∩ {plan.Pieces[j].Id}");
            }
    for (int i = 0; i < plan.Zones.Count; i++)
        for (int j = i + 1; j < plan.Zones.Count; j++)
            if (OvArea(plan.Zones[i].Rect, plan.Zones[j].Rect) > 0)
                row.ZoneOv.Add($"{plan.Zones[i].Id} ∩ {plan.Zones[j].Id}");
    foreach (var p in plan.Pieces)
        foreach (var z in plan.Zones)
            if (OvArea(p.Rect, z.Rect) > 0)
            {
                string tag = p.Role == PlanRoles.Buffer ? "buffer-in-zone" : PiecePrefix(p.Id) == "step" ? "step-in-band" : "piece-in-zone";
                row.PieceZoneOv.Add($"{p.Id} ⊂ {z.Id} ({tag})");
            }
    return row;
}

// ── load + render the rot_90 cards ──
var rotCards = new StringBuilder();
foreach (var stem in rotStems)
{
    var path = Path.Combine(teachDir, $"{stem}.plan.json");
    PlanModel? plan = null;
    try { plan = PlanModel.Parse(File.ReadAllText(path)); } catch { }
    var row = CrossCheck(stem, plan);
    report.Add(row);
    if (plan is null) { rotCards.Append(FailCard(stem)); continue; }
    if (plan.Pieces.Any(p => p.Role == PlanRoles.Buffer)) anyBufferHatch = true;
    var svg = BuildSvg(plan, $"buf-{stem}", dimMirror: false, pxPerBlock: 5.2, marginCells: 0.7);
    rotCards.Append(RotCard(stem, svg, row));
}

// ── load + render the wide mirror fold ──
string mirrorSection;
{
    var path = Path.Combine(teachDir, $"{mirrorStem}.plan.json");
    PlanModel? plan = null;
    try { plan = PlanModel.Parse(File.ReadAllText(path)); } catch { }
    var row = CrossCheck(mirrorStem, plan);
    report.Add(row);
    if (plan is null)
        mirrorSection = "<section class=\"fam\"><div class=\"fam-head\"><h2 class=\"fam-title\">mirror-mid-examples</h2></div>" + FailCard(mirrorStem) + "</section>";
    else
    {
        if (plan.Pieces.Any(p => p.Role == PlanRoles.Buffer)) anyBufferHatch = true;
        var svg = BuildSvg(plan, "buf-mirror", dimMirror: true, pxPerBlock: 9.0, marginCells: 1.5);
        mirrorSection = MirrorSection(plan, svg, row);
    }
}

// ── page ──
string legendPieces = string.Join("", new[]
{
    ("frontline-*", CFrontline), ("hub-*", CHub), ("step-*", CStep), ("piece / other", CPieceGen),
}.Select(t => $"<span class=\"lg\"><span class=\"sw\" style=\"background:{t.Item2}\"></span>{Esc(t.Item1)}</span>"));
legendPieces += $"<span class=\"lg\"><span class=\"sw sw--buffer\"></span>buffer (role)</span>";
string legendZones = string.Join("", new[]
{
    ("band-*", CBand), ("bridge-*", CBridge),
}.Select(t => $"<span class=\"lg\"><span class=\"sw sw--zone\" style=\"border-color:{t.Item2};background:{t.Item2}33\"></span>{Esc(t.Item1)}</span>"));

int okPlans = report.Count(r => r.Parsed);
int totalErr = report.Sum(r => r.Errors);
int totalBufferComplaints = report.Sum(r => r.BufferComplaints);

var html = Page(rotCards.ToString(), mirrorSection, legendPieces, legendZones, okPlans, report.Count, totalErr);
var outPath = Path.Combine("tools", "compose", "out", "mid-teaching.html");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, html);

// ── stdout cross-check report ──
Console.WriteLine("── mid teaching gallery — per-plan cross-check ──\n");
Console.WriteLine($"{"plan",-24} {"parse",-5} {"sym",-9} {"err",4} {"lint",5} {"pcs",4} {"zn",4} {"buf",4}  bbox(cells)");
foreach (var r in report)
{
    if (!r.Parsed) { Console.WriteLine($"{r.Stem,-24} {"NO",-5}  — PARSE FAILED"); continue; }
    Console.WriteLine($"{r.Stem,-24} {"yes",-5} {r.Symmetry,-9} {r.Errors,4} {r.Lint,5} {r.Pieces,4} {r.Zones,4} {r.Buffers,4}  x[{r.MinX}..{r.MaxX}] z[{r.MinZ}..{r.MaxZ}]");
}
Console.WriteLine();
foreach (var r in report.Where(r => r.Parsed))
{
    var notes = new List<string>();
    if (r.Findings.Count > 0) notes.AddRange(r.Findings.Select(f => "validator " + f));
    if (r.PieceOv.Count > 0) notes.Add($"piece∩piece: {string.Join(", ", r.PieceOv)}");
    if (r.ZoneOv.Count > 0) notes.Add($"zone∩zone: {string.Join(", ", r.ZoneOv)}");
    if (r.BufferPieceOv.Count > 0) notes.Add($"buffer∩piece (buffer-in-a-gap, expected): {string.Join(", ", r.BufferPieceOv)}");
    if (r.PieceZoneOv.Count > 0) notes.Add($"piece⊂zone: {string.Join(", ", r.PieceZoneOv)}");
    if (notes.Count > 0)
    {
        Console.WriteLine($"  {r.Stem}:");
        foreach (var n in notes) Console.WriteLine($"      {n}");
    }
}
Console.WriteLine();
Console.WriteLine($"plans parsed        : {okPlans}/{report.Count}");
Console.WriteLine($"total validator err : {totalErr}");
Console.WriteLine($"buffer-caused findings (should be 0): {totalBufferComplaints}");
Console.WriteLine($"buffer hatch present in SVGs (rot_90 + mirror): {(anyBufferHatch ? "yes" : "NO")}");
Console.WriteLine($"orange hatch fill markup emitted     : {(html.Contains("url(#buf-") ? "yes" : "NO")}");
// the only permitted http token is the SVG namespace URI (a declaration, never fetched)
string strippedHtml = html.Replace("http://www.w3.org/2000/svg", "");
Console.WriteLine($"external fetch url in output (must be none) : {(strippedHtml.Contains("http://") || strippedHtml.Contains("https://") ? "YES" : "no")}");
Console.WriteLine($"\nwrote {outPath}");

// ── helpers ──
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

static string FailCard(string stem) => $"""
      <article class="card card--fail">
        <div class="card-head"><span class="card-id">{Esc(stem)}</span></div>
        <div class="fail-body">PlanModel.Parse returned null — see cross-check output.</div>
      </article>
""";

string RotCard(string stem, string svg, Row r)
{
    string stat(string label, string val) => $"<span class=\"stat\"><span class=\"stat-v\">{val}</span> {label}</span>";
    var stats = string.Join("<span class=\"stat-dot\">·</span>",
        stat("pcs", r.Pieces.ToString()), stat("zn", r.Zones.ToString()), stat("buf", r.Buffers.ToString()),
        $"<span class=\"stat\"><span class=\"stat-v\">{r.Errors}</span> err</span>");
    return $"""
        <article class="card">
          <div class="card-head"><span class="card-id">{Esc(stem)}</span><span class="card-sym">rot_90 · order 4</span></div>
          <div class="svg-wrap">{svg}</div>
          <div class="card-stats">{stats} <span class="stat-dot">·</span> bbox x[{r.MinX}..{r.MaxX}] z[{r.MinZ}..{r.MaxZ}]</div>
        </article>

    """;
}

string MirrorSection(PlanModel plan, string svg, Row r)
{
    string overlapNote = r.PieceZoneOv.Count > 0
        ? $"{r.PieceZoneOv.Count(o => o.Contains("step-in-band"))} step-in-band + {r.PieceZoneOv.Count(o => o.Contains("buffer-in-zone"))} buffer-in-zone overlaps (intentional)"
        : "no piece⊂zone overlaps";
    return $$"""
      <section class="fam">
        <div class="fam-head">
          <h2 class="fam-title">mirror-mid-examples — mirror_z frontline / mid taxonomy</h2>
          <span class="fam-sub">one wide authored unit folded across <code>z = 0</code>; base labelled, mirror dimmed. Scroll →</span>
          <span class="fam-count">{{r.Pieces}} pieces · {{r.Zones}} zones · {{r.Buffers}} buffers</span>
        </div>
        <div class="figscroll">{{svg}}</div>
        <div class="meta">
          <span>base bbox x <b>[{{r.MinX}}..{{r.MaxX}}]</b> z <b>[{{r.MinZ}}..{{r.MaxZ}}]</b> cells</span>
          <span>fanned by <b>mirror_z</b> (order 2)</span>
          <span>validator <b>{{r.Errors}}</b> err · <b>{{r.Lint}}</b> lint</span>
          <span>{{Esc(overlapNote)}}</span>
        </div>
      </section>
    """;
}

string Page(string rotCards, string mirrorSection, string legendPieces, string legendZones, int okPlans, int total, int totalErr)
{
    const string css = """
      :root{
        --bg-base:#0f172a; --bg-panel:#1e293b; --bg-canvas:#080f1a; --border:#334155;
        --text-muted:#8397b0; --text-secondary:#94a3b8; --text-primary:#cbd5e1;
        --text-bright:#e2e8f0; --text-strong:#ffffff; --accent-light:#60a5fa; --ok:#22c55e; --err:#f87171;
        --mono:ui-monospace, SFMono-Regular, Menlo, "Cascadia Mono", monospace;
        --sans:-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      }
      @media (prefers-color-scheme: light){
        :root{ --bg-base:#f1f5f9; --bg-panel:#ffffff; --border:#cbd5e1; --text-muted:#586780;
          --text-secondary:#475569; --text-primary:#1e293b; --text-bright:#0f172a; --text-strong:#0f172a; --accent-light:#2563eb; }
      }
      *{ box-sizing:border-box; }
      html,body{ margin:0; padding:0; overflow-x:hidden; }
      body{ background:var(--bg-base); color:var(--text-primary); font-family:var(--sans); font-size:14px; line-height:1.5; -webkit-font-smoothing:antialiased; }
      .wrap{ max-width:1440px; margin:0 auto; padding:28px 24px 64px; }
      header.top{ border-bottom:1px solid var(--border); padding-bottom:20px; margin-bottom:22px; }
      .eyebrow{ font-family:var(--mono); font-size:11px; letter-spacing:.14em; text-transform:uppercase; color:var(--accent-light); margin:0 0 6px; }
      h1{ font-size:23px; line-height:1.25; margin:0 0 8px; color:var(--text-strong); font-weight:650; letter-spacing:-.01em; }
      .lede{ margin:0; max-width:88ch; color:var(--text-secondary); font-size:13.5px; }
      .lede code{ font-family:var(--mono); color:var(--text-bright); background:var(--bg-panel); padding:1px 5px; border-radius:3px; font-size:12px; }
      .green{ color:var(--ok); font-weight:600; } .red{ color:var(--err); font-weight:600; }
      .legend{ display:flex; flex-wrap:wrap; gap:8px 18px; margin-top:16px; padding:12px 14px; background:var(--bg-panel); border:1px solid var(--border); border-radius:6px; align-items:center; }
      .legend-lbl{ font-family:var(--mono); font-size:10px; letter-spacing:.1em; text-transform:uppercase; color:var(--text-muted); }
      .lg{ display:inline-flex; align-items:center; gap:6px; font-size:12px; color:var(--text-secondary); font-family:var(--mono); }
      .sw{ width:13px; height:13px; border-radius:2px; flex:none; }
      .sw--zone{ border:1.4px dashed; }
      .sw--buffer{ background:repeating-linear-gradient(45deg,#f2792b 0 1.4px,transparent 1.4px 4px), #f2792b22; border:1.3px dashed #f2792b; }
      .legend-sep{ width:1px; align-self:stretch; background:var(--border); }
      section.fam{ margin-top:30px; }
      .fam-head{ display:flex; align-items:baseline; gap:12px; flex-wrap:wrap; padding-bottom:8px; margin-bottom:14px; border-bottom:1px solid var(--border); }
      .fam-title{ font-size:14px; margin:0; color:var(--text-bright); font-weight:600; font-family:var(--mono); }
      .fam-sub{ font-size:12.5px; color:var(--text-muted); } .fam-sub code{ font-family:var(--mono); color:var(--text-bright); }
      .fam-count{ margin-left:auto; font-family:var(--mono); font-size:11px; color:var(--text-muted); }
      .grid{ display:grid; grid-template-columns:repeat(auto-fill, minmax(260px, 1fr)); gap:16px; align-items:start; }
      .card{ background:var(--bg-panel); border:1px solid var(--border); border-radius:8px; padding:10px 10px 8px; display:flex; flex-direction:column; gap:8px; }
      .card--fail{ border-color:var(--err); }
      .card-head{ display:flex; align-items:baseline; gap:8px; }
      .card-id{ font-family:var(--mono); font-size:12px; color:var(--text-bright); font-weight:600; }
      .card-sym{ margin-left:auto; font-family:var(--mono); font-size:10px; color:var(--text-muted); }
      .svg-wrap{ background:var(--bg-canvas); border:1px solid var(--border); border-radius:5px; overflow:hidden; line-height:0; }
      .svg-wrap svg.fig{ display:block; width:100%; height:auto; }
      .fail-body{ color:var(--err); font-family:var(--mono); font-size:12px; padding:12px 4px; }
      .card-stats{ display:flex; flex-wrap:wrap; align-items:center; gap:3px 6px; font-family:var(--mono); font-size:11px; color:var(--text-muted); font-variant-numeric:tabular-nums; }
      .stat-v{ color:var(--text-bright); font-weight:600; } .stat-dot{ color:var(--border); }
      .figscroll{ margin-top:6px; overflow-x:auto; overflow-y:hidden; border:1px solid var(--border); border-radius:8px; background:var(--bg-canvas); }
      .figscroll .fig{ display:block; }
      .meta{ margin-top:12px; font-family:var(--mono); font-size:11.5px; color:var(--text-muted); display:flex; flex-wrap:wrap; gap:4px 16px; }
      .meta b{ color:var(--text-bright); font-weight:600; }
      footer{ margin-top:34px; padding-top:16px; border-top:1px solid var(--border); font-family:var(--mono); font-size:11px; color:var(--text-muted); }
    """;

    string statusClass = (okPlans == total && totalErr == 0) ? "green" : "red";
    string statusText = $"{okPlans}/{total} parsed · {totalErr} validator error(s)";

    return $$"""
    <!doctype html>
    <html lang="en">
    <head>
      <meta charset="utf-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>Mid teaching plans — rot_90 + mirror_z</title>
      <style>{{css}}</style>
    </head>
    <body>
    <div class="wrap">
      <header class="top">
        <p class="eyebrow">Composer · teaching corpus · mid examples</p>
        <h1>Mid teaching plans — <code>rot_90</code> pinwheels + the <code>mirror_z</code> taxonomy</h1>
        <p class="lede">Hand-authored teaching plans for the composer's mid layer, fanned into their full
        symmetry orbit via <code>PgmStudio.Geom.Symmetry</code> and drawn in the <code>/plan</code> editor's
        visual language. The eight <code>rot_90</code> cards each fan one small example to four images about the
        origin (all solid; base labelled). Below, the wide <code>mirror_z</code> set folds across
        <code>z = 0</code> with the rotation-hole <strong>buffers</strong> annotated. Colour is by type —
        role <code>buffer</code> first (orange hatch), else by id-prefix. Status:
        <span class="{{statusClass}}">{{statusText}}</span>.</p>
        <div class="legend">
          <span class="legend-lbl">Pieces</span>{{legendPieces}}
          <span class="legend-sep"></span>
          <span class="legend-lbl">Zones</span>{{legendZones}}
        </div>
      </header>

      <section class="fam">
        <div class="fam-head">
          <h2 class="fam-title">rot_90 mid examples (×8)</h2>
          <span class="fam-sub">one wedge fanned to four by 90° rotation about the origin; both axes shown</span>
          <span class="fam-count">8 plans</span>
        </div>
        <div class="grid">
    {{rotCards}}    </div>
      </section>

    {{mirrorSection}}

      <footer>Static SVG · self-contained (no external requests) · fanned via PgmStudio.Geom.Symmetry · buffers rendered as the #f2792b diagonal hatch.</footer>
    </div>
    </body>
    </html>
    """;
}

sealed class Row
{
    public string Stem = "";
    public bool Parsed;
    public string Symmetry = "";
    public int Errors, Lint, Pieces, Zones, Buffers, BufferComplaints;
    public int MinX, MaxX, MinZ, MaxZ;
    public List<string> Findings = new();
    public List<string> PieceOv = new();
    public List<string> BufferPieceOv = new();
    public List<string> ZoneOv = new();
    public List<string> PieceZoneOv = new();
}
