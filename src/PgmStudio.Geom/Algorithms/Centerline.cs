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

    /// <summary>The arc length along <paramref name="centerline"/> at each of the <paramref name="drawn"/> points
    /// it was built from. Drawn point <c>k</c> is dense point <c>k · <see cref="SmoothSamples"/></c> in both
    /// branches of <see cref="Of(IReadOnlyList{double[]})"/> — a spline emits that many samples per drawn segment
    /// beginning at its own start, and a two-point line is subdivided the same number of times — so the mapping is
    /// the one rule stated here rather than re-derived by whoever needs it.</summary>
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

    /// <summary>How many blocks of line one bend of a wandering line takes when none is stated.</summary>
    public const int DefaultWanderLength = 16;

    /// <summary>The farthest a wandering line strays from the drawn one, in blocks.</summary>
    public const double MaxWander = 8;

    /// <summary>The shortest and longest bend a wandering line may state, in blocks of line.</summary>
    public const int MinWanderLength = 4, MaxWanderLength = 64;

    // The noise the drift reads, apart from the field a rough edge reads under the same seed.
    private const uint WanderSalt = 0x5A17;

    /// <summary>The drawn points as the dense curve, drawn aside by a smooth drift so a line stated by a few
    /// points meanders between them rather than running straight — a stroke's <c>wander</c>.
    ///
    /// <para>The curve is resampled at one block of arc, and each sample is pushed along its normal by up to
    /// <paramref name="wander"/> blocks. The push swings from one side to the other every
    /// <paramref name="length"/> blocks of arc (16 unstated), each bend reaching a random 55–100% of the wander,
    /// with a finer octave of value noise over it so no two bends are the same shape. The drift eases in over
    /// the first and last half bend, so the line still starts and ends where it was drawn. A wander of nought
    /// is <see cref="Of(IReadOnlyList{double[]})"/> itself. A parity surface: <c>geometry/stroke.js</c> draws
    /// the same line.</para></summary>
    public static List<double[]> Of(IReadOnlyList<double[]> vertices, double wander, int? length, uint seed)
    {
        var line = Of(vertices);
        var reach = Math.Clamp(wander, 0, MaxWander);
        if (reach <= 0 || line.Count < 2) return line;
        var period = Math.Clamp(length ?? DefaultWanderLength, MinWanderLength, MaxWanderLength);

        var total = Polyline.Length(line);
        var steps = (int)Math.Ceiling(total);
        if (steps < 2) return line;
        var even = Resample(line, total, steps);
        var ease = Math.Min(period / 2.0, total / 4);
        var flip = (int)(PatternNoise.Hash(0, 2, seed + WanderSalt) & 1);

        var drawn = new List<double[]>(even.Count);
        for (var i = 0; i < even.Count; i++)
        {
            var along = total * i / steps;
            var fade = Math.Clamp(Math.Min(along, total - along) / ease, 0, 1);
            fade = fade * fade * (3 - 2 * fade);
            var grain = 2 * PatternNoise.Value(i, 1, seed + WanderSalt, Math.Max(1, period / 2)) - 1;
            var drift = Math.Clamp(Swing(i, period, flip, seed + WanderSalt) + 0.25 * grain, -1, 1);
            var (nx, nz) = NormalAt(even, i);
            var offset = reach * fade * drift;
            drawn.Add([even[i][0] + nx * offset, even[i][1] + nz * offset]);
        }
        return drawn;
    }

    // Which side the line is pushed to at sample i: a bend every `period` samples, alternating sides, each
    // reaching a random 55–100% of the wander, eased between bends.
    private static double Swing(int i, int period, int flip, uint seed)
    {
        var f = (double)i / period;
        var n = (int)Math.Floor(f);
        var t = f - n;
        double a = Bend(n, flip, seed), b = Bend(n + 1, flip, seed);
        return a + (b - a) * (t * t * (3 - 2 * t));
    }

    private static double Bend(int n, int flip, uint seed)
        => (((n + flip) & 1) == 0 ? 1 : -1) * (0.55 + 0.45 * PatternNoise.Unit(n, 0, seed));

    // The line as steps + 1 points an equal arc apart, first and last exactly where they were.
    private static List<double[]> Resample(List<double[]> line, double total, int steps)
    {
        var even = new List<double[]>(steps + 1) { new[] { line[0][0], line[0][1] } };
        var segment = 0;
        var start = 0.0;
        var span = Span(line, 0);
        for (var k = 1; k < steps; k++)
        {
            var target = total * k / steps;
            while (start + span < target && segment < line.Count - 2)
            {
                start += span;
                segment++;
                span = Span(line, segment);
            }
            var t = span > 0 ? (target - start) / span : 0;
            double[] a = line[segment], b = line[segment + 1];
            even.Add([a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]);
        }
        even.Add([line[^1][0], line[^1][1]]);
        return even;
    }

    private static double Span(List<double[]> line, int segment)
    {
        double dx = line[segment + 1][0] - line[segment][0], dz = line[segment + 1][1] - line[segment][1];
        return Math.Sqrt(dx * dx + dz * dz);
    }

    // The left-hand unit normal at sample i, from the samples either side of it.
    private static (double X, double Z) NormalAt(List<double[]> line, int i)
    {
        double[] before = line[Math.Max(0, i - 1)], after = line[Math.Min(line.Count - 1, i + 1)];
        double tx = after[0] - before[0], tz = after[1] - before[1];
        var length = Math.Sqrt(tx * tx + tz * tz);
        return length < 1e-9 ? (0, 0) : (-tz / length, tx / length);
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
