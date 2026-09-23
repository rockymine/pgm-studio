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
        var filled = TeamUnitFiller.Fill(partition, new ComposeRng(5));
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

        var filled = TeamUnitFiller.Fill(partition, new ComposeRng(11))!;
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
                var filled = TeamUnitFiller.Fill(a, new ComposeRng(seed));
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
                if (TeamUnitFiller.Fill(alloc, rng) is not { } filled) continue;

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
    public async Task A_lateral_spawn_sits_level_with_the_hub_middle_or_behind_it()
    {
        // a spawn seated toward the front walks straight out onto the frontline; on a lateral edge its centre
        // is at or past the hub's centre, counted away from the front (mirror_z: the front is the hub's min z)
        var lateral = 0;
        foreach (var (players, land) in new[] { (6, 700.0), (8, 1600.0), (12, 2800.0), (20, 3800.0) })
            for (ulong seed = 0; seed < 300; seed++)
            {
                if (TeamUnitAllocator.Allocate(Env(players, land), new ComposeRng(seed)) is not { } a) continue;
                var hub = a.ById("hub")!.Rect;
                var spawn = a.Boxes.Single(b => b.Kind == BoxKind.Spawn).Rect;
                var onLeft = spawn.X + spawn.Width == hub.X;
                var onRight = spawn.X == hub.X + hub.Width;
                if (!onLeft && !onRight) continue;
                lateral++;
                await Assert.That(2 * spawn.Z + spawn.Height).IsGreaterThanOrEqualTo(2 * hub.Z + hub.Height)
                    .Because($"{players}p/{land:0} seed {seed}: spawn z {spawn.Z}+{spawn.Height}, hub z {hub.Z}+{hub.Height}");
            }
        await Assert.That(lateral).IsGreaterThan(0).Because("the sweep has to reach lateral spawns");
    }

    [Test]
    public async Task A_holed_hub_keeps_a_hole_too_wide_to_jump()
    {
        // WL12's floor for a plain hole, in cells on the envelope's grid: a narrower hole is jumped, not rounded
        var holed = 0;
        foreach (var (players, land) in new[] { (8, 1600.0), (12, 2800.0), (20, 3800.0), (30, 5200.0) })
            for (ulong seed = 0; seed < 150; seed++)
            {
                var env = Env(players, land);
                if (TeamUnitAllocator.Allocate(env, new ComposeRng(seed)) is not { } a) continue;
                var hub = a.ById("hub")!;
                if (hub.Form?.Form is not (Compound.Ring or Compound.P or Compound.DoubleHole or Compound.G)) continue;
                holed++;
                var cw = hub.HubCorridor;
                var walls = hub.HubWalls ?? RingWalls.Uniform(cw);
                var ringW = hub.Form.Form == Compound.Ring ? hub.Rect.Width : hub.Rect.Width - 2 * cw;
                var floor = (PlanValidator.MinPlainSpaceBlocks + env.Cell - 1) / env.Cell;
                await Assert.That(ringW - walls.Left - walls.Right).IsGreaterThanOrEqualTo(floor)
                    .Because($"{players}p seed {seed} {hub.Form.Form} {hub.Rect}");
                await Assert.That(hub.Rect.Height - walls.Top - walls.Bottom).IsGreaterThanOrEqualTo(floor)
                    .Because($"{players}p seed {seed} {hub.Form.Form} {hub.Rect}");
            }
        await Assert.That(holed).IsGreaterThan(0).Because("the sweep has to reach holed hubs");
    }

    [Test]
    public async Task Back_half_keeps_only_the_seats_level_with_the_middle_or_behind_it()
    {
        // a 12-cell edge, a 4-cell dock: seats 4..8 are the centred-or-behind ones when the front is at 0
        await Assert.That(UnitSeating.BackHalf([(0, 12)], 12, 4, frontAtLow: true)).IsEquivalentTo(new[] { (4, 8) });
        await Assert.That(UnitSeating.BackHalf([(0, 12)], 12, 4, frontAtLow: false)).IsEquivalentTo(new[] { (0, 8) });
        // a run wholly in the front half is dropped, one straddling the middle is cut at it
        await Assert.That(UnitSeating.BackHalf([(0, 3), (5, 7)], 12, 4, frontAtLow: true)).IsEquivalentTo(new[] { (5, 7) });
        await Assert.That(UnitSeating.BackHalf([(0, 3), (2, 6)], 12, 4, frontAtLow: true)).IsEquivalentTo(new[] { (4, 4) });
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
