namespace PgmStudio.Geom;

/// <summary>
/// An outline reshaped by points stated where they stand on it: each a fraction of the way along an edge, moved a
/// number of blocks at right angles to that edge — into the ring where the number is positive, out of it where it
/// is negative. What a board states when a coast is placed rather than drawn from a seed: a frontline pushed out
/// toward an island and pulled back beside it.
///
/// <para><b>Inside is asked of the ring.</b> A point half a block off the edge is tested against the ring, so the
/// answer is right for a ring wound either way and for an edge whose ring's centroid lies across it, as an arm of
/// a U's does. The ring's own vertices never move, and an edge with no pull keeps the line it was drawn with.</para>
/// </summary>
public static class RingPull
{
    /// <summary>One point: <see cref="At"/> of the way along its edge, moved <see cref="In"/> blocks across it.</summary>
    public readonly record struct Pull(double At, double In);

    /// <summary>The ring with each edge's points inserted after the vertex the edge leaves, in order along it, every
    /// edge named by its index on <paramref name="ring"/> and each point rounded to a tenth of a block; or null where
    /// the pulled ring crosses itself. An edge of no length takes no point.</summary>
    public static List<double[]>? Draw(IReadOnlyList<double[]> ring, IReadOnlyDictionary<int, IReadOnlyList<Pull>> pulls)
    {
        var drawn = new List<double[]>(ring.Count + pulls.Sum(edge => edge.Value.Count));
        for (var vertex = 0; vertex < ring.Count; vertex++)
        {
            var (from, to) = (ring[vertex], ring[(vertex + 1) % ring.Count]);
            drawn.Add([from[0], from[1]]);
            if (!pulls.TryGetValue(vertex, out var along)) continue;

            double alongX = to[0] - from[0], alongZ = to[1] - from[1];
            var length = Math.Sqrt(alongX * alongX + alongZ * alongZ);
            if (length < 1e-9) continue;
            double acrossX = -alongZ / length, acrossZ = alongX / length;
            foreach (var pull in along.OrderBy(pull => pull.At))
            {
                double x = from[0] + alongX * pull.At, z = from[1] + alongZ * pull.At;
                var inward = Polygon.PointInRing(x + acrossX * 0.5, z + acrossZ * 0.5, ring) ? 1 : -1;
                drawn.Add([Math.Round(x + inward * acrossX * pull.In, 1), Math.Round(z + inward * acrossZ * pull.In, 1)]);
            }
        }
        return Polygon.SelfIntersects(drawn) ? null : drawn;
    }
}
