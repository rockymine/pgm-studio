using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>
/// Douglas–Peucker open-polyline simplification: a straight run collapses to its endpoints, an L keeps the
/// corner, and the perpendicular-distance primitive measures deviation from the chord.
/// </summary>
public sealed class DouglasPeuckerTests
{
    private static double[] P(double x, double z) => [x, z];

    [Test]
    public async Task A_straight_polyline_keeps_only_its_endpoints()
    {
        var s = DouglasPeucker.Simplify([P(0, 0), P(3, 0), P(6, 0), P(9, 0)], 0.5);
        await Assert.That(s.Count).IsEqualTo(2);
    }

    [Test]
    public async Task An_L_path_keeps_the_corner()
    {
        List<double[]> path = [];
        for (var z = 0; z <= 10; z++) path.Add(P(0, z));
        for (var x = 1; x <= 10; x++) path.Add(P(x, 10));
        var s = DouglasPeucker.Simplify(path, 1.0);
        await Assert.That(s.Count).IsEqualTo(3);
        await Assert.That(s[1][0]).IsEqualTo(0d);
        await Assert.That(s[1][1]).IsEqualTo(10d);
    }

    [Test]
    public async Task Fewer_than_three_points_pass_through_unchanged()
    {
        var s = DouglasPeucker.Simplify([P(0, 0), P(5, 5)], 0.5);
        await Assert.That(s.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Perpendicular_distance_is_zero_on_the_line_and_positive_off_it()
    {
        await Assert.That(DouglasPeucker.PerpDistance(P(5, 0), P(0, 0), P(10, 0))).IsEqualTo(0d);
        await Assert.That(DouglasPeucker.PerpDistance(P(5, 3), P(0, 0), P(10, 0))).IsEqualTo(3d);
    }
}
