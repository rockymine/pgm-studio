using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// Closed-ring simplification + the polygon-with-holes wrapper. A ring padded with collinear points
/// collapses back to its corners; holes are simplified independently and noise holes are dropped.
/// </summary>
public sealed class PolygonSimplifyTests
{
    private static double[] P(double x, double z) => [x, z];

    [Test]
    public async Task A_rectangle_with_edge_points_collapses_to_four_corners()
    {
        // 6×6 square with extra collinear points along each edge
        List<double[]> ring =
        [
            P(0, 0), P(2, 0), P(4, 0), P(6, 0), P(6, 2), P(6, 4), P(6, 6),
            P(4, 6), P(2, 6), P(0, 6), P(0, 4), P(0, 2),
        ];
        var s = PolygonSimplify.Ring(ring, 0.5);
        await Assert.That(s.Count).IsEqualTo(4);
    }

    [Test]
    public async Task A_staircase_diagonal_straightens_under_tolerance()
    {
        // a triangle whose hypotenuse is drawn as unit steps; tolerance > 1 should recover ~3 corners
        List<double[]> ring = [P(0, 0), P(10, 0)];
        for (var k = 10; k >= 0; k--) { ring.Add(P(k, 10 - k)); }   // staircase from (10,0) up to (0,10)
        var s = PolygonSimplify.Ring(ring, 1.5);
        await Assert.That(s.Count).IsLessThanOrEqualTo(4);
    }

    [Test]
    public async Task Fewer_points_keep_the_rough_shape_of_a_circle()
    {
        var ring = new List<double[]>();
        for (var i = 0; i < 64; i++)
        {
            var a = 2 * Math.PI * i / 64;
            ring.Add(P(Math.Round(50 + 20 * Math.Cos(a)), Math.Round(50 + 20 * Math.Sin(a))));
        }
        var s = PolygonSimplify.Ring(ring, 2.0);
        await Assert.That(s.Count).IsLessThan(64);
        await Assert.That(s.Count).IsGreaterThanOrEqualTo(6);
    }

    [Test]
    public async Task Polygon_with_a_hole_simplifies_both_rings()
    {
        List<double[]> exterior =
        [
            P(0, 0), P(5, 0), P(10, 0), P(10, 5), P(10, 10), P(5, 10), P(0, 10), P(0, 5),
        ];
        List<double[]> hole = [P(3, 3), P(5, 3), P(7, 3), P(7, 7), P(5, 7), P(3, 7)];
        var poly = PolygonSimplify.Simplify(exterior, [hole], 0.5);
        await Assert.That(poly.Exterior.Count).IsEqualTo(4);
        await Assert.That(poly.Holes.Count).IsEqualTo(1);
        await Assert.That(poly.Holes[0].Count).IsEqualTo(4);
    }

    [Test]
    public async Task An_open_L_path_straightens_to_three_vertices()
    {
        // a staircase up then right (an L): simplify keeps the two ends + the corner
        List<double[]> path = [];
        for (var z = 0; z <= 10; z++) path.Add(P(0, z));
        for (var x = 1; x <= 10; x++) path.Add(P(x, 10));
        var s = PolygonSimplify.Polyline(path, 1.0);
        await Assert.That(s.Count).IsEqualTo(3);
        await Assert.That(s[1][0]).IsEqualTo(0d);   // the corner at (0,10)
        await Assert.That(s[1][1]).IsEqualTo(10d);
    }

    [Test]
    public async Task A_straight_open_path_keeps_only_its_endpoints()
    {
        List<double[]> path = [P(0, 0), P(3, 0), P(6, 0), P(9, 0)];
        var s = PolygonSimplify.Polyline(path, 0.5);
        await Assert.That(s.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Noise_holes_below_min_area_are_dropped()
    {
        List<double[]> exterior = [P(0, 0), P(20, 0), P(20, 20), P(0, 20)];
        List<double[]> bigHole = [P(4, 4), P(12, 4), P(12, 12), P(4, 12)];   // area 64
        List<double[]> tinyHole = [P(15, 15), P(16, 15), P(16, 16), P(15, 16)]; // area 1
        var poly = PolygonSimplify.Simplify(exterior, [bigHole, tinyHole], 0.5, minHoleArea: 4);
        await Assert.That(poly.Holes.Count).IsEqualTo(1);
    }
}
