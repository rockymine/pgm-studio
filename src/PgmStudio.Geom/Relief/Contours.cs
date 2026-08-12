namespace PgmStudio.Geom.Relief;

/// <summary>One traced contour: the height it follows, the points it passes through, and whether it closes on
/// itself. An open line is one that ran into the edge of the footprint, which is the common case — a hillside
/// meeting the void — and not a defect.</summary>
public sealed record ContourLine(double Level, IReadOnlyList<(double X, double Z)> Points, bool Closed);

/// <summary>
/// Traces a solved surface into contour lines. This is how a height field is read at a glance — the shape of
/// the ground, the direction it falls, and how hard, all from the spacing of the lines — and it is also the
/// surface the field is edited through, since dragging a line at a height is stating that height along it.
///
/// <para>The lines are traced from the <b>continuous</b> surface rather than the block one. A block surface is
/// a staircase, so contouring it would return the staircase's own treads: outlines of flat plateaus rather
/// than lines of constant height, and every one of them a rectilinear tangle. The continuous field is what the
/// blocks were rounded from, so a contour at a whole level is exactly the boundary the rounding will fall on.</para>
/// </summary>
public static class Contours
{
    /// <summary>Traces every level of a field at the given spacing, from the lowest whole multiple above its
    /// floor to its ceiling. A field with nothing in it, or one flatter than a single interval, traces
    /// nothing — a flat surface has no contours, and drawing its one arbitrary level would say there was a
    /// slope where there is none.</summary>
    public static IReadOnlyList<ContourLine> Of(HeightField field, double interval = 1)
    {
        if (interval <= 0 || field.Footprint.Cells == 0) return [];

        double low = double.MaxValue, high = double.MinValue;
        foreach (var (x, z) in field.Footprint.Land())
        {
            var height = field.Continuous[field.Footprint.Index(x, z)];
            if (height < low) low = height;
            if (height > high) high = height;
        }
        if (low > high) return [];

        var lines = new List<ContourLine>();
        for (var level = Math.Ceiling(low / interval) * interval; level <= high; level += interval)
            lines.AddRange(Trace(field, level));
        return lines;
    }

    /// <summary>Traces one level. Cells are sampled at their <b>centres</b>, so a cell's height sits at
    /// <c>(x + 0.5, z + 0.5)</c> — the same point the footprint tests membership at, and the same point the
    /// world builds that cell's column on. A contour reaching the edge of the land therefore stops half a
    /// block inside it rather than being extrapolated over the void.</summary>
    public static IReadOnlyList<ContourLine> Trace(HeightField field, double level)
    {
        var segments = new List<((double X, double Z) From, (double X, double Z) To)>();
        var footprint = field.Footprint;

        // A contour running exactly through a sampled height leaves both of that corner's edges crossing at
        // the corner itself, so the square emits a segment of no length. It is not a piece of the line — the
        // squares either side of it already carry the line through that point — and kept, it surfaces as a
        // stray two-point stub on every level a mark stated a whole number at.
        void Emit((double X, double Z) from, (double X, double Z) to)
        {
            if (Math.Abs(from.X - to.X) > 1e-9 || Math.Abs(from.Z - to.Z) > 1e-9) segments.Add((from, to));
        }

        for (var z = footprint.MinZ; z < footprint.MinZ + footprint.Depth - 1; z++)
            for (var x = footprint.MinX; x < footprint.MinX + footprint.Width - 1; x++)
            {
                // All four corners must be land: a square with one corner over the void has no fourth value
                // to interpolate against, and inventing one draws a contour out into the sea.
                if (!footprint.Inside(x, z) || !footprint.Inside(x + 1, z) ||
                    !footprint.Inside(x + 1, z + 1) || !footprint.Inside(x, z + 1)) continue;

                var a = field.Continuous[footprint.Index(x, z)];
                var b = field.Continuous[footprint.Index(x + 1, z)];
                var c = field.Continuous[footprint.Index(x + 1, z + 1)];
                var d = field.Continuous[footprint.Index(x, z + 1)];

                var code = (a >= level ? 1 : 0) | (b >= level ? 2 : 0) | (c >= level ? 4 : 0) | (d >= level ? 8 : 0);
                if (code == 0 || code == 15) continue;

                // The four crossing points, each on the edge between two corners, at the sampled centres.
                (double X, double Z) Top() => (x + 0.5 + Mix(a, b, level), z + 0.5);
                (double X, double Z) Right() => (x + 1.5, z + 0.5 + Mix(b, c, level));
                (double X, double Z) Bottom() => (x + 0.5 + Mix(d, c, level), z + 1.5);
                (double X, double Z) Left() => (x + 0.5, z + 0.5 + Mix(a, d, level));

                switch (code)
                {
                    case 1: case 14: Emit(Left(), Top()); break;
                    case 2: case 13: Emit(Top(), Right()); break;
                    case 3: case 12: Emit(Left(), Right()); break;
                    case 4: case 11: Emit(Right(), Bottom()); break;
                    case 6: case 9: Emit(Top(), Bottom()); break;
                    case 7: case 8: Emit(Left(), Bottom()); break;

                    // The two ambiguous squares — opposite corners high, the other two low. Whether they are
                    // one saddle or two separate lobes is not decidable from the corners, so the centre
                    // decides it, which is the reading that keeps a ridge crossing a ridge connected.
                    case 5:
                        if ((a + b + c + d) / 4 >= level) { Emit(Left(), Bottom()); Emit(Top(), Right()); }
                        else { Emit(Left(), Top()); Emit(Right(), Bottom()); }
                        break;
                    case 10:
                        if ((a + b + c + d) / 4 >= level) { Emit(Left(), Top()); Emit(Right(), Bottom()); }
                        else { Emit(Left(), Bottom()); Emit(Top(), Right()); }
                        break;
                }
            }

        return Stitch(segments, level);
    }

    private static double Mix(double from, double to, double level)
    {
        var span = to - from;
        return Math.Abs(span) < 1e-12 ? 0.5 : Math.Clamp((level - from) / span, 0, 1);
    }

    /// <summary>Chains loose segments into runs. Marching squares emits a level as unordered pieces, and a
    /// piece is not a line an author can drag — a stroke needs its points in the order they are walked.</summary>
    private static IReadOnlyList<ContourLine> Stitch(
        List<((double X, double Z) From, (double X, double Z) To)> segments, double level)
    {
        var atPoint = new Dictionary<(long, long), List<int>>();
        static (long, long) Key((double X, double Z) point) =>
            ((long)Math.Round(point.X * 4096), (long)Math.Round(point.Z * 4096));

        for (var index = 0; index < segments.Count; index++)
        {
            foreach (var end in new[] { Key(segments[index].From), Key(segments[index].To) })
            {
                if (!atPoint.TryGetValue(end, out var joined)) atPoint[end] = joined = [];
                joined.Add(index);
            }
        }

        var used = new bool[segments.Count];
        var lines = new List<ContourLine>();

        // Open runs first, walked from their loose ends — an endpoint only one segment touches. Seeding an
        // open run from its middle would trace one half, then report the other half as a second line. The
        // second pass takes whatever is left, which by then is only closed loops, and those may be seeded
        // anywhere.
        for (var pass = 0; pass < 2; pass++)
            for (var seed = 0; seed < segments.Count; seed++)
            {
                if (used[seed]) continue;
                var fromLoose = atPoint[Key(segments[seed].From)].Count == 1;
                var toLoose = atPoint[Key(segments[seed].To)].Count == 1;
                if (pass == 0 && !fromLoose && !toLoose) continue;

                var walkFromTo = pass == 0 && !fromLoose;
                var points = new List<(double X, double Z)>
                {
                    walkFromTo ? segments[seed].To : segments[seed].From,
                    walkFromTo ? segments[seed].From : segments[seed].To,
                };
                used[seed] = true;

                while (true)
                {
                    var tail = Key(points[^1]);
                    var next = atPoint.TryGetValue(tail, out var joined)
                        ? joined.FirstOrDefault(index => !used[index], -1) : -1;
                    if (next < 0) break;
                    used[next] = true;
                    points.Add(Key(segments[next].From) == tail ? segments[next].To : segments[next].From);
                }

                var closed = points.Count > 2 && Key(points[0]) == Key(points[^1]);
                lines.Add(new ContourLine(level, points, closed));
            }

        return lines;
    }
}
