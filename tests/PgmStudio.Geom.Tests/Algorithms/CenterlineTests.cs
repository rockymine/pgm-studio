using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>
/// The line a stroke is laid along, and what its <c>wander</c> does to it: a path drawn through a few points
/// meanders between them instead of running straight, starts and ends where it was drawn, never strays further
/// than it says, and is the same line the canvas twin (<c>geometry/stroke.js</c>) draws.
/// </summary>
public sealed class CenterlineTests
{
    private static readonly double[][] StraightLine = [[0, 0], [40, 0]];
    private static readonly double[][] BentLine = [[0, 0], [20, 0], [30, 20], [50, 24]];

    [Test]
    public async Task A_line_that_does_not_wander_is_the_smoothed_line_itself()
    {
        var plain = Centerline.Of(BentLine);
        var still = Centerline.Of(BentLine, wander: 0, length: null, seed: 5);
        await Assert.That(still.Count).IsEqualTo(plain.Count);
        for (var i = 0; i < plain.Count; i++)
            await Assert.That((still[i][0], still[i][1])).IsEqualTo((plain[i][0], plain[i][1]));
    }

    /// <summary>The ends are where the path arrives — a door, a bridge, a spawn — so the drift eases to nothing
    /// at both of them however far the middle strays.</summary>
    [Test]
    public async Task A_wandering_line_starts_and_ends_where_it_was_drawn()
    {
        foreach (var seed in new uint[] { 1, 7, 99 })
        {
            var line = Centerline.Of(BentLine, wander: 6, length: 10, seed);
            await Assert.That((line[0][0], line[0][1])).IsEqualTo((0.0, 0.0));
            await Assert.That((line[^1][0], line[^1][1])).IsEqualTo((50.0, 24.0));
        }
    }

    /// <summary>A straight line forty blocks long wandering four either side swings to both sides of where it
    /// was drawn, and never further than four from it.</summary>
    [Test]
    public async Task A_wandering_line_swings_to_both_sides_and_no_further_than_it_says()
    {
        foreach (var seed in new uint[] { 1, 7, 99, 1234 })
        {
            var sides = Centerline.Of(StraightLine, wander: 4, length: 16, seed).Select(point => point[1]).ToList();
            await Assert.That((seed, sides.Max() > 1, sides.Min() < -1)).IsEqualTo((seed, true, true));
            await Assert.That((seed, sides.All(z => Math.Abs(z) <= 4 + 1e-9))).IsEqualTo((seed, true));
        }
    }

    /// <summary>The wander is capped: a stroke asking for more than <see cref="Centerline.MaxWander"/> strays no
    /// further than that.</summary>
    [Test]
    public async Task A_wander_past_the_cap_strays_no_further_than_the_cap()
    {
        var sides = Centerline.Of(StraightLine, wander: 50, length: 16, seed: 7).Select(point => point[1]);
        await Assert.That(sides.All(z => Math.Abs(z) <= Centerline.MaxWander + 1e-9)).IsTrue();
    }

    /// <summary>Parity with the canvas twin. The numbers are what <c>strokePath</c> in
    /// <c>geometry/stroke.js</c> answers for the same two lines, and <c>tests/js/stroke.test.js</c> asserts the
    /// same ones: if either side moves, the path an author drags stops being the path the map paves.</summary>
    [Test]
    public async Task A_wandering_line_is_the_one_the_canvas_draws()
    {
        var straight = Centerline.Of(StraightLine, wander: 4, length: 16, seed: 7);
        await Assert.That(straight.Count).IsEqualTo(41);
        await Near(straight[10], 10, 1.7314490375516476);
        await Near(straight[20], 20, 1.9731107185594743);
        await Near(straight[30], 30, -2.130037984627416);

        var bent = Centerline.Of(BentLine, wander: 3, length: 12, seed: 11);
        await Assert.That(bent.Count).IsEqualTo(65);
        await Near(bent[5], 5.0044117901701926, -0.03763960969302771);
        await Near(bent[25], 21.197025938442764, 4.377416662103313);
        await Near(bent[45], 31.1441099054315, 21.50995563459855);
    }

    private static async Task Near(double[] point, double x, double z)
    {
        await Assert.That(Math.Abs(point[0] - x) < 1e-9).IsTrue();
        await Assert.That(Math.Abs(point[1] - z) < 1e-9).IsTrue();
    }
}
