using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The full pipeline (envelope → grown unit → assembled <see cref="PlanModel"/>), swept across seeds and
/// player/team combinations. Every combination must: reproduce byte-identically on a second compose; carry no
/// WL2/PC-C lint; classify every piece contact as land or disjoint (never narrow/corner/overlap — the grower
/// authors no narrow seams); keep pieces of different orbit images at least 10 blocks apart on some axis (so
/// the fanned board splits into exactly one island per team — CT1); keep every wool↔spawn/wool↔wool marker
/// distance in band; keep every maximal collinear lane chain within LN2's 50-block cap; and land its total
/// piece area within ±20% of the envelope's per-team budget. Separate distribution tests pin that the
/// structural budget-spenders (frontline pieces, third wools) actually occur across seeds.
///
/// <see cref="PlanValidator"/>'s cross-team reachability check is a structural exception, not a grower defect:
/// it requires a `gap` link across the symmetry axis, which only a build zone can carry — and zones are a
/// later stage's job (the design doc's step 4, "neutral middle"), deliberately empty here (Composer.cs). So a
/// composed plan's only possible errors are "wool unreachable from the enemy spawn" / SP1 — this sweep asserts
/// there are no others.
/// </summary>
public sealed class ComposerTests
{
    private static readonly int[] PlayerCounts = [5, 10, 12, 16, 20, 30];
    private static readonly int[] TeamCounts = [2, 4];

    private static IEnumerable<(int Players, int Teams, ulong Seed)> Sweep()
    {
        for (var players = 0; players < PlayerCounts.Length; players++)
            for (var teams = 0; teams < TeamCounts.Length; teams++)
                for (ulong seed = 1; seed <= 30; seed++)
                    yield return (PlayerCounts[players], TeamCounts[teams], seed);
    }

    [Test]
    public async Task Compose_is_deterministic_for_the_same_request()
    {
        foreach (var (players, teams, seed) in Sweep())
        {
            var a = Composer.Compose(new ComposeRequest(players, teams, seed: seed));
            var b = Composer.Compose(new ComposeRequest(players, teams, seed: seed));
            await Assert.That(a.ToJson()).IsEqualTo(b.ToJson());
        }
    }

    [Test]
    public async Task Composed_plans_satisfy_every_hard_invariant_across_the_sweep()
    {
        foreach (var (players, teams, seed) in Sweep())
        {
            var request = new ComposeRequest(players, teams, seed: seed);
            var plan = Composer.Compose(request);
            var findings = PlanValidator.Validate(plan);

            // no compile-blocking error other than the zone-less cross-team reachability gap (see class doc)
            var unexpectedErrors = findings.Where(f => f.Severity == PlanSeverity.Error
                && !f.Message.Contains("unreachable from")
                && !f.Message.Contains("only reachable through a spawn piece")).ToList();
            await Assert.That(unexpectedErrors).IsEmpty();

            await Assert.That(findings.Any(f => f.Rule == "WL2")).IsFalse();
            await Assert.That(findings.Any(f => f.Rule == "PC-C")).IsFalse();

            var derived = PlanDerived.Build(plan);
            foreach (var c in derived.Contacts)
                await Assert.That(c.Kind is ContactKind.Land or ContactKind.None).IsTrue();

            // LN2: no maximal collinear chain of land-joined pieces runs past 50 blocks
            var chain = TeamUnitGrower.MaxChainBlocks(plan.Globals.Cell, plan.Pieces.Select(p => p.Rect).ToList());
            await Assert.That(chain <= TeamUnitGrower.LaneChainMaxBlocks).IsTrue();

            await AssertFannedSeparationAndIslands(derived);
            await AssertMarkerDistances(plan, derived);
            await AssertAreaWithinBudget(request, plan);
            await AssertUnitOnItsSide(request, plan);
        }
    }

    // Fanned-board separation (CT1): pieces of DIFFERENT orbit images never overlap, touch, or come within
    // 10 blocks on both axes — team territories stay separate islands the mid stage can bridge — and the
    // fanned board therefore splits into exactly one land island per orbit image (connection = any positive
    // shared border or overlap; a corner point never connects).
    private static async Task AssertFannedSeparationAndIslands(PlanDerived derived)
    {
        var images = new List<(int K, BlockRect R)>();
        foreach (var p in derived.Pieces)
            for (var k = 0; k < derived.Order; k++)
                images.Add((k, derived.FanRect(p.Rect, k)));

        var parent = Enumerable.Range(0, images.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }

        for (var i = 0; i < images.Count; i++)
            for (var j = i + 1; j < images.Count; j++)
            {
                var (a, b) = (images[i], images[j]);
                var ix = Math.Min(a.R.MaxX, b.R.MaxX) - Math.Max(a.R.MinX, b.R.MinX);
                var iz = Math.Min(a.R.MaxZ, b.R.MaxZ) - Math.Max(a.R.MinZ, b.R.MinZ);
                if (a.K != b.K)
                    await Assert.That(ix <= -10 || iz <= -10).IsTrue();
                if ((ix > 0 && iz > 0) || (ix == 0 && iz > 0) || (iz == 0 && ix > 0))
                    parent[Find(i)] = Find(j);
            }

        var islands = Enumerable.Range(0, images.Count).Select(Find).Distinct().Count();
        await Assert.That(islands).IsEqualTo(derived.Order);
    }

    private static async Task AssertMarkerDistances(PlanModel plan, PlanDerived derived)
    {
        (double X, double Z) Resolve(string pieceId, double[] at)
        {
            var piece = derived.Piece(pieceId)!.Value;
            return (piece.Rect.MinX + at[0] * derived.Cell, piece.Rect.MinZ + at[1] * derived.Cell);
        }
        static double Dist((double X, double Z) a, (double X, double Z) b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

        var spawn = Resolve(plan.Placements.Spawns[0].Piece, plan.Placements.Spawns[0].At);
        var wools = plan.Placements.Wools.Select(w => Resolve(w.Piece, w.At)).ToList();

        foreach (var w in wools)
            await Assert.That(Dist(w, spawn) >= 20).IsTrue();
        for (var i = 0; i < wools.Count; i++)
            for (var j = i + 1; j < wools.Count; j++)
                await Assert.That(Dist(wools[i], wools[j]) >= 45).IsTrue();
    }

    private static async Task AssertAreaWithinBudget(ComposeRequest request, PlanModel plan)
    {
        var (envelope, _) = Composer.ComposeStages(request);
        var total = plan.Pieces.Sum(p => (double)p.Rect[2] * p.Rect[3] * envelope.Cell * envelope.Cell);
        await Assert.That(total >= envelope.LandPerTeam * 0.8 && total <= envelope.LandPerTeam * 1.2).IsTrue();
    }

    // The authored unit stays on its designated side of the symmetry axis: +z for rot_180/mirror_z/rot_90,
    // -x for mirror_x.
    private static async Task AssertUnitOnItsSide(ComposeRequest request, PlanModel plan)
    {
        foreach (var p in plan.Pieces)
        {
            if (request.Symmetry == "mirror_x")
                await Assert.That(p.Rect[0] + p.Rect[2] <= 0).IsTrue();
            else
                await Assert.That(p.Rect[1] >= 0).IsTrue();
        }
    }

    [Test]
    public async Task Compose_rejects_a_four_team_mirror_request()
    {
        await Assert.That(() => new ComposeRequest(12, teams: 4, symmetry: "mirror_x"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Composed_meta_name_is_deterministic_and_carries_the_request()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 99));
        await Assert.That(plan.Meta!.Name).Contains("12");
        await Assert.That(plan.Meta!.Name).Contains("99");
    }

    [Test]
    public async Task Composed_zones_cliffs_and_walls_are_empty()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 1));
        await Assert.That(plan.Zones).IsEmpty();
        await Assert.That(plan.Cliffs).IsEmpty();
        await Assert.That(plan.Walls).IsEmpty();
    }

    // ── structure distribution (across seeds, not per seed): the surplus-budget spenders must actually
    // occur — a grammar that samples them but never survives validation would silently degenerate ──

    [Test]
    public async Task Frontline_pieces_occur_across_seeds_at_20_players()
    {
        var count = 0;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var plan = Composer.Compose(new ComposeRequest(20, teams: 2, seed: seed));
            if (plan.Pieces.Any(p => p.Id.StartsWith("frontline"))) count++;
        }
        await Assert.That(count > 0).IsTrue();
    }

    [Test]
    public async Task Three_wool_plans_occur_across_seeds_at_16_and_20_players()
    {
        foreach (var players in new[] { 16, 20 })
        {
            var count = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var plan = Composer.Compose(new ComposeRequest(players, teams: 2, seed: seed));
                if (plan.Placements.Wools.Count == 3) count++;
            }
            await Assert.That(count > 0).IsTrue();
        }
    }
}
