#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// showcase: the layout-generation explainer — one designed, self-contained HTML page that walks a real
// composed board through every pipeline stage (the hero strip) and then deep-dives the model underneath:
// boxes, families, slots, bodies + designations, negative space, docking, budget, the mid, the gate.
// Every figure is rendered from the real emitters/composer at generation time — nothing is hand-drawn.
// The walkthrough board is pinned: huge corpus budget (20 players/team, rot_180), seed 10.
// Run from the repo root: dotnet run tools/compose/showcase.cs  →  tools/compose/out/showcase.html
using System.Globalization;
using System.Text;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;
using Sym = PgmStudio.Geom.Symmetry;

// ── palette (editor visual language: plan-doc.js / tokens.css / the board gallery) ─────────────────────
const string BgCanvas = "#080f1a";
const string AxisCol = "#a78bfa";
const string Ink = "#e8eef9";
const string CHub = "#a78bfa";        // box-kind hues (board-gallery language)
const string CSpawn = "#34d399";
const string CWool = "#fbbf24";
const string CFront = "#fb923c";
const string CBand = "#38bdf8";
const string CHole = "#f87171";
const string MkSpawn = "#e0b13c";
const string MkWool = "#e6e6e6";
const string MkStroke = "#1b2333";
// slot hues (the shape-internal layer): designation marks bright, structural slots muted
var slotColor = new Dictionary<string, string>
{
    [ApproachSlots.Entry] = "#58a6ff",
    [ApproachSlots.Room] = "#3fae74",
    [ApproachSlots.Run] = "#8a97a8",
    [ApproachSlots.Bar] = "#6d7f94",
    [ApproachSlots.Leg] = "#9b8cf0",
    [ApproachSlots.EntryRun] = "#45c4e8",
    [ApproachSlots.RoomRun] = "#35c69f",
    [ApproachSlots.EntryBar] = "#7fb3ff",
    [ApproachSlots.RoomBar] = "#57b58a",
};
string SlotCol(string? slot) => slot is not null && slotColor.TryGetValue(slot, out var c) ? c : "#8a97a8";

// ── the walkthrough board: huge corpus budget, seed 10 ─────────────────────────────────────────────────
var request = new ComposeRequest(20, seed: 10);
var stages = Composer.ComposeStages(request);
var env = stages.Envelope;
var unit = stages.Unit;
var part = BoxPartition.Of(unit);
var eval = LayoutEvaluator.Evaluate(stages.Plan, EvaluationProfile.Default);
var order = Sym.Order(env.Symmetry);
var axes = Sym.OrbitAxes(env.Symmetry);
var holeSizes = ClosureAnalysis.HoleSizes(stages.Plan);

var boxById = part.Boxes.ToDictionary(b => b.Id);
var hubBox = part.Boxes.First(b => b.Kind == BoxKind.Hub);

// derived wool families (read back from terrain — the shape deriver, the emitter's mirror)
var woolFamily = new Dictionary<string, ShapeFamily>();
foreach (var boxId in unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Wool).Select(p => p.Box!.Id).Distinct())
{
    var boxPieces = unit.Pieces.Where(p => p.Box?.Id == boxId).ToList();
    var room = boxPieces.FirstOrDefault(p => p.Slot == ApproachSlots.Room);
    if (room is null) continue;
    var filled = new HashSet<(int, int)>();
    var roomCells = new HashSet<(int, int)>();
    foreach (var p in boxPieces)
        for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
            for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++)
            { filled.Add((x, z)); if (p.Id == room.Id) roomCells.Add((x, z)); }
    woolFamily[boxId] = ShapeClassifier.Classify(filled, roomCells).Family;
}

// ── fanned rasters: closure holes (terrain + band seal) and spawn-to-spawn connectivity ────────────────
var pieceCells = new HashSet<(int, int)>();
foreach (var p in unit.Pieces)
    for (var k = 0; k < order; k++)
        RasterFan(p.Rect, k, pieceCells);
var bandCells = new HashSet<(int, int)>();
for (var k = 0; k < order; k++)
    RasterFan(stages.Mid.BandRect, k, bandCells);

var cover = new HashSet<(int, int)>(pieceCells); cover.UnionWith(bandCells);
int cMinX = cover.Min(c => c.Item1) - 1, cMaxX = cover.Max(c => c.Item1) + 1;
int cMinZ = cover.Min(c => c.Item2) - 1, cMaxZ = cover.Max(c => c.Item2) + 1;
var outside = new HashSet<(int, int)>();
var q0 = new Queue<(int, int)>();
outside.Add((cMinX, cMinZ)); q0.Enqueue((cMinX, cMinZ));
while (q0.Count > 0)
{
    var (x, z) = q0.Dequeue();
    foreach (var n in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
        if (n.Item1 >= cMinX && n.Item1 <= cMaxX && n.Item2 >= cMinZ && n.Item2 <= cMaxZ
            && !cover.Contains(n) && outside.Add(n)) q0.Enqueue(n);
}
var holeCells = new HashSet<(int, int)>();
for (var x = cMinX; x <= cMaxX; x++)
    for (var z = cMinZ; z <= cMaxZ; z++)
        if (!cover.Contains((x, z)) && !outside.Contains((x, z))) holeCells.Add((x, z));

var spawnPiece = unit.Pieces.First(p => p.Role == PlanRoles.Spawn);
var spawnCentres = Enumerable.Range(0, order)
    .Select(k => FanRect(spawnPiece.Rect, k))
    .Select(r => (r[0] + r[2] / 2, r[1] + r[3] / 2)).ToList();
var reach = new HashSet<(int, int)>();
if (cover.Contains(spawnCentres[0]))
{
    var q1 = new Queue<(int, int)>();
    reach.Add(spawnCentres[0]); q1.Enqueue(spawnCentres[0]);
    while (q1.Count > 0)
    {
        var (x, z) = q1.Dequeue();
        foreach (var n in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            if (cover.Contains(n) && reach.Add(n)) q1.Enqueue(n);
    }
}
var connected = spawnCentres.All(reach.Contains);

int[] FanRect(int[] r, int k)
{
    if (k == 0) return r;
    (double x, double z)[] corners = [(r[0], r[1]), (r[0], r[1] + r[3]), (r[0] + r[2], r[1]), (r[0] + r[2], r[1] + r[3])];
    var pts = corners.Select(c => Sym.Apply(c.x, c.z, axes[k - 1], 0, 0)).ToList();
    var x1 = (int)Math.Round(pts.Min(p => p.X));
    var z1 = (int)Math.Round(pts.Min(p => p.Z));
    return [x1, z1, (int)Math.Round(pts.Max(p => p.X)) - x1, (int)Math.Round(pts.Max(p => p.Z)) - z1];
}
void RasterFan(int[] r, int k, HashSet<(int, int)> into)
{
    var f = FanRect(r, k);
    for (var x = f[0]; x < f[0] + f[2]; x++)
        for (var z = f[1]; z < f[1] + f[3]; z++) into.Add((x, z));
}

// ── board-frame renderer (the hero strip) ──────────────────────────────────────────────────────────────
// One fixed camera over the fanned board; each stage chooses what is HOT (full strength), what is faint
// context, and which annotation layers to draw on top.
var allRects = new List<int[]>();
foreach (var p in unit.Pieces) for (var k = 0; k < order; k++) allRects.Add(FanRect(p.Rect, k));
for (var k = 0; k < order; k++) allRects.Add(FanRect(stages.Mid.BandRect, k));
int bMinX = allRects.Min(r => r[0]) - 2, bMinZ = allRects.Min(r => r[1]) - 2;
int bMaxX = allRects.Max(r => r[0] + r[2]) + 2, bMaxZ = allRects.Max(r => r[1] + r[3]) + 2;
const double S = 13.0;
double VW = (bMaxX - bMinX) * S, VH = (bMaxZ - bMinZ) * S;
double PX(double x) => (x - bMinX) * S;
double PZ(double z) => (z - bMinZ) * S;

string KindCol(BoxKind? k) => k switch
{
    BoxKind.Hub => CHub, BoxKind.Spawn => CSpawn, BoxKind.Wool => CWool,
    BoxKind.Frontline => CFront, _ => CBand,
};

string Frame(
    Func<GrownPiece, bool>? hot = null,          // full-strength pieces (null = all)
    double ghost = 0.10,                          // opacity of non-hot unit pieces
    double fanMul = 0.55,                         // fanned image strength relative to its unit tier
    bool showFan = true,
    double bandOp = 0.0,                          // 0 hides the band
    Func<Box, bool>? boxHot = null,               // dashed box outlines to draw at full strength
    double boxGhost = 0.0,                        // outline opacity for the other boxes (0 = none)
    bool joints = false,
    double markerOp = 0.0,
    Func<GrownPiece, bool>? slotLabels = null,
    bool holes = false,
    bool crossing = false,
    bool envelopeNotes = false,
    List<(double X, double Z, string Text, string Color)>? notes = null)
{
    var svg = new StringBuilder();
    svg.Append($"<svg viewBox='0 0 {N(VW)} {N(VH)}' xmlns='http://www.w3.org/2000/svg' class='board' role='img'>");
    svg.Append($"<rect width='{N(VW)}' height='{N(VH)}' fill='{BgCanvas}'/>");

    // cell grid + the symmetry centre
    svg.Append("<g>");
    for (var gx = bMinX; gx <= bMaxX; gx++)
        svg.Append($"<line x1='{N(PX(gx))}' y1='0' x2='{N(PX(gx))}' y2='{N(VH)}' stroke='{AxisCol}' stroke-opacity='0.07' stroke-width='0.6'/>");
    for (var gz = bMinZ; gz <= bMaxZ; gz++)
        svg.Append($"<line x1='0' y1='{N(PZ(gz))}' x2='{N(VW)}' y2='{N(PZ(gz))}' stroke='{AxisCol}' stroke-opacity='0.07' stroke-width='0.6'/>");
    svg.Append("</g>");
    svg.Append($"<line x1='{N(PX(0))}' y1='0' x2='{N(PX(0))}' y2='{N(VH)}' stroke='{AxisCol}' stroke-opacity='0.35' stroke-width='1'/>");
    svg.Append($"<line x1='0' y1='{N(PZ(0))}' x2='{N(VW)}' y2='{N(PZ(0))}' stroke='{AxisCol}' stroke-opacity='0.5' stroke-width='1.2'/>");
    svg.Append($"<circle cx='{N(PX(0))}' cy='{N(PZ(0))}' r='{N(S * 0.4)}' fill='none' stroke='{AxisCol}' stroke-opacity='0.6' stroke-width='1.2'/>");

    if (crossing)
    {
        var h = stages.Crossing.HalfGapCells;
        svg.Append($"<rect x='0' y='{N(PZ(-h))}' width='{N(VW)}' height='{N(2 * h * S)}' fill='{CBand}' fill-opacity='0.10'/>");
        svg.Append($"<line x1='0' y1='{N(PZ(-h))}' x2='{N(VW)}' y2='{N(PZ(-h))}' stroke='{CBand}' stroke-opacity='0.7' stroke-width='1' stroke-dasharray='5 4'/>");
        svg.Append($"<line x1='0' y1='{N(PZ(h))}' x2='{N(VW)}' y2='{N(PZ(h))}' stroke='{CBand}' stroke-opacity='0.7' stroke-width='1' stroke-dasharray='5 4'/>");
    }

    if (envelopeNotes)
    {
        double ux0 = PX(env.UnitMinX), uz0 = PZ(env.UnitMinZ);
        double uw = (env.UnitMaxX - env.UnitMinX) * S, uh = (env.UnitMaxZ - env.UnitMinZ) * S;
        svg.Append($"<rect x='{N(ux0)}' y='{N(uz0)}' width='{N(uw)}' height='{N(uh)}' fill='none' stroke='{Ink}' stroke-opacity='0.35' stroke-width='1' stroke-dasharray='2 4'/>");
    }

    // band (all orbit images)
    if (bandOp > 0)
        for (var k = 0; k < order; k++)
        {
            var b = FanRect(stages.Mid.BandRect, k);
            svg.Append($"<rect x='{N(PX(b[0]))}' y='{N(PZ(b[1]))}' width='{N(b[2] * S)}' height='{N(b[3] * S)}' "
                + $"fill='{CBand}' fill-opacity='{N(0.20 * bandOp)}' stroke='{CBand}' stroke-opacity='{N(0.7 * bandOp)}' "
                + "stroke-width='1.2' stroke-dasharray='6 4'/>");
        }

    // pieces (unit tier then fanned tier)
    foreach (var p in unit.Pieces)
    {
        var col = KindCol(p.Box?.Kind);
        var isRoom = p.Role != PlanRoles.Piece;
        var tier = hot is null || hot(p) ? 1.0 : ghost;
        for (var k = 0; k < order; k++)
        {
            if (k > 0 && !showFan) break;
            var r = FanRect(p.Rect, k);
            var op = tier * (k == 0 ? 1.0 : fanMul) * (isRoom ? 0.92 : 0.55);
            var sop = tier * (k == 0 ? 1.0 : fanMul);
            svg.Append($"<rect x='{N(PX(r[0]))}' y='{N(PZ(r[1]))}' width='{N(r[2] * S)}' height='{N(r[3] * S)}' rx='1.5' "
                + $"fill='{col}' fill-opacity='{N(op)}' stroke='{col}' stroke-opacity='{N(sop)}' stroke-width='1'/>");
        }
    }

    // closure holes
    if (holes)
        foreach (var (hx, hz) in holeCells)
            svg.Append($"<rect x='{N(PX(hx))}' y='{N(PZ(hz))}' width='{N(S)}' height='{N(S)}' fill='{CHole}' fill-opacity='0.32'/>");

    // box outlines
    foreach (var b in part.Boxes)
    {
        var opb = boxHot is not null && boxHot(b) ? 1.0 : boxGhost;
        if (opb <= 0) continue;
        var col = KindCol(b.Kind);
        for (var k = 0; k < order; k++)
        {
            if (k > 0 && !showFan) break;
            var r = FanRect(b.Rect, k);
            var op = opb * (k == 0 ? 1.0 : 0.4);
            svg.Append($"<rect x='{N(PX(r[0]) - 1.5)}' y='{N(PZ(r[1]) - 1.5)}' width='{N(r[2] * S + 3)}' height='{N(r[3] * S + 3)}' "
                + $"fill='none' stroke='{col}' stroke-opacity='{N(op)}' stroke-width='1.4' stroke-dasharray='5 3'/>");
        }
    }

    // hub joints: the shared-edge interface intervals, labelled with their widths
    if (joints)
        foreach (var j in part.Joints)
        {
            var a = boxById[j.BoxA]; var b = boxById[j.BoxB];
            var iface = BoxPartition.SharedEdge(a.Rect, b.Rect);
            if (iface is null) continue;
            double x1, z1, x2, z2;
            switch (iface.Edge)
            {
                case BoxEdge.Top: x1 = a.Rect[0] + iface.Start; x2 = x1 + iface.WidthCells; z1 = z2 = a.Rect[1]; break;
                case BoxEdge.Bottom: x1 = a.Rect[0] + iface.Start; x2 = x1 + iface.WidthCells; z1 = z2 = a.Rect[1] + a.Rect[3]; break;
                case BoxEdge.Left: z1 = a.Rect[1] + iface.Start; z2 = z1 + iface.WidthCells; x1 = x2 = a.Rect[0]; break;
                default: z1 = a.Rect[1] + iface.Start; z2 = z1 + iface.WidthCells; x1 = x2 = a.Rect[0] + a.Rect[2]; break;
            }
            svg.Append($"<line x1='{N(PX(x1))}' y1='{N(PZ(z1))}' x2='{N(PX(x2))}' y2='{N(PZ(z2))}' "
                + $"stroke='{Ink}' stroke-opacity='0.95' stroke-width='3' stroke-linecap='round'/>");
            var mx = PX((x1 + x2) / 2) + (iface.Edge == BoxEdge.Left ? -13 : iface.Edge == BoxEdge.Right ? 13 : 0);
            var mz = PZ((z1 + z2) / 2) + (iface.Edge == BoxEdge.Top ? -9 : iface.Edge == BoxEdge.Bottom ? 11 : 3);
            svg.Append(Halo(mx, mz, $"{iface.WidthCells}c", Ink, 10.5));
        }

    // markers
    if (markerOp > 0)
    {
        var pieceById = unit.Pieces.ToDictionary(p => p.Id);
        foreach (var w in unit.Wools)
        {
            var pc = pieceById[w.Piece];
            double wx = pc.Rect[0] + w.At[0], wz = pc.Rect[1] + w.At[1];
            for (var k = 0; k < order; k++)
            {
                var (mx, mz) = k == 0 ? (wx, wz) : Sym.Apply(wx, wz, axes[k - 1], 0, 0);
                svg.Append($"<rect x='{N(PX(mx) - 4.5)}' y='{N(PZ(mz) - 4.5)}' width='9' height='9' rx='1.5' "
                    + $"fill='{MkWool}' fill-opacity='{N(markerOp)}' stroke='{MkStroke}' stroke-width='1'/>");
            }
        }
        {
            var pc = pieceById[unit.Spawn.Piece];
            double sx = pc.Rect[0] + unit.Spawn.At[0], sz = pc.Rect[1] + unit.Spawn.At[1];
            (double dx, double dz) = unit.Spawn.Facing switch
            { "back" => (0.0, 1.0), "left" => (-1.0, 0.0), "right" => (1.0, 0.0), _ => (0.0, -1.0) };
            for (var k = 0; k < order; k++)
            {
                var (mx, mz) = k == 0 ? (sx, sz) : Sym.Apply(sx, sz, axes[k - 1], 0, 0);
                var (fx, fz) = k == 0 ? (dx, dz) : Sym.Apply(dx, dz, axes[k - 1], 0, 0);
                svg.Append($"<circle cx='{N(PX(mx))}' cy='{N(PZ(mz))}' r='5' fill='{MkSpawn}' "
                    + $"fill-opacity='{N(markerOp)}' stroke='{MkStroke}' stroke-width='1'/>");
                svg.Append($"<line x1='{N(PX(mx))}' y1='{N(PZ(mz))}' x2='{N(PX(mx) + fx * 9)}' y2='{N(PZ(mz) + fz * 9)}' "
                    + $"stroke='{MkStroke}' stroke-opacity='{N(markerOp)}' stroke-width='1.8'/>");
            }
        }
    }

    // slot labels on the hot pieces — staggered so labels on adjacent small pieces never collide
    if (slotLabels is not null)
    {
        var li = 0;
        foreach (var p in unit.Pieces.Where(p => p.Slot is not null && slotLabels(p)))
        {
            var dy = li++ % 2 == 0 ? -3.5 : 7.5;
            svg.Append(Halo(PX(p.Rect[0] + p.Rect[2] / 2.0), PZ(p.Rect[1] + p.Rect[3] / 2.0) + dy, p.Slot!, Ink, 8.5));
        }
    }

    if (notes is not null)
        foreach (var (nx, nz, text, color) in notes)
            svg.Append(Halo(PX(nx), PZ(nz), text, color, 10.5));

    svg.Append("</svg>");
    return svg.ToString();
}

string Halo(double px, double pz, string text, string color, double size) =>
    $"<text x='{N(px)}' y='{N(pz)}' font-size='{N(size)}' fill='{color}' text-anchor='middle' "
    + $"font-family='ui-monospace,Menlo,monospace' font-weight='600' paint-order='stroke' stroke='{BgCanvas}' "
    + $"stroke-width='3' stroke-linejoin='round'>{Esc(text)}</text>";

// ── the strip frames ───────────────────────────────────────────────────────────────────────────────────
bool In(GrownPiece p, string boxId) => p.Box?.Id == boxId;
var frames = new List<(string Num, string Title, string Svg, string Caption)>
{
    ("01", "The request",
        Frame(hot: _ => false, ghost: 0.07, fanMul: 1.0, envelopeNotes: true,
            notes: [(0, (double)bMinZ + 1.2, $"land budget {env.LandPerTeam:0} blocks per team · cell = {env.Cell} blocks", Ink),
                    (0, (double)bMaxZ - 1.0, $"symmetry {env.Symmetry} — one unit is authored, the orbit fans the rest", AxisCol)]),
        "Everything starts from one number: <b>20 players per team</b>. The envelope derives the budget from it — "
        + $"<b>{env.LandPerTeam:0} blocks of walkable land per team</b> on a 5-block cell grid — and fixes the frame: "
        + "<code>rot_180</code> symmetry about the centre, a unit window one team's half must stay inside (dotted). "
        + "The faint picture behind is where this walkthrough ends; every stage from here is sampled under law, "
        + "reproducible from <b>seed 10</b>."),

    ("02", "The crossing arithmetic",
        Frame(hot: _ => false, ghost: 0.07, fanMul: 1.0, crossing: true,
            notes: [(0, 0.0 - 3.4, $"half-gap {stages.Crossing.HalfGapCells} cells → a {stages.Crossing.HalfGapCells * 2 * env.Cell}-block void between the fronts", CBand)]),
        "Before anything is placed, the mid's arithmetic is fixed: a uniform <b>20-block void</b> spanning the axis "
        + "(10 blocks per side). This is the crossing the whole map will funnel into — and it is the <i>axis margin</i> "
        + "the team unit must keep back from. The mid is not carved out of terrain later; the gap is reserved first "
        + "and the unit grows behind it."),

    ("03", "The boxes",
        Frame(hot: _ => false, ghost: 0.16, fanMul: 0.35, boxHot: _ => true, markerOp: 0,
            notes: [(hubBox.Rect[0] + hubBox.Rect[2] / 2.0, hubBox.Rect[1] - 0.8, "hub", CHub),
                    (boxById["spawn"].Rect[0] + boxById["spawn"].Rect[2] / 2.0, boxById["spawn"].Rect[1] + boxById["spawn"].Rect[3] + 1.2, "spawn · back", CSpawn),
                    (boxById["wool-a"].Rect[0] + boxById["wool-a"].Rect[2] / 2.0, boxById["wool-a"].Rect[1] - 0.8, "wool-a · left", CWool),
                    (boxById["wool-b"].Rect[0] + boxById["wool-b"].Rect[2] / 2.0, boxById["wool-b"].Rect[1] - 0.8, "wool-b · right", CWool),
                    (boxById["frontline"].Rect[0] + boxById["frontline"].Rect[2] / 2.0, boxById["frontline"].Rect[1] - 0.8, "frontline · front", CFront)]),
        "The budget draws a partition of <b>typed boxes</b> — envelopes, not fill targets: contents must touch the "
        + "edges and stay connected, but need not fill them solid. The allocator samples the <i>placement plan</i> "
        + "(which hub side each neighbour takes: spawn on the back, a wool left and right, the frontline reserved the "
        + "front) and sizes each box from its budget share. Wool-b's share is big enough to afford a rich shape; "
        + "wool-a's buys a compact lane. <b>“No shape fits” is a signal, not a failure</b> — an over-constrained box "
        + "is answered by changing the box."),

    ("04", "The hub emits first",
        Frame(hot: p => In(p, "hub"), ghost: 0.12, fanMul: 0.3, boxHot: b => b.Kind == BoxKind.Hub,
            joints: true, slotLabels: p => In(p, "hub"),
            notes: [(hubBox.Rect[0] + hubBox.Rect[2] / 2.0, hubBox.Rect[1] + hubBox.Rect[3] / 2.0, "ring", CHub)]),
        "The hub is the <b>constraint source</b>, so it fills first. Here the sampled form is a <b>Ring</b> — two bars "
        + "and two legs around an enclosed hole (a big square-ish hub prefers negative space to solid area). Its edges "
        + "publish <b>offers</b>: on each free run, where a neighbour may dock and at what width. The bright ticks are "
        + "the four joints the allocator seated, labelled with their interface widths in cells — each neighbour will "
        + "build to exactly the width <i>its own joint</i> was granted. The hub dictates; the neighbours conform."),

    ("05", "The spawn consumes its grant",
        Frame(hot: p => In(p, "spawn"), ghost: 0.12, fanMul: 0.3, boxHot: b => b.Kind == BoxKind.Spawn,
            slotLabels: p => In(p, "spawn"), markerOp: 1.0),
        "The spawn is the second box kind — the same emit machinery as a wool, deliberately <b>un-escalated</b>: its "
        + "menu is just {I, L}. Here an <b>I</b>: an <code>entry</code> docking the hub's back edge at the granted "
        + "width, and a <code>room</code> terminal capping it — the spawn platform, its marker facing the enemy. "
        + "Simple by law: you leave spawn through one readable mouth, and the room never touches the fight."),

    ("06", "Wool a — the compact lane",
        Frame(hot: p => In(p, "wool-a"), ghost: 0.12, fanMul: 0.3, boxHot: b => b.Id == "wool-a",
            slotLabels: p => In(p, "wool-a"), markerOp: 1.0),
        $"The first wool box fills with the un-escalated approach: an <b>{woolFamily.GetValueOrDefault("wool-a")}</b> — "
        + "an <code>entry</code> lane at the hub's granted w2 (the wool lane is always 2 cells, never the map's wider "
        + "lane class) capped by the <code>room</code> holding the wool. The budget decides the lane's depth; a lane "
        + "that would run too long tucks its room to the side instead (the wool length rule). This is the baseline "
        + "every richer family escalates from."),

    ("07", "Wool b — the escalation",
        Frame(hot: p => In(p, "wool-b"), ghost: 0.12, fanMul: 0.3, boxHot: b => b.Id == "wool-b",
            slotLabels: p => In(p, "wool-b"), markerOp: 1.0),
        $"The second wool drew a rich shape: a <b>{woolFamily.GetValueOrDefault("wool-b")}</b>-family staple — two "
        + "<code>entry</code> legs off the hub's right edge meeting a <code>bar</code>, with the wool lifted onto its "
        + "own <code>room-run</code> stub (the stub is exactly what makes it an H rather than a U). The void between "
        + "the legs is a <b>bay</b>: defenders rotate around it, attackers choose a leg. Family identity is the turn "
        + "count and the wool's seating, read width-free — a thick leg is a wide spot, never a different shape."),

    ("08", "The frontline join",
        Frame(hot: p => In(p, "frontline"), ghost: 0.12, fanMul: 0.3, boxHot: b => b.Kind == BoxKind.Frontline,
            slotLabels: p => In(p, "frontline"),
            notes: [(0, boxById["frontline"].Rect[1] - 0.8, "face → axis", CFront)]),
        "The frontline is a <b>join, not a placement</b>. Its body docks the hub with its spine and turns its "
        + "<b>arm-tips toward the axis as the face</b> — the edge the mid will meet. Here the sampled form is the "
        + "<b>twin</b>: two legs with a recess between them, so the face offers two separate intervals rather than one "
        + "wide bar. Where the fanned images meet decides the map's length — the frontline's position is an "
        + "<i>output</i> of how much each half generated, never an input."),

    ("09", "Fan, then the mid",
        Frame(fanMul: 0.8, bandOp: 1.0, markerOp: 1.0),
        "Symmetry fans the authored unit into its orbit image, and the mid is carved as <b>one clean band</b> spanning "
        + "the axis: laterally it spans exactly the hull of the opposing faces (no slack), in depth it docks "
        + "<b>flush</b> against them — zero overlap, zero gap. The band is a <i>build region</i>: no terrain, only the "
        + "void players must bridge under fire. It must clear every wool by two full cells across all images — a wool "
        + "the mid could bridge straight to would erase the map's direction of play."),

    ("10", "The gate",
        Frame(fanMul: 0.8, bandOp: 0.6, markerOp: 1.0, holes: true,
            notes: [((double)bMinX + 5.6, (double)bMinZ + 1.2, $"score {eval.Score:0.0} · {(connected ? "connected" : "disconnected")}", Ink)]),
        $"Every attempt must pass the evaluator's <b>hard gate</b> — this one did (all {eval.Terms.Count(t => t.Kind == TermKind.Hard)} hard terms clean, "
        + "the two teams connected through the band). The tinted voids are the board's <b>closure holes</b> "
        + $"({string.Join(", ", holeSizes)} cells): the ring hub's void, the twin frontline's recess sealed by the flush "
        + "band, and the H wool's bay — all deliberate negative space, none ringed by a wool. The full evaluation also "
        + $"scores the soft feel terms; this board reads <b>{eval.Score:0.0}</b>, driven by "
        + "<code>spawn-wool-ratio</code> — one wool sits much closer to its spawn than the other, a guarded/abandoned "
        + "imbalance the rules flag but the gate tolerates. That verdict is the composer's taste, made legible."),
};

// ── mini-figure renderer (deep dives) ──────────────────────────────────────────────────────────────────
string Mini(IEnumerable<MiniEl> els, double? forceScale = null, double pad = 0.8, List<(double X, double Z, string Text, string Color)>? notes = null)
{
    var list = els.ToList();
    double minX = list.Min(e => e.X) - pad, minZ = list.Min(e => e.Z) - pad;
    double maxX = list.Max(e => e.X + e.W) + pad, maxZ = list.Max(e => e.Z + e.H) + pad;
    double s = forceScale ?? Math.Min(230.0 / (maxX - minX), 210.0 / (maxZ - minZ));
    s = Math.Min(s, 26);
    double vw = (maxX - minX) * s, vh = (maxZ - minZ) * s;
    var svg = new StringBuilder($"<svg viewBox='0 0 {N(vw)} {N(vh)}' xmlns='http://www.w3.org/2000/svg' class='mini' role='img'>");
    svg.Append($"<rect width='{N(vw)}' height='{N(vh)}' fill='{BgCanvas}'/>");
    for (var gx = (int)Math.Floor(minX); gx <= (int)Math.Ceiling(maxX); gx++)
        svg.Append($"<line x1='{N((gx - minX) * s)}' y1='0' x2='{N((gx - minX) * s)}' y2='{N(vh)}' stroke='{AxisCol}' stroke-opacity='0.08' stroke-width='0.6'/>");
    for (var gz = (int)Math.Floor(minZ); gz <= (int)Math.Ceiling(maxZ); gz++)
        svg.Append($"<line x1='0' y1='{N((gz - minZ) * s)}' x2='{N(vw)}' y2='{N((gz - minZ) * s)}' stroke='{AxisCol}' stroke-opacity='0.08' stroke-width='0.6'/>");
    foreach (var e in list)
    {
        var dash = e.Dash is null ? "" : $" stroke-dasharray='{e.Dash}'";
        svg.Append($"<rect x='{N((e.X - minX) * s)}' y='{N((e.Z - minZ) * s)}' width='{N(e.W * s)}' height='{N(e.H * s)}' rx='1.5' "
            + $"fill='{e.Fill}' fill-opacity='{N(e.FillOp)}' stroke='{e.Stroke}' stroke-opacity='{N(e.StrokeOp)}' stroke-width='{N(e.StrokeW)}'{dash}/>");
        if (e.Label is not null)
            svg.Append(HaloAt((e.X + e.W / 2 - minX) * s, (e.Z + e.H / 2 - minZ) * s + 3.2, e.Label, Ink, Math.Min(10.5, e.W * s / 3.2)));
    }
    if (notes is not null)
        foreach (var (nx, nz, text, color) in notes)
            svg.Append(HaloAt((nx - minX) * s, (nz - minZ) * s, text, color, 10));
    svg.Append("</svg>");
    return svg.ToString();

    string HaloAt(double px, double pz, string text, string color, double size) =>
        $"<text x='{N(px)}' y='{N(pz)}' font-size='{N(size)}' fill='{color}' text-anchor='middle' "
        + $"font-family='ui-monospace,Menlo,monospace' font-weight='600' paint-order='stroke' stroke='{BgCanvas}' "
        + $"stroke-width='3' stroke-linejoin='round'>{Esc(text)}</text>";
}

MiniEl SlotEl((int[] Rect, string Slot) p, bool label = true) =>
    new(p.Rect[0], p.Rect[1], p.Rect[2], p.Rect[3], SlotCol(p.Slot), 0.6, SlotCol(p.Slot), 1, 1.1, label ? p.Slot : null);

// a family figure: emit → slot-coloured pieces + room + wool marker + vacancy tints. The box is the
// composer's own sizing — the family's minimum box at the w2 wool lane (what the allocator seats via
// MouthBox) — so the proportions are production proportions, and one shared scale keeps them comparable.
const double FamilyScale = 19;
string FamilyFig(ShapeFamily fam, int cw, RoomPlacement placement = RoomPlacement.Inline,
    bool woolAtEnd = false, WoolBox? boxOverride = null)
{
    var box = boxOverride ?? MinWoolBox(fam, cw, placement, woolAtEnd);
    var a = WoolBoxEmitter.Emit(fam, box, cw, roomPlacement: placement, woolAtEnd: woolAtEnd);
    var els = new List<MiniEl>();
    var voidNotes = new List<(double X, double Z, string Text, string Color)>();
    foreach (var v in a.Vacancies.Where(v => v.Kind != "notch"))
    {
        els.Add(new(v.Rect[0], v.Rect[1], v.Rect[2], v.Rect[3], CHole, 0.14, CHole, 0.55, 1, null, "3 3"));
        voidNotes.Add((v.Rect[0] + v.Rect[2] / 2.0, v.Rect[1] + 0.68, v.Kind, CHole));
    }
    foreach (var t in a.Terrain)
        els.Add(new(t.Rect[0], t.Rect[1], t.Rect[2], t.Rect[3], SlotCol(t.Slot), 0.6, SlotCol(t.Slot), 1, 1.1, t.Slot));
    var r = a.WoolRoom;
    els.Add(new(r.Rect[0], r.Rect[1], r.Rect[2], r.Rect[3], SlotCol(ApproachSlots.Room), 0.75, SlotCol(ApproachSlots.Room), 1, 1.2, null));
    var wx = r.Rect[0] + a.At[0]; var wz = r.Rect[1] + a.At[1];
    els.Add(new(wx - 0.32, wz - 0.32, 0.64, 0.64, MkWool, 0.95, MkStroke, 1, 1, null));
    voidNotes.Add((r.Rect[0] + r.Rect[2] / 2.0, r.Rect[1] + 0.62, "room", Ink));
    return Mini(els, forceScale: FamilyScale, notes: voidNotes);
}

// the family's minimum box in its own emit frame — the exact fit gate the allocator sizes against
WoolBox MinWoolBox(ShapeFamily fam, int cw, RoomPlacement placement = RoomPlacement.Inline, bool woolAtEnd = false)
{
    var (w, h) = ShapeEmitter.MinBox(fam, cw, placement, woolAtEnd: woolAtEnd);
    return new WoolBox(0, 0, w, h);
}

// a tile-grid figure from the doc's character glyphs: t terrain · v void · w wool · h host · b build
string TileFig(string[] rows, double scale = 22)
{
    var els = new List<MiniEl>();
    for (var z = 0; z < rows.Length; z++)
        for (var x = 0; x < rows[z].Length; x++)
        {
            var c = rows[z][x];
            if (c == '.') continue;
            var el = c switch
            {
                't' => new MiniEl(x, z, 1, 1, "#7c8899", 0.55, "#7c8899", 0.9, 1, null),
                'w' => new MiniEl(x, z, 1, 1, MkWool, 0.9, MkStroke, 1, 1, null),
                'v' => new MiniEl(x, z, 1, 1, "#000000", 0, AxisCol, 0.35, 0.8, null, "2 3"),
                'h' => new MiniEl(x, z, 1, 1, CHub, 0.5, CHub, 0.9, 1, null),
                'b' => new MiniEl(x, z, 1, 1, CBand, 0.35, CBand, 0.8, 1, null, "3 2"),
                _ => new MiniEl(x, z, 1, 1, "#333", 0.3, "#333", 0.5, 1, null),
            };
            els.Add(el);
        }
    return Mini(els, forceScale: scale, pad: 0.45);
}

// a body figure straight off the body emitter
string BodyFig(ShapeBody body, bool tintVoids = true)
{
    var els = new List<MiniEl>();
    if (tintVoids)
        foreach (var v in body.Vacancies)
            els.Add(new(v.Rect[0], v.Rect[1], v.Rect[2], v.Rect[3], CHole, 0.13, CHole, 0.5, 1, v.Kind, "3 3"));
    foreach (var p in body.Pieces) els.Add(SlotEl(p));
    return Mini(els);
}

// a hub-form figure: fill on a canonical box, offers drawn as bright edge intervals
string HubFig(CompoundRead form, int w, int h)
{
    var box = new Box("hub", BoxKind.Hub, [0, 0, w, h], 0, form);
    var hub = HubBoxEmitter.Fill(box, form, FillProfiles.HubWallCells);
    if (hub is null) return "<div class='fig-missing'>too small</div>";
    var els = new List<MiniEl> { new(0, 0, w, h, "#000", 0, CHub, 0.5, 1.2, null, "4 3") };
    foreach (var p in hub.Pieces) els.Add(new(p.Rect[0], p.Rect[1], p.Rect[2], p.Rect[3], CHub, 0.5, CHub, 1, 1.1, p.Slot));
    foreach (var o in hub.Offers)
    {
        double x1, z1, x2, z2;
        switch (o.Edge)
        {
            case BoxEdge.Top: x1 = o.Interval.Start; x2 = x1 + o.Interval.LengthCells; z1 = z2 = 0; break;
            case BoxEdge.Bottom: x1 = o.Interval.Start; x2 = x1 + o.Interval.LengthCells; z1 = z2 = h; break;
            case BoxEdge.Left: z1 = o.Interval.Start; z2 = z1 + o.Interval.LengthCells; x1 = x2 = 0; break;
            default: z1 = o.Interval.Start; z2 = z1 + o.Interval.LengthCells; x1 = x2 = w; break;
        }
        els.Add(new(Math.Min(x1, x2) - 0.12, Math.Min(z1, z2) - 0.12,
            Math.Abs(x2 - x1) + 0.24, Math.Abs(z2 - z1) + 0.24, "#ffffff", 0.85, "#ffffff", 0, 0, null));
    }
    return Mini(els);
}

// a frontline-form figure: canonical frame (spine top, face bottom), face offers bright
string FrontFig(CompoundRead form, int w, int h, int cw, OfferGrouping grouping)
{
    var box = new Box("front", BoxKind.Frontline, [0, 0, w, h], 0, form);
    var ef = FrontlineBoxEmitter.Fill(box, form, cw, grouping, BoxEdge.Top);
    if (ef is null) return "<div class='fig-missing'>too small</div>";
    var els = new List<MiniEl> { new(0, 0, w, h, "#000", 0, CFront, 0.5, 1.2, null, "4 3") };
    foreach (var p in ef.Pieces) els.Add(new(p.Rect[0], p.Rect[1], p.Rect[2], p.Rect[3], CFront, 0.5, CFront, 1, 1.1, p.Slot));
    foreach (var o in ef.FaceOffers)
    {
        double x1 = o.Interval.Start, x2 = x1 + o.Interval.LengthCells;
        double z = o.Edge == BoxEdge.Bottom ? h : 0;
        els.Add(new(x1 - 0.12, z - 0.12, x2 - x1 + 0.24, 0.24, "#ffffff", 0.85, "#ffffff", 0, 0, null));
    }
    return Mini(els, notes: [(w / 2.0, h + 0.55, "face → mid", CFront)]);
}

// ── deep-dive figure sets ──────────────────────────────────────────────────────────────────────────────
var failures = new List<string>();
string Guard(Func<string> render, string name)
{
    try { return render(); }
    catch (Exception ex) { failures.Add($"{name}: {ex.GetType().Name} {ex.Message}"); return "<div class='fig-missing'>render failed</div>"; }
}

// the nine approach families, each at the composer's own sizing: the minimum box the allocator seats
// (MouthBox at the w2 wool lane), the budget I at its capped lane depth. One shared scale, so the
// relative footprints read true. Isolated is build-only: rendered from its glyph.
var familyCards = new List<(string Name, string Fig, string Slots, string Reads, string Badge)>
{
    ("Isolated", TileFig(["vv", "wv", "vv"], scale: FamilyScale), "—",
        "wool ringed by void — no terrain approach; reachable only by building", "build-only"),
    ("I", Guard(() => FamilyFig(ShapeFamily.I, 2, boxOverride: new WoolBox(0, 0, 2, 5)), "fam-I"),
        "entry · room", "a terrain lane caps the wool inline — zero bends; the budget sets the depth, "
        + "the length rule caps it (a longer lane side-tucks its room instead)", "sampled"),
    ("I · side-tuck", Guard(() => FamilyFig(ShapeFamily.I, 2, RoomPlacement.SideTuck), "fam-I-tuck"),
        "entry · room", "the compact variant: the room tucked beside the lane — where a budget lane "
        + "would run too long", "sampled"),
    ("L", Guard(() => FamilyFig(ShapeFamily.L, 2), "fam-L"),
        "entry · run · room", "one bend — terrain reaches the wool from two adjacent sides; docks by the "
        + "seat-and-shift overhang", "sampled"),
    ("Z", Guard(() => FamilyFig(ShapeFamily.Z, 2), "fam-Z"),
        "entry · bar · room-run · room", "two opposing bends — an S with no bay", "menu-legal · unsampled"),
    ("Scythe", Guard(() => FamilyFig(ShapeFamily.Scythe, 2), "fam-Scythe"),
        "entry · entry-run · bar · room-run · room", "a fold that wraps an open bay beside the wool — a "
        + "flush dock would seal the bay into a wool-ringed hole", "gated · WL8"),
    ("Clamp", Guard(() => FamilyFig(ShapeFamily.Clamp, 2), "fam-Clamp"),
        "entry · entry · room", "the wool bridges two otherwise-separate bars — remove it and the terrain "
        + "splits", "sampled"),
    ("U", Guard(() => FamilyFig(ShapeFamily.U, 2), "fam-U"),
        "bar · entry · entry · room", "two legs meet a crossbar; the wool docks flush on it", "sampled"),
    ("H", Guard(() => FamilyFig(ShapeFamily.H, 2), "fam-H"),
        "bar · entry · entry · room-run · room", "the wool caps a room-run stub its own width — lifted off "
        + "the bar", "sampled"),
    ("Donut", Guard(() => FamilyFig(ShapeFamily.Donut, 2), "fam-Donut"),
        "entry-bar · leg · leg · entry · room-bar · room", "terrain encloses a void — a full loop, "
        + "multi-access; growth knobs widen the entry to 5 and the hole to 3×5 cells", "sampled"),
};

// the body vocabulary (terminal-free compounds)
var bodyCards = new List<(string Name, string Fig, string Gloss)>
{
    ("Rectangle", Guard(() => BodyFig(BodyEmitter.Rectangle(8, 5)), "body-rect"),
        "a spine — the solid base body; reads I as a corridor, □ as an area"),
    ("Spine + 1 arm", Guard(() => BodyFig(BodyEmitter.SpineArms(2, 1)), "body-arm1"),
        "the branch family at one arm — the L/T placement reads"),
    ("Spine + 2 arms", Guard(() => BodyFig(BodyEmitter.SpineArms(2, 2)), "body-arm2"),
        "two arms — U/Π/F as the arms slide; the staple body"),
    ("Spine + 3 arms", Guard(() => BodyFig(BodyEmitter.SpineArms(2, 3)), "body-arm3"),
        "three arms — E/comb; the letters drift, the topology stays"),
    ("Ring", Guard(() => BodyFig(BodyEmitter.Ring(2, 9, 6)), "body-ring"),
        "one enclosed void — the donut's body, the hub's favourite"),
    ("P", Guard(() => BodyFig(BodyEmitter.P(2, 7, 6, 4)), "body-p"),
        "a loop on a longer overhanging bar — a ring with a tail of free run"),
    ("Double-hole", Guard(() => BodyFig(BodyEmitter.DoubleHole(2, 9, 8)), "body-dh"),
        "a ring plus a docked U — two enclosed voids behind one wall"),
    ("G", Guard(() => BodyFig(BodyEmitter.G(2, 9, 6, 13)), "body-g"),
        "a ring plus an L — the ring's hole and an open bay a docking frontline can seal"),
    ("Two U on I", Guard(() => BodyFig(BodyEmitter.TwoUOnI(2, 6)), "body-2u"),
        "twin loops on one bar — an open channel between the voids"),
};

// hub + frontline form menus
var hubCards = new List<(string Name, string Fig, string Gloss)>
{
    ("Rectangle", Guard(() => HubFig(new CompoundRead(Compound.Rectangle), 6, 4), "hub-rect"), "the solid default — four full edges of offer"),
    ("L", Guard(() => HubFig(new CompoundRead(Compound.SpineArms, 1), 7, 5), "hub-l"), "a bent hub — the arm shelters one side"),
    ("U", Guard(() => HubFig(new CompoundRead(Compound.SpineArms, 2), 8, 5), "hub-u"), "a bay hub — the recess is not offered"),
    ("Ring", Guard(() => HubFig(new CompoundRead(Compound.Ring), 7, 5), "hub-ring"), "the seed-10 hub — an enclosed hole, full outer offer"),
    ("P", Guard(() => HubFig(new CompoundRead(Compound.P), 12, 6), "hub-p"), "wide: a loop plus a long free bar run"),
    ("Double-hole", Guard(() => HubFig(new CompoundRead(Compound.DoubleHole), 12, 6), "hub-dh"), "wide: two equal holes"),
    ("G", Guard(() => HubFig(new CompoundRead(Compound.G), 12, 6), "hub-g"), "wide: a hole plus a sealable bay — asymmetric negative space"),
};
var frontCards = new List<(string Name, string Fig, string Gloss)>
{
    ("Bar", Guard(() => FrontFig(new CompoundRead(Compound.Rectangle), 8, 3, 3, OfferGrouping.Joint), "front-bar"),
        "the wide face — one joint offer the mid must span flush"),
    ("Single", Guard(() => FrontFig(new CompoundRead(Compound.SpineArms, 1), 8, 5, 3, OfferGrouping.Several), "front-single"),
        "the fat L — one leg forward, the notch stays open"),
    ("Twin", Guard(() => FrontFig(new CompoundRead(Compound.SpineArms, 2), 8, 5, 3, OfferGrouping.Several), "front-twin"),
        "the seed-10 form — two tips offered severally, the recess between them not offered at all"),
};

// negative space: the three classes read off real emissions
var negativeCards = new List<(string Name, string Fig, string Gloss)>
{
    ("Notch — 2 walls", Guard(() => BodyFig(BodyEmitter.SpineArms(2, 1)), "neg-notch"),
        "the corner an L wraps — open two ways; both run-ends are its mouths"),
    ("Bay — 3 walls", Guard(() => BodyFig(BodyEmitter.SpineArms(2, 2)), "neg-bay"),
        "the staple's recess — open one way; its single mouth tapers to a wN width class"),
    ("Hole — 4 walls", Guard(() => BodyFig(BodyEmitter.Ring(2, 9, 6)), "neg-hole"),
        "fully enclosed — no mouth; nothing docks through it, the fight rotates around it"),
};

// docking diagrams (tile glyphs from the canonical doc)
var dockCards = new List<(string Name, string Fig, string Gloss, bool Legal)>
{
    ("Entry dock", TileFig(["hhhh", "tt..", "tt..", "ww.."]),
        "a dock lands on an entry slot: the mouth faces the host, the room points away", true),
    ("Room dock", TileFig(["hhhh", "ww..", "tt..", "tt.."]),
        "the mouth is the room — sealed wool, no approach; SlotDockRole says never-dock", false),
    ("Clamp — full short edge", TileFig(["twt", "tvt", "hhh"]),
        "both entries land on one host; the bay closes into a deliberate, declared hole", true),
    ("Clamp — corner wrap", TileFig(["twtv", "tvth", "hvvh"]),
        "two hosts take one entry each; the bay stays open — the dual-host dock", true),
    ("Clamp — wool-side", TileFig(["hhh", "twt", "tvt"]),
        "docking the wool-side edge leaves both entry stubs dangling in the void", false),
    ("Scythe — flush seal", TileFig(["tttv", "tvtw", "hhhh"]),
        "a flush host seals the scythe's bay into a wool-ringed hole — WL8's forbidden motif", false),
};

// ── the budget table (two currencies, measured off the walkthrough board) ──────────────────────────────
var budgetRows = new StringBuilder();
double totalFoot = 0, totalLand = 0;
foreach (var b in part.Boxes)
{
    var foot = b.Rect[2] * b.Rect[3];
    var land = unit.Pieces.Where(p => p.Box?.Id == b.Id).Sum(p => p.Rect[2] * p.Rect[3]);
    totalFoot += foot; totalLand += land;
    var pct = foot == 0 ? 0 : 100.0 * land / foot;
    budgetRows.Append($"<tr><td><span class='dotk' style='background:{KindCol(b.Kind)}'></span>{Esc(b.Id)}</td>"
        + $"<td>{b.Rect[2]}×{b.Rect[3]}</td><td>{foot}</td><td>{land}</td>"
        + $"<td><div class='barw'><div class='barf' style='width:{pct:0}%;background:{KindCol(b.Kind)}'></div></div>{pct:0}%</td></tr>");
}
var bandFoot = stages.Mid.BandRect[2] * stages.Mid.BandRect[3];
budgetRows.Append($"<tr><td><span class='dotk' style='background:{CBand}'></span>mid band</td>"
    + $"<td>{stages.Mid.BandRect[2]}×{stages.Mid.BandRect[3]}</td><td>{bandFoot}</td><td>0</td>"
    + "<td><div class='barw'></div>0% — build costs footprint, never land</td></tr>");
var landBudgetCells = env.LandPerTeam / (env.Cell * (double)env.Cell);

// ── the evaluator readout ──────────────────────────────────────────────────────────────────────────────
var termRows = new StringBuilder();
foreach (var t in eval.Terms.OrderByDescending(t => t.Kind == TermKind.Hard).ThenByDescending(t => t.Distance))
{
    var fired = t.Violation is not null || t.Distance > 0;
    var cls = t.Kind == TermKind.Hard ? (fired ? "t-hardfire" : "t-clean") : (fired ? "t-softfire" : "t-clean");
    var val = t.Kind == TermKind.Hard ? (fired ? "VIOLATED" : "clean") : (t.Distance > 0 ? $"+{t.Distance:0.###}" : "in band");
    termRows.Append($"<tr class='{cls}'><td>{Esc(t.TermId)}</td><td>{t.Kind}</td><td>{val}</td></tr>");
}

// ── HTML assembly ──────────────────────────────────────────────────────────────────────────────────────
string FigCard(string name, string fig, string gloss, string? slots = null, string? badge = null) => $"""
    <figure class="fig">
      <div class="fig-head"><span class="fig-name">{Esc(name)}</span>{(badge is null ? "" : $"<span class='fig-badge {(badge == "✓" ? "ok" : badge == "✗" ? "no" : "mut")}'>{Esc(badge)}</span>")}</div>
      <div class="fig-svg">{fig}</div>
      {(slots is null ? "" : $"<div class='fig-slots'>{Esc(slots)}</div>")}
      <figcaption>{gloss}</figcaption>
    </figure>
""";

var stripHtml = new StringBuilder();
foreach (var (num, title, svg, caption) in frames)
    stripHtml.Append($"""
        <article class="stage" id="stage-{num}">
          <div class="stage-card">
            <div class="stage-head"><span class="stage-num">{num}</span><h3>{Esc(title)}</h3></div>
            <div class="stage-svg">{svg}</div>
            <p class="stage-cap">{caption}</p>
          </div>
        </article>
""");

var famGrid = string.Concat(familyCards.Select(f => FigCard(f.Name, f.Fig, f.Reads, f.Slots, f.Badge)));
var bodyGrid = string.Concat(bodyCards.Select(f => FigCard(f.Name, f.Fig, f.Gloss)));
var hubGrid = string.Concat(hubCards.Select(f => FigCard(f.Name, f.Fig, f.Gloss)));
var frontGrid = string.Concat(frontCards.Select(f => FigCard(f.Name, f.Fig, f.Gloss)));
var negGrid = string.Concat(negativeCards.Select(f => FigCard(f.Name, f.Fig, f.Gloss)));
var dockGrid = string.Concat(dockCards.Select(f => FigCard(f.Name, f.Fig, f.Gloss, badge: f.Legal ? "✓" : "✗")));

var menuRows = string.Concat(FillMenu.Rows.Select(r =>
    $"<tr><td><b>w{r.WidthCells}</b> · {r.WidthCells * env.Cell} blocks</td><td>{Esc(r.Reads)}</td>"
    + $"<td>{(r.Families.Count > 0 ? string.Join(" · ", r.Families) : "<i>patterns — not single-shape emittable yet</i>")}</td>"
    + $"<td>{Esc(r.Note)}</td></tr>"));

var heroBoard = Frame(fanMul: 0.85, bandOp: 1.0, markerOp: 1.0);
var css = Stylesheet();

var html = $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>How a map layout is generated — pgm-studio</title>
<style>{{css}}</style>
</head>
<body>
<nav class="topbar">
  <span class="brand">pgm-studio · layout generation</span>
  <div class="nav-links">
    <a href="#walkthrough">Walkthrough</a><a href="#boxes">Boxes</a><a href="#families">Families</a>
    <a href="#slots">Slots</a><a href="#bodies">Bodies</a><a href="#negative">Negative&nbsp;space</a>
    <a href="#docking">Docking</a><a href="#budget">Budget</a><a href="#mid">Mid</a><a href="#gate">The&nbsp;gate</a>
  </div>
</nav>

<header class="hero">
  <div class="hero-grid">
    <div class="hero-text">
      <p class="eyebrow">The generation pipeline, end to end</p>
      <h1>How a map layout is generated</h1>
      <p class="lede">A CTW map is not drawn — it is <em>composed</em>: a budget draws typed boxes, each box
      fills with a shape from a lawful menu, the shapes dock along published offers, symmetry fans one authored
      half into a whole board, and an evaluator refuses everything that reads wrong. The residue is the style.
      This page walks one real board through every stage, then opens up the model underneath — every figure on
      this page is rendered live from the generator itself.</p>
    </div>
    <div class="hero-board">{{heroBoard}}</div>
  </div>
  <div class="chips">
    <span class="chip">huge board</span><span class="chip">{{env.PlayersPerTeam}} players / team</span>
    <span class="chip">{{env.Symmetry}}</span><span class="chip">seed {{request.Seed}}</span>
    <span class="chip">cell = {{env.Cell}} blocks</span>
    <span class="chip chip-score">score {{eval.Score.ToString("0.0", CultureInfo.InvariantCulture)}}</span>
    <span class="chip {{(connected ? "chip-ok" : "chip-warn")}}">{{(connected ? "connected" : "disconnected")}}</span>
  </div>
  <div class="verbs">
    <div><b>emit</b><span>fill one box with one shape</span></div>
    <div><b>derive</b><span>read structure back out of geometry</span></div>
    <div><b>compose</b><span>budget → boxes → emit → join</span></div>
    <div><b>evaluate</b><span>score a plan, name its violations</span></div>
    <div><b>realize</b><span>compile the plan into a world</span></div>
  </div>
</header>

<section class="strip-section" id="walkthrough">
  <div class="sec-head wide">
    <p class="eyebrow">The walkthrough</p>
    <h2>One board, ten stages</h2>
    <p class="sec-lede">Huge corpus budget, seed 10 — scroll through the compose the way the composer ran it.
    The finished board stays faint in every frame; each stage lights up exactly what it contributed.</p>
  </div>
  <div class="strip-nav">
    <button class="snav" id="sprev" aria-label="previous stage">←</button>
    <div class="sdots" id="sdots"></div>
    <button class="snav" id="snext" aria-label="next stage">→</button>
  </div>
  <div class="strip" id="strip">
{{stripHtml}}
  </div>
</section>

<main>
<section id="boxes">
  <p class="eyebrow">01 · the scaffold</p>
  <h2>Boxes — envelopes, not fill targets</h2>
  <p class="prose">Before any terrain exists, the budget draws a coarse partition of <b>typed boxes</b>:
  <span class="k k-hub">hub</span> <span class="k k-spawn">spawn</span> <span class="k k-wool">wools</span>
  <span class="k k-front">frontline</span> <span class="k k-mid">mid</span>. A box is a bounding envelope —
  its contents must touch its edges and stay connected, but need not fill it solid. That one rule is what lets
  a single family take many footprints inside a fixed envelope. The box model abstracts how an author actually
  works — stake out regions, fill them, cut them up — but boxes exist <i>only during composition</i>: no
  finished map carries them.</p>
  <p class="prose">Every box side carries an <b>interface width</b> — the master variable of generation. The
  reference frame: <code>cell = 5 blocks</code>, <code>lane = 2 cells = 10 blocks</code>, <code>wN = N
  cells</code>. One width does three things at once: it sets connectivity (a w2 touch is a chokepoint), it
  classifies the joint (≤1 lane continues a lane; ≥3 lanes is an area), and it gates the fill menu:</p>
  <table class="tbl">
    <thead><tr><th>touch</th><th>reads as</th><th>legal fills</th><th>note</th></tr></thead>
    <tbody>{{menuRows}}</tbody>
  </table>
  <p class="prose">Generation runs <b>from the spawn outward, in a relative frame</b> — spawn side, hub, wool
  boxes, frontline — and embeds into absolute coordinates only after the join resolves. Under symmetry just one
  half is grown and fanned, so the frontline is <i>where the fanned images meet</i>: the map's length is an
  output of how much each half generated, never an input.</p>
</section>

<section id="families">
  <p class="eyebrow">02 · the shape vocabulary</p>
  <h2>The nine approach families</h2>
  <p class="prose">A <b>family</b> is the base-shape class of a wool approach — and the nine are an
  <b>escalation</b>, not a flat set: an L whose lane doubles back is a scythe; a scythe whose bay closes is a
  donut; a clamp whose wool docks flush on one bar is a U; a U that lifts its wool onto a stub is an H. A
  family's identity is its <b>turn count plus the wool's seating, read width-independently</b> — a thick leg or
  a wide bay is a <i>wide spot</i>, never a different family. Each figure below is a live emission at
  <b>the composer's own sizing</b> — the minimum box the allocator seats the family in, at the w2 wool
  lane — drawn to one shared scale so the relative footprints read true; the white square is the wool.
  The badge is the family's production status today: <i>sampled</i> by the allocator's shape mix,
  <i>menu-legal</i> but not yet drawn, or <i>gated</i> from flush docking by rule.</p>
  <div class="grid">{{famGrid}}</div>
  <p class="prose">The classifier that reads these back is one decision tree, strongest signal first: no
  terrain at the wool → <b>Isolated</b>; an enclosed void → <b>Donut</b>; the wool a cut cell → <b>Clamp</b>;
  otherwise bend count — 0 → <b>I</b>, 1 → <b>L</b>, then a branch test splits <b>U</b> from <b>H</b> by the
  room-run stub, and a fold test splits <b>Scythe</b> from <b>Z</b>. Emit and derive form a mirror: everything
  the emitter builds must classify back to itself — that agreement is the correctness test of the whole shape
  layer.</p>
</section>

<section id="slots">
  <p class="eyebrow">03 · the internal anatomy</p>
  <h2>Slots — how rules address a shape's pieces</h2>
  <p class="prose">The emitter lays every family as the <b>same fixed set of rectangles, only resized</b> — so
  a family is an ordered template of <b>slot</b>-typed pieces, and composition rules bind to slots instead of
  raw geometry. A slot is a <i>template position</i>, not a property of the rectangle: a scythe's
  <code>entry-run</code> and a donut's <code>leg</code> may be the very same rect in different roles.</p>
  <div class="slot-legend">
    <span><i style="background:#58a6ff"></i>entry — the docked mouth</span>
    <span><i style="background:#3fae74"></i>room — the terminal</span>
    <span><i style="background:#8a97a8"></i>run</span>
    <span><i style="background:#6d7f94"></i>bar</span>
    <span><i style="background:#9b8cf0"></i>leg</span>
    <span><i style="background:#45c4e8"></i>entry-run</span>
    <span><i style="background:#35c69f"></i>room-run</span>
  </div>
  <p class="prose">A slot is really two layers: a <b>structural slot</b> (<code>run · bar · leg</code> — the
  rect's role in the body, shared by every box kind) and a <b>designation mark</b> (<code>entry · room</code> —
  stamped by the approach designation). Splitting them is what lets the same shift/widen knobs drive a wool
  mouth, a spawn mouth, a hub interface, or a frontline face. The labels are compose-internal: they drive every
  later move (which pieces may be cut into build zones is stated <i>per slot</i>), survive the whole pipeline,
  and drop only when the plan is written — a plan on disk is label-free, and the derivers can recover the slots
  from topology alone.</p>
</section>

<section id="bodies">
  <p class="eyebrow">04 · under the families</p>
  <h2>Bodies and designations</h2>
  <p class="prose">Every base shape is two layers. A <b>body</b> is a pure rectilinear compound — rectangles
  joined along shared edge intervals, identified by <b>topology alone</b> (voids · arms · bends). A
  <b>designation</b> is what a box kind stamps onto a body to finish it: the <b>approach</b> designation adds an
  entry and a terminal room (wool, spawn); the <b>hub</b> designation adds per-run interface widths and no
  terminal; the <b>frontline</b> designation marks one edge the <b>face</b>. One body serves several families —
  the staple body reads U with the wool flush on its bar, H with the wool lifted onto a stub. Letters name a
  placement; identity stays topological.</p>
  <div class="grid">{{bodyGrid}}</div>
  <h3 class="sub">The hub menu — the constraint source</h3>
  <p class="prose">The hub emits first and its edges are the law: one <b>offer</b> per free run (the bright
  ticks below), each at the width that run can support. A neighbour's fill width is whatever <i>its own
  joint</i> was granted. The menu stays deliberately rectangle-ish — compact forms plus the wide holed bodies a
  laterally elongated hub affords; a hub grows <b>wider, not squarer</b>.</p>
  <div class="grid">{{hubGrid}}</div>
  <h3 class="sub">The frontline menu — the join</h3>
  <p class="prose">The frontline consumes the hub's front offer with its spine and turns its arm-tips toward
  the axis as the face. Its face offer carries a <b>grouping</b>: <i>joint</i> — one mid consumer must span all
  tips flush (the wide bar) — or <i>several</i> — one per tip, the recess between them simply not offered,
  surviving as a deliberate hole.</p>
  <div class="grid">{{frontGrid}}</div>
</section>

<section id="negative">
  <p class="eyebrow">05 · the space between</p>
  <h2>Negative space — notch, bay, hole</h2>
  <p class="prose">A body's negative spaces escalate by <b>wall count</b> — how many axis directions the body
  walls the void from. Two walls make a <b>notch</b>, three a <b>bay</b> (open one way — its mouth is an
  interval that tapers to a wN width class), four an enclosed <b>hole</b>. This is a shape-relative fact read
  off finished geometry, and it decides what may be offered: the <b>offerable surface</b> a neighbour may dock
  onto is exactly <i>open ∧ not-terminal ∧ not-guarded</i> — a bay's inside is sheltered, a hole's is
  unreachable, and the room's own walls are never for sale.</p>
  <div class="grid grid-3">{{negGrid}}</div>
  <p class="prose">On the walkthrough board all three classes appear fanned: the ring hub's <b>hole</b>, the
  twin frontline's recess — a <b>bay</b> the flush band seals into a hole — and the H wool's <b>bay</b> between
  its legs. Deliberate negative space is where fights rotate; <i>accidental</i> negative space is a defect the
  deriver hunts (an undeclared enclosed void is a suspected mistake, and a hole ringed by a wool plateau is
  hard-forbidden).</p>
</section>

<section id="docking">
  <p class="eyebrow">06 · how shapes meet</p>
  <h2>Docking — offers, demands, and the gate</h2>
  <p class="prose">Two directions of constraint meet at every joint. An approach <b>demands</b> — its entry
  must find a host. A hub or frontline <b>offers</b> — its edges dictate where and how wide neighbours land.
  Between them sits one declarative gate: <i>a dock is legal iff it lands on an entry, seals no wool, and meets
  the family's span demand</i>. No per-family special cases — the same table decides the clamp's two-entry
  short edge and rejects the scythe's flush seal.</p>
  <div class="grid grid-3">{{dockGrid}}</div>
  <p class="prose">Placement around the hub adds two more laws. The <b>seat-separation law</b>: no spawn or
  wool may seat within the map's lane width of another — projected onto edges, so same-edge neighbours and
  around-the-corner meetings are caught by one mechanism. And the <b>seat-and-shift</b>: a single-entry rich
  shape (L, donut) docks its narrow entry on a run while the wide body overhangs into free space — both
  handednesses tried, every legal placement collected, one sampled. A shape that finds no legal seat demotes to
  the compact I rather than failing the unit; a spawn or frontline that cannot seat is a real too-small signal
  the allocator answers by resampling the box.</p>
</section>

<section id="budget">
  <p class="eyebrow">07 · the economy</p>
  <h2>Budget — two currencies that must both balance</h2>
  <p class="prose">Every box carries a share of two currencies. <b>Land</b> is walkable terrain area — set by
  player count, spent by every emitted piece. <b>Footprint</b> is total box area — terrain, build and gap
  together, fixed once at partition. The key asymmetry: <b>a build zone costs footprint but not land</b>. On the
  walkthrough board the land target is {{env.LandPerTeam.ToString("0", CultureInfo.InvariantCulture)}} blocks
  ≈ {{landBudgetCells.ToString("0", CultureInfo.InvariantCulture)}} cells per team; the fills below spent
  {{totalLand.ToString("0", CultureInfo.InvariantCulture)}} cells of it inside
  {{totalFoot.ToString("0", CultureInfo.InvariantCulture)}} cells of footprint:</p>
  <table class="tbl tbl-budget">
    <thead><tr><th>box</th><th>envelope</th><th>footprint (cells)</th><th>land (cells)</th><th>fill ratio</th></tr></thead>
    <tbody>{{budgetRows}}</tbody>
  </table>
  <p class="prose">Later pipeline moves keep the invariant <b>“never remove, just replace”</b>: fragmentation
  converts a terrain piece into a build-zone piece — same size, land spent, footprint conserved. Every such cut
  buys difficulty (isolation, risk) in the same move that spends land, so the economy and the gameplay knob move
  together. The mid is the same model inverted: footprint-rich, land-poor — its purpose <i>is</i> the crossing.</p>
</section>

<section id="mid">
  <p class="eyebrow">08 · the contested middle</p>
  <h2>The mid — an output, not a choice</h2>
  <p class="prose">The mid's form is not sampled — it is a function of the frontline: <code>mid =
  f(frontline)</code>. The halves grow, the join fixes the frontline, and the frontline dictates the crossing.
  Today the composer ships the <b>clean form</b>: one band spanning the axis, laterally fit to the exact hull
  of the opposing faces (no slack), flush-docked in depth — zero overlap, zero gap — and kept two full cells
  clear of every wool across all orbit images. One band, one merged build region, one honest crossing.</p>
  <div class="grid grid-3">
    {{FigCard("channelled", TileFig(["tttttt", "b.bb.b", "tttttt"]), "exactly one front↔front crossing — the fight funnels")}}
    {{FigCard("parallel", TileFig(["tttttt", "bbbbbb", "t.tt.t", "bbbbbb", "tttttt"]), "two or more crossings ring the middle — flanks exist")}}
    {{FigCard("hash", TileFig(["t.tttt", "bb.bbb", "t.t.tt", "bbb.bb", "tttt.t"]), "neutral↔neutral links fracture the mid into islands")}}
  </div>
  <p class="prose">The richer vocabulary — stone rows, the centre island, the split band around a hole, depth
  variation — layers back onto this same crossing arithmetic as authored designs. The band-only mid is v0's
  honest floor, not the ceiling.</p>
</section>

<section id="gate">
  <p class="eyebrow">09 · taste, made explicit</p>
  <h2>The gate and the evaluator</h2>
  <p class="prose">The emitter can make anything; the maps' character comes from what evaluation <b>refuses to
  let through</b>. The model is three layers — author intent (the plan), derived structure (islands, contacts,
  holes, lanes — computed, never stored), and judgment: <code>score = Σ hard-penalty + Σ weight ·
  envelope-distance</code>. <b>Hard terms</b> are structural law — a violated one rejects the attempt outright,
  and the composer resamples (this board passed on its first accepted attempt out of a 60-attempt allowance).
  <b>Soft terms</b> measure distance from the authored seeds' envelopes — they never block, they price.</p>
  <table class="tbl tbl-terms">
    <thead><tr><th>term</th><th>kind</th><th>this board</th></tr></thead>
    <tbody>{{termRows}}</tbody>
  </table>
  <p class="prose">The two fired terms tell one story: <code>spawn-wool-ratio</code> — the spawn sits far from
  one wool and near the other (the guarded/abandoned imbalance), and <code>wool-front-ratio</code> — the same
  asymmetry read against the frontline. Both are <i>authored caps</i>: the intent seeds set the tolerance, and
  no traced map can widen it. Every verdict cites its rule id, so a failure is legible — and every future rule
  starts life as a human looking at a board like this one and saying <i>no, and here is why</i>.</p>
</section>

<footer>
  <p>Every figure rendered from the live generator — <code>tools/compose/showcase.cs</code> ·
  walkthrough board <code>p{{env.PlayersPerTeam}} · t{{env.Teams}} · {{env.Symmetry}} · seed {{request.Seed}}</code> ·
  the canonical model lives in <code>docs/contracts/map-generation.md</code>, the rule law in
  <code>docs/contracts/layout-rules.md</code>.</p>
</footer>
</main>

<script>
(() => {
  const strip = document.getElementById('strip');
  const dots = document.getElementById('sdots');
  const cards = [...strip.querySelectorAll('.stage')];
  cards.forEach((c, i) => {
    const d = document.createElement('button');
    d.className = 'sdot'; d.setAttribute('aria-label', 'stage ' + (i + 1));
    d.addEventListener('click', () => c.scrollIntoView({behavior: 'smooth', inline: 'center', block: 'nearest'}));
    dots.appendChild(d);
  });
  const all = [...dots.children];
  const io = new IntersectionObserver(es => es.forEach(e => {
    const i = cards.indexOf(e.target);
    if (e.isIntersecting) { all.forEach(d => d.classList.remove('on')); all[i].classList.add('on'); }
  }), {root: strip, threshold: 0.6});
  cards.forEach(c => io.observe(c));
  const step = dir => {
    const w = cards[0].getBoundingClientRect().width + 18;
    strip.scrollBy({left: dir * w, behavior: 'smooth'});
  };
  document.getElementById('sprev').addEventListener('click', () => step(-1));
  document.getElementById('snext').addEventListener('click', () => step(1));
  strip.addEventListener('keydown', e => {
    if (e.key === 'ArrowRight') { step(1); e.preventDefault(); }
    if (e.key === 'ArrowLeft') { step(-1); e.preventDefault(); }
  });
})();
</script>
</body>
</html>
""";

Directory.CreateDirectory("tools/compose/out");
File.WriteAllText("tools/compose/out/showcase.html", html);
Console.WriteLine($"wrote tools/compose/out/showcase.html ({html.Length / 1024} KB)");
Console.WriteLine($"walkthrough: p{env.PlayersPerTeam} t{env.Teams} {env.Symmetry} seed {request.Seed} — "
    + $"score {eval.Score:0.###} · holes [{string.Join(",", holeSizes)}] · {(connected ? "connected" : "DISCONNECTED")}");
Console.WriteLine($"wool families: {string.Join(", ", woolFamily.Select(kv => $"{kv.Key}={kv.Value}"))}");
Console.WriteLine($"figure failures: {failures.Count}");
foreach (var f in failures) Console.WriteLine($"  FAIL {f}");

// ── the stylesheet (kept last so the layout above reads top-down) ─────────────────────────────────────
static string Stylesheet() => """
:root{
  --bg:#0b1220; --bg2:#0e1526; --panel:#131c30; --panel2:#0f1728; --line:#233150;
  --ink:#e7edf8; --bright:#ffffff; --mut:#8a99b8; --dim:#5f6f8f;
  --accent:#3b82f6; --accent2:#60a5fa; --violet:#a78bfa;
  --hub:#a78bfa; --spawn:#34d399; --wool:#fbbf24; --front:#fb923c; --band:#38bdf8; --hole:#f87171;
  --ok:#34d399; --warn:#f87171;
  --mono:ui-monospace,SFMono-Regular,Menlo,"Cascadia Mono",monospace;
  --sans:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif;
}
*{box-sizing:border-box}
html{scroll-behavior:smooth}
body{margin:0;background:var(--bg);color:var(--ink);font-family:var(--sans);font-size:15px;line-height:1.65;
  -webkit-font-smoothing:antialiased;text-rendering:optimizeLegibility}
body::before{content:"";position:fixed;inset:0;pointer-events:none;z-index:0;
  background:radial-gradient(1100px 500px at 50% -80px, rgba(96,165,250,.10), transparent 60%),
             radial-gradient(800px 420px at 85% 8%, rgba(167,139,250,.07), transparent 55%)}
code{font-family:var(--mono);font-size:.86em;background:var(--panel);border:1px solid var(--line);
  padding:1px 5px;border-radius:4px;color:var(--bright)}

.topbar{position:sticky;top:0;z-index:50;display:flex;align-items:center;gap:18px;flex-wrap:wrap;
  padding:10px 28px;background:color-mix(in srgb, var(--bg) 78%, transparent);
  backdrop-filter:blur(10px);-webkit-backdrop-filter:blur(10px);border-bottom:1px solid var(--line)}
.brand{font-family:var(--mono);font-size:11.5px;letter-spacing:.14em;text-transform:uppercase;color:var(--accent2)}
.nav-links{display:flex;gap:4px;flex-wrap:wrap;margin-left:auto}
.nav-links a{color:var(--mut);text-decoration:none;font-size:12.5px;padding:4px 9px;border-radius:6px}
.nav-links a:hover{color:var(--bright);background:var(--panel)}

.hero{position:relative;z-index:1;max-width:1060px;margin:0 auto;padding:58px 28px 34px}
.hero-grid{display:grid;grid-template-columns:1.2fr .8fr;gap:38px;align-items:center;margin-bottom:24px}
.hero-board{border:1px solid var(--line);border-radius:14px;overflow:hidden;line-height:0;background:#080f1a;
  box-shadow:0 26px 64px -26px rgba(0,0,0,.65);max-height:460px}
.hero-board svg{width:100%;height:100%;max-height:458px;object-fit:contain;display:block}
@media (max-width:900px){.hero-grid{grid-template-columns:1fr}.hero-board{max-width:380px}}
.eyebrow{font-family:var(--mono);font-size:11.5px;letter-spacing:.18em;text-transform:uppercase;
  color:var(--accent2);margin:0 0 10px}
.hero h1{font-size:clamp(30px,4.6vw,46px);line-height:1.08;margin:0 0 18px;font-weight:750;
  letter-spacing:-.022em;color:var(--bright)}
.lede{font-size:16.5px;color:var(--mut);max-width:76ch;margin:0 0 22px}
.lede em{color:var(--ink);font-style:normal;font-weight:600}
.chips{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:26px}
.chip{font-family:var(--mono);font-size:11.5px;padding:5px 11px;border-radius:999px;
  border:1px solid var(--line);background:var(--panel2);color:var(--ink)}
.chip-score{border-color:var(--accent);color:var(--accent2)}
.chip-ok{border-color:color-mix(in srgb, var(--ok) 55%, var(--line));color:var(--ok)}
.chip-warn{border-color:var(--warn);color:var(--warn)}
.verbs{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:10px;
  border-top:1px solid var(--line);padding-top:20px}
.verbs div{display:flex;flex-direction:column;gap:2px}
.verbs b{font-family:var(--mono);font-size:12.5px;letter-spacing:.06em;color:var(--bright);text-transform:uppercase}
.verbs span{font-size:12px;color:var(--dim)}

.strip-section{position:relative;z-index:1;margin:26px 0 10px}
.sec-head{max-width:1060px;margin:0 auto;padding:0 28px}
.sec-head h2{font-size:24px;margin:0 0 8px;color:var(--bright);letter-spacing:-.015em}
.sec-lede{color:var(--mut);max-width:74ch;margin:0}
.strip-nav{max-width:1060px;margin:14px auto 0;padding:0 28px;display:flex;align-items:center;gap:14px}
.snav{background:var(--panel);border:1px solid var(--line);color:var(--ink);border-radius:8px;
  width:34px;height:30px;cursor:pointer;font-size:14px}
.snav:hover{background:var(--panel2);border-color:var(--accent)}
.sdots{display:flex;gap:6px}
.sdot{width:8px;height:8px;border-radius:50%;border:none;padding:0;background:var(--line);cursor:pointer}
.sdot.on{background:var(--accent2)}
.strip{display:flex;gap:18px;overflow-x:auto;scroll-snap-type:x mandatory;padding:18px 28px 26px;
  scrollbar-width:thin;scrollbar-color:var(--line) transparent;outline:none}
.stage{scroll-snap-align:center;flex:0 0 auto}
.stage-card{width:min(88vw,470px);background:var(--panel);border:1px solid var(--line);border-radius:14px;
  padding:16px 16px 14px;display:flex;flex-direction:column;gap:12px;
  box-shadow:0 18px 44px -20px rgba(0,0,0,.55)}
.stage-head{display:flex;align-items:baseline;gap:12px}
.stage-num{font-family:var(--mono);font-size:26px;font-weight:700;color:var(--line);
  -webkit-text-stroke:1px var(--dim);letter-spacing:.02em}
.stage-head h3{margin:0;font-size:16.5px;color:var(--bright);letter-spacing:-.01em}
.stage-svg{border-radius:8px;overflow:hidden;border:1px solid var(--line);line-height:0;background:#080f1a}
.stage-svg svg{width:100%;height:auto;display:block}
.stage-cap{margin:0;font-size:13px;line-height:1.6;color:var(--mut)}
.stage-cap b{color:var(--ink)}
.stage-cap code{font-size:.84em}

main{position:relative;z-index:1;max-width:1060px;margin:0 auto;padding:8px 28px 60px}
main section{margin-top:66px;scroll-margin-top:70px}
main h2{font-size:24px;margin:0 0 14px;color:var(--bright);letter-spacing:-.015em}
main h3.sub{font-size:16px;margin:34px 0 10px;color:var(--bright)}
.prose{color:var(--mut);max-width:80ch;margin:0 0 16px}
.prose b{color:var(--ink)}
.prose i{color:var(--ink)}

.k{font-family:var(--mono);font-size:11.5px;padding:2px 8px;border-radius:999px;border:1px solid}
.k-hub{color:var(--hub);border-color:var(--hub)}
.k-spawn{color:var(--spawn);border-color:var(--spawn)}
.k-wool{color:var(--wool);border-color:var(--wool)}
.k-front{color:var(--front);border-color:var(--front)}
.k-mid{color:var(--band);border-color:var(--band)}

.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(215px,1fr));gap:14px;margin:20px 0}
.grid-3{grid-template-columns:repeat(auto-fill,minmax(250px,1fr))}
.fig{margin:0;background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:10px;
  display:flex;flex-direction:column;gap:8px}
.fig-head{display:flex;align-items:center;gap:8px}
.fig-name{font-family:var(--mono);font-size:12.5px;font-weight:650;color:var(--bright)}
.fig-badge{margin-left:auto;font-size:12px;font-weight:700}
.fig-badge.ok{color:var(--ok)} .fig-badge.no{color:var(--warn)}
.fig-badge.mut{color:var(--dim);font-family:var(--mono);font-size:9px;font-weight:600;
  letter-spacing:.06em;text-transform:uppercase;border:1px solid var(--line);border-radius:999px;
  padding:2px 7px;white-space:nowrap}
.fig-svg{background:#080f1a;border:1px solid var(--line);border-radius:6px;line-height:0;
  display:flex;align-items:center;justify-content:center;min-height:120px;padding:6px}
.fig-svg svg{max-width:100%;height:auto}
.fig-slots{font-family:var(--mono);font-size:10px;color:var(--dim);letter-spacing:.02em}
.fig figcaption{font-size:12px;color:var(--mut);line-height:1.5}
.fig-missing{color:var(--dim);font-size:12px;padding:26px}

.slot-legend{display:flex;flex-wrap:wrap;gap:8px 18px;margin:14px 0 18px;padding:12px 14px;
  background:var(--panel);border:1px solid var(--line);border-radius:8px;font-size:12px;color:var(--mut)}
.slot-legend i{display:inline-block;width:11px;height:11px;border-radius:2.5px;margin-right:6px;vertical-align:-1px}

.tbl{width:100%;border-collapse:collapse;margin:18px 0;font-size:13px}
.tbl th{font-family:var(--mono);font-size:10.5px;letter-spacing:.1em;text-transform:uppercase;color:var(--dim);
  text-align:left;padding:8px 12px;border-bottom:1px solid var(--line)}
.tbl td{padding:9px 12px;border-bottom:1px solid color-mix(in srgb, var(--line) 55%, transparent);
  color:var(--mut);vertical-align:top}
.tbl td b{color:var(--bright)}
.tbl td:first-child{color:var(--ink);white-space:nowrap}
.dotk{display:inline-block;width:9px;height:9px;border-radius:2px;margin-right:8px;vertical-align:0}
.barw{display:inline-block;width:110px;height:7px;background:var(--panel2);border-radius:4px;
  margin-right:9px;vertical-align:1px;overflow:hidden}
.barf{height:100%;border-radius:4px}
.tbl-terms td{padding:5px 12px;font-family:var(--mono);font-size:12px}
.t-clean td{color:var(--dim)}
.t-softfire td{color:var(--wool)}
.t-hardfire td{color:var(--warn);font-weight:700}

footer{border-top:1px solid var(--line);margin-top:70px;padding:22px 0 6px;
  font-size:12px;color:var(--dim)}
footer code{font-size:11px}

@media (max-width:640px){
  .hero{padding-top:48px}
  .verbs{grid-template-columns:repeat(2,1fr)}
}
""";

static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

/// <summary>One rectangle of a mini figure, in cell coordinates.</summary>
sealed record MiniEl(
    double X, double Z, double W, double H, string Fill, double FillOp, string Stroke, double StrokeOp,
    double StrokeW, string? Label, string? Dash = null);
