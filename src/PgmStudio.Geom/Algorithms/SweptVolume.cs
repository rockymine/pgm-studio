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
    /// <paramref name="startRadius"/> at the first sample to <paramref name="endRadius"/> at the last, joined
    /// into a run every cell of which shares a <b>face</b> with the next. Cells are yielded as they are
    /// stamped, so a caller that writes into a set gets each one once whatever the overlap.
    ///
    /// <para>The face is the load-bearing half. A limb thinner than a block is one cell per sample, and two
    /// consecutive samples can cross two or three block boundaries at once — which leaves the cells they
    /// stamp touching along an edge or at a corner, a join a viewer sees straight through and reads as two
    /// blocks hanging in the air. So the walk from one sample's cell to the next is broken into single-axis
    /// steps (<see cref="Between"/>), and every cell of those steps is stamped.</para></summary>
    public static IEnumerable<(int X, int Y, int Z)> Sweep(
        IReadOnlyList<Vec3> path, double startRadius, double endRadius)
    {
        if (path.Count == 0) yield break;
        (int X, int Y, int Z)? previous = null;
        for (var i = 0; i < path.Count; i++)
        {
            var t = path.Count > 1 ? (double)i / (path.Count - 1) : 0;
            var radius = startRadius + (endRadius - startRadius) * t;
            var here = Cell(path[i]);
            if (previous is { } from)
                foreach (var step in Between(from, here)) yield return step;
            foreach (var cell in Ball(path[i], radius)) yield return cell;
            previous = here;
        }
    }

    /// <summary>The block a point sits in — the one <see cref="Ball"/> stamps whatever the radius, and the
    /// one a run is threaded through.</summary>
    public static (int X, int Y, int Z) Cell(Vec3 point)
        => ((int)Math.Round(point.X), (int)Math.Round(point.Y), (int)Math.Round(point.Z));

    /// <summary>The cells threading <paramref name="from"/> to <paramref name="to"/> one axis at a time, so
    /// consecutive cells share a face. The axis advanced at each step is the one whose share of the walk is
    /// furthest behind, which keeps the staircase on the straight line between the two rather than turning
    /// all of one axis before starting the next. <paramref name="from"/> is not yielded and
    /// <paramref name="to"/> is.</summary>
    public static IEnumerable<(int X, int Y, int Z)> Between((int X, int Y, int Z) from, (int X, int Y, int Z) to)
    {
        int stepX = Math.Sign(to.X - from.X), stepY = Math.Sign(to.Y - from.Y), stepZ = Math.Sign(to.Z - from.Z);
        int spanX = Math.Abs(to.X - from.X), spanY = Math.Abs(to.Y - from.Y), spanZ = Math.Abs(to.Z - from.Z);
        var steps = spanX + spanY + spanZ;
        int errorX = 0, errorY = 0, errorZ = 0;
        var cell = from;
        for (var taken = 0; taken < steps; taken++)
        {
            errorX += spanX; errorY += spanY; errorZ += spanZ;
            if (spanX > 0 && errorX >= errorY && errorX >= errorZ) { cell.X += stepX; errorX -= steps; }
            else if (spanY > 0 && errorY >= errorZ) { cell.Y += stepY; errorY -= steps; }
            else { cell.Z += stepZ; errorZ -= steps; }
            yield return cell;
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
