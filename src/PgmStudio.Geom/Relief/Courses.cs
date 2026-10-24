namespace PgmStudio.Geom.Relief;

/// <summary>
/// The operations that follow a line or fill an area at a stated height: a river and a lake, which make water,
/// and a road. Each answers the change it makes to every column's height; a river and a lake also write the
/// surface their water stands at into <c>waters</c> for each column they wet.
/// </summary>
internal static class Courses
{
    private static double[] None(AreaView view) => new double[view.E.Length];

    /// <summary>
    /// A river along a line, flowing from its first point to its last. The bed is the ground the line follows
    /// <c>depth</c> down, never climbing, held level in reaches and stepping down in falls where the ground drops
    /// two blocks or more after a reach of ten. The channel is the mask's width; its banks ease back to the ground
    /// over <c>bank</c> blocks from the water's surface; <c>water</c> blocks of water stand in it. The ring of
    /// columns beside the water is raised to the surface where it is lower, so no water stands over its bank.
    /// </summary>
    public static double[] River(ModifierSpec spec, AreaView view, Ground ground, double[] waters)
    {
        var change = None(view);
        if (view.Along is not { } along || ground.Along(view.Area.Points, 1) is not { } profile) return change;
        var (arc, height) = profile;
        double depth = spec.Depth ?? ModifierDefaults.RiverDepth, water = spec.Water ?? ModifierDefaults.RiverWater;
        double bank = spec.Bank ?? ModifierDefaults.RiverBank, half = view.Area.Scale;

        var bed = new double[height.Length];
        var level = height[0] - depth;
        double running = level, since = 0;
        for (var j = 0; j < height.Length; j++)
        {
            running = Math.Min(running, height[j] - depth);
            if (j > 0 && running <= level - ModifierDefaults.RiverFall && since >= ModifierDefaults.RiverReach)
            {
                level = running;
                since = 0;
            }
            bed[j] = Math.Floor(level);
            if (j > 0) since += arc[j] - arc[j - 1];
        }

        var count = change.Length;
        var lowered = (double[])ground.Heights.Clone();
        var surface = new double[count];
        Array.Fill(surface, double.NaN);
        for (var i = 0; i < count; i++)
        {
            var distance = view.Blocks(i);
            if (distance > half + bank) continue;
            var floor = Math.Floor(Ground.Interpolate(arc, bed, along[i]));
            var top = floor + water;
            var cut = distance <= half ? floor : top + Modifiers.SmoothStep(half, half + bank, distance) * (ground.Heights[i] - top);
            lowered[i] = Math.Min(ground.Heights[i], cut);
            if (distance > half) continue;
            surface[i] = top;
            if (double.IsNaN(waters[i]) || waters[i] < top) waters[i] = top;
        }

        var held = new double[count];
        Array.Fill(held, double.NegativeInfinity);
        for (var i = 0; i < count; i++)
        {
            if (double.IsNaN(surface[i])) continue;
            int cellX = (int)Math.Floor(ground.Xs[i]), cellZ = (int)Math.Floor(ground.Zs[i]);
            for (var dx = -1; dx <= 1; dx++)
                for (var dz = -1; dz <= 1; dz++)
                {
                    var neighbour = ground.IndexAt(cellX + dx, cellZ + dz);
                    if (neighbour >= 0 && double.IsNaN(surface[neighbour])) held[neighbour] = Math.Max(held[neighbour], surface[i]);
                }
        }
        for (var i = 0; i < count; i++)
        {
            if (double.IsFinite(held[i])) lowered[i] = Math.Max(lowered[i], held[i]);
            change[i] = lowered[i] - ground.Heights[i];
        }
        return change;
    }

    /// <summary>
    /// A lake in an area: its bed lies <c>depth</c> under the surface in the middle and rises to a block under it at
    /// the rim, and the ground within <c>shore</c> blocks outside is held at the surface or above, easing back to
    /// the ground, so no water stands against air. The surface is <c>level</c>, or one block under the ground a tenth
    /// of the rim stands at or below.
    /// </summary>
    public static double[] Lake(ModifierSpec spec, AreaView view, Ground ground, double[] waters)
    {
        var change = None(view);
        if (!view.Is(MaskShape.Ellipse, MaskShape.Polygon)) return change;
        double depth = spec.Depth ?? ModifierDefaults.LakeDepth, shore = spec.Shore ?? ModifierDefaults.LakeShore;
        var scale = view.Area.Scale;
        var surface = spec.Level is { } stated ? Math.Floor(stated) : RimLevel(view, ground, Math.Max(shore, 1)) - 1;
        if (!double.IsFinite(surface)) return change;

        for (var i = 0; i < change.Length; i++)
        {
            var height = ground.Heights[i];
            var e = view.E[i];
            if (e <= 1)
            {
                var bed = Math.Floor(surface - 1 - (depth - 1) * Math.Pow(Math.Clamp(1 - e, 0, 1), 0.6));
                change[i] = Math.Min(height, bed) - height;
                if (double.IsNaN(waters[i]) || waters[i] < surface) waters[i] = surface;
                continue;
            }
            var beyond = (e - 1) * scale;
            if (beyond > shore) continue;
            var ease = Modifiers.SmoothStep(0, shore, beyond);
            var shaped = Math.Max(height, surface) * (1 - ease) + height * ease;
            if (beyond <= 1.5) shaped = Math.Max(shaped, surface);
            change[i] = shaped - height;
        }
        return change;
    }

    /// <summary>
    /// A sea over an area, its surface at <c>level</c> or one under the ground a tenth of its rim stands at or
    /// below. Inside, the floor falls from the shore <c>slope</c> blocks a block to <c>depth</c> under the surface, and the sea
    /// stands over it; outside, the land eases down over <c>shelf</c> blocks to a beach one block over the sea, so
    /// the shore is a beach and not a cliff. Only cuts inside; outside it cuts, and lifts low land beside the
    /// water to the beach.
    /// </summary>
    public static double[] Coast(ModifierSpec spec, AreaView view, Ground ground, double[] waters)
    {
        var change = None(view);
        if (!view.Is(MaskShape.Ellipse, MaskShape.Polygon)) return change;
        double shelf = Math.Max(spec.Shelf ?? ModifierDefaults.CoastShelf, 1), depth = spec.Depth ?? ModifierDefaults.CoastDepth;
        var slope = spec.Slope ?? ModifierDefaults.CoastSlope;
        var scale = view.Area.Scale;
        var sea = spec.Level is { } stated ? Math.Floor(stated) : RimLevel(view, ground, shelf) - 1;
        if (!double.IsFinite(sea)) return change;
        for (var i = 0; i < change.Length; i++)
        {
            var height = ground.Heights[i];
            var e = view.E[i];
            if (e <= 1)
            {
                var floor = Math.Floor(sea - Math.Min(depth, 1 + slope * (1 - e) * scale));
                change[i] = Math.Min(height, floor) - height;
                if (double.IsNaN(waters[i]) || waters[i] < sea) waters[i] = sea;
                continue;
            }
            var inland = (e - 1) * scale;
            if (inland >= shelf) continue;
            var beach = sea + 1;
            var land = beach + (height - beach) * Modifiers.SmoothStep(0, shelf, inland);
            change[i] = Math.Max(Math.Min(height, land), beach) - height;
        }
        return change;
    }

    /// <summary>The share of the rim a water's level is read under: the ground a tenth of the ring stands at or
    /// below, so one cleft or hole cut into the rim does not drain the whole water to its depth.</summary>
    private const double RimShare = 0.1;

    // The ground a tenth of the ring just outside the mask stands at or below, or of the ground inside it where the
    // board stops at the rim.
    private static double RimLevel(AreaView view, Ground ground, double reach)
    {
        var limit = 1 + reach / view.Area.Scale;
        var rim = new List<double>();
        var inside = new List<double>();
        for (var i = 0; i < view.E.Length; i++)
        {
            if (view.E[i] <= 1) inside.Add(ground.Heights[i]);
            else if (view.E[i] <= limit) rim.Add(ground.Heights[i]);
        }
        var ring = rim.Count > 0 ? rim : inside;
        if (ring.Count == 0) return double.NaN;
        ring.Sort();
        return Math.Floor(ring[(int)Math.Floor(RimShare * (ring.Count - 1))]);
    }

    /// <summary>
    /// A route graded into the ground: the ground along the line smoothed so it climbs or falls no more than
    /// <c>grade</c> blocks a block, with its ends holding the ground they stand on, cut and filled to that level
    /// across the mask's width, and shoulders easing back to the ground over <c>shoulder</c> blocks.
    /// </summary>
    public static double[] Road(ModifierSpec spec, AreaView view, Ground ground)
    {
        var change = None(view);
        if (view.Along is not { } along || ground.Along(view.Area.Points, 1) is not { } profile) return change;
        var (arc, height) = profile;
        double grade = spec.Grade ?? ModifierDefaults.RoadGrade, shoulder = spec.Shoulder ?? ModifierDefaults.RoadShoulder;
        var half = view.Area.Scale;

        var graded = (double[])height.Clone();
        for (var j = 1; j < graded.Length; j++)
        {
            var step = grade * (arc[j] - arc[j - 1]);
            graded[j] = Math.Clamp(graded[j], graded[j - 1] - step, graded[j - 1] + step);
        }
        for (var j = graded.Length - 2; j >= 0; j--)
        {
            var step = grade * (arc[j + 1] - arc[j]);
            graded[j] = Math.Clamp(graded[j], graded[j + 1] - step, graded[j + 1] + step);
        }
        HoldEnd(graded, arc, height[0], grade, fromStart: true);
        HoldEnd(graded, arc, height[^1], grade, fromStart: false);

        for (var i = 0; i < change.Length; i++)
        {
            var distance = view.Blocks(i);
            if (distance > half + shoulder) continue;
            var deck = Math.Round(Ground.Interpolate(arc, graded, along[i]));
            var before = ground.Heights[i];
            var laid = distance <= half ? deck : deck + Modifiers.SmoothStep(half, half + shoulder, distance) * (before - deck);
            change[i] = laid - before;
        }
        return change;
    }

    // The profile held to `value` at one end, the change eased back along it no steeper than the grade.
    private static void HoldEnd(double[] profile, double[] arc, double value, double grade, bool fromStart)
    {
        var origin = fromStart ? arc[0] : arc[^1];
        for (var step = 0; step < profile.Length; step++)
        {
            var j = fromStart ? step : profile.Length - 1 - step;
            var reach = grade * Math.Abs(arc[j] - origin);
            double low = value - reach, high = value + reach;
            if (profile[j] >= low && profile[j] <= high) break;
            profile[j] = Math.Min(Math.Max(profile[j], low), high);
        }
    }
}
