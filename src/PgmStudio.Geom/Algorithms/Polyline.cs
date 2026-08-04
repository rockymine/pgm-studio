namespace PgmStudio.Geom.Algorithms;

/// <summary>Where a point sits relative to a polyline: how far it is from the nearest point on the line, how
/// far along the line that point is as a fraction of the whole, and the same in absolute length. A band that
/// tapers reads <see cref="Along"/>; stones spaced down a track read <see cref="Arc"/>; everything reads
/// <see cref="Distance"/>.</summary>
public readonly record struct PathHit(double Distance, double Along, double Arc);

/// <summary>
/// Distance from a point to a polyline — the primitive a stroke of any width is filled by. A path is not the
/// outline of a shape but the set of cells within a radius of a line, which is a swept disc (a capsule): test
/// the distance, not the boundary. That is what lets one line carry a width that varies along it, an outline
/// that wanders, or gaps, without any of those becoming a different shape to trace.
///
/// <para>Two dimensions here, because a path lies on the ground. The same test one dimension up is a limb, and
/// <see cref="SweptVolume"/> is where that lives.</para>
/// </summary>
public static class Polyline
{
    /// <summary>The nearest point on <paramref name="centerline"/> to (<paramref name="x"/>,
    /// <paramref name="z"/>). Each point is <c>[x, z]</c>. A centerline of fewer than two points has no
    /// segments, so everything is infinitely far from it.</summary>
    public static PathHit Nearest(IReadOnlyList<double[]> centerline, double x, double z)
    {
        if (centerline.Count < 2) return new PathHit(double.MaxValue, 0, 0);

        double best = double.MaxValue, bestArc = 0, walked = 0, total = 0;
        for (var i = 0; i < centerline.Count - 1; i++)
        {
            var (ax, az) = (centerline[i][0], centerline[i][1]);
            var (bx, bz) = (centerline[i + 1][0], centerline[i + 1][1]);
            double vx = bx - ax, vz = bz - az;
            var lengthSq = vx * vx + vz * vz;
            var segment = Math.Sqrt(lengthSq);

            var t = lengthSq > 1e-12 ? Math.Clamp(((x - ax) * vx + (z - az) * vz) / lengthSq, 0, 1) : 0;
            double dx = x - (ax + t * vx), dz = z - (az + t * vz);
            var distance = Math.Sqrt(dx * dx + dz * dz);
            if (distance < best) { best = distance; bestArc = walked + t * segment; }

            walked += segment;
            total += segment;
        }
        return new PathHit(best, total > 1e-12 ? bestArc / total : 0, bestArc);
    }

    /// <summary>The total length of a polyline — what a caller spaces things along.</summary>
    public static double Length(IReadOnlyList<double[]> centerline)
    {
        double total = 0;
        for (var i = 0; i < centerline.Count - 1; i++)
            total += Math.Sqrt(Sq(centerline[i + 1][0] - centerline[i][0]) + Sq(centerline[i + 1][1] - centerline[i][1]));
        return total;
    }

    /// <summary>The integer cells within <paramref name="radius"/> of the centerline, scanned over the band's
    /// own bounding box rather than the caller's world. <paramref name="radiusAt"/> may widen the radius per
    /// cell — a rough or tapered edge is that function, not a different traversal.</summary>
    public static IEnumerable<(int X, int Z)> Band(
        IReadOnlyList<double[]> centerline, double radius, Func<int, int, PathHit, double>? radiusAt = null)
        => Hits(centerline, radius, radiusAt).Select(found => (found.X, found.Z));

    /// <summary>As <see cref="Band"/>, but each cell keeps the hit that admitted it. A caller that gates cells
    /// <em>inside</em> the band — a worn surface, stones spaced down the arc — needs the same measurement the
    /// traversal already took, and computing it twice is the whole cost of the scan done again.</summary>
    public static IEnumerable<(int X, int Z, PathHit Hit)> Hits(
        IReadOnlyList<double[]> centerline, double radius, Func<int, int, PathHit, double>? radiusAt = null)
    {
        if (centerline.Count < 2 || radius <= 0) yield break;

        double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
        foreach (var point in centerline)
        {
            minX = Math.Min(minX, point[0]); maxX = Math.Max(maxX, point[0]);
            minZ = Math.Min(minZ, point[1]); maxZ = Math.Max(maxZ, point[1]);
        }
        // A per-cell radius can only widen the band, and never past double the nominal — the scan covers that.
        var reach = radius * 2 + 2;
        for (var z = (int)Math.Floor(minZ - reach); z <= (int)Math.Ceiling(maxZ + reach); z++)
        for (var x = (int)Math.Floor(minX - reach); x <= (int)Math.Ceiling(maxX + reach); x++)
        {
            var hit = Nearest(centerline, x + 0.5, z + 0.5);
            var here = radiusAt is null ? radius : radiusAt(x, z, hit);
            if (hit.Distance <= here) yield return (x, z, hit);
        }
    }

    private static double Sq(double v) => v * v;
}
