using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>G63-C.2 — the box-model placement plan: the spawn may sit on the back or a lateral side, and the
/// wools are assigned around it (the free sides first, back preferred, a third doubling on the spawn's side).</summary>
public class TeamUnitAllocatorTests
{
    private static ComposeEnvelope Env(int players = 8, double land = 2250, string band = SizeBands.Nano) =>
        new("mirror_z", 2, players, band, 5, 9, 2, 2, 200, 200, land, 0, 0, 40, 40);

    [Test]
    public async Task Spawn_on_the_back_puts_wools_on_the_sides_then_a_back_wool_c()
    {
        // reduces to the grower's model: two side wools, a third back beside the spawn
        await Assert.That(UnitTuning.AssignWools(UnitSide.Back, 1)).IsEquivalentTo(new[] { UnitSide.Left });
        await Assert.That(UnitTuning.AssignWools(UnitSide.Back, 2)).IsEquivalentTo(new[] { UnitSide.Left, UnitSide.Right });
        await Assert.That(UnitTuning.AssignWools(UnitSide.Back, 3)).IsEquivalentTo(new[] { UnitSide.Left, UnitSide.Right, UnitSide.Back });
    }

    [Test]
    public async Task Spawn_on_a_side_prefers_the_back_then_the_other_side()
    {
        await Assert.That(UnitTuning.AssignWools(UnitSide.Left, 1)).IsEquivalentTo(new[] { UnitSide.Back });
        await Assert.That(UnitTuning.AssignWools(UnitSide.Left, 2)).IsEquivalentTo(new[] { UnitSide.Back, UnitSide.Right });
        // the third doubles up on the spawn's own side
        await Assert.That(UnitTuning.AssignWools(UnitSide.Left, 3)).IsEquivalentTo(new[] { UnitSide.Back, UnitSide.Right, UnitSide.Left });
        // symmetric for the other lateral side
        await Assert.That(UnitTuning.AssignWools(UnitSide.Right, 2)).IsEquivalentTo(new[] { UnitSide.Back, UnitSide.Left });
    }

    [Test]
    public async Task Wools_never_take_the_front_and_never_collide_with_the_spawn_when_free_sides_exist()
    {
        foreach (var spawn in new[] { UnitSide.Back, UnitSide.Left, UnitSide.Right })
        {
            var wools = UnitTuning.AssignWools(spawn, 2);
            await Assert.That(wools.Contains(UnitSide.Front)).IsFalse();
            await Assert.That(wools.Contains(spawn)).IsFalse();     // two wools fit the two free sides — no doubling yet
        }
    }

    [Test]
    public async Task Sample_plan_never_seats_the_spawn_or_a_wool_on_the_front()
    {
        var env = Env();
        var plan = UnitTuning.SamplePlan(env, new ComposeRng(7));

        await Assert.That(plan.Spawn).IsNotEqualTo(UnitSide.Front);
        await Assert.That(plan.Wools.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(plan.Wools.All(s => s != UnitSide.Front)).IsTrue();
    }

    [Test]
    public async Task Every_allocated_unit_carries_a_frontline()
    {
        foreach (var (players, land) in new[] { (6, 700.0), (8, 1600.0), (12, 2800.0), (20, 3800.0) })
            for (ulong seed = 0; seed < 200; seed++)
            {
                if (TeamUnitAllocator.Allocate(Env(players, land), new ComposeRng(seed)) is not { } a) continue;
                await Assert.That(a.Boxes.Any(b => b.Kind == BoxKind.Frontline)).IsTrue()
                    .Because($"{players}p/{land:0} seed {seed}");
            }
    }

    [Test]
    public async Task Allocates_a_hub_spawn_pair_the_filler_consumes_end_to_end()
    {
        var alloc = TeamUnitAllocator.Allocate(Env(), new ComposeRng(5));
        await Assert.That(alloc).IsNotNull();
        var partition = alloc!;

        // the hub and spawn are both allocated and do not overlap
        var hub = partition.ById("hub")!;
        var spawn = partition.ById("spawn")!;
        await Assert.That(Overlap(hub.Rect, spawn.Rect)).IsFalse();

        // the allocated partition round-trips through the filler: allocate -> fill, end to end for the first time
        var filled = TeamUnitFiller.Fill(partition, new ComposeRng(5), cell: 5);
        await Assert.That(filled).IsNotNull();
        await Assert.That(filled!.Unit.Pieces.Any(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
        await Assert.That(filled.Unit.Pieces.Any(p => p.Box!.Kind == BoxKind.Spawn)).IsTrue();
        // the spawn faces into the hub from whichever side it docks (mirror_z: front is −z)
        var intoHub = spawn.Rect.X >= hub.Rect.X + hub.Rect.Width ? "left"
            : spawn.Rect.X + spawn.Rect.Width <= hub.Rect.X ? "right"
            : spawn.Rect.Z >= hub.Rect.Z + hub.Rect.Height ? "front" : "back";
        await Assert.That(filled.Unit.Spawn.Facing).IsEqualTo(intoHub);
    }

    [Test]
    public async Task Allocates_a_full_unit_with_wools_and_no_overlaps_the_filler_consumes()
    {
        var partition = TeamUnitAllocator.Allocate(Env(players: 8, land: 1600), new ComposeRng(11))!;

        await Assert.That(partition.Boxes.Any(b => b.Kind == BoxKind.Wool)).IsTrue();
        await Assert.That(NoOverlaps(partition.Boxes.Select(b => b.Rect))).IsTrue();   // the boxes tile without collision

        var filled = TeamUnitFiller.Fill(partition, new ComposeRng(11), cell: 5)!;
        await Assert.That(filled.Unit.Wools.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(NoOverlaps(filled.Unit.Pieces.Select(p => p.Rect))).IsTrue();  // a valid layout — no piece overlaps
    }

    [Test]
    public async Task No_diagonal_pinch_across_many_seeds_and_budgets()
    {
        // every allocated+filled unit's composed mask must be free of a diagonal pinch — the mass-level corner
        // law (Cells.HasDiagonalPinch), read on the whole rasterized unit, not a pairwise bounding-box touch, so
        // a hub arm/bar that a neighbour docks reads clean where a third cell bridges the corner. Non-rectangular
        // hubs (an L/U/Ring the allocator now chooses) are multi-piece, so this is the invariant that holds.
        foreach (var (players, land) in new[] { (6, 700.0), (8, 1600.0), (12, 2800.0), (20, 3800.0) })
            for (ulong seed = 0; seed < 40; seed++)
            {
                var alloc = TeamUnitAllocator.Allocate(Env(players, land), new ComposeRng(seed));
                if (alloc is not { } a) continue;
                var filled = TeamUnitFiller.Fill(a, new ComposeRng(seed), cell: 5);
                if (filled is null) continue;
                await Assert.That(Cells.HasDiagonalPinch(Mask(filled.Unit.Pieces))).IsFalse();
            }
    }

    /// <summary>The lane width every shoulder must hold (UnitTuning.WoolLaneCells, which is internal).</summary>
    private const int LaneCells = 2;

    /// <summary>
    /// The spanning dock (G123): a frontline face reaching across a bay-fronted hub's bay must hold at least a
    /// corridor's width on <b>every</b> shoulder it lands on, not just one. A face anchored on one side and
    /// resting on a sliver on the other is cantilevered over the hole — the seat that used to be legal when the
    /// rule was "some contact patch is wide enough".
    /// </summary>
    [Test]
    public async Task A_bay_spanning_frontline_holds_a_lane_on_every_shoulder()
    {
        var spanning = 0;
        foreach (var players in new[] { 8, 12, 16, 20, 24, 30 })
            for (ulong seed = 0; seed < 60; seed++)
            {
                var request = new ComposeRequest(players, 2, "rot_180", seed);
                var rng = new ComposeRng(request.Seed);
                var envelope = Envelope.Derive(request, rng);
                var crossing = MidCarver.Crossing(envelope, splitBand: false, doubleRank: false, fine: false);
                if (TeamUnitAllocator.Allocate(envelope, rng, crossing) is not { } alloc) continue;

                var hub = alloc.Boxes.FirstOrDefault(b => b.Kind == BoxKind.Hub);
                var front = alloc.Boxes.FirstOrDefault(b => b.Kind == BoxKind.Frontline);
                if (hub is null || front is null) continue;
                if (TeamUnitFiller.Fill(alloc, rng, cell: 5) is not { } filled) continue;

                var hubCells = Mask(filled.Unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Hub).ToList());
                if (hubCells.Count == 0) continue;

                // the hub row the face docks against, and the patches the face makes along it
                var row = front.Rect.Z < hub.Rect.Z ? hub.Rect.Z : hub.Rect.Z + hub.Rect.Height - 1;
                int lo = Math.Max(front.Rect.X, hub.Rect.X);
                int hi = Math.Min(front.Rect.X + front.Rect.Width, hub.Rect.X + hub.Rect.Width);
                var patches = new List<int>();
                var run = 0;
                for (var x = lo; x < hi; x++)
                {
                    if (hubCells.Contains((x, row))) run++;
                    else if (run > 0) { patches.Add(run); run = 0; }
                }
                if (run > 0) patches.Add(run);

                if (patches.Count <= 1) continue;      // a solid front — the single-patch case
                spanning++;
                await Assert.That(patches.Min()).IsGreaterThanOrEqualTo(LaneCells)
                    .Because($"{players}p seed {seed}: face spans a bay on shoulders {string.Join("/", patches)}");
            }
        await Assert.That(spanning).IsGreaterThan(0).Because("the bay-spanning case must actually be exercised");
    }

    [Test]
    public async Task Neighbour_spawn_and_wool_bodies_keep_the_lane_width_gap()
    {
        // the seat-step separation law: no spawn/wool neighbour touches (or corner-touches) another spawn/wool —
        // they stay at least the band's own corridor width apart.
        foreach (var players in new[] { 8, 12, 16, 24, 32 })
        {
            var env = Envelope.Derive(new ComposeRequest(players), new ComposeRng(1));
            var w = env.CorridorCells;                               // the map-wide lane width, and so the gap
            var land = env.LandPerTeam;
            for (ulong seed = 0; seed < 64; seed++)
            {
                var alloc = TeamUnitAllocator.Allocate(env, new ComposeRng(seed));
                if (alloc is not { } a) continue;
                var nbs = a.Boxes.Where(b => b.Kind is BoxKind.Spawn or BoxKind.Wool).ToList();
                for (var i = 0; i < nbs.Count; i++)
                    for (var j = i + 1; j < nbs.Count; j++)
                    {
                        await Assert.That(Separated(nbs[i].Rect, nbs[j].Rect, w)).IsTrue()
                            .Because($"{nbs[i].Id}<->{nbs[j].Id} @ {players}p/{land:0} seed {seed}");
                    }
            }
        }
    }

    [Test]
    public async Task A_lateral_spawn_sits_in_line_with_the_hub_hole_or_its_middle()
    {
        // a spawn beside the hub faces its hole squarely: its centre within a cell of the hole's centre, or of the
        // edge's middle on a hub without one (mirror_z: a lateral spawn runs along z)
        var lateral = 0;
        foreach (var (players, land) in new[] { (6, 700.0), (8, 1600.0), (12, 2800.0), (20, 3800.0) })
            for (ulong seed = 0; seed < 300; seed++)
            {
                if (TeamUnitAllocator.Allocate(Env(players, land), new ComposeRng(seed)) is not { } a) continue;
                if (TeamUnitFiller.Fill(a, new ComposeRng(seed), cell: 5) is not { } filled) continue;
                var hub = a.ById("hub")!.Rect;
                var spawn = a.Boxes.Single(b => b.Kind == BoxKind.Spawn).Rect;
                if (spawn.X + spawn.Width != hub.X && spawn.X != hub.X + hub.Width) continue;
                lateral++;
                var hole = Enclosed(hub, filled.Unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Hub).Select(p => p.Rect));
                var target2 = hole.Count > 0 ? hole.Min(c => c.Z) + hole.Max(c => c.Z) + 1 : 2 * hub.Z + hub.Height;
                await Assert.That(Math.Abs(2 * spawn.Z + spawn.Height - target2)).IsLessThanOrEqualTo(2)
                    .Because($"{players}p/{land:0} seed {seed}: spawn z {spawn.Z}+{spawn.Height}, hub {hub}, hole cells {hole.Count}");
            }
        await Assert.That(lateral).IsGreaterThan(0).Because("the sweep has to reach lateral spawns");
    }

    [Test]
    public async Task A_unit_with_a_donut_puts_its_spawn_on_the_back_at_the_end_nearer_the_donut()
    {
        // a donut draws the unit lopsided, so the spawn stands behind the hub, in the back edge's half on the
        // donut's side (mirror_z: the back edge runs along x, the donut docks an x-facing side)
        var donuts = 0;
        foreach (var (players, land) in new[] { (12, 2800.0), (20, 3800.0), (20, 5000.0) })
            for (ulong seed = 0; seed < 300; seed++)
            {
                if (TeamUnitAllocator.Allocate(Env(players, land), new ComposeRng(seed)) is not { } a) continue;
                var hub = a.ById("hub")!.Rect;
                var lateral = a.Boxes.Where(b => b.Wool?.Family == ShapeFamily.Donut
                    && (b.Rect.X + b.Rect.Width == hub.X || b.Rect.X == hub.X + hub.Width)).ToList();
                if (lateral.Count != 1) continue;
                donuts++;
                var front = a.Boxes.Single(b => b.Kind == BoxKind.Frontline).Rect;
                var spawn = a.Boxes.Single(b => b.Kind == BoxKind.Spawn).Rect;
                var backZ = front.Z < hub.Z ? hub.Z + hub.Height : hub.Z;
                var because = $"{players}p/{land:0} seed {seed}: spawn {spawn}, hub {hub}, donut {lateral[0].Rect}";
                await Assert.That(spawn.Z == backZ || spawn.Z + spawn.Height == backZ).IsTrue().Because(because);
                var donutSide = Math.Sign(2 * lateral[0].Rect.X + lateral[0].Rect.Width - (2 * hub.X + hub.Width));
                var spawnSide = Math.Sign(2 * spawn.X + spawn.Width - (2 * hub.X + hub.Width));
                await Assert.That(spawnSide).IsEqualTo(donutSide).Because(because);
            }
        await Assert.That(donuts).IsGreaterThan(0).Because("the sweep has to reach a donut");
    }

    [Test]
    public async Task In_line_keeps_only_the_seats_centred_within_a_cell_of_the_target()
    {
        // a 12-cell edge, a 4-cell dock, the target at 6: seats 3..5 centre within a cell of it
        await Assert.That(UnitSeating.InLine([(0, 12)], 4, 6)).IsEquivalentTo(new[] { (3, 6) });
        // a run that stops short of the window is cut to what it covers, one wholly outside is dropped
        await Assert.That(UnitSeating.InLine([(0, 5), (10, 2)], 4, 6)).IsEquivalentTo(new[] { (3, 2) });
    }

    // the cells of a box no rect covers and no empty path reaches from the box's border
    private static HashSet<(int X, int Z)> Enclosed(CellRect box, IEnumerable<CellRect> rects)
    {
        var land = new HashSet<(int, int)>();
        foreach (var r in rects)
            for (var x = r.X; x < r.X + r.Width; x++)
                for (var z = r.Z; z < r.Z + r.Height; z++) land.Add((x, z));
        var empty = new HashSet<(int X, int Z)>();
        for (var x = box.X; x < box.X + box.Width; x++)
            for (var z = box.Z; z < box.Z + box.Height; z++)
                if (!land.Contains((x, z))) empty.Add((x, z));
        var edge = empty.Where(c => c.X == box.X || c.Z == box.Z || c.X == box.X + box.Width - 1 || c.Z == box.Z + box.Height - 1);
        var reached = new HashSet<(int X, int Z)>(edge);
        var queue = new Queue<(int X, int Z)>(reached);
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            foreach (var n in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
                if (empty.Contains(n) && reached.Add(n)) queue.Enqueue(n);
        }
        empty.ExceptWith(reached);
        return empty;
    }

    // two [x,z,w,h] rects keep at least `gap` cells between them on some axis — no touch, no corner-touch (the
    // negation of the allocator's TooClose: separated by >= gap on at least one axis)
    private static bool Separated(CellRect a, CellRect b, int gap) =>
        !(a.X - gap < b.X + b.Width && b.X < a.X + a.Width + gap &&
          a.Z - gap < b.Z + b.Height && b.Z < a.Z + a.Height + gap);

    // the unit's composed cell mask — every piece rasterized into one set (the surface the corner law reads)
    private static HashSet<(int, int)> Mask(IReadOnlyList<GrownPiece> pieces)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var p in pieces)
            for (var x = p.Rect.X; x < p.Rect.X + p.Rect.Width; x++)
                for (var z = p.Rect.Z; z < p.Rect.Z + p.Rect.Height; z++)
                    cells.Add((x, z));
        return cells;
    }

    // two [x,z,w,h] cell rects overlap iff they intersect on both axes
    private static bool Overlap(CellRect a, CellRect b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Z < b.Z + b.Height && b.Z < a.Z + a.Height;

    private static bool NoOverlaps(IEnumerable<CellRect> rects)
    {
        var r = rects.ToList();
        for (var a = 0; a < r.Count; a++)
            for (var b = a + 1; b < r.Count; b++)
                if (Overlap(r[a], r[b])) return false;
        return true;
    }
}
