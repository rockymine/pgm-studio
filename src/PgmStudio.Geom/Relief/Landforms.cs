namespace PgmStudio.Geom.Relief;

/// <summary>
/// The landform operations of the modifier stack other than a hill, a hollow, a level and noise. Each reads one
/// mask outline measured against the columns (<see cref="AreaView"/>) and answers the change it makes to each
/// column's height; an outline of a kind the operation does not read answers no change.
/// </summary>
internal static class Landforms
{
    private static double[] None(AreaView view) => new double[view.E.Length];

    /// <summary>A crest of <c>rise × (1 − e²)</c> along a line or the spine of an area. The foot wanders in from
    /// the mask by up to <c>wander</c> blocks of half width, by noise.</summary>
    public static double[] Ridge(ModifierSpec spec, AreaView view, Ground ground)
    {
        var change = None(view);
        if (!view.Is(MaskShape.Line, MaskShape.Ellipse, MaskShape.Polygon)) return change;
        double rise = spec.Rise ?? ModifierDefaults.RidgeRise, wander = spec.Wander ?? ModifierDefaults.RidgeWander;
        var share = wander / view.Area.Scale;
        for (var i = 0; i < change.Length; i++)
        {
            if (view.E[i] >= 1) continue;
            var wandering = 0.5 + 0.5 * ValueNoise.Fractal(ground.Xs[i], ground.Zs[i], RidgeCell, 2, spec.Seed);
            var across = view.E[i] * (1 + share * wandering);
            if (across < 1) change[i] = rise * (1 - across * across);
        }
        return change;
    }

    private const double RidgeCell = 10;
    private const double ButteCell = 8;
    private const double SpireCell = 4;

    /// <summary>A flat top at <c>top</c> out to the mask's rim, a cliff <c>cliff</c> blocks wide beyond it, and a
    /// talus apron <c>talus</c> blocks wide from <c>apron</c> of the way up the cliff down to the ground. Only
    /// lifts.</summary>
    public static double[] Butte(ModifierSpec spec, AreaView view, Ground ground)
    {
        var change = None(view);
        if (!view.Is(MaskShape.Ellipse, MaskShape.Polygon)) return change;
        double cliff = spec.Cliff ?? ModifierDefaults.ButteCliff, talus = spec.Talus ?? ModifierDefaults.ButteTalus;
        double apron = spec.Apron ?? ModifierDefaults.Apron, jag = spec.Jag ?? ModifierDefaults.ButteJag;
        var top = spec.Top ?? Modifiers.MedianWithin(ground.Heights, view.E, 1) + ModifierDefaults.ButteHeight;
        if (double.IsNaN(top)) return change;
        var scale = view.Area.Scale;
        var reach = 1 + (cliff + talus) / scale;
        for (var i = 0; i < change.Length; i++)
        {
            var across = view.E[i] * (1 + jag * ValueNoise.Fractal(ground.Xs[i], ground.Zs[i], ButteCell, 3, spec.Seed));
            if (across >= reach) continue;
            var height = ground.Heights[i];
            var foot = height + (top - height) * apron;
            var beyond = (across - 1) * scale;
            var shaped = across <= 1 ? top
                       : beyond <= cliff ? foot + (top - foot) * (1 - beyond / Math.Max(cliff, 1e-6))
                       : foot + (height - foot) * Math.Clamp((beyond - cliff) / Math.Max(talus, 1e-6), 0, 1);
            change[i] = Math.Max(0, shaped - height);
        }
        return change;
    }

    /// <summary>A needle from the ground at the mask's rim to <c>top</c> at its middle, its sides falling as
    /// <c>(1 − e)^(1 / taper)</c>. Only lifts.</summary>
    public static double[] Spire(ModifierSpec spec, AreaView view, Ground ground)
    {
        var change = None(view);
        if (!view.Is(MaskShape.Ellipse, MaskShape.Polygon)) return change;
        double taper = spec.Taper ?? ModifierDefaults.SpireTaper, jag = spec.Jag ?? ModifierDefaults.SpireJag;
        var (middleX, middleZ) = view.Area.Centre;
        var median = Modifiers.MedianWithin(ground.Heights, view.E, 1);
        if (double.IsNaN(median)) return change;
        var baseHeight = ground.HeightAt(middleX, middleZ) ?? median;
        var top = spec.Top ?? median + ModifierDefaults.SpireHeight;
        for (var i = 0; i < change.Length; i++)
        {
            var across = view.E[i] * (1 + jag * ValueNoise.Fractal(ground.Xs[i], ground.Zs[i], SpireCell, 3, spec.Seed));
            if (across >= 1) continue;
            var shaped = baseHeight + (top - baseHeight) * Math.Pow(1 - Math.Max(across, 0), 1 / Math.Max(taper, 1e-6));
            change[i] = Math.Max(0, shaped - ground.Heights[i]);
        }
        return change;
    }

    /// <summary>The ground on one hand of a line lifted <c>height</c>, a cliff <c>cliff</c> blocks wide on the
    /// line and a talus apron <c>talus</c> blocks wide on the other hand. The lift runs out at the mask's edge.</summary>
    public static double[] Scarp(ModifierSpec spec, AreaView view)
    {
        var change = None(view);
        if (view.Side is not { } sides) return change;
        double height = spec.Height ?? ModifierDefaults.ScarpHeight, cliff = spec.Cliff ?? ModifierDefaults.ScarpCliff;
        double talus = spec.Talus ?? ModifierDefaults.ScarpTalus, apron = spec.Apron ?? ModifierDefaults.Apron;
        var raised = (spec.Side ?? ModifierDefaults.ScarpSide) >= 0 ? 1 : -1;
        for (var i = 0; i < change.Length; i++)
        {
            if (view.E[i] >= 1) continue;
            var distance = view.Blocks(i);
            if (sides[i] == raised)
                change[i] = distance <= cliff ? height * (0.5 + 0.5 * distance / Math.Max(cliff, 1e-6)) : height;
            else
                change[i] = height * apron * Math.Clamp(1 - distance / Math.Max(talus, 1e-6), 0, 1);
        }
        return change;
    }

    /// <summary>A trench along a line: a floor <c>bed</c> of the half width across and <c>depth</c> under the ground
    /// the line follows, walls climbing to the rim as the distance across to the power <c>wall</c>, cut in steps of
    /// <c>ledges</c> blocks when it states any. Only cuts.</summary>
    public static double[] Canyon(ModifierSpec spec, AreaView view, Ground ground)
    {
        var change = None(view);
        if (view.Along is not { } along || ground.Along(view.Area.Points, 1) is not { } profile) return change;
        var (arc, height) = profile;
        double depth = spec.Depth ?? ModifierDefaults.CanyonDepth, wall = spec.Wall ?? ModifierDefaults.CanyonWall;
        double bed = spec.Bed ?? ModifierDefaults.CanyonBed, ledges = spec.Ledges ?? ModifierDefaults.CanyonLedges;
        var floorAlong = height.Select(value => value - depth).ToArray();
        var half = view.Area.Scale;
        var flat = bed * half;
        for (var i = 0; i < change.Length; i++)
        {
            var distance = view.Blocks(i);
            if (distance > half) continue;
            var floor = Ground.Interpolate(arc, floorAlong, along[i]);
            var across = Math.Clamp((distance - flat) / Math.Max(half - flat, 1e-6), 0, 1);
            var target = floor + (ground.Heights[i] - floor) * Math.Pow(across, wall);
            if (ledges > 0) target = floor + Math.Floor((target - floor) / ledges) * ledges;
            change[i] = Math.Min(ground.Heights[i], target) - ground.Heights[i];
        }
        return change;
    }

    /// <summary>Grooves cut outward from the middle of an area between a quarter of its scale and the scale, each
    /// up to <c>depth</c> deep and its own depth by noise. <c>wander</c> bends them, <c>sharpness</c> narrows them.
    /// Only cuts.</summary>
    public static double[] Gullies(ModifierSpec spec, AreaView view, Ground ground)
    {
        var change = None(view);
        if (!view.Is(MaskShape.Ellipse, MaskShape.Polygon)) return change;
        var count = spec.Count ?? ModifierDefaults.GulliesCount;
        double depth = Math.Abs(spec.Depth ?? ModifierDefaults.GulliesDepth), wander = spec.Wander ?? ModifierDefaults.GulliesWander;
        var sharpness = spec.Sharpness ?? ModifierDefaults.GulliesSharpness;
        var (middleX, middleZ) = view.Area.Centre;
        double inner = 0.25 * view.Area.Scale, outer = view.Area.Scale;
        for (var i = 0; i < change.Length; i++)
        {
            if (view.E[i] >= 1) continue;
            double x = ground.Xs[i], z = ground.Zs[i], distance = view.Blocks(i);
            var turns = Math.Atan2(z - middleZ, x - middleX) * count / (2 * Math.PI);
            if (wander != 0) turns += wander * ValueNoise.Fractal(x, z, 50, 3, spec.Seed);
            var groove = Math.Pow(1 - Math.Abs(Math.Sin(Math.PI * turns)), sharpness);
            var each = Math.Max(0, 0.55 + 0.45 * ValueNoise.Fractal(x, z, 25, 2, spec.Seed + 1));
            var along = Modifiers.SmoothStep(inner, inner + 0.25 * (outer - inner), distance)
                        * Modifiers.SmoothStep(outer, outer - 0.3 * (outer - inner), distance);
            change[i] = -depth * groove * each * along;
        }
        return change;
    }

    /// <summary>The ground inside resampled at positions shifted by noise, up to <c>amount</c> blocks, so its
    /// outlines swirl while its heights are the ground's own.</summary>
    public static double[] Warp(ModifierSpec spec, AreaView view, Ground ground)
    {
        var change = None(view);
        double amount = spec.Amount ?? ModifierDefaults.WarpAmount, cell = spec.Cell ?? ModifierDefaults.WarpCell;
        for (var i = 0; i < change.Length; i++)
        {
            if (view.E[i] >= 1) continue;
            double x = ground.Xs[i], z = ground.Zs[i];
            var shiftX = amount * (0.75 * ValueNoise.Fractal(x, z, cell, 3, spec.Seed)
                                   + 0.25 * ValueNoise.Fractal(x, z, cell / 4, 2, spec.Seed + 2));
            var shiftZ = amount * (0.75 * ValueNoise.Fractal(x, z, cell, 3, spec.Seed + 1)
                                   + 0.25 * ValueNoise.Fractal(x, z, cell / 4, 2, spec.Seed + 3));
            change[i] = ground.Sample(x + shiftX, z + shiftZ, ground.Heights[i]) - ground.Heights[i];
        }
        return change;
    }
}
