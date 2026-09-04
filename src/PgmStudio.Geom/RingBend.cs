using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Geom;

/// <summary>Reads and writes a <see cref="BendSide"/> as the word a caller states — <c>out</c>, <c>in</c>,
/// <c>both</c> — rather than as its ordinal.</summary>
public sealed class BendSideConverter() : JsonStringEnumConverter<BendSide>(JsonNamingPolicy.CamelCase);

/// <summary>Which side of its own edge an inserted point is pulled to.</summary>
[JsonConverter(typeof(BendSideConverter))]
public enum BendSide
{
    /// <summary>Out of the ring. The coast gains a little ground and reads as a slight bloat of the outline
    /// the plan drew, which is what makes a compiled rectangle look like land.</summary>
    Out,

    /// <summary>Into the ring. The coast loses a little ground and stays within the footprint the plan
    /// drew — what a board whose shapes abut on a measured strait asks for.</summary>
    In,

    /// <summary>Whichever way the wander points at that place, so the coast crosses the plan's line and
    /// wanders both sides of it. The reach falls to nothing where the side turns over, so the crossing is a
    /// smooth one rather than a step.</summary>
    Both,
}

/// <summary>
/// A rectilinear outline drawn as a coast: the ring resampled along its long edges, each inserted point
/// pulled off its edge by a deterministic wander, and <see cref="RingRounding"/>'s handles fitted over the
/// result. What turns the staircase of rectangles a plan compiles to into ground that reads as land.
///
/// <para><b>The ring's own vertices never move.</b> Only the points <em>between</em> them do, so a corner
/// stays where the plan put it and the neck a spur hangs off keeps its width. Which way those points move is
/// the caller's, as <see cref="BendSide"/>: a coast that bloats outward reads organic and is what most boards
/// want, one held inward keeps the plan's footprint where shapes abut on a measured strait, and one free to
/// go either way wanders across the line the plan drew.</para>
///
/// <para>The side is decided by asking rather than by winding. Each inserted point is offered both
/// perpendiculars and takes the one that lands on the side asked for, so the answer is right for a ring wound
/// either way and for a concave stretch as readily as a convex one. A point whose two offsets read the same
/// side — a neck or a notch thinner than twice the wander — stays on its edge and is counted, because a coast
/// quietly straighter than the one that was asked for is worse than one that says so.</para>
/// </summary>
public static class RingBend
{
    /// <summary>A drawn coast: the resampled ring, the handles over it, how many points were inserted, and
    /// how many of those had no room on the side asked for and stayed on their edge.</summary>
    public readonly record struct Coast(
        IReadOnlyList<double[]> Ring, Dictionary<int, RingRounding.Handles> Controls, int Inserted, int Held);

    /// <summary>The coast for <paramref name="ring"/>, or null where the drawn ring crosses itself — a
    /// wander wide enough, or a step short enough, to fold the outline over its own far side. Nothing is
    /// clamped: an outline that cannot be drawn as asked is one the caller has to ask for differently.</summary>
    /// <param name="ring">The outline to draw as a coast, as <c>[x, z]</c> pairs.</param>
    /// <param name="wander">How far, in blocks, an inserted point may be pulled off its edge.</param>
    /// <param name="step">How often to insert along an edge, in blocks. An edge with room for fewer than two
    /// cuts is left straight, which keeps a neck and a short face exactly as the plan drew them.</param>
    /// <param name="seed">Which coast. The wander is two sines of incommensurate period over the point's own
    /// place on the board, so a coast never repeats along an edge and the same seed draws the same one.</param>
    /// <param name="tension">The Catmull-Rom handle length as a fraction of its own edge.</param>
    /// <param name="cornerAngleDeg">The turn at or above which a vertex stays a hard corner.</param>
    /// <param name="side">Which way the inserted points move.</param>
    public static Coast? Draw(IReadOnlyList<double[]> ring, double wander, double step, uint seed,
                              double tension = 0.22, double cornerAngleDeg = 40,
                              BendSide side = BendSide.Out)
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
                var signal = Signal(px, pz, seed);
                var reach = wander * (side is BendSide.Both ? Math.Abs(signal) : 0.5 + 0.5 * signal);
                inserted++;

                // A wander that comes out at nothing is the coast the noise asked for, not a point with
                // nowhere to go: the point stays on its edge and is not counted against the reach.
                if (reach < 0.05) { drawn.Add([Round(px), Round(pz)]); continue; }

                var wantInside = side is BendSide.In || (side is BendSide.Both && signal < 0);
                var plus = Inside(px + nx * reach, pz + nz * reach, ring);
                var minus = Inside(px - nx * reach, pz - nz * reach, ring);

                if (plus == wantInside && minus != wantInside) drawn.Add([Round(px + nx * reach), Round(pz + nz * reach)]);
                else if (minus == wantInside && plus != wantInside) drawn.Add([Round(px - nx * reach), Round(pz - nz * reach)]);
                else { drawn.Add([Round(px), Round(pz)]); held++; }
            }
        }

        return Polygon.SelfIntersects(drawn) ? null
            : new Coast(drawn, RingRounding.Smooth(drawn, cornerAngleDeg, tension), inserted, held);
    }

    /// <summary>The wander at this point, in <c>[-1, 1]</c>. Two sines whose periods share no common multiple,
    /// over the point's own place on the board, so the coast never repeats along a long edge and re-drawing
    /// the same ring with the same seed gives the same coast. A one-sided bend reads it as the magnitude
    /// <c>0.5 + 0.5·signal</c>; a two-sided one reads the sign as the side and the modulus as the reach.</summary>
    private static double Signal(double x, double z, uint seed) =>
        Math.Sin(x / 13.7 + seed) * Math.Sin(z / 21.3 + seed * 1.7);

    private static bool Inside(double x, double z, IReadOnlyList<double[]> ring) =>
        Polygon.PointInRing(x, z, ring);

    private static double Round(double at) => Math.Round(at, 1);
}
