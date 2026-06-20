namespace PgmStudio.Geom;

/// <summary>
/// Lane geometry: offset a centerline polyline into a strip polygon by adding points to either side of it —
/// the "points around a spline" step. Pure (no deps) so the sketch generators and any preview share the
/// exact offsets. A straight centerline yields a rectangle ring; a bent or smoothed one (smooth a sparse
/// control polyline first with <see cref="Algorithms.CatmullRom.Spline"/>) yields a true (non-rectangular)
/// strip that stays ~<c>width</c> wide through its turns.
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

    // Left-hand unit normal of the tangent a→b.
    private static (double Nx, double Nz) Normal(double[] a, double[] b)
    {
        double tx = b[0] - a[0], tz = b[1] - a[1];
        var len = Math.Sqrt(tx * tx + tz * tz);
        return len < 1e-9 ? (0, 0) : (-tz / len, tx / len);
    }
}
