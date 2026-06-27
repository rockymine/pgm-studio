namespace PgmStudio.Geom;

/// <summary>
/// Ear-clipping triangulation of a simple polygon ring (no holes) plus barycentric height interpolation —
/// the TIN surface model for per-vertex (anchor) heights. Deterministic (CCW normalisation + first-ear-each-pass)
/// so a future JS twin can reproduce the same triangles for the live 3-D preview. Pure: no DOM, no deps.
/// </summary>
public static class Triangulation
{
    private const double Eps = 1e-9;

    /// <summary>Triangulate <paramref name="poly"/> (an open ring of <c>[x,z]</c> vertices, ≥3) into triangles
    /// given as index triples into <paramref name="poly"/>. Returns a fan fallback if the polygon is degenerate.</summary>
    public static List<(int A, int B, int C)> EarClip(IReadOnlyList<double[]> poly)
    {
        var tris = new List<(int, int, int)>();
        int n = poly.Count;
        if (n < 3) return tris;

        // Work on a CCW index list (clip ears off convex corners only).
        var idx = new List<int>(n);
        if (SignedArea(poly) < 0) for (int i = n - 1; i >= 0; i--) idx.Add(i);
        else                      for (int i = 0; i < n; i++)     idx.Add(i);

        int guard = n * n;
        while (idx.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            int m = idx.Count;
            for (int i = 0; i < m; i++)
            {
                int i0 = idx[(i + m - 1) % m], i1 = idx[i], i2 = idx[(i + 1) % m];
                double[] a = poly[i0], b = poly[i1], c = poly[i2];
                if (Cross(a, b, c) <= Eps) continue;          // reflex / collinear — not an ear (CCW)
                bool empty = true;
                for (int j = 0; j < m; j++)
                {
                    int vj = idx[j];
                    if (vj == i0 || vj == i1 || vj == i2) continue;
                    if (InTriangle(poly[vj], a, b, c)) { empty = false; break; }
                }
                if (!empty) continue;
                tris.Add((i0, i1, i2));
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break;                              // degenerate — stop and fan the remainder
        }
        if (idx.Count == 3) tris.Add((idx[0], idx[1], idx[2]));
        else if (idx.Count > 3) for (int i = 1; i + 1 < idx.Count; i++) tris.Add((idx[0], idx[i], idx[i + 1]));
        return tris;
    }

    /// <summary>Interpolate a height at <c>(px,pz)</c> over the TIN: barycentric blend in the containing
    /// triangle, falling back to the nearest vertex's height for points just outside (e.g. a Bézier fringe).</summary>
    public static double Interpolate(IReadOnlyList<double[]> poly, IReadOnlyList<double> heights,
                                     IReadOnlyList<(int A, int B, int C)> tris, double px, double pz)
    {
        foreach (var (a, b, c) in tris)
        {
            var w = Bary(px, pz, poly[a], poly[b], poly[c]);
            if (w is { } bw && bw.U >= -Eps && bw.V >= -Eps && bw.W >= -Eps)
                return bw.U * heights[a] + bw.V * heights[b] + bw.W * heights[c];
        }
        int best = -1; double bd = double.MaxValue;
        for (int i = 0; i < poly.Count; i++)
        {
            double dx = poly[i][0] - px, dz = poly[i][1] - pz, d = dx * dx + dz * dz;
            if (d < bd) { bd = d; best = i; }
        }
        return best >= 0 ? heights[best] : 0;
    }

    private static double SignedArea(IReadOnlyList<double[]> poly)
    {
        double a = 0;
        for (int i = 0, n = poly.Count; i < n; i++)
        {
            double[] p = poly[i], q = poly[(i + 1) % n];
            a += p[0] * q[1] - q[0] * p[1];
        }
        return a / 2;
    }

    private static double Cross(double[] a, double[] b, double[] c)
        => (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);

    private static bool InTriangle(double[] p, double[] a, double[] b, double[] c)
        => Bary(p[0], p[1], a, b, c) is { } w && w.U >= -Eps && w.V >= -Eps && w.W >= -Eps;

    private static (double U, double V, double W)? Bary(double px, double pz, double[] a, double[] b, double[] c)
    {
        double v0x = b[0] - a[0], v0z = b[1] - a[1], v1x = c[0] - a[0], v1z = c[1] - a[1];
        double den = v0x * v1z - v1x * v0z;
        if (Math.Abs(den) < 1e-12) return null;
        double v2x = px - a[0], v2z = pz - a[1];
        double v = (v2x * v1z - v1x * v2z) / den;
        double w = (v0x * v2z - v2x * v0z) / den;
        return (1 - v - w, v, w);
    }
}
