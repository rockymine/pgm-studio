namespace PgmStudio.Geom;

/// <summary>
/// Lane geometry: turn a centerline polyline into a constant-width strip polygon, and smooth a sparse
/// control polyline into a dense curve. Pure (no deps) so the sketch generators and any preview share the
/// exact offsets. A straight centerline yields a rectangle ring; a bent or smoothed one yields a true
/// (non-rectangular) strip that stays ~<c>width</c> wide through its turns.
/// </summary>
public static class Lane
{
    /// <summary>Offset <paramref name="centerline"/> by ±<paramref name="width"/>/2 into a closed ring
    /// (left side forward, right side back). Per-vertex averaged normals keep corners ~<paramref
    /// name="width"/> wide. Needs ≥2 points; fewer returns empty.</summary>
    public static List<double[]> Strip(IReadOnlyList<double[]> centerline, double width)
    {
        var n = centerline.Count;
        if (n < 2) return [];
        var half = width / 2.0;
        var left = new List<double[]>(n);
        var right = new List<double[]>(n);
        for (var i = 0; i < n; i++)
        {
            var (nx, nz) = VertexNormal(centerline, i);
            var p = centerline[i];
            left.Add([p[0] + nx * half, p[1] + nz * half]);
            right.Add([p[0] - nx * half, p[1] - nz * half]);
        }
        right.Reverse();
        left.AddRange(right);
        return left;
    }

    /// <summary>A variable-width, possibly asymmetric strip: each centerline point is offset outward by
    /// <paramref name="leftOffset"/>[i] along its left normal and <paramref name="rightOffset"/>[i] along
    /// its right normal. Lets a lane taper and its outline jitter (an organic hull, not a clean rectangle).
    /// The offset lists must match the centerline length; needs ≥2 points.</summary>
    public static List<double[]> Ribbon(IReadOnlyList<double[]> centerline, IReadOnlyList<double> leftOffset, IReadOnlyList<double> rightOffset)
    {
        var n = centerline.Count;
        if (n < 2 || leftOffset.Count != n || rightOffset.Count != n) return [];
        var left = new List<double[]>(n);
        var right = new List<double[]>(n);
        for (var i = 0; i < n; i++)
        {
            var (nx, nz) = VertexNormal(centerline, i);
            var p = centerline[i];
            left.Add([p[0] + nx * leftOffset[i], p[1] + nz * leftOffset[i]]);
            right.Add([p[0] - nx * rightOffset[i], p[1] - nz * rightOffset[i]]);
        }
        right.Reverse();
        left.AddRange(right);
        return left;
    }

    // Averaged left-hand unit normal at centerline vertex i.
    private static (double Nx, double Nz) VertexNormal(IReadOnlyList<double[]> c, int i)
    {
        var n = c.Count;
        if (i == 0) return Normal(c[0], c[1]);
        if (i == n - 1) return Normal(c[i - 1], c[i]);
        var (ax, az) = Normal(c[i - 1], c[i]);
        var (bx, bz) = Normal(c[i], c[i + 1]);
        double mx = ax + bx, mz = az + bz;
        var len = Math.Sqrt(mx * mx + mz * mz);
        return len < 1e-9 ? (ax, az) : (mx / len, mz / len);
    }

    /// <summary>Catmull-Rom through <paramref name="points"/> → a dense polyline (<paramref
    /// name="samplesPerEdge"/> samples per segment, endpoints duplicated so the curve passes through the
    /// first and last). Fewer than 3 points pass through unchanged.</summary>
    public static List<double[]> Smooth(IReadOnlyList<double[]> points, int samplesPerEdge = 12)
    {
        if (points.Count < 3) return [.. points.Select(p => new[] { p[0], p[1] })];
        var p = new List<double[]> { points[0] };
        p.AddRange(points);
        p.Add(points[^1]);
        var outp = new List<double[]>();
        for (var i = 1; i < p.Count - 2; i++)
        {
            double[] p0 = p[i - 1], p1 = p[i], p2 = p[i + 1], p3 = p[i + 2];
            for (var s = 0; s < samplesPerEdge; s++)
            {
                double t = (double)s / samplesPerEdge, t2 = t * t, t3 = t2 * t;
                outp.Add([
                    0.5 * (2*p1[0] + (-p0[0]+p2[0])*t + (2*p0[0]-5*p1[0]+4*p2[0]-p3[0])*t2 + (-p0[0]+3*p1[0]-3*p2[0]+p3[0])*t3),
                    0.5 * (2*p1[1] + (-p0[1]+p2[1])*t + (2*p0[1]-5*p1[1]+4*p2[1]-p3[1])*t2 + (-p0[1]+3*p1[1]-3*p2[1]+p3[1])*t3),
                ]);
            }
        }
        outp.Add([points[^1][0], points[^1][1]]);
        return outp;
    }

    // Left-hand unit normal of the tangent a→b.
    private static (double Nx, double Nz) Normal(double[] a, double[] b)
    {
        double tx = b[0] - a[0], tz = b[1] - a[1];
        var len = Math.Sqrt(tx * tx + tz * tz);
        return len < 1e-9 ? (0, 0) : (-tz / len, tx / len);
    }
}
