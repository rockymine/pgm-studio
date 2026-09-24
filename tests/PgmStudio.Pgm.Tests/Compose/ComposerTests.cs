using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

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

    /// <summary>The land a plan's pieces cover, in cells — the distinct cells, so a shared run counts once.
    /// The same reading the spend gate takes off the grown unit, read back off the assembled plan.</summary>
    private static int UnitLandCells(PlanModel plan)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var piece in plan.Pieces.Where(p => !PlanRoles.Annotations.Contains(p.Role)
                                                  && !MidCarver.IsStone(p.Id)))
            for (var x = piece.Rect.X; x < piece.Rect.X + piece.Rect.Width; x++)
                for (var z = piece.Rect.Z; z < piece.Rect.Z + piece.Rect.Height; z++)
                    cells.Add((x, z));
        return cells.Count;
    }

    [Test]
    public async Task Every_composed_board_spends_its_bands_budget()
    {
        // the spend gate is the budget's teeth: a unit that left land unplaced, or built a board bigger than
        // its band, is resampled rather than shipped
        foreach (var (players, seed) in Sweep())
        {
            var stages = Composer.ComposeStages(new ComposeRequest(players, seed: seed));
            var built = UnitLandCells(stages.Plan);
            var budget = stages.Envelope.UnitBudgetCells;
            await Assert.That(built >= budget * UnitTuning.SpendFloor && built <= budget * UnitTuning.SpendCeiling)
                .IsTrue().Because($"built {built} of {budget:F0} cells @ {players}p seed {seed}");
        }
    }

    [Test]
    public async Task A_bigger_band_builds_more_land()
    {
        // the band is what the budget keys on, so the ladder must show in what comes out
        double last = 0;
        foreach (var players in new[] { 8, 16, 24, 32 })
        {
            var land = Enumerable.Range(0, 8)
                .Select(seed => (double)UnitLandCells(Composer.Compose(new ComposeRequest(players, seed: (ulong)seed))))
                .Average();
            await Assert.That(land > last).IsTrue().Because($"{players} players built {land:F0} against {last:F0}");
            last = land;
        }
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
            var findings = PlanValidator.Check(plan);
            await Assert.That(findings.Any(f => f.Severity == Severity.Refusal)).IsFalse()
                .Because($"errors @ {players}p seed {seed}");
            foreach (var rule in new[] { "PC-C", "G2", "G5", "FR9" })
                await Assert.That(findings.Any(f => f.Rule == rule)).IsFalse()
                    .Because($"{rule} lint @ {players}p seed {seed}");
        }
    }

    [Test]
    public async Task Composed_units_stay_on_their_side_of_the_axis()
    {
        // rot_180 (the default): the authored unit sits wholly on the +z side, clear of the crossing gap.
        // A mid stone is the one piece that may reach the axis, and it does so in exactly one of two ways —
        // symmetric about it, so its own image abuts it (CT11), or wholly clear of it, so its image is the
        // facing rank. What no stone may be is asymmetrically overlapping: that is an interior clash.
        foreach (var (players, seed) in Sweep())
        {
            var plan = Composer.Compose(new ComposeRequest(players, seed: seed));
            foreach (var p in plan.Pieces.Where(p => !MidCarver.IsStone(p.Id)))
                await Assert.That(p.Rect.Z > 0).IsTrue()
                    .Because($"piece {p.Id} crosses the axis @ {players}p seed {seed}");
            foreach (var stone in plan.Pieces.Where(p => MidCarver.IsStone(p.Id)))
            {
                var (near, far) = (stone.Rect.Z, stone.Rect.Z + stone.Rect.Height);
                await Assert.That(near == -far || near >= 0).IsTrue()
                    .Because($"{stone.Id} is astride the axis or clear of it @ {players}p seed {seed}");
            }
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
    public async Task Composed_zones_carry_the_mid_band_and_each_wall_bars_a_wool_approach()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 1));
        await Assert.That(plan.Zones.Any(z => z.Id == "mid-band")).IsTrue();
        await Assert.That(plan.Zones.All(z => z.Id == "mid-band")).IsTrue();
        await Assert.That(plan.Walls.Count).IsGreaterThan(0);
        await Assert.That(plan.Walls.All(w => w.B.StartsWith("wool-"))).IsTrue();
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
        // the wool-count sampler is alive: small boards split between one and two wools
        var counts = new HashSet<int>();
        for (ulong seed = 0; seed < 30; seed++)
            counts.Add(Composer.Compose(new ComposeRequest(12, teams: 2, seed: seed)).Placements.Wools.Count);
        await Assert.That(counts.Contains(1)).IsTrue();
        await Assert.That(counts.Contains(2)).IsTrue();
    }


    [Test]
    public async Task From_micro_up_a_board_carries_two_wools_a_front_in_its_range_and_a_hub_under_its_ceiling()
    {
        // the author's per-band ranges, in blocks: the frontline face and the hub's lateral ceiling (rot_180, the
        // default, runs the lateral axis along x)
        var face = new Dictionary<string, (int Lo, int Hi)> { ["micro"] = (32, 48), ["milli"] = (40, 56), ["centi"] = (48, 64) };
        var hubMax = new Dictionary<string, int> { ["micro"] = 52, ["milli"] = 68, ["centi"] = 76 };
        foreach (var players in new[] { 20, 30, 44 })
            for (ulong seed = 0; seed < 15; seed++)
            {
                var stages = Composer.ComposeStages(new ComposeRequest(players, seed: seed));
                var (band, cell) = (stages.Envelope.Band, stages.Envelope.Cell);
                int Span(BoxKind kind)
                {
                    var rects = stages.Unit.Pieces.Where(p => p.Box?.Kind == kind).Select(p => p.Rect).ToList();
                    return (rects.Max(r => r.X + r.Width) - rects.Min(r => r.X)) * cell;
                }
                var because = $"{players}p seed {seed} ({band})";
                await Assert.That(stages.Plan.Placements.Wools.Count).IsEqualTo(2).Because(because);
                await Assert.That(Span(BoxKind.Frontline)).IsBetween(face[band].Lo, face[band].Hi).Because(because);
                await Assert.That(Span(BoxKind.Hub)).IsLessThanOrEqualTo(hubMax[band]).Because(because);
            }
    }

    [Test]
    public async Task Box_composition_closes_the_loop_with_a_carved_mid()
    {
        // every composed board carries a carved mid, composes deterministically, and is CONNECTED — a flood
        // from the spawn over land + band reaches every fanned spawn image (the loop-closed criterion the
        // band exists to satisfy)
        foreach (var players in new[] { 6, 8, 12, 20, 30 })
            for (ulong seed = 0; seed < 10; seed++)
            {
                var stages = Composer.ComposeStages(new ComposeRequest(players, seed: seed));

                // the flush law: the band docks straight against the front faces and overlaps no piece of the
                // unit. The stones it carries are inside it by construction — BZ7's sanctioned encasing.
                var bandRect = stages.Mid.BandRect;
                foreach (var p in stages.Plan.Pieces.Where(p => !PlanRoles.Annotations.Contains(p.Role)
                                                             && !MidCarver.IsStone(p.Id)))
                {
                    var ox = Math.Min(p.Rect.X + p.Rect.Width, bandRect.X + bandRect.Width) - Math.Max(p.Rect.X, bandRect.X);
                    var oz = Math.Min(p.Rect.Z + p.Rect.Height, bandRect.Z + bandRect.Height) - Math.Max(p.Rect.Z, bandRect.Z);
                    await Assert.That(ox > 0 && oz > 0).IsFalse()
                        .Because($"band overlaps piece {p.Id} @ {players}p seed {seed}");
                }

                // BZ9: the band spans exactly the hull of the front faces AND their mirrors (min-z pieces under
                // the default z-frame). Not the unit's own hull alone — a face slid off the axis (G123) makes
                // the two images' fronts differ, and the band has to reach both; taking the mirrored hull is
                // also what keeps the band self-symmetric, so its fan image coincides with itself. A narrower
                // band underfits a twin/U front and desyncs from that image.
                var unitPieces = stages.Unit.Pieces;
                var minZ = unitPieces.Min(p => p.Rect.Z);
                var fronts = unitPieces.Where(p => p.Rect.Z == minZ).ToList();
                var ownL = fronts.Min(p => p.Rect.X);
                var ownR = fronts.Max(p => p.Rect.X + p.Rect.Width);
                var hullL = Math.Min(ownL, -ownR);          // rot_180 mirrors x about the axis
                var hullR = Math.Max(ownR, -ownL);
                var bandR = bandRect.X + bandRect.Width;
                // one band spans the hull; a split band spans one leg and its own image spans the other, so
                // what both owe is that band ∪ image covers the hull and reaches no further
                var split = bandRect.X != -bandR;
                var coverL = split ? Math.Min(bandRect.X, -bandR) : bandRect.X;
                var coverR = split ? Math.Max(bandR, -bandRect.X) : bandR;
                await Assert.That(coverL == hullL && coverR == hullR).IsTrue()
                    .Because($"band [{bandRect.X}..{bandR}]{(split ? " + image" : "")} covers [{coverL}..{coverR}], "
                             + $"front hull [{hullL}..{hullR}] @ {players}p seed {seed}");

                var again = Composer.ComposeStages(new ComposeRequest(players, seed: seed));
                await Assert.That(again.Plan.Pieces.Select(p => p.Rect)
                    .SequenceEqual(stages.Plan.Pieces.Select(p => p.Rect))).IsTrue();

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
                    .Select(k => { var r = FanRect(spawnPiece.Rect, axes, k); return (r.X + r.Width / 2, r.Z + r.Height / 2); })
                    .ToList();
                var reached = Flood(walk, spawnCells[0]);
                await Assert.That(spawnCells.All(reached.Contains)).IsTrue()
                    .Because($"board {players}p seed {seed} is not connected through the band");
            }
    }

    private static CellRect FanRect(CellRect r, string[] axes, int k)
    {
        if (k == 0) return r;
        (double x, double z)[] corners = [(r.X, r.Z), (r.X, r.Z + r.Height), (r.X + r.Width, r.Z), (r.X + r.Width, r.Z + r.Height)];
        var pts = corners.Select(c => Symmetry.Apply(c.x, c.z, axes[k - 1], 0, 0)).ToList();
        var x1 = (int)Math.Round(pts.Min(p => p.X));
        var z1 = (int)Math.Round(pts.Min(p => p.Z));
        return new(x1, z1, (int)Math.Round(pts.Max(p => p.X)) - x1, (int)Math.Round(pts.Max(p => p.Z)) - z1);
    }

    private static void Rasterize(CellRect r, HashSet<(int, int)> into)
    {
        for (var x = r.X; x < r.X + r.Width; x++)
            for (var z = r.Z; z < r.Z + r.Height; z++)
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
