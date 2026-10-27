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
            var (drawnX, drawnZ, _) = view.Drawn(i);
            var wandering = 0.5 + 0.5 * ValueNoise.Fractal(drawnX, drawnZ, RidgeCell, 2, spec.Seed);
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
            var (drawnX, drawnZ, _) = view.Drawn(i);
            var across = view.E[i] * (1 + jag * ValueNoise.Fractal(drawnX, drawnZ, ButteCell, 3, spec.Seed));
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
            var (drawnX, drawnZ, _) = view.Drawn(i);
            var across = view.E[i] * (1 + jag * ValueNoise.Fractal(drawnX, drawnZ, SpireCell, 3, spec.Seed));
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
        var (middleX, middleZ) = view.Area.Drawn.Centre;
        double inner = 0.25 * view.Area.Scale, outer = view.Area.Scale;
        for (var i = 0; i < change.Length; i++)
        {
            if (view.E[i] >= 1) continue;
            var (x, z, _) = view.Drawn(i);
            var distance = view.Blocks(i);
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
            var (x, z, axis) = view.Drawn(i);
            var shiftX = amount * (0.75 * ValueNoise.Fractal(x, z, cell, 3, spec.Seed)
                                   + 0.25 * ValueNoise.Fractal(x, z, cell / 4, 2, spec.Seed + 2));
            var shiftZ = amount * (0.75 * ValueNoise.Fractal(x, z, cell, 3, spec.Seed + 1)
                                   + 0.25 * ValueNoise.Fractal(x, z, cell / 4, 2, spec.Seed + 3));
            var (sampleX, sampleZ) = view.Area.FromDrawn(x + shiftX, z + shiftZ, axis);
            change[i] = ground.Sample(sampleX, sampleZ, ground.Heights[i]) - ground.Heights[i];
        }
        return change;
    }

    private const double ProfileCell = 6;

    /// <summary>A cross-section set by distance from a line. Out to the first step the floor is the ground, or
    /// <c>floor</c> where it is stated; each step then eases from the level before it to <c>rise</c> blocks over
    /// the floor between its start and its end, and holds there beyond, so a floor and two steps are a lower
    /// cliff, a bench, an upper cliff and a plateau. The first cliff rises out of the floor and never cuts it.
    /// <c>talus</c> lifts the floor over that many blocks before the first step by <c>apron</c> of its rise;
    /// <c>jag</c> moves every step's edges in and out by noise; <c>side</c> keeps one hand of the line.</summary>
    public static double[] Profile(ModifierSpec spec, AreaView view, Ground ground)
    {
        var change = None(view);
        if (view.Side is not { } sides) return change;
        var half = view.Area.Scale;
        var steps = spec.Steps is { Count: > 0 } stated
            ? stated.OrderBy(step => step.Start).ToArray()
            : ModifierDefaults.ProfileSteps.Select(step => new ProfileStep(step.Start * half, step.End * half, step.Rise)).ToArray();
        var nominal = spec.Floor ?? (ground.Along(view.Area.Points, 1) is { } profile ? Median(profile.Height) : double.NaN);
        if (double.IsNaN(nominal)) return change;
        double jag = spec.Jag ?? ModifierDefaults.ProfileJag, talus = spec.Talus ?? ModifierDefaults.ProfileTalus;
        var apron = spec.Apron ?? ModifierDefaults.Apron;
        for (var i = 0; i < change.Length; i++)
        {
            if (view.E[i] >= 1 || spec.Side is { } hand && sides[i] != (hand >= 0 ? 1 : -1)) continue;
            var distance = view.Blocks(i);
            var (drawnX, drawnZ, _) = view.Drawn(i);
            var ragged = jag * ValueNoise.Fractal(drawnX, drawnZ, ProfileCell, 2, spec.Seed);
            var height = ground.Heights[i];
            var shaped = spec.Floor ?? height;
            if (talus > 0)
                shaped += apron * steps[0].Rise * Modifiers.SmoothStep(steps[0].Start - talus, steps[0].Start, distance);
            var before = nominal;
            for (var n = 0; n < steps.Length; n++)
            {
                var top = nominal + steps[n].Rise;
                var cliff = before + (top - before) * Modifiers.SmoothStep(steps[n].Start + ragged, steps[n].End + ragged, distance);
                if (n == 0) cliff = top >= before ? Math.Max(shaped, cliff) : Math.Min(shaped, cliff);
                if (distance >= steps[n].Start + ragged) shaped = cliff;
                if (distance >= steps[n].End + ragged) shaped = top;
                before = top;
            }
            change[i] = shaped - height;
        }
        return change;
    }

    private static double Median(double[] values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? double.NaN
             : sorted.Length % 2 == 1 ? sorted[sorted.Length / 2]
             : 0.5 * (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]);
    }
}
