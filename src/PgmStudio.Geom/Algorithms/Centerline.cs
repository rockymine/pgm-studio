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

    /// <summary>The arc length along <paramref name="centerline"/> at each of the <paramref name="drawn"/>
    /// points it was built from. Drawn point <c>k</c> is dense point <c>k · <see cref="SmoothSamples"/></c>
    /// in both branches of <see cref="Of"/> — a spline emits that many samples per drawn segment beginning at
    /// its own start, and a two-point line is subdivided the same number of times — so the mapping is the one
    /// rule stated here rather than re-derived by whoever needs it.</summary>
    public static double[] Anchors(IReadOnlyList<double[]> centerline, int drawn)
    {
        var arcs = new double[Math.Max(drawn, 0)];
        if (drawn < 2 || centerline.Count < 2) return arcs;

        double along = 0;
        var next = 1;
        for (var i = 1; i < centerline.Count && next < drawn; i++)
        {
            double dx = centerline[i][0] - centerline[i - 1][0], dz = centerline[i][1] - centerline[i - 1][1];
            along += Math.Sqrt(dx * dx + dz * dz);
            if (i == next * SmoothSamples) arcs[next++] = along;
        }
        // A drawn point past the last dense sample — a degenerate curve — takes the whole length rather than
        // nought, so the profile stays monotone and a lookup never brackets backwards.
        for (; next < drawn; next++) arcs[next] = along;
        return arcs;
    }

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

/// <summary>A quantity stated at each drawn point of an open line and read anywhere along it — the thickness
/// a graded polyline carries, interpolated <b>along the arc</b> between the two drawn points that bracket the
/// place asked about.
///
/// <para>It is the open line's answer to what a TIN is for a closed ring. A ring encloses its own footprint,
/// so a height per vertex interpolates over a triangulation of it; an open line's drawn points are its
/// centreline and enclose nothing, and every cell of the band around it is somewhere <em>along</em> that line
/// rather than inside a polygon of it.</para></summary>
/// <param name="Anchors">The arc length at each drawn point, ascending.</param>
/// <param name="Stated">What is stated at each of them, one to one with <paramref name="Anchors"/>.</param>
public readonly record struct ArcProfile(double[] Anchors, double[] Stated)
{
    /// <summary>The profile for <paramref name="stated"/> along <paramref name="centerline"/>, or null where
    /// the two do not line up — a value per drawn point is what makes the reading one to one, and fewer than
    /// two points is no line to read along.</summary>
    public static ArcProfile? Of(IReadOnlyList<double[]> centerline, IReadOnlyList<double> stated)
    {
        if (stated.Count < 2 || centerline.Count < 2) return null;
        var anchors = Centerline.Anchors(centerline, stated.Count);
        return anchors[^1] <= 0 ? null : new ArcProfile(anchors, [.. stated]);
    }

    /// <summary>What is stated <paramref name="arc"/> blocks along the line: the two drawn points bracketing
    /// it, mixed by how far between them it sits. Before the first and past the last it is that end's own
    /// value, so a band's square-cut ends carry the height they were drawn at.</summary>
    public double At(double arc)
    {
        if (arc <= Anchors[0]) return Stated[0];
        for (var i = 1; i < Anchors.Length; i++)
        {
            if (arc > Anchors[i]) continue;
            var span = Anchors[i] - Anchors[i - 1];
            var t = span <= 1e-9 ? 0 : (arc - Anchors[i - 1]) / span;
            return Stated[i - 1] + (Stated[i] - Stated[i - 1]) * t;
        }
        return Stated[^1];
    }
}
