namespace PgmStudio.Geom;

/// <summary>
/// Polygon-ring primitives shared across the runtimes. The even-odd ray-cast here is the C# twin of JS
/// <c>geometry/polygon.js</c> <c>pointInRing</c> (the live-canvas copy stays in JS for the hot path).
/// </summary>
public static class Polygon
{
    /// <summary>True if <c>(px,pz)</c> is inside the polygon <paramref name="ring"/> (a list of
    /// <c>[x,z]</c> pairs; the closing repeat is harmless). Even-odd winding rule.
    /// <para>The pair is taken as any indexable pair, so a ring built as <c>double[][]</c> and one
    /// deserialized off the wire are the same argument rather than two overloads.</para></summary>
    public static bool PointInRing(double px, double pz, IReadOnlyList<IReadOnlyList<double>> ring)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            double xi = ring[i][0], zi = ring[i][1], xj = ring[j][0], zj = ring[j][1];
            if (zi > pz != zj > pz && px < (xj - xi) * (pz - zi) / (zj - zi) + xi) inside = !inside;
        }
        return inside;
    }

    /// <summary>Whether the closed polyline <paramref name="ring"/> crosses itself. Every pair of
    /// non-adjacent edges, which is a few thousand tests on the rings a plan compiles to and is the whole of
    /// what makes an edited outline safe to store — a ring folded over its own far side rasterizes as ground
    /// with a hole nobody drew.
    ///
    /// <para>Touching is not crossing: two edges that share an endpoint or meet at one collinear point pass,
    /// and only a proper crossing fails, so an outline drawn back onto one of its own vertices is kept.</para></summary>
    public static bool SelfIntersects(IReadOnlyList<IReadOnlyList<double>> ring)
    {
        var n = ring.Count;
        for (var i = 0; i < n; i++)
            for (var j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue;                  // the closing edge meets the first
                if (SegmentsCross(ring[i], ring[(i + 1) % n], ring[j], ring[(j + 1) % n])) return true;
            }
        return false;
    }

    /// <summary>Whether segments <c>a→b</c> and <c>c→d</c> properly cross — each straddles the other's line.
    /// A shared or collinear endpoint reads as no crossing.</summary>
    public static bool SegmentsCross(
        IReadOnlyList<double> a, IReadOnlyList<double> b, IReadOnlyList<double> c, IReadOnlyList<double> d) =>
        Side(a, b, c) * Side(a, b, d) < 0 && Side(c, d, a) * Side(c, d, b) < 0;

    private static double Side(IReadOnlyList<double> p, IReadOnlyList<double> q, IReadOnlyList<double> r) =>
        Math.Sign((q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]));
}
