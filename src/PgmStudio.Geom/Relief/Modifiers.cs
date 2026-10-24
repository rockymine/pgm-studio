namespace PgmStudio.Geom.Relief;

/// <summary>
/// The terraform operations a sketch's modifier stack runs over the ground, each one a change to a field of column
/// heights inside its mask. <see cref="Apply"/> measures every column against each area, computes the operation's
/// change per area, combines the areas, fades the result by <see cref="ModifierSpec.Soft"/>, lets
/// <see cref="ModifierSpec.Mode"/> clamp its direction and adds it to the heights.
/// </summary>
public static class Modifiers
{
    private const int SeedStride = 37;

    /// <summary>The spacing of the curve a River, a Canyon or a Road follows through the points of its line.</summary>
    private const double PathStep = 1;

    /// <summary>
    /// Runs one modifier over <paramref name="heights"/>, in place. Column <c>i</c> stands at
    /// (<paramref name="xs"/>[i], <paramref name="zs"/>[i]) — its centre, not its corner — and the three arrays
    /// are the same length. A River or a Lake writes the height its water surface stands at into
    /// <paramref name="waters"/>[i] for each column it wets, replacing what an earlier modifier wrote there; a
    /// caller that wants the water passes an array of the same length, filled with <see cref="double.NaN"/> where
    /// there is none.
    /// </summary>
    public static void Apply(ModifierSpec spec, double[] heights, double[] xs, double[] zs, double[]? waters = null)
    {
        var areas = spec.Areas;
        var count = heights.Length;
        if (areas.Count == 0 || count == 0) return;

        var ground = new Ground(heights, xs, zs);
        var stepWater = new double[count];
        Array.Fill(stepWater, double.NaN);
        var nearest = new double[count];
        Array.Fill(nearest, double.PositiveInfinity);
        var changes = new double[areas.Count][];
        for (var at = 0; at < areas.Count; at++)
        {
            var area = areas[at];
            if (spec.Op is ModifierOp.Canyon or ModifierOp.River or ModifierOp.Road) area = area.Smoothed(PathStep);
            var view = new AreaView(area, xs, zs);
            for (var i = 0; i < count; i++) nearest[i] = Math.Min(nearest[i], view.E[i]);
            changes[at] = spec.Op switch
            {
                ModifierOp.Mound => MoundChange(spec, view.E),
                ModifierOp.Crater => CraterChange(spec, area.Scale, view.E, heights),
                ModifierOp.Level => LevelChange(spec, view.E, heights),
                ModifierOp.Noise => NoiseChange(spec, view.E, xs, zs),
                ModifierOp.Ridge => Landforms.Ridge(spec, view, ground),
                ModifierOp.Butte => Landforms.Butte(spec, view, ground),
                ModifierOp.Spire => Landforms.Spire(spec, view, ground),
                ModifierOp.Scarp => Landforms.Scarp(spec, view),
                ModifierOp.Canyon => Landforms.Canyon(spec, view, ground),
                ModifierOp.Gullies => Landforms.Gullies(spec, view, ground),
                ModifierOp.Warp => Landforms.Warp(spec, view, ground),
                ModifierOp.River => Courses.River(spec, view, ground, stepWater),
                ModifierOp.Lake => Courses.Lake(spec, view, ground, stepWater),
                ModifierOp.Road => Courses.Road(spec, view, ground),
                _ => new double[count],
            };
        }
        if (waters is not null)
            for (var i = 0; i < count; i++)
                if (!double.IsNaN(stepWater[i])) waters[i] = stepWater[i];

        var change = Combine(spec, changes);
        var soft = spec.Soft is > 0 ? spec.Soft : null;
        for (var i = 0; i < count; i++)
        {
            var moved = soft is { } share ? change[i] * SmoothStep(1, 1 - share, nearest[i]) : change[i];
            heights[i] += spec.Mode switch
            {
                ModifierMode.Lift => Math.Max(moved, 0),
                ModifierMode.Cut => Math.Min(moved, 0),
                _ => moved,
            };
        }
    }

    /// <summary>The lower of two heights with the seam between them rounded over <paramref name="melt"/> blocks;
    /// where they meet it lies a quarter of the melt under both. A melt of zero is the plain minimum.</summary>
    public static double SmoothMin(double ground, double bowl, double melt)
    {
        if (melt <= 0) return Math.Min(ground, bowl);
        var blend = Math.Clamp(0.5 + 0.5 * (bowl - ground) / melt, 0, 1);
        return bowl * (1 - blend) + ground * blend - melt * blend * (1 - blend);
    }

    /// <summary>0 at <paramref name="edge0"/>, 1 at <paramref name="edge1"/>, eased between. Edges given high to
    /// low make a falling ramp; equal edges make a step, 0 below the edge and 1 from it on.</summary>
    public static double SmoothStep(double edge0, double edge1, double x)
    {
        var span = edge1 - edge0;
        if (span == 0) return x >= edge0 ? 1 : 0;
        var along = Math.Clamp((x - edge0) / span, 0, 1);
        return along * along * (3 - 2 * along);
    }

    // A Hill's height falls as 1 - e^power to nothing at the rim; a negative rise sinks that shape.
    private static double[] MoundChange(ModifierSpec spec, double[] distance)
    {
        var rise = spec.Rise ?? ModifierDefaults.MoundRise;
        var power = spec.Power ?? ModifierDefaults.MoundPower;
        var change = new double[distance.Length];
        for (var i = 0; i < change.Length; i++)
        {
            if (distance[i] >= 1) continue;
            change[i] = rise * (1 - Math.Pow(Math.Clamp(distance[i], 0, 1), power));
        }
        return change;
    }

    // The bowl is flat to `flat` blocks, climbs `slope` a block, and past the rim climbs out of reach.
    private static double[] CraterChange(ModifierSpec spec, double scale, double[] distance, double[] heights)
    {
        double floor = spec.Floor ?? ModifierDefaults.CraterFloor, flat = spec.Flat ?? ModifierDefaults.CraterFlat;
        double slope = spec.Slope ?? ModifierDefaults.CraterSlope, melt = spec.Melt ?? ModifierDefaults.CraterMelt;
        var change = new double[distance.Length];
        for (var i = 0; i < change.Length; i++)
        {
            var bowl = floor + slope * Math.Max(0, distance[i] * scale - flat)
                       + (distance[i] > 1 ? 1000 * (distance[i] - 1) : 0);
            change[i] = SmoothMin(heights[i], bowl, melt) - heights[i];
        }
        return change;
    }

    // Inside `inner` the ground is the top; to `outer` it eases back, and a mode on the skirt keeps it from
    // crossing the ground it eases to.
    private static double[] LevelChange(ModifierSpec spec, double[] distance, double[] heights)
    {
        double inner = spec.Inner ?? ModifierDefaults.LevelInner, outer = spec.Outer ?? ModifierDefaults.LevelOuter;
        var change = new double[distance.Length];
        var top = spec.Top ?? MedianWithin(heights, distance, inner);
        if (double.IsNaN(top)) return change;
        for (var i = 0; i < change.Length; i++)
        {
            var ground = heights[i];
            if (distance[i] < inner) { change[i] = top - ground; continue; }
            if (distance[i] >= outer) continue;
            var ease = SmoothStep(inner, outer, distance[i]);
            var shaped = top * (1 - ease) + ground * ease;
            change[i] = spec.Mode switch
            {
                ModifierMode.Lift => Math.Max(ground, shaped),
                ModifierMode.Cut => Math.Min(ground, shaped),
                _ => shaped,
            } - ground;
        }
        return change;
    }

    private static double[] NoiseChange(ModifierSpec spec, double[] distance, double[] xs, double[] zs)
    {
        IReadOnlyList<NoiseSize> sizes = spec.Sizes is { Count: > 0 } stated ? stated : [ModifierDefaults.Size];
        var change = new double[distance.Length];
        for (var i = 0; i < change.Length; i++)
        {
            if (distance[i] >= 1) continue;
            var sum = 0.0;
            for (var size = 0; size < sizes.Count; size++)
                sum += sizes[size].Rise * ValueNoise.Fractal(xs[i], zs[i], sizes[size].Cell, sizes[size].Octaves,
                                                              unchecked(spec.Seed + size * SeedStride));
            change[i] = sum;
        }
        return change;
    }

    // Several areas of a Hill or Hollow join by the p-norm of their magnitudes, which fills the saddle where two
    // overlap; zero keeps the greater. Cuts are depths and join the same way. Any other operation takes, per
    // column, the change that moves the ground furthest.
    private static double[] Combine(ModifierSpec spec, double[][] changes)
    {
        if (changes.Length == 1) return changes[0];
        var combined = new double[changes[0].Length];
        if (spec.Op is ModifierOp.Mound or ModifierOp.Crater)
        {
            var cut = changes.All(area => area.All(value => value <= 0)) && changes.Any(area => area.Any(value => value < 0));
            var join = spec.Join ?? ModifierDefaults.Join;
            var sign = cut ? -1.0 : 1.0;
            for (var i = 0; i < combined.Length; i++)
            {
                double largest = 0, sum = 0;
                foreach (var area in changes)
                {
                    var magnitude = Math.Max(sign * area[i], 0);
                    largest = Math.Max(largest, magnitude);
                    if (join > 0) sum += Math.Pow(magnitude, join);
                }
                combined[i] = sign * (join > 0 ? Math.Pow(sum, 1 / join) : largest);
            }
            return combined;
        }
        Array.Copy(changes[0], combined, combined.Length);
        foreach (var area in changes.Skip(1))
            for (var i = 0; i < combined.Length; i++)
                if (Math.Abs(area[i]) > Math.Abs(combined[i])) combined[i] = area[i];
        return combined;
    }

    internal static double MedianWithin(double[] heights, double[] distance, double below)
    {
        var within = new List<double>();
        for (var i = 0; i < heights.Length; i++)
            if (distance[i] < below) within.Add(heights[i]);
        if (within.Count == 0) return double.NaN;
        within.Sort();
        var middle = within.Count / 2;
        return within.Count % 2 == 1 ? within[middle] : (within[middle - 1] + within[middle]) / 2;
    }
}
