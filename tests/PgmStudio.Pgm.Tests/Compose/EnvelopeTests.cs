using PgmStudio.Pgm.Compose;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// Stage one: the board envelope derived from player count alone. The count selects a size band and the band
/// carries the land (G8); the corridor width is the band's blocks laid on the grid; the fanned board dims land
/// within their corpus-measured bands (G3) across a sweep of player counts and seeds; the whole stage is
/// deterministic.
/// </summary>
public sealed class EnvelopeTests
{
    private static ComposeEnvelope Derive(int players, int teams = 2, string? symmetry = null, ulong seed = 0) =>
        Envelope.Derive(new ComposeRequest(players, teams, symmetry, seed), new ComposeRng(seed));

    [Test]
    [Arguments(6, SizeBands.Nano)]
    [Arguments(13, SizeBands.Nano)]
    [Arguments(14, SizeBands.Micro)]
    [Arguments(21, SizeBands.Micro)]
    [Arguments(22, SizeBands.Milli)]
    [Arguments(31, SizeBands.Milli)]
    [Arguments(32, SizeBands.Centi)]
    [Arguments(47, SizeBands.Centi)]
    public async Task The_player_count_selects_its_band_and_the_band_carries_the_land(int players, string band)
    {
        var env = Derive(players, seed: 1);
        await Assert.That(env.Band).IsEqualTo(band);
        await Assert.That(env.LandPerTeam).IsEqualTo(Envelope.LandPerTeamOf(band));
    }

    [Test]
    public async Task Every_count_inside_a_band_composes_to_the_same_budget()
    {
        // a map is built for a range of counts, not one: the budget may not move inside a band
        foreach (var (low, high) in SizeBands.All.Select(SizeBands.Players))
            for (var players = low; players <= high; players++)
                await Assert.That(Derive(players, seed: 1).LandPerTeam)
                    .IsEqualTo(Derive(low, seed: 1).LandPerTeam)
                    .Because($"{players} players sits in the same band as {low}");
    }

    [Test]
    public async Task Land_per_team_rises_with_every_band()
    {
        double last = 0;
        foreach (var band in SizeBands.All)
        {
            var land = Envelope.LandPerTeamOf(band);
            await Assert.That(land > last).IsTrue().Because($"{band} must hold more than the band under it");
            last = land;
        }
    }

    [Test]
    public async Task A_count_below_the_ladder_takes_the_smallest_band()
    {
        await Assert.That(Derive(5, seed: 1).Band).IsEqualTo(SizeBands.Nano);
    }

    [Test]
    [Arguments(5, 3, 3)]
    [Arguments(4, 4, 4)]
    [Arguments(3, 5, 5)]
    [Arguments(2, 7, 8)]
    public async Task The_corridor_is_the_bands_blocks_on_this_grid(int cell, int expectedMicro, int expectedCenti)
    {
        // micro is 14 blocks and centi 16; both round to the nearest whole cell and never fall under two
        var micro = Envelope.Derive(new ComposeRequest(16, cell: cell), new ComposeRng(1));
        var centi = Envelope.Derive(new ComposeRequest(32, cell: cell), new ComposeRng(1));
        await Assert.That(micro.CorridorCells).IsEqualTo(expectedMicro);
        await Assert.That(centi.CorridorCells).IsEqualTo(expectedCenti);
        await Assert.That(micro.WoolCorridorCells <= micro.CorridorCells).IsTrue();
    }

    [Test]
    public async Task Two_team_board_dims_land_within_the_corpus_bands_across_a_sweep()
    {
        // measured: 2-team CTW boards run 50-300 blocks on the short side and 95-620 on the long
        foreach (var players in new[] { 6, 8, 12, 16, 20, 24, 32, 40, 50 })
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var env = Derive(players, seed: seed);
                await Assert.That(env.BoardWidthBlocks >= 40 && env.BoardWidthBlocks <= 300).IsTrue();
                await Assert.That(env.BoardLengthBlocks >= 100 && env.BoardLengthBlocks <= 620).IsTrue();
                await Assert.That(env.BoardLengthBlocks >= env.BoardWidthBlocks).IsTrue();
            }
    }

    [Test]
    public async Task A_bigger_band_fans_a_bigger_board()
    {
        foreach (var seed in new ulong[] { 1, 2, 3, 4 })
        {
            long last = 0;
            foreach (var players in new[] { 8, 16, 24, 32 })     // one count per band, up the whole ladder
            {
                var env = Derive(players, seed: seed);
                long area = (long)env.BoardWidthBlocks * env.BoardLengthBlocks;
                await Assert.That(area > last).IsTrue().Because($"{players} players on seed {seed}");
                last = area;
            }
        }
    }

    [Test]
    public async Task Four_team_board_is_square_within_its_band()
    {
        // measured: 4-team boards are square at every quartile, 165-279 blocks a side
        foreach (var players in new[] { 6, 10, 16, 20, 32 })
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var env = Derive(players, teams: 4, seed: seed);
                await Assert.That(env.BoardWidthBlocks).IsEqualTo(env.BoardLengthBlocks);
                await Assert.That(env.BoardWidthBlocks >= 90 && env.BoardWidthBlocks <= 340).IsTrue();
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
