using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>
/// Centripetal Catmull-Rom spline. The curve interpolates (passes through) its control points, a collinear
/// control set stays on the line (no overshoot), and density scales with samples-per-edge.
/// </summary>
public sealed class CatmullRomTests
{
    private static double[] P(double x, double z) => [x, z];

    [Test]
    public async Task Fewer_than_three_points_pass_through_unchanged()
    {
        var s = CatmullRom.Spline([P(0, 0), P(10, 0)]);
        await Assert.That(s.Count).IsEqualTo(2);
    }

    [Test]
    public async Task The_curve_passes_through_the_first_and_last_control_point()
    {
        var s = CatmullRom.Spline([P(0, 0), P(5, 8), P(10, 0)], samplesPerEdge: 8);
        await Assert.That(s[0][0]).IsEqualTo(0d);
        await Assert.That(s[0][1]).IsEqualTo(0d);
        await Assert.That(s[^1][0]).IsEqualTo(10d);
        await Assert.That(s[^1][1]).IsEqualTo(0d);
    }

    [Test]
    public async Task Collinear_controls_stay_on_the_line_without_overshoot()
    {
        var s = CatmullRom.Spline([P(0, 0), P(5, 0), P(10, 0)], samplesPerEdge: 12);
        foreach (var p in s) await Assert.That(Math.Abs(p[1])).IsLessThan(1e-6);
    }

    [Test]
    public async Task Density_scales_with_samples_per_edge()
    {
        var sparse = CatmullRom.Spline([P(0, 0), P(5, 8), P(10, 0)], samplesPerEdge: 4);
        var dense = CatmullRom.Spline([P(0, 0), P(5, 8), P(10, 0)], samplesPerEdge: 16);
        await Assert.That(dense.Count).IsGreaterThan(sparse.Count);
    }
}
