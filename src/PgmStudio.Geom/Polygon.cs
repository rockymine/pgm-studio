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

    /// <summary>Which cells of the box <c>[minX..maxX] × [minZ..maxZ]</c> have their centre inside
    /// <paramref name="ring"/>, row-major from <c>(minX, minZ)</c>: exactly <see cref="PointInRing"/> at every
    /// <c>(x + 0.5, z + 0.5)</c>, with the same crossing test and the same arithmetic, so the two can never
    /// disagree about a cell.
    /// <para>A row's crossings are found once and the row is filled from them, so the cost is one pass over the
    /// edges per row rather than per cell — what a whole-shape rasterization wants, since a bent coast carries
    /// hundreds of edges.</para></summary>
    public static bool[] CentresInside(IReadOnlyList<IReadOnlyList<double>> ring,
                                       int minX, int minZ, int maxX, int maxZ)
    {
        int width = Math.Max(0, maxX - minX + 1), depth = Math.Max(0, maxZ - minZ + 1);
        var inside = new bool[width * depth];
        var count = ring.Count;
        if (count == 0 || width == 0) return inside;

        var xs = new double[count];
        var zs = new double[count];
        for (var i = 0; i < count; i++) { xs[i] = ring[i][0]; zs[i] = ring[i][1]; }

        var crossings = new double[count];
        for (var row = 0; row < depth; row++)
        {
            double pz = minZ + row + 0.5;
            var found = 0;
            for (int i = 0, j = count - 1; i < count; j = i++)
                if (zs[i] > pz != zs[j] > pz)
                    crossings[found++] = (xs[j] - xs[i]) * (pz - zs[i]) / (zs[j] - zs[i]) + xs[i];
            if (found == 0) continue;
            Array.Sort(crossings, 0, found);

            // A centre is inside when an odd number of crossings lie strictly to its right.
            var passed = 0;
            var offset = row * width;
            for (var column = 0; column < width; column++)
            {
                double px = minX + column + 0.5;
                while (passed < found && crossings[passed] <= px) passed++;
                inside[offset + column] = ((found - passed) & 1) == 1;
            }
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
