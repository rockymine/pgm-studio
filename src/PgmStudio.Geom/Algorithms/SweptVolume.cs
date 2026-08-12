namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// The cells a thickening line occupies in three dimensions: a ball stamped at every sample along a path, its
/// radius interpolated from end to end. This is <see cref="Polyline.Band"/> lifted one dimension — a path on
/// the ground and a limb in the air are the same shape, a disc swept along a curve — so a tree's wood and a
/// track's gravel come out of one primitive rather than two that drift apart.
///
/// <para>Stamping per sample rather than testing distance to the whole path is the right trade here: a limb is
/// densely sampled and thin, so the balls overlap into a continuous tube, and each stamp costs its own small
/// neighbourhood instead of a scan of the limb's whole bounding box.</para>
/// </summary>
public static class SweptVolume
{
    /// <summary>The cells within a radius of <paramref name="path"/>, the radius running from
    /// <paramref name="startRadius"/> at the first sample to <paramref name="endRadius"/> at the last. Cells
    /// are yielded as they are stamped, so a caller that writes into a set gets each one once whatever the
    /// overlap.</summary>
    public static IEnumerable<(int X, int Y, int Z)> Sweep(
        IReadOnlyList<Vec3> path, double startRadius, double endRadius)
    {
        if (path.Count == 0) yield break;
        for (var i = 0; i < path.Count; i++)
        {
            var t = path.Count > 1 ? (double)i / (path.Count - 1) : 0;
            var radius = startRadius + (endRadius - startRadius) * t;
            foreach (var cell in Ball(path[i], radius)) yield return cell;
        }
    }

    /// <summary>The cells within <paramref name="radius"/> of a point — always including the block the point
    /// sits in, so a thin limb cannot evaporate.
    ///
    /// <para>The membership test measures to a cell's integer coordinate, so a centre sitting near a cell
    /// corner is √3/2 = 0.866 from every candidate around it. Any radius below that can therefore select
    /// nothing at all — at 0.55 a third of positions do, at 0.6 a fifth — and a limb swept at a twig's radius
    /// comes out as a dotted line of detached blocks rather than a branch. Stamping the containing block
    /// unconditionally is what closes that band; above 0.866 it is already inside the ball and the extra
    /// yield is a duplicate a caller writing into a set never sees.</para></summary>
    public static IEnumerable<(int X, int Y, int Z)> Ball(Vec3 center, double radius)
    {
        var reach = (int)Math.Ceiling(Math.Max(radius, 0.5));
        int cx = (int)Math.Round(center.X), cy = (int)Math.Round(center.Y), cz = (int)Math.Round(center.Z);
        yield return (cx, cy, cz);
        if (radius < 0.5) yield break;

        var radiusSq = radius * radius;
        for (var dy = -reach; dy <= reach; dy++)
        for (var dz = -reach; dz <= reach; dz++)
        for (var dx = -reach; dx <= reach; dx++)
        {
            // Measured from the continuous centre, not the rounded one, so a curve stays smooth as it drifts
            // across a block boundary instead of stepping.
            double ox = cx + dx - center.X, oy = cy + dy - center.Y, oz = cz + dz - center.Z;
            if (ox * ox + oy * oy + oz * oz <= radiusSq) yield return (cx + dx, cy + dy, cz + dz);
        }
    }
}
