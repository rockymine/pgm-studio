using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// Ribbon offsetting: a straight 2-point centerline becomes a 4-corner rectangle ring exactly <c>width</c>
/// across; the variable-width strip matches a uniform one when its offsets are constant; degenerate inputs
/// (too few points, mismatched offset lists) return empty.
/// </summary>
public sealed class RibbonTests
{
    private static double[] P(double x, double z) => [x, z];

    [Test]
    public async Task A_straight_centerline_becomes_a_rectangle_of_the_given_width()
    {
        var ring = Ribbon.Uniform([P(0, 0), P(10, 0)], 4);
        await Assert.That(ring.Count).IsEqualTo(4);
        var zSpan = ring.Max(p => p[1]) - ring.Min(p => p[1]);
        await Assert.That(zSpan).IsEqualTo(4d);
        var xSpan = ring.Max(p => p[0]) - ring.Min(p => p[0]);
        await Assert.That(xSpan).IsEqualTo(10d);
    }

    [Test]
    public async Task Fewer_than_two_points_yields_an_empty_strip()
    {
        await Assert.That(Ribbon.Uniform([P(0, 0)], 4).Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_constant_varied_strip_matches_the_uniform_one()
    {
        List<double[]> center = [P(0, 0), P(5, 0), P(10, 0)];
        var strip = Ribbon.Uniform(center, 4);
        var varied = Ribbon.Varied(center, [2, 2, 2], [2, 2, 2]);
        await Assert.That(varied.Count).IsEqualTo(strip.Count);
        for (var i = 0; i < strip.Count; i++)
        {
            await Assert.That(Math.Abs(varied[i][0] - strip[i][0])).IsLessThan(1e-9);
            await Assert.That(Math.Abs(varied[i][1] - strip[i][1])).IsLessThan(1e-9);
        }
    }

    [Test]
    public async Task A_varied_strip_with_mismatched_offset_lengths_returns_empty()
    {
        await Assert.That(Ribbon.Varied([P(0, 0), P(10, 0)], [2], [2, 2]).Count).IsEqualTo(0);
    }
}
