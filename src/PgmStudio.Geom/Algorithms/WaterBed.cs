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
    /// <summary>A watercourse that beads along its length — the width pinches and swells on a fixed beat down the
    /// arc (never wider than the nominal, pinching to half it), and the water runs shallower throughout, so it
    /// reads as a stream running out into a string of riffles rather than one even channel.</summary>
    Stream,
}

/// <summary>What a body of water is drawn as, which is what its own points mean. A <see cref="Channel"/>
/// strokes them as a centerline and takes its width from a radius; a <see cref="Pool"/> closes them into a
/// ring and fills it. The bowl is the same law either way — deepest away from the shore, one block at it —
/// and only the distance it is measured along differs, which is the argument for one prop rather than two.</summary>
public enum WaterShape
{
    /// <summary>A stroked line: a canal, a river, a moat.</summary>
    Channel,
    /// <summary>A filled ring: a harbour, a lake, a flooded basin. A rectangle is four points.</summary>
    Pool,
}

/// <summary>One cell a channel carves: where it is, and how deep the bed is cut below the water line there —
/// deepest on the centerline, one block at the shore.</summary>
public readonly record struct WaterCell(int X, int Z, int Depth);

/// <summary>
/// The bed a drawn channel cuts. Water cannot drape on a slope the way gravel can — laid on the surface it
/// reads as blue paint — so a channel is not a finish over the ground but a shape taken <em>out</em> of it: a
/// carved bed under a level fill. This is the pure side of that, the same distance field a
/// <see cref="StrokeFill"/> is (a swept disc of a line), yielding a depth per cell rather than a paving block.
///
/// <para>The depth law is a parabolic U — deepest on the centerline, rising to a single block at the band's
/// edge — so the fill sits in a bowl rather than a trench with vertical walls. The world-writing side (the
/// dressing pass) turns a depth into a carve-and-fill against the surface it actually crosses; here there is
/// no world, only the profile.</para>
/// </summary>
public static class WaterBed
{
    // The channel constants the bed and beach are cut to, stated once here because the width and shore laws
    // below all read them (`docs/world-export/decoration.md` §5).
    private const double StreamBeat = 0.42;    // radians of the width sine per block of arc — a pinch every ~π/beat ≈ 7.5 blocks
    private const double StreamDepth = 0.6;    // a stream runs this much of a canal's depth throughout
    private const int WidthNoiseScale = 5;     // blocks per wander of the natural/stream edge wobble
    private const int ShoreScale = 6;          // blocks per wander of the shore's own width field

    /// <summary>The cells the channel through <paramref name="points"/> carves, each with the depth its bed is
    /// cut to below the water line. <paramref name="radius"/> is the nominal half-width, <paramref name="depth"/>
    /// the deepest cut on the centerline, and <paramref name="edge"/> the amplitude of the width wobble a natural
    /// or stream form carries (in blocks).</summary>
    public static IEnumerable<WaterCell> Cells(
        IReadOnlyList<double[]> points, double radius, double depth, ChannelForm form, double edge, uint seed)
    {
        var centerline = Centerline.Of(points);
        if (centerline.Count < 2 || radius <= 0 || depth <= 0) yield break;

        var reach = (int cx, int cz, PolylineHit hit) => WidthAt(form, radius, edge, cx, cz, hit, seed);
        foreach (var (x, z, hit) in Polyline.Hits(centerline, radius + Math.Max(0, edge) + 1, reach))
        {
            var here = WidthAt(form, radius, edge, x, z, hit, seed);
            if (here <= 0 || hit.Distance > here) continue;

            // How far this cell sits from the centerline, as a fraction of the local half-width: 0 on the line,
            // 1 at the shore. The bowl is deepest at 0 and one block deep at 1 — the prototype's cross-section.
            var offset = Math.Clamp(hit.Distance / here, 0, 1);
            var bowl = 1 - offset * offset;
            // A stream runs shallow throughout, not just at its ends — its whole length is a riffle.
            var run = form == ChannelForm.Stream ? StreamDepth : 1.0;

            var cellDepth = (int)Math.Round(1 + (depth - 1) * bowl * run);
            yield return new WaterCell(x, z, Math.Max(1, cellDepth));
        }
    }

    /// <summary>The beach cells the channel meets the land through — the band <em>outside</em> the water. It rides
    /// just past the water edge, so a beach cell's inner edge <em>is</em> the water's: the shore always hugs the
    /// water, whatever shape the water takes. The bank material is laid on these cells' surface; the water never
    /// reaches them, so they carry no depth.
    ///
    /// <para>How wide the beach runs is parameterised along the channel's <b>arc</b>, not the plan grid: at a point
    /// down the run both banks take the same width, so the beach is symmetric about the water and widens and narrows
    /// <em>with</em> it — a flat here, meeting the grass directly there — rather than drifting onto one bank the way
    /// a plain spatial field does on a bend. With <paramref name="wander"/> off the beach is an even band the whole
    /// way; with it on, a smooth field along the arc opens and closes it, dropping it to nothing in places.</para></summary>
    public static IEnumerable<(int X, int Z)> ShoreCells(
        IReadOnlyList<double[]> points, double radius, ChannelForm form, double shoreWidth, double edge, bool wander, uint seed)
    {
        if (shoreWidth <= 0) yield break;
        var centerline = Centerline.Of(points);
        if (centerline.Count < 2 || radius <= 0) yield break;

        var scan = radius + Math.Max(0, edge) + shoreWidth + 1;
        var reach = (int cx, int cz, PolylineHit hit) => WidthAt(form, radius, edge, cx, cz, hit, seed) + ShoreAt(shoreWidth, wander, hit, seed);
        foreach (var (x, z, hit) in Polyline.Hits(centerline, scan, reach))
        {
            var water = WidthAt(form, radius, edge, x, z, hit, seed);
            if (hit.Distance <= water) continue;   // inside the water — that is the bed's, not the beach's
            if (hit.Distance <= water + ShoreAt(shoreWidth, wander, hit, seed)) yield return (x, z);
        }
    }

    /// <summary>The cells a pool fills inside <paramref name="ring"/>, each with the depth its bed is cut to
    /// below the water line. The same bowl a channel cuts, measured inward from the shore instead of out from
    /// a centerline: one block deep at the ring and full <paramref name="depth"/> once
    /// <paramref name="shelf"/> blocks in, so a harbour shelves off its quays rather than dropping to a
    /// trench at the wall. A ring narrower than twice the shelf never reaches full depth, which is what a
    /// shallow pool is.</summary>
    public static IEnumerable<WaterCell> PoolCells(
        IReadOnlyList<double[]> ring, double shelf, double depth, double edge, uint seed)
    {
        if (ring.Count < 3 || depth <= 0) yield break;
        var reach = Math.Max(1.0, shelf);

        foreach (var (x, z, inset) in RingCells(ring, edge, seed))
        {
            var bowl = Math.Clamp(inset / reach, 0, 1);
            yield return new WaterCell(x, z, Math.Max(1, (int)Math.Round(1 + (depth - 1) * bowl)));
        }
    }

    /// <summary>The beach a pool meets the land through — the band just <em>outside</em> its ring, the same
    /// relation a channel's shore has to its water. Empty for a pool that states no shore.</summary>
    public static IEnumerable<(int X, int Z)> PoolShoreCells(
        IReadOnlyList<double[]> ring, double shoreWidth, double edge, bool wander, uint seed)
    {
        if (ring.Count < 3 || shoreWidth <= 0) yield break;
        var inside = new HashSet<(int, int)>(RingCells(ring, edge, seed).Select(cell => (cell.X, cell.Z)));
        var scanned = new HashSet<(int, int)>();

        foreach (var (x, z) in Polyline.Hits(Closed(ring), shoreWidth + Math.Max(0, edge) + 1)
                                       .Select(hit => (hit.X, hit.Z)))
        {
            if (inside.Contains((x, z)) || !scanned.Add((x, z))) continue;
            var width = wander
                ? Math.Max(0, shoreWidth * (1.9 * PatternNoise.Value(x, z, seed + 91, ShoreScale) - 0.25))
                : shoreWidth;
            if (DistanceToRing(ring, x + 0.5, z + 0.5) <= width) yield return (x, z);
        }
    }

    // Every cell inside the ring with how far into it that cell stands, in blocks. A natural edge wobbles the
    // boundary in and out by `edge` blocks so a lake does not read as a ruled polygon; the wobble is a value
    // field on the cell, the same one a channel's width carries, so the two edges are the same roughness at
    // the same setting. The inset rides along because the caller's bowl is a function of it and computing the
    // distance twice for one cell is the same walk twice.
    private static IEnumerable<(int X, int Z, double Inset)> RingCells(
        IReadOnlyList<double[]> ring, double edge, uint seed)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
        foreach (var point in ring)
        {
            minX = Math.Min(minX, point[0]); maxX = Math.Max(maxX, point[0]);
            minZ = Math.Min(minZ, point[1]); maxZ = Math.Max(maxZ, point[1]);
        }
        var slack = Math.Max(0, edge) + 1;
        var outline = Cast(ring);
        for (var x = (int)Math.Floor(minX - slack); x <= (int)Math.Ceiling(maxX + slack); x++)
        for (var z = (int)Math.Floor(minZ - slack); z <= (int)Math.Ceiling(maxZ + slack); z++)
        {
            var wobble = edge <= 0 ? 0 : edge * (PatternNoise.Value(x, z, seed + 5, WidthNoiseScale) - 0.5);
            var reach = DistanceToRing(ring, x + 0.5, z + 0.5);
            var inset = Polygon.PointInRing(x + 0.5, z + 0.5, outline) ? reach : -reach;
            if (inset + wobble > 0) yield return (x, z, Math.Max(0, inset + wobble));
        }
    }

    private static double DistanceToRing(IReadOnlyList<double[]> ring, double px, double pz)
    {
        var best = double.MaxValue;
        for (var i = 0; i < ring.Count; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Count];
            best = Math.Min(best, SegmentDistance(px, pz, a[0], a[1], b[0], b[1]));
        }
        return best;
    }

    private static double SegmentDistance(double px, double pz, double ax, double az, double bx, double bz)
    {
        double dx = bx - ax, dz = bz - az;
        var length = dx * dx + dz * dz;
        var t = length <= 0 ? 0 : Math.Clamp(((px - ax) * dx + (pz - az) * dz) / length, 0, 1);
        double cx = ax + t * dx, cz = az + t * dz;
        return Math.Sqrt((px - cx) * (px - cx) + (pz - cz) * (pz - cz));
    }

    private static List<double[]> Closed(IReadOnlyList<double[]> ring)
        => [.. ring, ring[0]];

    private static List<IReadOnlyList<double>> Cast(IReadOnlyList<double[]> ring)
        => [.. ring.Select(point => (IReadOnlyList<double>)point)];

    // The half-width the water reaches at a cell, the prototype's `drawChannel` R. A canal holds the nominal
    // radius. A natural edge wobbles it by an absolute amount (a value field, ±edge blocks). A stream beads: the
    // width runs a rectified sine along the arc — pinching to half the radius and swelling back to it on a fixed
    // beat — with the same small wobble on top, so it narrows and widens down its length rather than tapering once.
    private static double WidthAt(ChannelForm form, double radius, double edge, int x, int z, PolylineHit hit, uint seed)
    {
        var wobble = edge * (PatternNoise.Value(x, z, seed + 5, WidthNoiseScale) - 0.5);
        return form switch
        {
            ChannelForm.Natural => radius + 2 * wobble,
            ChannelForm.Stream  => radius * (0.5 + 0.5 * Math.Abs(Math.Sin(hit.Arc * StreamBeat))) + wobble,
            _                   => radius,
        };
    }

    // How far the beach reaches past the water at a cell. Off: the full width, an even band. On: a smooth field
    // read along the arc — the same rescaling the prototype's `shoreWidth` uses to drop a shore to nothing in
    // places — but sampled by arc position so both banks share one width and the beach stays wrapped to the water
    // rather than a spatial field that opens on one bank and closes on the other around a bend.
    private static double ShoreAt(double shoreWidth, bool wander, PolylineHit hit, uint seed)
        => wander
            ? Math.Max(0, shoreWidth * (1.9 * PatternNoise.Value((int)Math.Round(hit.Arc), 0, seed + 91, ShoreScale) - 0.25))
            : shoreWidth;
}
