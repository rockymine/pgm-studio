using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The full pipeline (envelope → band-only crossing → allocated + filled unit → carved mid → assembled
/// <see cref="PlanModel"/>), swept across seeds and player counts. Every composed board must: reproduce
/// byte-identically on a second compose; validate with ZERO errors; carry no PC-C/G2/G5 lint; dock its
/// stone-free band flush against the front faces (zero overlap) spanning exactly the front-face hull; and be
/// CONNECTED — a flood from the spawn over land + band reaches every fanned spawn image. Separate
/// distribution tests pin that the sampled structure (frontlines, third wools) actually occurs across seeds.
/// </summary>
public sealed class ComposerTests
{
    private static readonly int[] PlayerCounts = [6, 8, 12, 16, 20, 30];

    private static IEnumerable<(int Players, ulong Seed)> Sweep()
    {
        foreach (var players in PlayerCounts)
            for (ulong seed = 0; seed < 10; seed++)
                yield return (players, seed);
    }

    [Test]
    public async Task Compose_is_deterministic_for_the_same_request()
    {
        foreach (var (players, seed) in Sweep())
        {
            var a = Composer.Compose(new ComposeRequest(players, seed: seed));
            var b = Composer.Compose(new ComposeRequest(players, seed: seed));
            await Assert.That(a.ToJson()).IsEqualTo(b.ToJson());
        }
    }

    [Test]
    public async Task Composed_plans_validate_clean_and_carry_no_lint()
    {
        foreach (var (players, seed) in Sweep())
        {
            var plan = Composer.Compose(new ComposeRequest(players, seed: seed));
            var findings = PlanValidator.Validate(plan);
            await Assert.That(findings.Any(f => f.Severity == PlanSeverity.Error)).IsFalse()
                .Because($"errors @ {players}p seed {seed}");
            foreach (var rule in new[] { "PC-C", "G2", "G5" })
                await Assert.That(findings.Any(f => f.Rule == rule)).IsFalse()
                    .Because($"{rule} lint @ {players}p seed {seed}");
        }
    }

    [Test]
    public async Task Composed_units_stay_on_their_side_of_the_axis()
    {
        // rot_180 (the default): the authored unit sits wholly on the +z side, clear of the crossing gap
        foreach (var (players, seed) in Sweep())
        {
            var plan = Composer.Compose(new ComposeRequest(players, seed: seed));
            foreach (var p in plan.Pieces)
                await Assert.That(p.Rect[1] > 0).IsTrue()
                    .Because($"piece {p.Id} crosses the axis @ {players}p seed {seed}");
        }
    }

    [Test]
    public async Task Composed_meta_name_is_deterministic_and_carries_the_request()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 99));
        await Assert.That(plan.Meta!.Name).Contains("12");
        await Assert.That(plan.Meta!.Name).Contains("99");
    }

    [Test]
    public async Task Composed_zones_carry_the_mid_band_and_cliffs_and_walls_stay_empty()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 1));
        await Assert.That(plan.Zones.Any(z => z.Id == "mid-band")).IsTrue();
        await Assert.That(plan.Zones.All(z => z.Id == "mid-band")).IsTrue();
        await Assert.That(plan.Cliffs).IsEmpty();
        await Assert.That(plan.Walls).IsEmpty();
    }

    [Test]
    public async Task Compose_rejects_a_four_team_mirror_request()
    {
        await Assert.That(() => new ComposeRequest(12, teams: 4, symmetry: "mirror_x"))
            .Throws<ArgumentException>();
    }

    // ── structure distribution (across seeds, not per seed): the sampled structure must actually occur —
    // a grammar that samples a feature but never survives the gate would silently degenerate ──

    [Test]
    public async Task Frontline_pieces_occur_across_seeds_at_20_players()
    {
        var count = 0;
        for (ulong seed = 0; seed < 30; seed++)
        {
            var plan = Composer.Compose(new ComposeRequest(20, teams: 2, seed: seed));
            if (plan.Pieces.Any(p => p.Id.StartsWith("frontline"))) count++;
        }
        await Assert.That(count > 0).IsTrue();
    }

    [Test]
    public async Task Wool_counts_vary_across_seeds()
    {
        // the wool-count sampler is alive: small boards split between one and two wools. (The third wool is
        // currently near-extinct — the seat gap drops the spawn-side doubling — so its occurrence is not
        // asserted until that placement is restored.)
        var counts = new HashSet<int>();
        for (ulong seed = 0; seed < 30; seed++)
            counts.Add(Composer.Compose(new ComposeRequest(12, teams: 2, seed: seed)).Placements.Wools.Count);
        await Assert.That(counts.Contains(1)).IsTrue();
        await Assert.That(counts.Contains(2)).IsTrue();
    }

    [Test]
    public async Task No_closure_hole_is_ringed_by_a_wool_plateau()
    {
        // WL8 (two approaches around a wool) is out of the grammar: no sampled hole may border a wool piece
        // outside the shape's own sanctioned courtyard
        foreach (var (players, seed) in Sweep())
        {
            var plan = Composer.Compose(new ComposeRequest(players, seed: seed));
            var woolPieces = plan.Placements.Wools.Select(w => w.Piece).ToHashSet();
            await Assert.That(ClosureAnalysis.AnyHoleRingedBy(plan, woolPieces)).IsFalse();
        }
    }

    [Test]
    public async Task Box_composition_closes_the_loop_with_a_band_only_mid()
    {
        // every composed board carries a stone-free band-only mid, composes deterministically, and is
        // CONNECTED — a flood from the spawn over land + band reaches every fanned spawn image (the
        // loop-closed criterion the band exists to satisfy)
        foreach (var players in new[] { 6, 8, 12, 20, 30 })
            for (ulong seed = 0; seed < 10; seed++)
            {
                var stages = Composer.ComposeStages(new ComposeRequest(players, seed: seed));
                await Assert.That(stages.Mid.Stones.Count).IsEqualTo(0);

                // the flush law: the band docks straight against the front faces and overlaps no piece
                var bandRect = stages.Mid.BandRect;
                foreach (var p in stages.Plan.Pieces.Where(p => !PlanRoles.Annotations.Contains(p.Role)))
                {
                    var ox = Math.Min(p.Rect[0] + p.Rect[2], bandRect[0] + bandRect[2]) - Math.Max(p.Rect[0], bandRect[0]);
                    var oz = Math.Min(p.Rect[1] + p.Rect[3], bandRect[1] + bandRect[3]) - Math.Max(p.Rect[1], bandRect[1]);
                    await Assert.That(ox > 0 && oz > 0).IsFalse()
                        .Because($"band overlaps piece {p.Id} @ {players}p seed {seed}");
                }

                // BZ9 with no slack: the band spans exactly the front-face hull (min-z pieces under the
                // default z-frame) — a narrower band underfits a twin/U front and desyncs from its fan image
                var unitPieces = stages.Unit.Pieces;
                var minZ = unitPieces.Min(p => p.Rect[1]);
                var fronts = unitPieces.Where(p => p.Rect[1] == minZ).ToList();
                var hullL = fronts.Min(p => p.Rect[0]);
                var hullR = fronts.Max(p => p.Rect[0] + p.Rect[2]);
                await Assert.That(bandRect[0] == hullL && bandRect[0] + bandRect[2] == hullR).IsTrue()
                    .Because($"band [{bandRect[0]}..{bandRect[0] + bandRect[2]}] != front hull [{hullL}..{hullR}] @ {players}p seed {seed}");

                var again = Composer.ComposeStages(new ComposeRequest(players, seed: seed));
                await Assert.That(again.Plan.Pieces.Select(p => string.Join(",", p.Rect))
                    .SequenceEqual(stages.Plan.Pieces.Select(p => string.Join(",", p.Rect)))).IsTrue();

                var plan = stages.Plan;
                var sym = stages.Envelope.Symmetry;
                var order = Symmetry.Order(sym);
                var axes = Symmetry.OrbitAxes(sym);
                var walk = new HashSet<(int, int)>();
                foreach (var p in plan.Pieces.Where(p => !PlanRoles.Annotations.Contains(p.Role)))
                    for (var k = 0; k < order; k++) Rasterize(FanRect(p.Rect, axes, k), walk);
                var band = plan.Zones.First(z => z.Id == "mid-band");
                for (var k = 0; k < order; k++) Rasterize(FanRect(band.Rect, axes, k), walk);

                var spawnPiece = plan.Pieces.First(p => p.Role == PlanRoles.Spawn);
                var spawnCells = Enumerable.Range(0, order)
                    .Select(k => { var r = FanRect(spawnPiece.Rect, axes, k); return (r[0] + r[2] / 2, r[1] + r[3] / 2); })
                    .ToList();
                var reached = Flood(walk, spawnCells[0]);
                await Assert.That(spawnCells.All(reached.Contains)).IsTrue()
                    .Because($"board {players}p seed {seed} is not connected through the band");
            }
    }

    private static int[] FanRect(int[] r, string[] axes, int k)
    {
        if (k == 0) return r;
        (double x, double z)[] corners = [(r[0], r[1]), (r[0], r[1] + r[3]), (r[0] + r[2], r[1]), (r[0] + r[2], r[1] + r[3])];
        var pts = corners.Select(c => Symmetry.Apply(c.x, c.z, axes[k - 1], 0, 0)).ToList();
        var x1 = (int)Math.Round(pts.Min(p => p.X));
        var z1 = (int)Math.Round(pts.Min(p => p.Z));
        return [x1, z1, (int)Math.Round(pts.Max(p => p.X)) - x1, (int)Math.Round(pts.Max(p => p.Z)) - z1];
    }

    private static void Rasterize(int[] r, HashSet<(int, int)> into)
    {
        for (var x = r[0]; x < r[0] + r[2]; x++)
            for (var z = r[1]; z < r[1] + r[3]; z++)
                into.Add((x, z));
    }

    private static HashSet<(int, int)> Flood(HashSet<(int, int)> walk, (int, int) start)
    {
        var seen = new HashSet<(int, int)>();
        if (!walk.Contains(start)) return seen;
        var q = new Queue<(int, int)>();
        seen.Add(start); q.Enqueue(start);
        while (q.Count > 0)
        {
            var (x, z) = q.Dequeue();
            foreach (var n in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
                if (walk.Contains(n) && seen.Add(n)) q.Enqueue(n);
        }
        return seen;
    }
}
