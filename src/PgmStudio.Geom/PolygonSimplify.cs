using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom;

/// <summary>
/// Polygon simplification — collapse a dense ring (e.g. a block-union island outline, all unit-step
/// staircases) into a readable handful of vertices. Wraps the <see cref="DouglasPeucker"/> primitive for a
/// closed ring: split the ring at its two farthest-apart vertices into two open chains, simplify each, then
/// drop any leftover collinear points. Pure — the same outline reads the same on the server and in a
/// preview. Raising <c>tolerance</c> trades fidelity for fewer vertices (a staircase of unit steps
/// straightens once tolerance ≥ ~1).
/// </summary>
public static class PolygonSimplify
{
    /// <summary>A simplified polygon: an exterior ring and zero or more interior rings (holes), each an
    /// open ring (no duplicated closing point).</summary>
    public sealed record SimplePolygon(List<double[]> Exterior, List<List<double[]>> Holes)
    {
        public int VertexCount => Exterior.Count + Holes.Sum(h => h.Count);
    }

    /// <summary>Simplify a polygon with holes: the exterior ring and every interior ring are simplified
    /// independently at <paramref name="tolerance"/>. Holes that collapse below 3 vertices, or below
    /// <paramref name="minHoleArea"/> after simplification, are dropped (staircase noise rather than real
    /// openings). The exterior ring is always kept.</summary>
    public static SimplePolygon Simplify(
        IReadOnlyList<double[]> exterior, IEnumerable<IReadOnlyList<double[]>> holes,
        double tolerance, double minHoleArea = 0)
    {
        var ext = Ring(exterior, tolerance);
        var keptHoles = new List<List<double[]>>();
        foreach (var h in holes)
        {
            var s = Ring(h, tolerance);
            if (s.Count >= 3 && SignedArea(s) >= minHoleArea) keptHoles.Add(s);
        }
        return new SimplePolygon(ext, keptHoles);
    }

    /// <summary>Absolute area of a ring (shoelace).</summary>
    public static double SignedArea(IReadOnlyList<double[]> ring)
    {
        double a = 0;
        for (var i = 0; i < ring.Count; i++)
        {
            var j = (i + 1) % ring.Count;
            a += ring[i][0] * ring[j][1] - ring[j][0] * ring[i][1];
        }
        return Math.Abs(a) / 2;
    }

    /// <summary>Simplify a closed ring to the vertices that keep every dropped point within
    /// <paramref name="tolerance"/> of the kept outline. Input may be open or closed (a duplicated closing
    /// point is handled); returns an open ring (no duplicated closing point). ≤3 distinct points pass
    /// through unchanged.</summary>
    public static List<double[]> Ring(IReadOnlyList<double[]> ring, double tolerance)
    {
        var pts = Dedup(ring);
        var n = pts.Count;
        if (n <= 3) return pts;

        var (a, b) = FarthestPair(pts);
        var chain1 = Slice(pts, a, b);   // a → b (forward)
        var chain2 = Slice(pts, b, a);   // b → a (wrap)
        var s1 = DouglasPeucker.Simplify(chain1, tolerance);
        var s2 = DouglasPeucker.Simplify(chain2, tolerance);

        // each chain shares its endpoints with the other; stitch without duplicating them
        var outp = new List<double[]>(s1);
        for (var i = 1; i < s2.Count - 1; i++) outp.Add(s2[i]);
        return RemoveCollinear(outp, tolerance * tolerance + 1e-9);
    }

    /// <summary>Straighten an OPEN polyline (a lane centerline from a skeleton): keeps the endpoints and the
    /// vertices whose perpendicular deviation exceeds <paramref name="tolerance"/>, so a staircased thinned
    /// path becomes clean segments with vertices only at real bends.</summary>
    public static List<double[]> Polyline(IReadOnlyList<double[]> points, double tolerance)
    {
        var pts = new List<double[]>();
        foreach (var p in points)
            if (pts.Count == 0 || pts[^1][0] != p[0] || pts[^1][1] != p[1]) pts.Add([p[0], p[1]]);
        return DouglasPeucker.Simplify(pts, tolerance);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────
    private static (int A, int B) FarthestPair(List<double[]> pts)
    {
        // anchor at the lowest-leftmost vertex (stable), then take the vertex farthest from it
        var a = 0;
        for (var i = 1; i < pts.Count; i++)
            if (pts[i][0] < pts[a][0] || (pts[i][0] == pts[a][0] && pts[i][1] < pts[a][1])) a = i;
        var b = a;
        double best = -1;
        for (var i = 0; i < pts.Count; i++)
        {
            double dx = pts[i][0] - pts[a][0], dz = pts[i][1] - pts[a][1];
            var d = dx * dx + dz * dz;
            if (d > best) { best = d; b = i; }
        }
        return (a, b);
    }

    // Points from i to j inclusive, walking forward modulo n.
    private static List<double[]> Slice(List<double[]> pts, int i, int j)
    {
        var n = pts.Count;
        var outp = new List<double[]> { pts[i] };
        var k = i;
        while (k != j) { k = (k + 1) % n; outp.Add(pts[k]); }
        return outp;
    }

    private static List<double[]> Dedup(IReadOnlyList<double[]> ring)
    {
        var pts = new List<double[]>(ring.Count);
        foreach (var p in ring)
            if (pts.Count == 0 || pts[^1][0] != p[0] || pts[^1][1] != p[1]) pts.Add([p[0], p[1]]);
        if (pts.Count > 1 && pts[0][0] == pts[^1][0] && pts[0][1] == pts[^1][1]) pts.RemoveAt(pts.Count - 1);
        return pts;
    }

    // Drop vertices whose removal moves the outline by no more than sqrt(areaEps) (collinear cleanup).
    private static List<double[]> RemoveCollinear(List<double[]> pts, double areaEps)
    {
        var n = pts.Count;
        if (n <= 3) return pts;
        var keep = new bool[n];
        for (var i = 0; i < n; i++) keep[i] = true;
        for (var i = 0; i < n; i++)
        {
            var prev = pts[(i - 1 + n) % n];
            var cur = pts[i];
            var next = pts[(i + 1) % n];
            var area2 = Math.Abs((cur[0] - prev[0]) * (next[1] - prev[1]) - (next[0] - prev[0]) * (cur[1] - prev[1]));
            if (area2 <= areaEps) keep[i] = false;
        }
        var outp = new List<double[]>();
        for (var i = 0; i < n; i++) if (keep[i]) outp.Add(pts[i]);
        return outp.Count >= 3 ? outp : pts;
    }
}
