#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
// team-unit gallery: the box-model allocator (C.2) → filler (C.1) over a grid of budgets × seeds, each unit's
// emitted pieces drawn colour-coded by box kind (hub / spawn / wool / frontline; solid = a room). The visual
// ground for the switch — what allocate-then-fill actually produces.
using System.Text;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

var presets = new[]
{
    ("Small board", "6 players · 700 land", 6, 700.0),
    ("Mid board", "8 players · 1600 land", 8, 1600.0),
    ("Big board", "12 players · 2800 land", 12, 2800.0),
};
const int seeds = 8;

var totalTouches = 0;
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
        var touches = DiagonalTouches(filled.Unit.Pieces);
        totalTouches += touches;
        cards.Append(Card(seed, filled.Unit.Pieces, touches));
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
    + "<p class=tag>The box-model switch (G63-C): allocator (C.2) lays box footprints from the budget, "
    + "filler (C.1) emits into them — hub-first, neighbours consuming the hub's edge offers.</p>");
html.Append("<div class=legend>"
    + "<b style='color:#a78bfa'>■ hub</b><b style='color:#34d399'>■ spawn</b>"
    + "<b style='color:#fbbf24'>■ wool</b><b style='color:#fb923c'>■ frontline</b>"
    + "<i>solid = room · translucent = terrain</i></div>");
html.Append(sections.ToString());

Directory.CreateDirectory("tools/compose/out");
File.WriteAllText("tools/compose/out/unit-gallery.html", html.ToString());
Console.WriteLine($"wrote tools/compose/out/unit-gallery.html — {totalTouches} diagonal touch(es) across all units");

// two pieces meet only at a corner (a t*/*t diagonal pinch) iff they are adjacent on BOTH axes — then they
// share exactly the corner point, never a full edge.
static int DiagonalTouches(IReadOnlyList<GrownPiece> pieces)
{
    var r = pieces.Select(p => p.Rect).ToList();
    var n = 0;
    for (var i = 0; i < r.Count; i++)
        for (var j = i + 1; j < r.Count; j++)
        {
            var hAdj = r[i][0] + r[i][2] == r[j][0] || r[j][0] + r[j][2] == r[i][0];
            var vAdj = r[i][1] + r[i][3] == r[j][1] || r[j][1] + r[j][3] == r[i][1];
            if (hAdj && vAdj) n++;
        }
    return n;
}

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

static string Card(int seed, IReadOnlyList<GrownPiece> pieces, int touches)
{
    int minX = pieces.Min(p => p.Rect[0]), minZ = pieces.Min(p => p.Rect[1]);
    int maxX = pieces.Max(p => p.Rect[0] + p.Rect[2]), maxZ = pieces.Max(p => p.Rect[1] + p.Rect[3]);
    int w = maxX - minX, h = maxZ - minZ;
    const int scale = 32, pad = 12;
    int vw = w * scale + 2 * pad, vh = h * scale + 2 * pad;

    var svg = new StringBuilder($"<svg viewBox='0 0 {vw} {vh}' width='{vw}' height='{vh}'>");
    foreach (var p in pieces)
    {
        int x = (p.Rect[0] - minX) * scale + pad, y = (p.Rect[1] - minZ) * scale + pad;
        int pw = p.Rect[2] * scale, ph = p.Rect[3] * scale;
        var col = Color(p.Box?.Kind ?? BoxKind.Mid);
        var room = p.Role != PlanRoles.Piece;                 // wool / spawn rooms drawn solid
        svg.Append($"<rect x='{x}' y='{y}' width='{pw}' height='{ph}' rx='2' fill='{col}' "
            + $"fill-opacity='{(room ? "0.95" : "0.4")}' stroke='{col}' stroke-width='1.5'/>");
    }
    svg.Append("</svg>");
    var warn = touches > 0 ? " warn" : "";
    var badge = touches > 0 ? $" <span class=badge>⚠ {touches} diag</span>" : "";
    return $"<div class='card{warn}'><div class=title>seed {seed}{badge}</div>{svg}</div>";
}
