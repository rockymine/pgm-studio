using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>G63-C.2 — the box-model placement plan: the spawn may sit on the back or a lateral side, and the
/// wools are assigned around it (the free sides first, back preferred, a third doubling on the spawn's side).</summary>
public class TeamUnitAllocatorTests
{
    private static ComposeEnvelope Env(int players = 8, double land = 1600) =>
        new("mirror_z", Teams: 2, players, Cell: 5, Surface: 9, Headroom: 11,
            BoardWidthBlocks: 200, BoardLengthBlocks: 200, land, UnitMinX: 0, UnitMinZ: 0, UnitMaxX: 40, UnitMaxZ: 40);

    [Test]
    public async Task Spawn_on_the_back_puts_wools_on_the_sides_then_a_back_wool_c()
    {
        // reduces to the grower's model: two side wools, a third back beside the spawn
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Back, 1)).IsEquivalentTo(new[] { UnitSide.Left });
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Back, 2)).IsEquivalentTo(new[] { UnitSide.Left, UnitSide.Right });
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Back, 3)).IsEquivalentTo(new[] { UnitSide.Left, UnitSide.Right, UnitSide.Back });
    }

    [Test]
    public async Task Spawn_on_a_side_prefers_the_back_then_the_other_side()
    {
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Left, 1)).IsEquivalentTo(new[] { UnitSide.Back });
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Left, 2)).IsEquivalentTo(new[] { UnitSide.Back, UnitSide.Right });
        // the third doubles up on the spawn's own side
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Left, 3)).IsEquivalentTo(new[] { UnitSide.Back, UnitSide.Right, UnitSide.Left });
        // symmetric for the other lateral side
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Right, 2)).IsEquivalentTo(new[] { UnitSide.Back, UnitSide.Left });
    }

    [Test]
    public async Task Wools_never_take_the_front_and_never_collide_with_the_spawn_when_free_sides_exist()
    {
        foreach (var spawn in new[] { UnitSide.Back, UnitSide.Left, UnitSide.Right })
        {
            var wools = TeamUnitAllocator.AssignWools(spawn, 2);
            await Assert.That(wools.Contains(UnitSide.Front)).IsFalse();
            await Assert.That(wools.Contains(spawn)).IsFalse();     // two wools fit the two free sides — no doubling yet
        }
    }

    [Test]
    public async Task Sample_plan_reserves_the_front_for_the_frontline_and_seats_the_spawn_off_front()
    {
        var env = Env();
        var plan = TeamUnitAllocator.SamplePlan(env, new ComposeRng(7), hasFrontline: true);

        await Assert.That(plan.Frontline).IsEqualTo(UnitSide.Front);
        await Assert.That(plan.Spawn).IsNotEqualTo(UnitSide.Front);
        await Assert.That(plan.Wools.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(plan.Wools.All(s => s != UnitSide.Front)).IsTrue();
    }

    [Test]
    public async Task No_frontline_leaves_the_front_unassigned()
    {
        var env = Env();
        var plan = TeamUnitAllocator.SamplePlan(env, new ComposeRng(7), hasFrontline: false);
        await Assert.That(plan.Frontline).IsNull();
    }

    [Test]
    public async Task Allocates_a_hub_spawn_pair_the_filler_consumes_end_to_end()
    {
        var alloc = TeamUnitAllocator.Allocate(Env(), new ComposeRng(5));
        await Assert.That(alloc).IsNotNull();
        var (partition, facing) = alloc!.Value;

        // the hub and spawn are both allocated and do not overlap
        var hub = partition.ById("hub")!;
        var spawn = partition.ById("spawn")!;
        await Assert.That(Overlap(hub.Rect, spawn.Rect)).IsFalse();

        // the allocated partition round-trips through the filler: allocate -> fill, end to end for the first time
        var filled = TeamUnitFiller.Fill(partition, facing, new ComposeRng(5));
        await Assert.That(filled).IsNotNull();
        await Assert.That(filled!.Unit.Pieces.Any(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
        await Assert.That(filled.Unit.Pieces.Any(p => p.Box!.Kind == BoxKind.Spawn)).IsTrue();
        await Assert.That(filled.Unit.Spawn.Facing).IsEqualTo(facing);
    }

    [Test]
    public async Task Allocates_a_full_unit_with_wools_and_no_overlaps_the_filler_consumes()
    {
        var (partition, facing) = TeamUnitAllocator.Allocate(Env(players: 8, land: 1600), new ComposeRng(11))!.Value;

        await Assert.That(partition.Boxes.Any(b => b.Kind == BoxKind.Wool)).IsTrue();
        await Assert.That(NoOverlaps(partition.Boxes.Select(b => b.Rect))).IsTrue();   // the boxes tile without collision

        var filled = TeamUnitFiller.Fill(partition, facing, new ComposeRng(11))!;
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
                var filled = TeamUnitFiller.Fill(a.Partition, a.SpawnFacing, new ComposeRng(seed));
                if (filled is null) continue;
                await Assert.That(Cells.HasDiagonalPinch(Mask(filled.Unit.Pieces))).IsFalse();
            }
    }

    [Test]
    public async Task Neighbour_spawn_and_wool_bodies_keep_the_lane_width_gap()
    {
        // the seat-step separation law: no spawn/wool neighbour touches (or corner-touches) another spawn/wool —
        // they stay at least the map lane width apart (w2 = 10 blocks, w3 = 15 on wide boards). A third wool
        // doubling onto the spawn's own edge (the huge preset) was the reported regression: a wool flush against
        // the spawn with no gap. The wide presets (land > 2500) exercise the 15-block (w3) jump.
        foreach (var (players, land) in new[] { (6, 700.0), (8, 1600.0), (12, 2800.0), (20, 3800.0) })
        {
            var w = land > 2500 ? 3 : 2;                             // the map-wide lane width, and so the gap
            for (ulong seed = 0; seed < 64; seed++)
            {
                var alloc = TeamUnitAllocator.Allocate(Env(players, land), new ComposeRng(seed));
                if (alloc is not { } a) continue;
                var nbs = a.Partition.Boxes.Where(b => b.Kind is BoxKind.Spawn or BoxKind.Wool).ToList();
                for (var i = 0; i < nbs.Count; i++)
                    for (var j = i + 1; j < nbs.Count; j++)
                        await Assert.That(Separated(nbs[i].Rect, nbs[j].Rect, w)).IsTrue()
                            .Because($"{nbs[i].Id}<->{nbs[j].Id} @ {players}p/{land:0} seed {seed}");
            }
        }
    }

    // two [x,z,w,h] rects keep at least `gap` cells between them on some axis — no touch, no corner-touch (the
    // negation of the allocator's TooClose: separated by >= gap on at least one axis)
    private static bool Separated(int[] a, int[] b, int gap) =>
        !(a[0] - gap < b[0] + b[2] && b[0] < a[0] + a[2] + gap &&
          a[1] - gap < b[1] + b[3] && b[1] < a[1] + a[3] + gap);

    // the unit's composed cell mask — every piece rasterized into one set (the surface the corner law reads)
    private static HashSet<(int, int)> Mask(IReadOnlyList<GrownPiece> pieces)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var p in pieces)
            for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++)
                    cells.Add((x, z));
        return cells;
    }

    // two [x,z,w,h] cell rects overlap iff they intersect on both axes
    private static bool Overlap(int[] a, int[] b) =>
        a[0] < b[0] + b[2] && b[0] < a[0] + a[2] && a[1] < b[1] + b[3] && b[1] < a[1] + a[3];

    private static bool NoOverlaps(IEnumerable<int[]> rects)
    {
        var r = rects.ToList();
        for (var a = 0; a < r.Count; a++)
            for (var b = a + 1; b < r.Count; b++)
                if (Overlap(r[a], r[b])) return false;
        return true;
    }
}
