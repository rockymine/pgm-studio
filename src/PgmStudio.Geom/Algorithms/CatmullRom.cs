namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Centripetal Catmull-Rom spline: interpolate a sparse control polyline into a dense curve that passes
/// through every control point. Centripetal parameterisation (knot spacing = chord-length^½) is used rather
/// than uniform because uniform Catmull-Rom overshoots and forms cusps/loops when one segment is much
/// shorter than the next — which, once a lane is offset into a strip, self-intersects. Pure (no deps); the
/// offsetting that turns a spline into a strip lives in <see cref="Lane"/>.
/// </summary>
public static class CatmullRom
{
    /// <summary>A dense polyline through <paramref name="points"/> (<paramref name="samplesPerEdge"/> samples
    /// per segment; endpoints duplicated so the curve passes through the first and last point). Fewer than 3
    /// points pass through unchanged.</summary>
    public static List<double[]> Spline(IReadOnlyList<double[]> points, int samplesPerEdge = 12)
    {
        if (points.Count < 3) return [.. points.Select(p => new[] { p[0], p[1] })];
        var p = new List<double[]> { points[0] };
        p.AddRange(points);
        p.Add(points[^1]);
        var outp = new List<double[]>();
        for (var i = 1; i < p.Count - 2; i++)
        {
            double[] p0 = p[i - 1], p1 = p[i], p2 = p[i + 1], p3 = p[i + 2];
            double t0 = 0, t1 = t0 + Knot(p0, p1), t2 = t1 + Knot(p1, p2), t3 = t2 + Knot(p2, p3);
            for (var s = 0; s < samplesPerEdge; s++)
            {
                var t = t1 + (t2 - t1) * s / samplesPerEdge;
                var a1 = Mix(p0, p1, (t1 - t) / (t1 - t0), (t - t0) / (t1 - t0));
                var a2 = Mix(p1, p2, (t2 - t) / (t2 - t1), (t - t1) / (t2 - t1));
                var a3 = Mix(p2, p3, (t3 - t) / (t3 - t2), (t - t2) / (t3 - t2));
                var b1 = Mix(a1, a2, (t2 - t) / (t2 - t0), (t - t0) / (t2 - t0));
                var b2 = Mix(a2, a3, (t3 - t) / (t3 - t1), (t - t1) / (t3 - t1));
                outp.Add(Mix(b1, b2, (t2 - t) / (t2 - t1), (t - t1) / (t2 - t1)));
            }
        }
        outp.Add([points[^1][0], points[^1][1]]);
        return outp;
    }

    // centripetal knot delta = |b−a|^½ (floored so coincident/duplicated control points don't divide by zero)
    private static double Knot(double[] a, double[] b)
    {
        double dx = b[0] - a[0], dz = b[1] - a[1];
        var d = Math.Sqrt(Math.Sqrt(dx * dx + dz * dz));
        return d < 1e-6 ? 1e-6 : d;
    }

    private static double[] Mix(double[] a, double[] b, double wa, double wb) =>
        [a[0] * wa + b[0] * wb, a[1] * wa + b[1] * wb];
}
