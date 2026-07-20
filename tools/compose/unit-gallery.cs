#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
// team-unit gallery: the box-model allocator (C.2) → filler (C.1) over a grid of budgets × seeds, each unit's
// emitted pieces drawn colour-coded by box kind (hub / spawn / wool / frontline; solid = a room). The visual
// ground for the switch — what allocate-then-fill actually produces.
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

var presets = new[]
{
    ("Small board", "6 players · 700 land", 6, 700.0),
    ("Mid board", "8 players · 1600 land", 8, 1600.0),
    ("Big board", "12 players · 2800 land", 12, 2800.0),
    ("Huge board", "20 players · 3800 land — cap-6 hubs, staple/branch wools", 20, 3800.0),
};
const int seeds = 16;

var totalPinches = 0;
var sections = new StringBuilder();
foreach (var (label, sub, players, land) in presets)
{
    var env = Env("mirror_z", players, land);
    var cards = new StringBuilder();
    for (var seed = 0; seed < seeds; seed++)
    {
        var alloc = TeamUnitAllocator.Allocate(env, new ComposeRng((ulong)seed));
        if (alloc is not { } a) { cards.Append(Fail(seed, "no allocation")); continue; }
        var filled = TeamUnitFiller.Fill(a.Partition, a.SpawnFacing, new ComposeRng((ulong)seed));
        if (filled is null) { cards.Append(Fail(seed, "no fill")); continue; }
        var pinched = Cells.HasDiagonalPinch(Mask(filled.Unit.Pieces));   // the mass-level corner law (G79)
        if (pinched) totalPinches++;
        var hub = a.Partition.ById("hub")!;
        var front = a.SpawnFacing switch { "front" => BoxEdge.Top, "back" => BoxEdge.Bottom, "left" => BoxEdge.Left, _ => BoxEdge.Right };
        cards.Append(Card(seed, filled.Unit.Pieces, FormLabel(hub.Form), pinched, hub.Rect, front));
    }
    sections.Append($"<section><header><h2>{label}</h2><p>{sub}</p></header><div class=gallery>{cards}</div></section>");
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
    + ".badge{color:#f87171;font-size:9px;letter-spacing:0}"
    + ".title{font-size:10.5px;color:var(--dim);margin-bottom:6px;letter-spacing:0.03em}"
    + ".fail{color:#f87171;font-size:11px;padding:26px;text-align:center}"
    + "svg{background:var(--canvas);border-radius:4px;display:block}</style>");
html.Append("<h1>Team-unit layouts</h1>"
    + "<p class=tag>The box-model switch (G63-C): allocator (C.2) chooses the hub form and lays box footprints "
    + "from the budget, seating neighbours on the form's real free edges (§1.13); filler (C.1) re-emits the hub "
    + "form and fills the neighbours, hub-first, each consuming the hub's edge offer.</p>");
html.Append("<div class=legend>"
    + "<b style='color:#a78bfa'>■ hub</b><b style='color:#34d399'>■ spawn</b>"
    + "<b style='color:#fbbf24'>■ wool</b><b style='color:#fb923c'>■ frontline</b>"
    + "<i>solid = room · translucent = terrain</i>"
    + "<b style='color:#38bdf8'>— hub front edge</b><b style='color:#f87171'>— flush (bad)</b>"
    + "<i>grid = 5-block cells · title shows W×H cells · front = longest flat frontier (cells/blocks)</i></div>");
html.Append(sections.ToString());

Directory.CreateDirectory("tools/compose/out");
File.WriteAllText("tools/compose/out/unit-gallery.html", html.ToString());
Console.WriteLine($"wrote tools/compose/out/unit-gallery.html — {totalPinches} diagonal pinch(es) across all units");

// the unit's composed cell mask — every piece rasterized into one set, the surface the corner law reads
static HashSet<(int, int)> Mask(IReadOnlyList<GrownPiece> pieces)
{
    var cells = new HashSet<(int, int)>();
    foreach (var p in pieces)
        for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
            for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++)
                cells.Add((x, z));
    return cells;
}

// a short label for the hub form the allocator chose (Ell/Staple for the branch forms, else the compound name)
static string FormLabel(CompoundRead? form) => form is null ? "rect"
    : form.Form == Compound.SpineArms ? (form.Arms == 1 ? "L" : form.Arms == 2 ? "U" : $"spine+{form.Arms}")
    : form.Form == Compound.Rectangle ? "rect"
    : form.Form.ToString().ToLowerInvariant();

static ComposeEnvelope Env(string sym, int players, double land) =>
    new(sym, Teams: 2, players, Cell: 5, Surface: 9, Headroom: 11,
        BoardWidthBlocks: 300, BoardLengthBlocks: 300, land, UnitMinX: 0, UnitMinZ: 0, UnitMaxX: 60, UnitMaxZ: 60);

static string Color(BoxKind k) => k switch
{
    BoxKind.Hub => "#a78bfa",
    BoxKind.Spawn => "#34d399",
    BoxKind.Wool => "#fbbf24",
    BoxKind.Frontline => "#fb923c",
    _ => "#64748b",
};

static string Fail(int seed, string why) =>
    $"<div class=card><div class=title>seed {seed}</div><div class=fail>{why}</div></div>";

static string Card(int seed, IReadOnlyList<GrownPiece> pieces, string form, bool pinched, int[] hubRect, BoxEdge front)
{
    int minX = pieces.Min(p => p.Rect[0]), minZ = pieces.Min(p => p.Rect[1]);
    int maxX = pieces.Max(p => p.Rect[0] + p.Rect[2]), maxZ = pieces.Max(p => p.Rect[1] + p.Rect[3]);
    int w = maxX - minX, h = maxZ - minZ;
    const int scale = 20, pad = 14;
    int vw = w * scale + 2 * pad, vh = h * scale + 2 * pad;

    var svg = new StringBuilder($"<svg viewBox='0 0 {vw} {vh}' width='{vw}' height='{vh}'>");
    // faint 5×5-block cell grid (one line per cell) so shape dimensions are legible
    for (var gx = 0; gx <= w; gx++)
        svg.Append($"<line x1='{gx * scale + pad}' y1='{pad}' x2='{gx * scale + pad}' y2='{h * scale + pad}' stroke='#ffffff' stroke-opacity='0.07'/>");
    for (var gz = 0; gz <= h; gz++)
        svg.Append($"<line x1='{pad}' y1='{gz * scale + pad}' x2='{w * scale + pad}' y2='{gz * scale + pad}' stroke='#ffffff' stroke-opacity='0.07'/>");

    foreach (var p in pieces)
    {
        int x = (p.Rect[0] - minX) * scale + pad, y = (p.Rect[1] - minZ) * scale + pad;
        int pw = p.Rect[2] * scale, ph = p.Rect[3] * scale;
        var col = Color(p.Box?.Kind ?? BoxKind.Mid);
        var room = p.Role != PlanRoles.Piece;                 // wool / spawn rooms drawn solid
        svg.Append($"<rect x='{x}' y='{y}' width='{pw}' height='{ph}' rx='1.5' fill='{col}' "
            + $"fill-opacity='{(room ? "0.95" : "0.4")}' stroke='{col}' stroke-width='1'/>");
    }

    // the hub's front (axis-facing) bounding-box edge, and any spawn/wool whose own front edge sits flush on it —
    // the long-flat-edge defect: draw the hub front line, and mark a flush neighbour front edge in red
    var (fa, fb, faceCoord) = FrontLine(hubRect, front, minX, minZ, scale, pad);
    svg.Append($"<line x1='{fa.X}' y1='{fa.Y}' x2='{fb.X}' y2='{fb.Y}' stroke='#38bdf8' stroke-width='1.5' stroke-dasharray='3 2'/>");
    var flush = false;
    foreach (var g in pieces.Where(p => p.Box?.Kind is BoxKind.Wool or BoxKind.Spawn).GroupBy(p => p.Box!.Id))
    {
        if (!FlushFront(g.ToList(), front, faceCoord)) continue;
        flush = true;
        var (la, lb) = NeighbourFrontLine(g.ToList(), front, minX, minZ, scale, pad);
        svg.Append($"<line x1='{la.X}' y1='{la.Y}' x2='{lb.X}' y2='{lb.Y}' stroke='#f87171' stroke-width='2.5'/>");
    }
    svg.Append("</svg>");

    var run = FlatFrontRun(pieces, front);
    var bad = pinched || flush;
    var badges = (pinched ? " <span class=badge>⚠ pinch</span>" : "")
        + (flush ? " <span class=badge>⚠ flush front</span>" : "");
    return $"<div class='card{(bad ? " warn" : "")}'><div class=title>seed {seed} · hub {form} · {w}×{h} "
        + $"· front {run}c/{run * 5}b{badges}</div>{svg}</div>";
}

// the longest flat stretch of the unit's front-most frontier, in cells: per cross-axis column the front-most
// occupied cell, then the longest run of adjacent columns whose frontier coordinate is equal — the "one long
// straight edge" measure (5 blocks per cell)
static int FlatFrontRun(IReadOnlyList<GrownPiece> pieces, BoxEdge front)
{
    var frontier = new Dictionary<int, int>();
    foreach (var p in pieces)
        for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
            for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++)
            {
                var (along, o) = front switch
                {
                    BoxEdge.Top => (x, -z),
                    BoxEdge.Bottom => (x, z),
                    BoxEdge.Left => (z, -x),
                    _ => (z, x),
                };
                frontier[along] = frontier.TryGetValue(along, out var cur) ? Math.Max(cur, o) : o;
            }
    var cols = frontier.Keys.OrderBy(k => k).ToList();
    int best = 0, run = 0;
    for (var i = 0; i < cols.Count; i++)
    {
        run = i > 0 && cols[i] == cols[i - 1] + 1 && frontier[cols[i]] == frontier[cols[i - 1]] ? run + 1 : 1;
        best = Math.Max(best, run);
    }
    return best;
}

// the hub bounding-box front edge as a screen line, plus the plan-cell coordinate of that face
static ((int X, int Y) A, (int X, int Y) B, int Face) FrontLine(int[] hub, BoxEdge front, int minX, int minZ, int scale, int pad)
{
    int hx0 = (hub[0] - minX) * scale + pad, hx1 = (hub[0] + hub[2] - minX) * scale + pad;
    int hz0 = (hub[1] - minZ) * scale + pad, hz1 = (hub[1] + hub[3] - minZ) * scale + pad;
    return front switch
    {
        BoxEdge.Top => ((hx0, hz0), (hx1, hz0), hub[1]),
        BoxEdge.Bottom => ((hx0, hz1), (hx1, hz1), hub[1] + hub[3]),
        BoxEdge.Left => ((hx0, hz0), (hx0, hz1), hub[0]),
        _ => ((hx1, hz0), (hx1, hz1), hub[0] + hub[2]),
    };
}

// a spawn/wool is flush when its own bounding-box front edge lands on the hub's front face
static bool FlushFront(IReadOnlyList<GrownPiece> g, BoxEdge front, int face) => front switch
{
    BoxEdge.Top => g.Min(p => p.Rect[1]) == face,
    BoxEdge.Bottom => g.Max(p => p.Rect[1] + p.Rect[3]) == face,
    BoxEdge.Left => g.Min(p => p.Rect[0]) == face,
    _ => g.Max(p => p.Rect[0] + p.Rect[2]) == face,
};

static ((int X, int Y) A, (int X, int Y) B) NeighbourFrontLine(IReadOnlyList<GrownPiece> g, BoxEdge front, int minX, int minZ, int scale, int pad)
{
    int nx0 = (g.Min(p => p.Rect[0]) - minX) * scale + pad, nx1 = (g.Max(p => p.Rect[0] + p.Rect[2]) - minX) * scale + pad;
    int nz0 = (g.Min(p => p.Rect[1]) - minZ) * scale + pad, nz1 = (g.Max(p => p.Rect[1] + p.Rect[3]) - minZ) * scale + pad;
    return front switch
    {
        BoxEdge.Top => ((nx0, nz0), (nx1, nz0)),
        BoxEdge.Bottom => ((nx0, nz1), (nx1, nz1)),
        BoxEdge.Left => ((nx0, nz0), (nx0, nz1)),
        _ => ((nx1, nz0), (nx1, nz1)),
    };
}
