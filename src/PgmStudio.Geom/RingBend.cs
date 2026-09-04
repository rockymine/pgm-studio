namespace PgmStudio.Geom;

/// <summary>
/// A rectilinear outline drawn as a coast: the ring resampled along its long edges, each inserted point
/// pulled <b>inward</b> by a deterministic wander, and <see cref="RingRounding"/>'s handles fitted over the
/// result. What turns the staircase of rectangles a plan compiles to into ground that reads as land.
///
/// <para><b>The ring's own vertices never move, and no point ever moves outward.</b> Those are the two
/// rules, and they are what make a bend safe on a board that has been measured: a vertex moved outward can
/// cross the mirror line, close the strait a capture board is judged on, or leave the plan's own footprint,
/// and a corner moved at all narrows the neck a spur hangs off. So only the points <em>between</em> the
/// vertices move, and only into the land — the coast can lose a few blocks and can never gain one.</para>
///
/// <para>Inward is decided by asking rather than by winding. Each inserted point is offered both
/// perpendiculars and takes whichever lands inside the original ring, so the answer is right for a ring
/// wound either way and for a concave stretch as readily as a convex one. A point with land on neither side
/// — a neck thinner than twice the wander — stays on its edge and is counted, because a coast quietly
/// straighter than the one that was asked for is worse than one that says so.</para>
/// </summary>
public static class RingBend
{
    /// <summary>A drawn coast: the resampled ring, the handles over it, how many points were inserted, and
    /// how many of those had no room to move and stayed on their edge.</summary>
    public readonly record struct Coast(
        IReadOnlyList<double[]> Ring, Dictionary<int, RingRounding.Handles> Controls, int Inserted, int Held);

    /// <summary>The coast for <paramref name="ring"/>, or null where the drawn ring crosses itself — a
    /// wander wide enough, or a step short enough, to fold the outline over its own far side. Nothing is
    /// clamped: an outline that cannot be drawn as asked is one the caller has to ask for differently.</summary>
    /// <param name="ring">The outline to draw as a coast, as <c>[x, z]</c> pairs.</param>
    /// <param name="wander">How far, in blocks, an inserted point may be pulled in.</param>
    /// <param name="step">How often to insert along an edge, in blocks. An edge with room for fewer than two
    /// cuts is left straight, which keeps a neck and a short face exactly as the plan drew them.</param>
    /// <param name="seed">Which coast. The wander is two sines of incommensurate period over the point's own
    /// place on the board, so a coast never repeats along an edge and the same seed draws the same one.</param>
    /// <param name="tension">The Catmull-Rom handle length as a fraction of its own edge.</param>
    /// <param name="cornerAngleDeg">The turn at or above which a vertex stays a hard corner.</param>
    public static Coast? Draw(IReadOnlyList<double[]> ring, double wander, double step, uint seed,
                              double tension = 0.22, double cornerAngleDeg = 40)
    {
        if (ring.Count < 3 || step <= 0) return null;

        var drawn = new List<double[]>();
        int inserted = 0, held = 0;
        for (var i = 0; i < ring.Count; i++)
        {
            var (ax, az) = (ring[i][0], ring[i][1]);
            var (bx, bz) = (ring[(i + 1) % ring.Count][0], ring[(i + 1) % ring.Count][1]);
            drawn.Add([ax, az]);

            var length = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            var cuts = (int)(length / step);
            if (cuts < 2 || length < 1e-9) continue;

            double nx = -(bz - az) / length, nz = (bx - ax) / length;
            for (var cut = 1; cut < cuts; cut++)
            {
                var t = (double)cut / cuts;
                double px = ax + (bx - ax) * t, pz = az + (bz - az) * t;
                var reach = wander * Wander(px, pz, seed);
                inserted++;

                // A wander that comes out at nothing is the coast the noise asked for, not a point with
                // nowhere to go: the point stays on its edge and is not counted against the reach.
                if (reach < 0.05) { drawn.Add([Round(px), Round(pz)]); continue; }

                if (Inside(px + nx * reach, pz + nz * reach, ring)) drawn.Add([Round(px + nx * reach), Round(pz + nz * reach)]);
                else if (Inside(px - nx * reach, pz - nz * reach, ring)) drawn.Add([Round(px - nx * reach), Round(pz - nz * reach)]);
                else { drawn.Add([Round(px), Round(pz)]); held++; }
            }
        }

        return Crosses(drawn) ? null
            : new Coast(drawn, RingRounding.Smooth(drawn, cornerAngleDeg, tension), inserted, held);
    }

    /// <summary>How far in this point is pulled, as a fraction of the wander. Two sines whose periods share
    /// no common multiple, over the point's own place on the board, so the coast never repeats along a long
    /// edge and re-drawing the same ring with the same seed gives the same coast.</summary>
    private static double Wander(double x, double z, uint seed) =>
        0.5 + 0.5 * Math.Sin(x / 13.7 + seed) * Math.Sin(z / 21.3 + seed * 1.7);

    private static bool Inside(double x, double z, IReadOnlyList<double[]> ring) =>
        Polygon.PointInRing(x, z, ring);

    private static double Round(double at) => Math.Round(at, 1);

    /// <summary>Whether the drawn ring crosses itself. Every pair of non-adjacent edges, which is a few
    /// thousand tests on the rings a plan compiles to and is the whole of what makes the answer safe to
    /// store — a coast folded over its own far side rasterizes as ground with a hole nobody drew.</summary>
    private static bool Crosses(IReadOnlyList<double[]> ring)
    {
        var n = ring.Count;
        for (var i = 0; i < n; i++)
            for (var j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue;                  // the closing edge meets the first
                if (Meets(ring[i], ring[(i + 1) % n], ring[j], ring[(j + 1) % n])) return true;
            }
        return false;
    }

    private static bool Meets(double[] a, double[] b, double[] c, double[] d) =>
        Side(a, b, c) * Side(a, b, d) < 0 && Side(c, d, a) * Side(c, d, b) < 0;

    private static double Side(double[] p, double[] q, double[] r) =>
        Math.Sign((q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]));
}
