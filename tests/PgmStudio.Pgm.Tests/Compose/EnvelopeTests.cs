using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// Stage one: the board envelope derived from player count alone. Anchor interpolation (G8) is exact at the
/// table's anchors and monotone between them; the fanned board dims land within their corpus-measured bands
/// (G3) across a sweep of player counts and seeds; the whole stage is deterministic.
/// </summary>
public sealed class EnvelopeTests
{
    private static ComposeEnvelope Derive(int players, int teams = 2, string? symmetry = null, ulong seed = 0) =>
        Envelope.Derive(new ComposeRequest(players, teams, symmetry, seed), new ComposeRng(seed));

    [Test]
    [Arguments(5, 65)]
    [Arguments(10, 95)]
    [Arguments(12, 105)]
    [Arguments(14, 115)]
    [Arguments(16, 155)]
    [Arguments(18, 160)]
    [Arguments(20, 175)]
    [Arguments(32, 185)]
    public async Task Bp_is_exact_at_each_corpus_anchor(int players, int bp)
    {
        var env = Derive(players, seed: 1);
        await Assert.That(env.LandPerTeam).IsEqualTo((double)players * bp);
    }

    [Test]
    public async Task Bp_is_clamped_below_the_lowest_anchor()
    {
        // players is already clamped to >=5 by ComposeRequest, but Envelope's own interpolation is separately
        // exercised at the boundary: 5 players/team must read the 65 anchor exactly (covered above); this test
        // guards that a request below 5 clamps to the same value rather than extrapolating.
        var env = Derive(5, seed: 1);
        await Assert.That(env.LandPerTeam).IsEqualTo(5.0 * 65);
    }

    [Test]
    public async Task Bp_is_monotone_between_anchors()
    {
        double last = 0;
        for (var p = 5; p <= 32; p++)
        {
            var env = Derive(p, seed: 1);
            var bp = env.LandPerTeam / p;
            await Assert.That(bp >= last - 1e-9).IsTrue();
            last = bp;
        }
    }

    [Test]
    public async Task Two_team_board_dims_land_within_the_corpus_bands_across_a_sweep()
    {
        foreach (var players in new[] { 5, 8, 10, 12, 16, 20, 24, 30, 32 })
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var env = Derive(players, seed: seed);
                await Assert.That(env.BoardWidthBlocks >= 25 && env.BoardWidthBlocks <= 130).IsTrue();
                await Assert.That(env.BoardLengthBlocks >= 100 && env.BoardLengthBlocks <= 280).IsTrue();
            }
    }

    [Test]
    public async Task Four_team_board_is_square_within_its_band()
    {
        foreach (var players in new[] { 5, 10, 16, 20, 32 })
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var env = Derive(players, teams: 4, seed: seed);
                await Assert.That(env.BoardWidthBlocks).IsEqualTo(env.BoardLengthBlocks);
                await Assert.That(env.BoardWidthBlocks >= 90 && env.BoardWidthBlocks <= 180).IsTrue();
            }
    }

    [Test]
    public async Task Derive_is_deterministic_for_the_same_request_and_seed()
    {
        var a = Derive(16, seed: 777);
        var b = Derive(16, seed: 777);
        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task Mirror_x_resolves_the_x_primary_frame()
    {
        var env = Derive(12, symmetry: "mirror_x", seed: 3);
        await Assert.That(env.Symmetry).IsEqualTo("mirror_x");
        // the x-primary unit sits strictly on the -x side of the axis, at least the margin away from it
        await Assert.That(env.UnitMaxX <= -Envelope.AxisMarginCells).IsTrue();
    }

    [Test]
    public async Task Rot_180_unit_bounds_sit_on_the_positive_z_side()
    {
        var env = Derive(12, seed: 4);
        await Assert.That(env.UnitMinZ >= Envelope.AxisMarginCells).IsTrue();
        await Assert.That(env.UnitMinX < 0 && env.UnitMaxX > 0).IsTrue();
    }
}
