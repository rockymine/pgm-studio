namespace PgmStudio.Geom.Algorithms;

/// <summary>How a stroke's two long edges are drawn — the only thing that separates the variants of a stroke
/// that are still one closed band. <see cref="Solid"/> holds one width the whole way; <see cref="Rough"/> lets
/// the width wander so the outline reads organic rather than ruled; <see cref="Tapered"/> varies it along the
/// stroke, fat in the middle and thin at the ends.</summary>
public enum StrokeEdge { Solid, Rough, Tapered }

/// <summary>
/// A stroke's footprint: a drawn centerline and a half-width become the closed outline of the band around it.
/// A polyline is authored as a line, but every system downstream of the sketch — island detection, the orbit
/// fan, per-anchor height, the world export — consumes a ring, so the line becomes a ring here and nothing
/// downstream learns a new shape.
///
/// <para>Two steps, and the order matters. The drawn points are first smoothed into a dense curve
/// (<see cref="Centerline"/>), and only then offset to both sides (<see cref="Ribbon"/>). Offsetting the raw
/// points instead would corner the band at every click; and varying the width, which is what separates the
/// three edges above, needs somewhere dense enough to vary along.</para>
///
/// <para>The ends are cut square rather than rounded. A stroke in a map arrives somewhere — a plaza, a bridge
/// mouth, another stroke — and a flat end meets those cleanly, where a rounded cap would bulge past them.</para>
/// </summary>
public static class StrokeOutline
{
    private const int RoughPeriod = 8;         // curve samples per wander, so the edge waves rather than fuzzes
    private const double RoughSwing = 0.45;    // how much of the width a rough edge may gain or lose
    private const int RoughSide = 512;      // the noise row the right edge reads, so the two sides differ
    private const double TaperEnds = 0.35;     // what is left of the width where a tapered stroke runs out

    /// <summary>The closed outline of the band <paramref name="radius"/> blocks to each side of the centerline
    /// through <paramref name="vertices"/>. Fewer than two points, or a non-positive radius, is no band.</summary>
    public static List<double[]> Ring(IReadOnlyList<double[]> vertices, double radius, StrokeEdge edge, uint seed)
    {
        var centerline = Centerline.Of(vertices);
        if (centerline.Count < 2 || radius <= 0) return [];
        return edge == StrokeEdge.Solid
            ? Ribbon.Uniform(centerline, radius * 2)
            : Ribbon.Varied(centerline,
                Widths(centerline.Count, radius, edge, seed, 0),
                Widths(centerline.Count, radius, edge, seed, RoughSide));
    }

    // The half-width at each point of the dense centerline. Both non-solid edges are this one function with a
    // different reason to vary: a taper reads how far along the stroke a point is, a rough edge reads a noise
    // field — sampled a long way apart for the two sides (`row`), so they wander independently and the band
    // does not merely breathe in and out.
    private static List<double> Widths(int count, double radius, StrokeEdge edge, uint seed, int row)
    {
        var widths = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            var along = count > 1 ? i / (double)(count - 1) : 0;
            var scale = edge switch
            {
                StrokeEdge.Tapered => TaperEnds + (1 - TaperEnds) * Math.Sin(Math.PI * along),
                StrokeEdge.Rough   => 1 - RoughSwing + 2 * RoughSwing * PatternNoise.Value(i, row, seed, RoughPeriod),
                _                  => 1,
            };
            widths.Add(radius * scale);
        }
        return widths;
    }
}
