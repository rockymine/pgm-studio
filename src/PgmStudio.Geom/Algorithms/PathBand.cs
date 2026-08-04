namespace PgmStudio.Geom.Algorithms;

/// <summary>How a path's two long edges are drawn — the only thing that separates the variants of a stroke that
/// are still one closed band. <see cref="Solid"/> holds one width the whole way; <see cref="Rough"/> lets the
/// width wander so the outline reads organic rather than ruled; <see cref="Tapered"/> varies it along the
/// stroke, fat in the middle and thin at the ends.</summary>
public enum PathEdge { Solid, Rough, Tapered }

/// <summary>
/// A stroke's footprint: a drawn centerline and a half-width become the closed outline of the band around it.
/// A path is authored as a line, but every system downstream of the sketch — island detection, the orbit fan,
/// per-anchor height, the world export — consumes a ring, so the line becomes a ring here and nothing
/// downstream learns a new shape.
///
/// <para>Two steps, and the order matters. The drawn points are first smoothed into a dense curve
/// (<see cref="CatmullRom.Spline"/>, centripetal so a tight bend cannot cusp), and only then offset to both
/// sides (<see cref="Ribbon"/>). Offsetting the raw points instead would corner the band at every click; and
/// varying the width, which is what separates the three edges below, needs somewhere dense enough to vary
/// along.</para>
///
/// <para>The ends are cut square rather than rounded. A path in a map arrives somewhere — a plaza, a bridge
/// mouth, another path — and a flat end meets those cleanly, where a rounded cap would bulge past them.</para>
/// </summary>
public static class PathBand
{
    /// <summary>Curve samples per drawn segment. A parity constant: the JS twin in
    /// <c>geometry/path.js</c> must smooth by the same count or the drawn band and the exported one differ.</summary>
    public const int SmoothSamples = 8;

    private const int RoughPeriod = 8;         // curve samples per wander, so the edge waves rather than fuzzes
    private const double RoughSwing = 0.45;    // how much of the width a rough edge may gain or lose
    private const int RoughSide = 512;      // the noise row the right edge reads, so the two sides differ
    private const double TaperEnds = 0.35;     // what is left of the width where a tapered path runs out

    /// <summary>The drawn points as the dense curve the band is built around. Also what a preview strokes and
    /// what a distance test measures from, so the line the author sees is the line the band is centred on.
    /// <para>A two-point line is densified rather than passed through. It has no curve to sample, but a width
    /// that varies along the stroke has to have somewhere to vary: left at two points, a taper would read only
    /// its two ends and come out uniformly thin, and a rough edge would not wander at all.</para></summary>
    public static List<double[]> Centerline(IReadOnlyList<double[]> vertices)
        => vertices.Count switch
        {
            < 2 => [],
            2   => Subdivide(vertices[0], vertices[1]),
            _   => CatmullRom.Spline(vertices, SmoothSamples),
        };

    // The same point count a spline of one segment gives, so a straight path and a curved one vary at the
    // same rate along their length.
    private static List<double[]> Subdivide(double[] from, double[] to)
    {
        var points = new List<double[]>(SmoothSamples + 1);
        for (var i = 0; i <= SmoothSamples; i++)
        {
            var t = i / (double)SmoothSamples;
            points.Add([from[0] + (to[0] - from[0]) * t, from[1] + (to[1] - from[1]) * t]);
        }
        return points;
    }

    /// <summary>The closed outline of the band <paramref name="radius"/> blocks to each side of the centerline
    /// through <paramref name="vertices"/>. Fewer than two points, or a non-positive radius, is no band.</summary>
    public static List<double[]> Ring(IReadOnlyList<double[]> vertices, double radius, PathEdge edge, uint seed)
    {
        var centerline = Centerline(vertices);
        if (centerline.Count < 2 || radius <= 0) return [];
        return edge == PathEdge.Solid
            ? Ribbon.Uniform(centerline, radius * 2)
            : Ribbon.Varied(centerline,
                Widths(centerline.Count, radius, edge, seed, 0),
                Widths(centerline.Count, radius, edge, seed, RoughSide));
    }

    // The half-width at each point of the dense centerline. Both non-solid edges are this one function with a
    // different reason to vary: a taper reads how far along the stroke a point is, a rough edge reads a noise
    // field — sampled a long way apart for the two sides (`row`), so they wander independently and the band
    // does not merely breathe in and out.
    private static List<double> Widths(int count, double radius, PathEdge edge, uint seed, int row)
    {
        var widths = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            var along = count > 1 ? i / (double)(count - 1) : 0;
            var scale = edge switch
            {
                PathEdge.Tapered => TaperEnds + (1 - TaperEnds) * Math.Sin(Math.PI * along),
                PathEdge.Rough   => 1 - RoughSwing + 2 * RoughSwing * PatternNoise.Value(i, row, seed, RoughPeriod),
                _                => 1,
            };
            widths.Add(radius * scale);
        }
        return widths;
    }
}
