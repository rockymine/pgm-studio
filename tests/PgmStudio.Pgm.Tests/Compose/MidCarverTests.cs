using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The mid crossing's sampled arithmetic (G5/G7 hop bands over 0..2 stone rows) and the CT7 stone-column
/// alignment helper. The full carved-band/stone behaviour on real plans is asserted by the
/// <see cref="ComposerTests"/> sweep; these tests pin the two pure pieces the carver is built from.
/// </summary>
public sealed class MidCarverTests
{
    private static ComposeEnvelope Env(int players, int teams = 2, ulong seed = 1)
    {
        var request = new ComposeRequest(players, teams, seed: seed);
        return Envelope.Derive(request, new ComposeRng(seed));
    }

    // ── crossing arithmetic ─────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Sampled_crossings_keep_every_hop_in_the_g5_band()
    {
        foreach (var players in new[] { 5, 12, 16, 20, 30 })
            foreach (var teams in new[] { 2, 4 })
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    var env = Env(players, teams, seed);
                    var d = MidCarver.SampleCrossing(env, new ComposeRng(seed + 1000));
                    var cell = env.Cell;
                    await Assert.That(d.Rows.Count is >= 0 and <= 2).IsTrue();

                    if (d.Center)
                    {
                        // a centre crossing: one hop each side, front → centre stone; the stone abuts its own
                        // fan image (they are one island), so there is no innermost-image hop
                        var hop = (d.HalfGapCells - d.Rows[0].Depth) * cell;
                        await Assert.That(hop is >= 10 and <= 20).IsTrue();
                        await Assert.That(teams).IsEqualTo(2);
                        continue;
                    }

                    if (d.Rows.Count == 0)
                    {
                        // a single hop: 20 blocks, or the sanctioned 30 for big teams (≤35 at 20+ players)
                        var hop = 2 * d.HalfGapCells * cell;
                        if (players >= 20) await Assert.That(hop is 20 or 30).IsTrue();
                        else await Assert.That(hop).IsEqualTo(20);
                        await Assert.That(teams).IsEqualTo(2);   // rot_90 margins exclude the shallow crossing
                    }
                    else
                    {
                        // chain: front → rows (near to far) → innermost image → mirrored back — every hop
                        // 10..20, and the hop sum lands in the 30..60 total-crossing band
                        var rows = d.Rows.OrderByDescending(r => r.UMin).ToList();
                        var hops = new List<int>();
                        var cursor = d.HalfGapCells;
                        foreach (var row in rows)
                        {
                            hops.Add((cursor - row.UMin - row.Depth) * cell);
                            cursor = row.UMin;
                        }
                        hops.Add(2 * rows[^1].UMin * cell);              // innermost row → its own image
                        var sum = 2 * hops.Take(hops.Count - 1).Sum() + hops[^1];
                        foreach (var hop in hops)
                            await Assert.That(hop is >= 10 and <= 20).IsTrue();
                        await Assert.That(sum is >= 30 and <= 60).IsTrue();
                    }
                }
    }

    [Test]
    public async Task Crossing_sampling_is_deterministic()
    {
        var env = Env(16);
        var a = MidCarver.SampleCrossing(env, new ComposeRng(7));
        var b = MidCarver.SampleCrossing(env, new ComposeRng(7));
        await Assert.That(a.HalfGapCells).IsEqualTo(b.HalfGapCells);
        await Assert.That(a.Rows.SequenceEqual(b.Rows)).IsTrue();
    }

    [Test]
    public async Task Two_row_crossings_are_four_team_only()
    {
        // 2-team crossings stay shallow (≤1 landing row) at every size — the mid is a wide lateral grid,
        // never a deep stacked chain
        foreach (var players in new[] { 12, 20, 30 })
            for (ulong seed = 1; seed <= 60; seed++)
                await Assert.That(MidCarver.SampleCrossing(Env(players), new ComposeRng(seed)).Rows.Count <= 1).IsTrue();
        // 4-team big-team wedges still take the deep two-row crossing
        var any2 = false;
        for (ulong seed = 1; seed <= 60; seed++)
            any2 |= MidCarver.SampleCrossing(Env(20, teams: 4), new ComposeRng(seed)).Rows.Count == 2;
        await Assert.That(any2).IsTrue();
    }

    [Test]
    public async Task Twin_frontlines_are_forbidden_when_the_front_hop_is_long()
    {
        for (ulong seed = 1; seed <= 80; seed++)
        {
            var d = MidCarver.SampleCrossing(Env(16), new ComposeRng(seed));
            if (d.Center || d.Rows.Count != 1) continue;   // centre crossings force twin off by construction
            var h1 = d.HalfGapCells - d.Rows[0].UMin - d.Rows[0].Depth;
            if (h1 > 3) await Assert.That(d.TwinFrontlineAllowed).IsFalse();
            else await Assert.That(d.TwinFrontlineAllowed).IsTrue();
        }
    }

    // ── stone-column alignment (CT7) ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Candidates_snap_to_the_team_edge_lines()
    {
        var lines = new[] { -2, 0, 3 };
        var candidates = MidCarver.CandidateColumns(lines, [(-2, 3)], stoneW: 2, lateralFlip: false, innerUMin: 1, cell: 5);
        await Assert.That(candidates).IsNotEmpty();
        foreach (var vMin in candidates)
            await Assert.That(lines.Contains(vMin) || lines.Contains(vMin + 2)).IsTrue();
    }

    [Test]
    public async Task Candidates_must_reach_the_single_front_interval()
    {
        // front interval [0,2]: a column at v [3,5] (snapped to line 3) leaves a gap → filtered; touching
        // and overlapping columns give the front a clean straight hop and stay
        var candidates = MidCarver.CandidateColumns([0, 2, 3], [(0, 2)], 2, lateralFlip: false, innerUMin: 1, cell: 5);
        await Assert.That(candidates).IsNotEmpty();
        await Assert.That(candidates.Contains(3)).IsFalse();
        foreach (var vMin in candidates)
            await Assert.That(Math.Min(vMin + 2, 2) - Math.Max(vMin, 0) >= 0).IsTrue();
    }

    [Test]
    public async Task Twin_front_intervals_allow_a_two_cell_reach_gap()
    {
        // twin chains at [-3,-1] and [1,3]: a column [-2,0] overlaps the left chain and sits one cell short
        // of the right one — reachable diagonally on a twin front, but a gapped miss against a single front
        var twin = MidCarver.CandidateColumns([-2], [(-3, -1), (1, 3)], 2, lateralFlip: false, innerUMin: 1, cell: 5);
        await Assert.That(twin).Contains(-2);
        var single = MidCarver.CandidateColumns([-2], [(1, 3)], 2, lateralFlip: false, innerUMin: 1, cell: 5);
        await Assert.That(single.Contains(-2)).IsFalse();
    }

    [Test]
    public async Task Lateral_flip_caps_the_stone_to_image_hop()
    {
        // rot images mirror v: at inner offset 2 the straight hop is already 20 blocks, so any lateral gap
        // to the flipped image pushes the diagonal past 20 — only axis-covering columns survive
        var far = MidCarver.CandidateColumns([2], [(0, 4)], 2, lateralFlip: true, innerUMin: 2, cell: 5);
        await Assert.That(far.Contains(2)).IsFalse();     // v [2,4]: flipped image sits 4 cells away
        var centred = MidCarver.CandidateColumns([0], [(0, 4)], 2, lateralFlip: true, innerUMin: 2, cell: 5);
        await Assert.That(centred.Contains(0)).IsTrue();  // v [0,2] touches the axis line: pure 20-block hop
        // at inner offset 1 (10-block hop) a one-cell lateral offset keeps the diagonal within 20
        var near = MidCarver.CandidateColumns([1], [(0, 4)], 2, lateralFlip: true, innerUMin: 1, cell: 5);
        await Assert.That(near.Contains(1)).IsTrue();     // √(10² + 10²) ≈ 14 blocks — a real hop
    }

    [Test]
    public async Task Mirror_symmetries_do_not_flip_and_rotations_do()
    {
        await Assert.That(MidCarver.LateralFlip("rot_180")).IsTrue();
        await Assert.That(MidCarver.LateralFlip("rot_90")).IsTrue();
        await Assert.That(MidCarver.LateralFlip("mirror_x")).IsFalse();
        await Assert.That(MidCarver.LateralFlip("mirror_z")).IsFalse();
    }
}
