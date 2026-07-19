#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
// seat probe: run allocate->fill across the preset budgets x many seeds and count the failure modes
// catalogued in docs/map-generation-constraint-taxonomy.md §9 directly from the geometry — neighbour
// boxes abutting, lanes flush against hub mass, the frontline form mix (and what it pairs with), donut
// box dims, and clamp void depth. Console-only diagnostics; entries leave §9 (and these counters go
// quiet) as the fixes land. Run: dotnet run tools/compose/seat-probe.cs
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

var presets = new[]
{
    ("small", 6, 700.0),
    ("mid", 8, 1600.0),
    ("big", 12, 2800.0),
    ("huge", 20, 3800.0),
};
const int seeds = 200;

foreach (var (label, players, land) in presets)
{
    var env = new ComposeEnvelope("mirror_z", Teams: 2, players, Cell: 5, Surface: 9, Headroom: 11,
        BoardWidthBlocks: 300, BoardLengthBlocks: 300, land, UnitMinX: 0, UnitMinZ: 0, UnitMaxX: 60, UnitMaxZ: 60);

    int ok = 0, noAlloc = 0, noFill = 0, nbAbut = 0, flushMass = 0, flushMassNoFront = 0, noFront = 0, branchNoFront = 0;
    var frontForms = new Dictionary<string, int>();
    var pairs = new Dictionary<string, int>();
    var donutDims = new List<(int W, int H)>();
    var clampVoid = new List<int>();

    for (var seed = 0; seed < seeds; seed++)
    {
        if (TeamUnitAllocator.Allocate(env, new ComposeRng((ulong)seed)) is not { } a) { noAlloc++; continue; }
        if (TeamUnitFiller.Fill(a.Partition, a.SpawnFacing, new ComposeRng((ulong)seed)) is not { } filled) { noFill++; continue; }
        ok++;

        var part = a.Partition;
        var hub = part.ById("hub")!;
        var hubForm = FormLabel(hub.Form);
        var nbs = part.Boxes.Where(b => b.Kind != BoxKind.Hub).ToList();
        var hasFront = nbs.Any(b => b.Kind == BoxKind.Frontline);
        if (!hasFront) noFront++;
        if (!hasFront && hub.Form?.Form == Compound.SpineArms) branchNoFront++;

        // two neighbour boxes abutting (share a boundary segment of length > 0, no overlap) — the missing
        // inter-seat gap
        var abut = false;
        for (var i = 0; i < nbs.Count && !abut; i++)
            for (var j = i + 1; j < nbs.Count && !abut; j++)
                if (Touches(nbs[i].Rect, nbs[j].Rect)) abut = true;
        if (abut) nbAbut++;

        // a wool/spawn box flush against hub MASS beyond its dock interface: a hub piece touching the box
        // laterally (contact perpendicular to the docked hub edge). Note the flush-against-a-leg mode also
        // occurs WITHOUT box contact — a dock consuming a leg-tip run leaves zero margin to the leg walls —
        // so a zero here does not clear that mode; it counts only the box-contact form.
        var hubPieces = filled.Unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Hub).ToList();
        var flush = false;
        foreach (var nb in nbs.Where(b => b.Kind is BoxKind.Wool or BoxKind.Spawn))
        {
            var dockEdge = DockEdge(hub.Rect, nb.Rect);
            foreach (var hp in hubPieces)
                if (LateralContact(nb.Rect, hp.Rect, dockEdge)) { flush = true; break; }
            if (flush) break;
        }
        if (flush) { flushMass++; if (!hasFront) flushMassNoFront++; }

        // frontline form (by piece count: 1 = Bar, 2 = single/T, 3 = twin) x the hub form it pairs with
        var fl = filled.Unit.Pieces.Count(p => p.Box?.Kind == BoxKind.Frontline);
        if (fl > 0)
        {
            var ff = fl == 1 ? "Bar" : fl == 2 ? "single-T" : "twin";
            frontForms[ff] = frontForms.GetValueOrDefault(ff) + 1;
            var key = $"{hubForm}+{ff}";
            pairs[key] = pairs.GetValueOrDefault(key) + 1;
        }

        // donut box dims (the min-box sliver check); clamp void depth below the wool (box depth minus the room)
        foreach (var nb in nbs.Where(b => b.Wool is { } wf))
        {
            if (nb.Wool!.Family == ShapeFamily.Donut) donutDims.Add((nb.Rect[2], nb.Rect[3]));
            if (nb.Wool!.Family == ShapeFamily.Clamp)
            {
                var dock = DockEdge(hub.Rect, nb.Rect);
                var depth = dock is BoxEdge.Top or BoxEdge.Bottom ? nb.Rect[3] : nb.Rect[2];
                clampVoid.Add(depth - ShapeEmitter.RoomDepthCells);
            }
        }
    }

    Console.WriteLine($"== {label}: {ok}/{seeds} units (no-alloc {noAlloc}, no-fill {noFill})");
    Console.WriteLine($"   neighbour-abuts-neighbour: {nbAbut}  flush-against-hub-mass: {flushMass} " +
        $"(of which frontline-less: {flushMassNoFront})  no-frontline units: {noFront} (branch-hub: {branchNoFront})");
    Console.WriteLine($"   frontline forms: {string.Join(", ", frontForms.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}"))}");
    Console.WriteLine($"   hub+front pairs: {string.Join(", ", pairs.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}"))}");
    if (donutDims.Count > 0) Console.WriteLine($"   donut boxes: {string.Join(" ", donutDims.Distinct().Select(d => $"{d.W}x{d.H}"))} (n={donutDims.Count})");
    if (clampVoid.Count > 0) Console.WriteLine($"   clamp void depth below the wool (cells): {string.Join(",", clampVoid.Distinct().OrderBy(v => v))} (n={clampVoid.Count})");
}

static string FormLabel(CompoundRead? form) => form is null ? "rect"
    : form.Form == Compound.SpineArms ? (form.Arms == 1 ? "L" : "U")
    : form.Form == Compound.Rectangle ? "rect"
    : form.Form.ToString().ToLowerInvariant();

// which hub edge nb docks (the side of the hub's rect nb touches)
static BoxEdge DockEdge(int[] hub, int[] nb) =>
    nb[1] + nb[3] == hub[1] ? BoxEdge.Top
    : nb[1] == hub[1] + hub[3] ? BoxEdge.Bottom
    : nb[0] + nb[2] == hub[0] ? BoxEdge.Left
    : BoxEdge.Right;

// rects touch: no area overlap but a shared boundary segment of positive length
static bool Touches(int[] a, int[] b)
{
    bool xOver = a[0] < b[0] + b[2] && b[0] < a[0] + a[2];
    bool zOver = a[1] < b[1] + b[3] && b[1] < a[1] + a[3];
    bool xTouch = a[0] + a[2] == b[0] || b[0] + b[2] == a[0];
    bool zTouch = a[1] + a[3] == b[1] || b[1] + b[3] == a[1];
    return (xTouch && zOver) || (zTouch && xOver);
}

// the hub piece touches the box on the axis PERPENDICULAR to the dock edge (lateral contact = flush mass)
static bool LateralContact(int[] box, int[] piece, BoxEdge dock)
{
    bool xOver = box[0] < piece[0] + piece[2] && piece[0] < box[0] + box[2];
    bool zOver = box[1] < piece[1] + piece[3] && piece[1] < box[1] + box[3];
    bool xTouch = box[0] + box[2] == piece[0] || piece[0] + piece[2] == box[0];
    bool zTouch = box[1] + box[3] == piece[1] || piece[1] + piece[3] == box[1];
    return dock is BoxEdge.Top or BoxEdge.Bottom ? xTouch && zOver : zTouch && xOver;
}
