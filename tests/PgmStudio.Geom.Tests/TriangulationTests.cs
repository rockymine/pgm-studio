using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>Ear-clip triangulation + barycentric height interpolation (the TIN surface for anchor heights).</summary>
public sealed class TriangulationTests
{
    private static double[] P(double x, double z) => [x, z];

    [Test]
    public async Task Square_triangulates_to_two_triangles()
    {
        var poly = new List<double[]> { P(0, 0), P(4, 0), P(4, 4), P(0, 4) };
        var tris = Triangulation.EarClip(poly);
        await Assert.That(tris.Count).IsEqualTo(2);          // a quad → 2 triangles
    }

    [Test]
    public async Task Concave_L_triangulates_without_leaving_the_shape()
    {
        // L on a 2×3 grid (the library L) — 6 vertices, one reflex corner → 4 triangles.
        var poly = new List<double[]> { P(0, 0), P(1, 0), P(1, 2), P(2, 2), P(2, 3), P(0, 3) };
        var tris = Triangulation.EarClip(poly);
        await Assert.That(tris.Count).IsEqualTo(4);          // n-2 for a simple polygon
    }

    [Test]
    public async Task Ramp_interpolates_linearly()
    {
        // A unit-scaled square ramp: north edge (z=0) at height 0, south edge (z=10) at height 10.
        var poly = new List<double[]> { P(0, 0), P(10, 0), P(10, 10), P(0, 10) };
        var heights = new double[] { 0, 0, 10, 10 };
        var tris = Triangulation.EarClip(poly);
        // Mid-height at z=5, regardless of x.
        await Assert.That(Triangulation.Interpolate(poly, heights, tris, 3, 5)).IsEqualTo(5).Within(1e-6);
        await Assert.That(Triangulation.Interpolate(poly, heights, tris, 8, 5)).IsEqualTo(5).Within(1e-6);
        await Assert.That(Triangulation.Interpolate(poly, heights, tris, 5, 2)).IsEqualTo(2).Within(1e-6);
    }

    [Test]
    public async Task Vertex_query_returns_its_own_height()
    {
        var poly = new List<double[]> { P(0, 0), P(10, 0), P(10, 10), P(0, 10) };
        var heights = new double[] { 1, 2, 3, 4 };
        var tris = Triangulation.EarClip(poly);
        // A point just inside the (10,10) corner reads ~its height (3).
        await Assert.That(Triangulation.Interpolate(poly, heights, tris, 9.99, 9.99)).IsEqualTo(3).Within(0.05);
    }

    [Test]
    public async Task Outside_point_falls_back_to_nearest_vertex_height()
    {
        var poly = new List<double[]> { P(0, 0), P(10, 0), P(10, 10), P(0, 10) };
        var heights = new double[] { 0, 0, 0, 50 };          // only the (0,10) corner is tall
        var tris = Triangulation.EarClip(poly);
        // Well outside, nearest to (0,10) → 50.
        await Assert.That(Triangulation.Interpolate(poly, heights, tris, -5, 15)).IsEqualTo(50).Within(1e-6);
    }
}
