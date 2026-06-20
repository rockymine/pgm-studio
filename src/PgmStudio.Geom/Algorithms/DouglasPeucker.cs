namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Douglas–Peucker polyline simplification: keep the endpoints and every vertex whose perpendicular
/// deviation from the running chord exceeds <c>tolerance</c>, dropping the rest — so a staircased / dense
/// path becomes clean segments with vertices only at real bends. Pure (no deps); operates on an OPEN
/// polyline. The closed-ring + holes wrapper (split at the farthest-apart pair, collinear cleanup) lives in
/// <see cref="PolygonSimplify"/>.
/// </summary>
public static class DouglasPeucker
{
    /// <summary>Simplify an open polyline to the vertices that keep every dropped point within
    /// <paramref name="tolerance"/> of the kept outline. Fewer than 3 points pass through unchanged.</summary>
    public static List<double[]> Simplify(IReadOnlyList<double[]> points, double tolerance)
    {
        var n = points.Count;
        if (n < 3) return [.. points.Select(p => new[] { p[0], p[1] })];
        var keep = new bool[n];
        keep[0] = keep[n - 1] = true;
        Recurse(points, 0, n - 1, tolerance, keep);
        var outp = new List<double[]>();
        for (var i = 0; i < n; i++) if (keep[i]) outp.Add([points[i][0], points[i][1]]);
        return outp;
    }

    private static void Recurse(IReadOnlyList<double[]> pts, int lo, int hi, double tol, bool[] keep)
    {
        if (hi <= lo + 1) return;
        double maxD = -1;
        var idx = -1;
        for (var i = lo + 1; i < hi; i++)
        {
            var d = PerpDistance(pts[i], pts[lo], pts[hi]);
            if (d > maxD) { maxD = d; idx = i; }
        }
        if (maxD <= tol || idx < 0) return;
        keep[idx] = true;
        Recurse(pts, lo, idx, tol, keep);
        Recurse(pts, idx, hi, tol, keep);
    }

    /// <summary>Perpendicular distance from <paramref name="p"/> to the segment a–b (point-to-point when a==b).</summary>
    public static double PerpDistance(double[] p, double[] a, double[] b)
    {
        double dx = b[0] - a[0], dz = b[1] - a[1];
        var len2 = dx * dx + dz * dz;
        if (len2 < 1e-12) return Math.Sqrt((p[0] - a[0]) * (p[0] - a[0]) + (p[1] - a[1]) * (p[1] - a[1]));
        var cross = Math.Abs(dx * (a[1] - p[1]) - (a[0] - p[0]) * dz);
        return cross / Math.Sqrt(len2);
    }
}
