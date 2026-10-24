namespace PgmStudio.Geom.Relief;

/// <summary>
/// The operations that strew pieces over an area by seed: lumps on the ground, and the ground broken into plates.
/// Each places its pieces in the frame of the outline as drawn (<see cref="MaskArea.ToDrawn"/>), so every image of
/// the mask under the map's symmetry carries the same pieces.
/// </summary>
internal static class Scatter
{
    private const double LumpTurn = 25;
    private const double Jitter = 0.8;

    /// <summary>Up to <c>count</c> lumps whose middles fall inside the mask, each a hill on an ellipse
    /// <c>across</c> blocks long, its short axis a share <c>stretch</c> of that, turned at random or within 25
    /// degrees of <c>angle</c>, its foot roughened by <c>jag</c>, <c>heights</c> blocks high and falling as
    /// <c>1 − e^power</c>. Where two lumps overlap their heights add. Only lifts.</summary>
    public static double[] Lumps(ModifierSpec spec, AreaView view)
    {
        var change = new double[view.E.Length];
        var drawn = view.Area.Drawn;
        if (!view.Is(MaskShape.Ellipse, MaskShape.Polygon) || drawn.Bounds is not { } bounds) return change;
        var count = spec.Count ?? ModifierDefaults.LumpsCount;
        var across = spec.Across ?? ModifierDefaults.LumpsAcross;
        var heights = spec.Heights ?? ModifierDefaults.LumpsHeights;
        var stretch = spec.Stretch ?? ModifierDefaults.LumpsStretch;
        double jag = spec.Jag ?? ModifierDefaults.LumpsJag, power = spec.Power ?? ModifierDefaults.LumpsPower;

        var lumps = new List<(MaskArea Outline, double Reach, double Rise, int Seed)>();
        for (var k = 0; k < count; k++)
        {
            double Draw(int row) => ValueNoise.Hash(k, row, spec.Seed);
            var x = bounds.MinX + (bounds.MaxX - bounds.MinX) * Draw(0);
            var z = bounds.MinZ + (bounds.MaxZ - bounds.MinZ) * Draw(1);
            if (drawn.E(x, z) >= 1) continue;
            var radius = Math.Max(0.5, Between(across, Draw(2)) / 2);
            var turn = spec.Angle is { } heading ? heading + LumpTurn * (2 * Draw(4) - 1) : 180 * Draw(4);
            var outline = MaskArea.Ellipse(x, z, radius, Math.Max(0.5, radius * Between(stretch, Draw(3))), turn);
            lumps.Add((outline, radius * (1 + Math.Abs(jag)), Between(heights, Draw(5)), unchecked(spec.Seed + k)));
        }
        if (lumps.Count == 0) return change;

        for (var i = 0; i < change.Length; i++)
        {
            var (x, z, _) = view.Drawn(i);
            foreach (var lump in lumps)
            {
                var (middleX, middleZ) = lump.Outline.Centre;
                if (Math.Abs(x - middleX) > lump.Reach || Math.Abs(z - middleZ) > lump.Reach) continue;
                var e = lump.Outline.E(x, z) + jag * ValueNoise.Fractal(x, z, 4, 2, lump.Seed);
                if (e < 1) change[i] += lump.Rise * (1 - Math.Pow(Math.Max(e, 0), power));
            }
        }
        return change;
    }

    /// <summary>The ground inside broken into pieces about <c>cell</c> blocks across, their seams bent up to
    /// <c>wander</c> blocks by noise. Each piece is lifted or dropped up to <c>amount</c> blocks and tilted up to
    /// <c>tilt</c> blocks a block about its middle; within <c>gap</c> blocks of a seam the ground is cut up to
    /// <c>depth</c> into a cleft. Plates, seracs, floes and outcrops.</summary>
    public static double[] Shatter(ModifierSpec spec, AreaView view)
    {
        var change = new double[view.E.Length];
        if (!view.Is(MaskShape.Ellipse, MaskShape.Polygon, MaskShape.Board)) return change;
        var cell = Math.Max(spec.Cell ?? ModifierDefaults.ShatterCell, 2);
        double amount = spec.Amount ?? ModifierDefaults.ShatterAmount, tilt = spec.Tilt ?? ModifierDefaults.ShatterTilt;
        double gap = spec.Gap ?? ModifierDefaults.ShatterGap, depth = Math.Abs(spec.Depth ?? ModifierDefaults.ShatterDepth);
        var wander = spec.Wander ?? ModifierDefaults.ShatterWander;
        var seed = spec.Seed;

        (double X, double Z) Middle(int a, int b) =>
            ((a + 0.5 + Jitter * (ValueNoise.Hash(a, b, seed) - 0.5)) * cell,
             (b + 0.5 + Jitter * (ValueNoise.Hash(a, b, unchecked(seed + 1)) - 0.5)) * cell);

        var others = new (double X, double Z, double Squared)[25];
        for (var i = 0; i < change.Length; i++)
        {
            if (view.E[i] >= 1) continue;
            var (x, z, _) = view.Drawn(i);
            var bentX = x + wander * ValueNoise.Fractal(x, z, 4 * cell, 2, unchecked(seed + 50));
            var bentZ = z + wander * ValueNoise.Fractal(x, z, 4 * cell, 2, unchecked(seed + 51));
            int cellX = (int)Math.Floor(bentX / cell), cellZ = (int)Math.Floor(bentZ / cell);

            var nearest = (A: 0, B: 0, X: 0.0, Z: 0.0, Squared: double.PositiveInfinity);
            var count = 0;
            for (var a = cellX - 2; a <= cellX + 2; a++)
                for (var b = cellZ - 2; b <= cellZ + 2; b++)
                {
                    var (middleX, middleZ) = Middle(a, b);
                    var squared = (bentX - middleX) * (bentX - middleX) + (bentZ - middleZ) * (bentZ - middleZ);
                    others[count++] = (middleX, middleZ, squared);
                    if (squared < nearest.Squared) nearest = (a, b, middleX, middleZ, squared);
                }
            var edge = double.PositiveInfinity;
            foreach (var other in others.AsSpan(0, count))
            {
                var apart = double.Hypot(other.X - nearest.X, other.Z - nearest.Z);
                if (apart < 1e-9) continue;
                edge = Math.Min(edge, (other.Squared - nearest.Squared) / (2 * apart));
            }

            double Draw(int row) => 2 * ValueNoise.Hash(nearest.A, nearest.B, unchecked(seed + row)) - 1;
            var piece = amount * Draw(2) + tilt * Draw(3) * (bentX - nearest.X) + tilt * Draw(4) * (bentZ - nearest.Z);
            var cleft = gap > 0 ? depth * Modifiers.SmoothStep(gap, 0, edge) : 0;
            change[i] = piece - cleft;
        }
        return change;
    }

    private static double Between((double Least, double Most) range, double share) =>
        range.Least + (range.Most - range.Least) * share;
}
