namespace PgmStudio.Geom.Algorithms;

/// <summary>How a stroke fills the ground under it. Every one is the same distance field with a different
/// gate, which is the argument for one type rather than six.</summary>
public enum StrokeStyle
{
    /// <summary>The whole band, one width the whole way — a clean utility road.</summary>
    Solid,
    /// <summary>The band, thinned by a per-cell dice — gravel scattered along a trail rather than a road.</summary>
    Worn,
    /// <summary>The band with its width wandered by a noise field, so the outline is organic, not ruled.</summary>
    Rough,
    /// <summary>Discs at intervals along the stroke's arc, with gaps between them — stones across a void.</summary>
    Stones,
    /// <summary>The band with its width varied along the arc: fat in the middle, thin at the ends.</summary>
    Tapered,
}

/// <summary>One cell a stroke claims. What is laid on it is the caller's material, resolved at the cell —
/// a fill decides the shape of the band and nothing about its finish.</summary>
public readonly record struct StrokeCell(int X, int Z);

/// <summary>
/// The ground a drawn line covers. A fill is not the outline of a shape but the set of cells within a radius
/// of a line, and every variant of it is that same distance test with one extra gate — which is what lets
/// gaps, a wandering width and a thinned surface all be one traversal instead of three shapes.
///
/// <para>The band's <em>outline</em> is <see cref="StrokeOutline"/>, which is what a canvas draws to show an
/// author where a stroke runs and what a polyline shape rasterizes as ground. This is what a stroke prop
/// actually writes, and the two deliberately differ: an outline cannot express a gap or a per-cell dice, so
/// the outline shows the corridor and the fill decides what within it is paved.</para>
/// </summary>
public static class StrokeFill
{
    private const double RoughSwing = 0.45;      // how much of the width a rough edge may gain or lose
    private const int RoughScale = 7;            // blocks per wander of a rough edge
    private const double TaperEnds = 0.35;       // what is left of the width where a tapered stroke runs out
    private const double StoneGap = 1.9;         // stone spacing as a multiple of the stone's own width

    /// <summary>The cells the stroke through <paramref name="points"/> paves. <paramref name="coverage"/> is
    /// what a worn stroke keeps; every other style paves its whole band.</summary>
    public static IEnumerable<StrokeCell> Cells(
        IReadOnlyList<double[]> points, double radius, StrokeStyle style, double coverage, uint seed)
    {
        var centerline = Centerline.Of(points);
        if (centerline.Count < 2 || radius <= 0) yield break;

        // Stones are the one style whose gate is not a width, so its reach must cover a whole stone rather
        // than the nominal band; everything else measures from the same radius the outline was drawn at.
        var stoneRadius = Math.Max(1.0, radius);
        var period = stoneRadius * 2 * StoneGap;

        var reach = (int cx, int cz, PolylineHit hit) => WidthAt(style, radius, cx, cz, hit, seed);
        foreach (var (x, z, hit) in Polyline.Hits(centerline, radius, reach))
        {
            if (!Paves(style, stoneRadius, period, coverage, x, z, hit, seed)) continue;
            yield return new StrokeCell(x, z);
        }
    }

    // The half-width the band reaches at a cell. Only the two styles that vary a width answer anything but the
    // nominal radius; the rest gate cells *inside* the band and so must not narrow it.
    private static double WidthAt(StrokeStyle style, double radius, int x, int z, PolylineHit hit, uint seed) => style switch
    {
        StrokeStyle.Rough   => radius * (1 - RoughSwing + 2 * RoughSwing * PatternNoise.Fbm(x, z, seed + 5, RoughScale, 2)),
        StrokeStyle.Tapered => radius * (TaperEnds + (1 - TaperEnds) * Math.Sin(Math.PI * hit.Along)),
        // Stones reach a full stone's width past the line, since a stone is centred on the arc, not inscribed.
        StrokeStyle.Stones  => Math.Max(1.0, radius),
        _                 => radius,
    };

    // Whether a cell already inside the band is actually paved. Worn rolls a dice per cell; stones ask how far
    // along the arc the cell is and keep only the ones near a stone's centre. Everything else paves.
    private static bool Paves(StrokeStyle style, double stoneRadius, double period,
        double coverage, int x, int z, PolylineHit hit, uint seed) => style switch
    {
        StrokeStyle.Worn   => PatternNoise.Unit(x, z, seed + 11) < coverage,
        StrokeStyle.Stones => StoneAt(stoneRadius, period, hit),
        _                => true,
    };

    // A stone every `period` along the arc: measure the distance to the nearest stone centre in both axes and
    // keep the cell if it falls inside that stone's disc. Doing it on arc length rather than on the straight
    // line is what keeps the spacing even around a bend.
    private static bool StoneAt(double stoneRadius, double period, PolylineHit hit)
    {
        var offset = hit.Arc - Math.Round(hit.Arc / period) * period;
        return offset * offset + hit.Distance * hit.Distance <= stoneRadius * stoneRadius;
    }
}
