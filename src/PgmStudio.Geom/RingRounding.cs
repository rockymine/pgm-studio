namespace PgmStudio.Geom;

/// <summary>
/// Round a simplified polygon ring back into a smooth curve by fitting Catmull–Rom tangent handles at its
/// gentle bends, while leaving sharp corners (a turn at or above <c>cornerAngleDeg</c>) as hard vertices.
/// Pure geometry: returns, per vertex index, the cubic-Bézier <c>in</c>/<c>out</c> control points (absolute
/// coords) the renderer and rasterizer consume. It is the inverse of simplifying a dense curve to a
/// polyline — so a generated lane reads as the smooth strip it was, not a staircase of chords — but it keeps
/// rectangles and regular polygons crisp (their turns clear the threshold). A vertex with no entry stays a
/// straight corner.
/// </summary>
public static class RingRounding
{
    /// <summary>The cubic-Bézier handles flanking one ring vertex (absolute coords): <c>Out</c> leads the
    /// outgoing edge, <c>In</c> trails the incoming edge.</summary>
    public readonly record struct Handles(double[] In, double[] Out);

    /// <summary>Bézier handles for a ring that are guaranteed not to make the sampled curve self-intersect:
    /// fit <see cref="Tangents"/> at <paramref name="maxTension"/>, and if the rounded ring crosses itself
    /// (a tight curl whose handles overshoot), ease the tension down and retry; if no tension keeps it simple,
    /// return none (the shape stays a plain polygon). <paramref name="samplesPerEdge"/> must match the
    /// consumer's curve sampling so the check reflects what is actually drawn/rasterized.</summary>
    public static Dictionary<int, Handles> Smooth(
        IReadOnlyList<double[]> ring, double cornerAngleDeg = 40, double maxTension = 1.0 / 3, int samplesPerEdge = 16)
    {
        for (var tension = maxTension; tension >= 0.06; tension *= 0.6)
        {
            var h = Tangents(ring, cornerAngleDeg, tension);
            if (h.Count == 0) return h;                                 // nothing rounds → done
            if (!SelfIntersects(ring, h, samplesPerEdge)) return h;     // simple at this tension → keep it
        }
        return [];                                                     // can't round cleanly → leave polygonal
    }

    /// <summary>Tangent handles for the gentle-bend vertices of a closed ring. <paramref name="cornerAngleDeg"/>
    /// is the turn (degrees) at or above which a vertex is kept sharp (90° = a rectangle corner; a regular
    /// octagon turns 45°). The handle direction is the Catmull–Rom tangent (the neighbour chord), but each
    /// handle's <i>length</i> scales with its own adjacent edge — <paramref name="tension"/> as a fraction of
    /// it (1/3 ≈ a uniform Catmull–Rom on an even curve) — so a handle can't reach past its neighbour and
    /// overshoot into a self-intersection on a tight curl.</summary>
    public static Dictionary<int, Handles> Tangents(IReadOnlyList<double[]> ring, double cornerAngleDeg = 40, double tension = 1.0 / 3)
    {
        var n = ring.Count;
        var map = new Dictionary<int, Handles>();
        if (n < 3) return map;

        for (var i = 0; i < n; i++)
        {
            var prev = ring[(i - 1 + n) % n];
            var cur = ring[i];
            var next = ring[(i + 1) % n];
            if (TurnDeg(prev, cur, next) >= cornerAngleDeg) continue;   // sharp corner → leave it hard

            double dx = next[0] - prev[0], dz = next[1] - prev[1];
            var dl = Math.Sqrt(dx * dx + dz * dz);
            if (dl < 1e-9) continue;
            double ux = dx / dl, uz = dz / dl;                          // Catmull–Rom tangent direction
            var outLen = Dist(cur, next) * tension;                     // bounded by the local edge → no overshoot
            var inLen = Dist(cur, prev) * tension;
            map[i] = new Handles(
                In: [cur[0] - ux * inLen, cur[1] - uz * inLen],
                Out: [cur[0] + ux * outLen, cur[1] + uz * outLen]);
        }
        return map;
    }

    private static double Dist(double[] a, double[] b)
    {
        double dx = b[0] - a[0], dz = b[1] - a[1];
        return Math.Sqrt(dx * dx + dz * dz);
    }

    // ── self-intersection check on the sampled (Bézier where handled, straight otherwise) closed ring ──
    private static bool SelfIntersects(IReadOnlyList<double[]> ring, Dictionary<int, Handles> handles, int per)
    {
        var pts = Sample(ring, handles, per);
        int m = pts.Count;
        for (var i = 0; i < m; i++)
            for (var j = i + 2; j < m; j++)
            {
                if (i == 0 && j == m - 1) continue;   // the wrap-around adjacency
                if (SegmentsCross(pts[i], pts[(i + 1) % m], pts[j], pts[(j + 1) % m])) return true;
            }
        return false;
    }

    private static List<double[]> Sample(IReadOnlyList<double[]> ring, Dictionary<int, Handles> handles, int per)
    {
        var n = ring.Count;
        var pts = new List<double[]>(n * per);
        for (var i = 0; i < n; i++)
        {
            double[] p0 = ring[i], p3 = ring[(i + 1) % n];
            var cpOut = handles.TryGetValue(i, out var hi) ? hi.Out : null;
            var cpIn = handles.TryGetValue((i + 1) % n, out var hj) ? hj.In : null;
            if (cpOut is not null || cpIn is not null)
            {
                double[] c1 = cpOut ?? p0, c2 = cpIn ?? p3;
                for (var k = 0; k < per; k++) pts.Add(Cubic(p0, c1, c2, p3, (double)k / per));
            }
            else
                for (var k = 0; k < per; k++) { var t = (double)k / per; pts.Add([p0[0] + (p3[0] - p0[0]) * t, p0[1] + (p3[1] - p0[1]) * t]); }
        }
        return pts;
    }

    private static double[] Cubic(double[] p0, double[] c1, double[] c2, double[] p3, double t)
    {
        var u = 1 - t;
        double w0 = u * u * u, w1 = 3 * u * u * t, w2 = 3 * u * t * t, w3 = t * t * t;
        return [w0 * p0[0] + w1 * c1[0] + w2 * c2[0] + w3 * p3[0], w0 * p0[1] + w1 * c1[1] + w2 * c2[1] + w3 * p3[1]];
    }

    private static bool SegmentsCross(double[] a, double[] b, double[] c, double[] d) =>
        Ccw(c, d, a) > 0 != Ccw(c, d, b) > 0 && Ccw(a, b, c) > 0 != Ccw(a, b, d) > 0;

    private static double Ccw(double[] p, double[] q, double[] r) =>
        (r[1] - p[1]) * (q[0] - p[0]) - (q[1] - p[1]) * (r[0] - p[0]);

    /// <summary>Turn angle (degrees) at <paramref name="b"/> along a→b→c: 0 = straight, 90 = a right angle,
    /// 180 = a reversal. Degenerate (zero-length) edges report 0.</summary>
    public static double TurnDeg(double[] a, double[] b, double[] c)
    {
        double ux = b[0] - a[0], uz = b[1] - a[1];
        double vx = c[0] - b[0], vz = c[1] - b[1];
        var lu = Math.Sqrt(ux * ux + uz * uz);
        var lv = Math.Sqrt(vx * vx + vz * vz);
        if (lu < 1e-9 || lv < 1e-9) return 0;
        var cos = Math.Clamp((ux * vx + uz * vz) / (lu * lv), -1, 1);
        return Math.Acos(cos) * 180 / Math.PI;
    }
}
