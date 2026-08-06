namespace PgmStudio.Geom.Algorithms;

/// <summary>How a channel is banked and how its bed is cut. Every form is the same swept-disc band the path
/// stroke fills, with a different width and depth law — which is the argument for one type rather than three
/// carve passes.</summary>
public enum ChannelForm
{
    /// <summary>A clean, uniform width the whole way, deepest on the centerline — a dug canal.</summary>
    Canal,
    /// <summary>The band with its width wandered by a noise field, so the shoreline is organic, not ruled.</summary>
    Natural,
    /// <summary>The band narrowing and shallowing towards its two ends — a watercourse that runs out into
    /// riffles rather than stopping square.</summary>
    Stream,
}

/// <summary>One cell a channel carves: where it is, and how deep the bed is cut below the water line there —
/// deepest on the centerline, one block at the shore.</summary>
public readonly record struct WaterCell(int X, int Z, int Depth);

/// <summary>
/// The bed a drawn channel cuts. Water cannot drape on a slope the way gravel can — laid on the surface it
/// reads as blue paint — so a channel is not a finish over the ground but a shape taken <em>out</em> of it: a
/// carved bed under a level fill. This is the pure side of that, the same distance field a
/// <see cref="PathStroke"/> is (a swept disc of a line), yielding a depth per cell rather than a paving block.
///
/// <para>The depth law is a parabolic U — deepest on the centerline, rising to a single block at the band's
/// edge — so the fill sits in a bowl rather than a trench with vertical walls. The world-writing side (the
/// dressing pass) turns a depth into a carve-and-fill against the surface it actually crosses; here there is
/// no world, only the profile.</para>
/// </summary>
public static class WaterBed
{
    private const double NaturalSwing = 0.4;   // how much of the width a natural edge may gain or lose
    private const int NaturalScale = 7;        // blocks per wander of a natural edge
    private const double StreamEnds = 0.4;     // what is left of the width and depth where a stream runs out
    private const int ShoreScale = 6;          // blocks per wander of the shore's own width field

    /// <summary>The cells the channel through <paramref name="points"/> carves, each with the depth its bed is
    /// cut to below the water line. <paramref name="radius"/> is the nominal half-width and
    /// <paramref name="depth"/> the deepest cut, on the centerline.</summary>
    public static IEnumerable<WaterCell> Cells(
        IReadOnlyList<double[]> points, double radius, double depth, ChannelForm form, uint seed)
    {
        var centerline = PathBand.Centerline(points);
        if (centerline.Count < 2 || radius <= 0 || depth <= 0) yield break;

        var reach = (int cx, int cz, PathHit hit) => WidthAt(form, radius, cx, cz, hit, seed);
        foreach (var (x, z, hit) in Polyline.Hits(centerline, radius, reach))
        {
            var here = WidthAt(form, radius, x, z, hit, seed);
            if (here <= 0) continue;

            // How far this cell sits from the centerline, as a fraction of the local half-width: 0 on the line,
            // 1 at the shore. The bowl is deepest at 0 and one block deep at 1.
            var offset = Math.Clamp(hit.Distance / here, 0, 1);
            var bowl = 1 - offset * offset;
            // A stream shallows towards its ends as well as narrowing, so its riffles are ankle-deep where a
            // canal of the same depth would still be cut to the bottom.
            var run = form == ChannelForm.Stream ? StreamEnds + (1 - StreamEnds) * Math.Sin(Math.PI * hit.Along) : 1.0;

            var cellDepth = (int)Math.Round(1 + (depth - 1) * bowl * run);
            yield return new WaterCell(x, z, Math.Max(1, cellDepth));
        }
    }

    /// <summary>The beach cells the channel meets the land through — the band <em>outside</em> the water, out to
    /// a shore width that wanders with a noise field and drops to nothing in places, so the water meets the grass
    /// directly in some stretches and spreads into a flat in others. The bank material is laid on these cells'
    /// surface; the water never reaches them, so they carry no depth.
    ///
    /// <para>How wide the beach runs is the channel's own read: a <see cref="ChannelForm.Canal"/> keeps a narrow,
    /// even bank, a <see cref="ChannelForm.Stream"/> spreads into wide flats, and a <see cref="ChannelForm.Natural"/>
    /// wanders between — the same distinction the water width already draws, carried out onto the land.</para></summary>
    public static IEnumerable<(int X, int Z)> ShoreCells(
        IReadOnlyList<double[]> points, double radius, ChannelForm form, double shoreWidth, uint seed)
    {
        if (shoreWidth <= 0) yield break;
        var centerline = PathBand.Centerline(points);
        if (centerline.Count < 2 || radius <= 0) yield break;

        var scan = radius * 1.5 + shoreWidth * 2 + 1;
        var reach = (int cx, int cz, PathHit hit) => WidthAt(form, radius, cx, cz, hit, seed) + ShoreAt(form, shoreWidth, cx, cz, seed);
        foreach (var (x, z, hit) in Polyline.Hits(centerline, scan, reach))
        {
            var water = WidthAt(form, radius, x, z, hit, seed);
            if (hit.Distance <= water) continue;   // inside the water — that is the bed's, not the beach's
            if (hit.Distance <= water + ShoreAt(form, shoreWidth, x, z, seed)) yield return (x, z);
        }
    }

    // The half-width the band reaches at a cell. Canal holds the nominal radius; a natural edge wanders it with
    // a noise field; a stream tapers it along the run. The same shape the path stroke's rough and tapered edges
    // take, so the two tools' bands read as one idea.
    private static double WidthAt(ChannelForm form, double radius, int x, int z, PathHit hit, uint seed) => form switch
    {
        ChannelForm.Natural => radius * (1 - NaturalSwing + 2 * NaturalSwing * PatternNoise.Fbm(x, z, seed + 5, NaturalScale, 2)),
        ChannelForm.Stream  => radius * (StreamEnds + (1 - StreamEnds) * Math.Sin(Math.PI * hit.Along)),
        _                   => radius,
    };

    // How far the beach reaches past the water at a cell: a per-form base width, wandered by a smooth field that
    // drops it to zero across much of its range so the shore is a broken flat, not a fixed-width ring.
    private static double ShoreAt(ChannelForm form, double shoreWidth, int x, int z, uint seed)
    {
        var baseWidth = shoreWidth * form switch
        {
            ChannelForm.Canal  => 0.55,   // clean-banked: a narrow, even beach
            ChannelForm.Stream => 1.6,    // spreads into wide flats
            _                  => 1.0,    // wanders between
        };
        return Math.Max(0, baseWidth * (1.8 * PatternNoise.Value(x, z, seed + 91, ShoreScale) - 0.3));
    }
}
