using PgmStudio.Analysis.Playability;
using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// What kind of ground a relief is, and whether it was ever graded — the two numbers that tell a quarry from
/// a mountainside, which carry the same elevation and are not the same ground.
///
/// <para>The bands are calibrated on the boards this repository has built and the author's own reading of
/// them, so the readings are the fixtures: they are what the rule means, and a band moved without them moving
/// is a rule about nothing.</para>
/// </summary>
public sealed class ReliefLandformTests
{
    // The measured boards, as (relief, cells) — the two numbers the classifier reads.
    private static readonly (string Board, int Relief, int Cells, string Expected)[] Measured =
    [
        ("opus5-whinnymoor", 4, 3775, Landform.Plain),        // author: plains — 0.065
        ("opus5-overwall", 7, 24516, Landform.Plain),         // 0.045
        ("opus5-hollowbank", 7, 5470, Landform.Plain),        // 0.095
        ("opus5-sandcaster", 23, 13851, Landform.Rolling),    // 0.195
        ("opus5-thornfell", 32, 19093, Landform.Rolling),     // author: good rolling hills — 0.232
        ("opus5-cairnmeadow", 24, 8181, Landform.Rolling),    // 0.265
        ("opus5-tarnfell", 74, 33942, Landform.Hills),        // author: smooth-ish — 0.402
        ("opus5-deepcut", 28, 4736, Landform.Hills),          // 0.407 — a mountain's elevation, small board
        ("opus5-ravensmere", 65, 21353, Landform.Hills),      // 0.445
        ("opus5-sandcaster-ii", 55, 10996, Landform.Mountain),// 0.524
    ];

    [Test]
    public async Task Every_measured_board_reads_as_the_ground_it_is()
    {
        foreach (var (board, relief, cells, expected) in Measured)
            await Assert.That(ReliefReadback.LandformOf(relief, cells))
                .IsEqualTo(expected).Because(board);
    }

    /// <summary>Elevation is read <b>for the board's own size</b>, which is the comparison an eye makes and a
    /// bare range does not. Deepcut carries 28 blocks over 4,736 cells and Elderwold 22 over 13,950: nearly
    /// the same range, and one is a quarry in a small board while the other is a slope across a big one.</summary>
    [Test]
    public async Task The_same_range_is_a_different_landform_on_a_different_board()
    {
        await Assert.That(ReliefReadback.LandformOf(28, 4736)).IsEqualTo(Landform.Hills);
        await Assert.That(ReliefReadback.LandformOf(22, 13950)).IsEqualTo(Landform.Rolling);
    }

    /// <summary>A third of Thornfell's elevation is what the author put a flatter plain at, and it reads as
    /// one: 0.232 becomes 0.077.</summary>
    [Test]
    public async Task A_third_of_a_rolling_boards_elevation_is_a_plain()
    {
        await Assert.That(ReliefReadback.LandformOf(32, 19093)).IsEqualTo(Landform.Rolling);
        await Assert.That(ReliefReadback.LandformOf(32 / 3, 19093)).IsEqualTo(Landform.Plain);
    }

    [Test]
    public async Task Smoothing_is_scramble_over_barrier_and_a_surface_with_no_barrier_is_smoothest()
    {
        // Thornfell: 36,072 walk · 1,337 scramble · 177 barrier
        await Assert.That(ReliefReadback.SmoothingOf(
            new Dictionary<string, int> { ["walk"] = 36072, ["scramble"] = 1337, ["barrier"] = 177 }))
            .IsGreaterThan(ReliefReadback.Smoothed);

        // Deepcut: not one scramble on it, and 7.85% of its steps are barriers
        await Assert.That(ReliefReadback.SmoothingOf(
            new Dictionary<string, int> { ["walk"] = 11000, ["scramble"] = 0, ["barrier"] = 940 }))
            .IsEqualTo(0);

        await Assert.That(ReliefReadback.SmoothingOf(new Dictionary<string, int> { ["walk"] = 100 }))
            .IsEqualTo(double.PositiveInfinity);
    }

    private static ReliefReadback.Result Read(int relief, int cells, int scramble, int barrier) =>
        new(cells, 0, relief, relief, [], new Dictionary<string, int>
            { ["walk"] = cells, ["scramble"] = scramble, ["barrier"] = barrier },
            [], 0, new ReliefReadback.Fords(0, 0, 0, 0), new ReliefReadback.Fords(0, 0, 0, 0), 0,
            ReliefReadback.LandformOf(relief, cells),
            ReliefReadback.SmoothingOf(new Dictionary<string, int>
                { ["scramble"] = scramble, ["barrier"] = barrier }));

    [Test]
    public async Task An_island_that_says_what_it_is_is_read_back_against_it()
    {
        // Deepcut's shape, declared a plain: hills measured, and the elevation never graded.
        var quarry = Read(28, 4736, scramble: 0, barrier: 940);
        var findings = ReliefReadback.Check(quarry, Landform.Plain, "team");
        await Assert.That(findings.Select(f => f.Rule))
            .Contains(ReliefRules.LandformMismatch).And.Contains(ReliefRules.NotSmoothed);
        await Assert.That(findings.All(f => f.Severity == Severity.Complaint)).IsTrue();

        // Thornfell's, declared what it is: nothing to say.
        var rolling = Read(32, 19093, scramble: 1337, barrier: 177);
        await Assert.That(ReliefReadback.Check(rolling, Landform.Rolling, "team")).IsEmpty();
    }

    /// <summary>An island that states nothing has nothing to disagree with, and still answers for how its
    /// ground was made — the smoothing is about the making, not about the intent.</summary>
    [Test]
    public async Task An_island_that_states_nothing_is_still_read_for_its_smoothing()
    {
        var quarry = Read(28, 4736, scramble: 0, barrier: 940);
        var findings = ReliefReadback.Check(quarry, null, "team");
        await Assert.That(findings.Single().Rule).IsEqualTo(ReliefRules.NotSmoothed);
    }

    /// <summary>A surface with no barrier on it divides by nothing. The read answers infinity, which is the
    /// smoothest a surface gets and is <b>not a number JSON can carry</b> — the wire answers null for it, and
    /// `opus5-whinnymoor` is such a board: four blocks of range and not one barrier.</summary>
    [Test]
    public async Task A_surface_with_no_barrier_is_infinitely_smooth_and_the_wire_carries_none()
    {
        var flat = ReliefReadback.SmoothingOf(new Dictionary<string, int> { ["walk"] = 20800 });
        await Assert.That(double.IsInfinity(flat)).IsTrue();
        await Assert.That(double.IsInfinity(flat) ? (double?)null : flat).IsNull();
    }

    /// <summary>A plain has no elevation to have shaped, so it is never unsmoothed ground.</summary>
    [Test]
    public async Task A_plain_is_never_unsmoothed()
    {
        var flat = Read(4, 3775, scramble: 0, barrier: 3);
        await Assert.That(ReliefReadback.Check(flat, Landform.Plain, "team")).IsEmpty();
    }
}
