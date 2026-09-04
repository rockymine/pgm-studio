namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// The dense curve a drawn open chain of points is read as. Both halves of a stroke are built on it — the
/// outline a preview and the rasterizer take (<see cref="StrokeOutline"/>) and the cells the export writes
/// (<see cref="StrokeFill"/>) — and a distance test measures from it, so the line the author sees is the line
/// everything downstream is centred on.
/// </summary>
public static class Centerline
{
    /// <summary>Curve samples per drawn segment. A parity constant: the JS twin in
    /// <c>geometry/stroke.js</c> must smooth by the same count or the drawn band and the exported one
    /// differ.</summary>
    public const int SmoothSamples = 8;

    /// <summary>The drawn points as the dense curve. Centripetal Catmull-Rom, so a tight bend cannot cusp.
    /// <para>A two-point line is densified rather than passed through. It has no curve to sample, but a width
    /// that varies along the stroke has to have somewhere to vary: left at two points, a taper would read only
    /// its two ends and come out uniformly thin, and a rough edge would not wander at all.</para></summary>
    public static List<double[]> Of(IReadOnlyList<double[]> vertices)
        => vertices.Count switch
        {
            < 2 => [],
            2   => Subdivide(vertices[0], vertices[1]),
            _   => CatmullRom.Spline(vertices, SmoothSamples),
        };

    // The same point count a spline of one segment gives, so a straight stroke and a curved one vary at the
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
}
