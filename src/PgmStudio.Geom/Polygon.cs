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
}
