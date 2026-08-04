namespace PgmStudio.Geom.Algorithms;

/// <summary>One leaf cluster: an oblate blob seated just beyond a branch tip. <paramref name="Tip"/> is the
/// index of the tip it belongs to, which is what the seam between neighbouring clusters is decided by.</summary>
public readonly record struct LeafCluster(Vec3 Center, Vec3 Radii, int Tip);

/// <summary>
/// Turns a grown tree's tips into its foliage. The crown is where a generated tree usually gives itself away:
/// a spherical brush with holes punched in it reads as one blob, and no viewer can tell which branch a patch
/// of leaves belongs to. So the crown is built the way a mapmaker builds one by hand — <b>one dense cluster
/// per outer tip</b>, pushed up and out along the branch, never back down toward the trunk, because leaves sit
/// at the branch ends and not on the wood.
///
/// <para>What keeps it readable is the <b>seam</b>. A cell fills only when it belongs clearly to one cluster
/// and not almost equally to its neighbour, so a band of air survives between adjacent clusters and each patch
/// still reads as its own branch's. The airiness lives <em>between</em> the clusters, not as holes punched
/// inside them — which is also the real reason to keep the branch count low, since clusters that overlap
/// everywhere have no seams left to show.</para>
/// </summary>
public static class TreeCrown
{
    /// <summary>How much closer a cell must be to its nearest cluster than to the next one to be filled. Zero
    /// merges every cluster into one mass; too high and the crown falls apart into separate balls.</summary>
    private const double SeamMargin = 0.30;

    /// <summary>How full a cluster is. The gaps that make a crown airy are the seams, so a cluster itself is
    /// nearly solid — punching holes through one only makes it read as a damaged sphere.</summary>
    private const double Density = 0.92;

    /// <summary>Place a cluster beyond each tip. <paramref name="leafSize"/> scales them and
    /// <paramref name="treeSize"/> shrinks them with the tree, so a sapling carries a few small clusters on a
    /// thin stalk rather than a scaled-down big tree's crown.</summary>
    public static List<LeafCluster> Clusters(IReadOnlyList<TreeTip> tips, double leafSize, double treeSize, uint seed)
    {
        var clusters = new List<LeafCluster>(tips.Count);
        for (var i = 0; i < tips.Count; i++)
        {
            var tip = tips[i];
            var radius = (2.3 + leafSize * 2.4) * (0.55 + 0.45 * treeSize) + PatternNoise.Unit(i, 2, seed) * 1.0;
            var height = radius * (0.55 + PatternNoise.Unit(i, 5, seed) * 0.12);   // an oblate disc, not a ball
            var outward = tip.Direction.Normalized;
            var center = tip.Position + outward * 1.6 + new Vec3(0, Math.Max(0, outward.Y) * 1.4 + height * 0.25, 0);
            clusters.Add(new LeafCluster(center, new Vec3(radius, height, radius), i));
        }
        return clusters;
    }

    /// <summary>Which cluster owns a cell, or null where none clearly does — the seam. A cell is owned when it
    /// is inside its nearest cluster, is <see cref="SeamMargin"/> clearer of that one than of the next, and
    /// survives the density roll.</summary>
    public static int? OwnerAt(IReadOnlyList<LeafCluster> clusters, Vec3 point, uint seed)
    {
        double nearest = double.MaxValue, second = double.MaxValue;
        var owner = -1;
        for (var i = 0; i < clusters.Count; i++)
        {
            var quadric = Quadric(clusters[i], point);
            if (quadric < nearest) { second = nearest; nearest = quadric; owner = i; }
            else if (quadric < second) second = quadric;
        }
        if (owner < 0 || nearest >= 1 || second - nearest <= SeamMargin) return null;

        var cell = (X: (int)Math.Round(point.X), Y: (int)Math.Round(point.Y), Z: (int)Math.Round(point.Z));
        return PatternNoise.Unit(cell.X, cell.Y, cell.Z, seed + 31) < Density ? owner : null;
    }

    /// <summary>The block range the whole crown can touch — the union of its clusters, with room for the
    /// strands that hang below.</summary>
    public static (Vec3 Min, Vec3 Max) Bounds(IReadOnlyList<LeafCluster> clusters, double strandReach = 4)
    {
        if (clusters.Count == 0) return (default, default);
        Vec3 min = new(double.MaxValue, double.MaxValue, double.MaxValue);
        Vec3 max = new(double.MinValue, double.MinValue, double.MinValue);
        foreach (var cluster in clusters)
        {
            min = new Vec3(Math.Min(min.X, cluster.Center.X - cluster.Radii.X),
                           Math.Min(min.Y, cluster.Center.Y - cluster.Radii.Y - strandReach),
                           Math.Min(min.Z, cluster.Center.Z - cluster.Radii.Z));
            max = new Vec3(Math.Max(max.X, cluster.Center.X + cluster.Radii.X),
                           Math.Max(max.Y, cluster.Center.Y + cluster.Radii.Y),
                           Math.Max(max.Z, cluster.Center.Z + cluster.Radii.Z));
        }
        return (min, max);
    }

    private static double Quadric(in LeafCluster cluster, Vec3 point)
    {
        var d = point - cluster.Center;
        return Sq(d.X / Math.Max(0.01, cluster.Radii.X))
             + Sq(d.Y / Math.Max(0.01, cluster.Radii.Y))
             + Sq(d.Z / Math.Max(0.01, cluster.Radii.Z));
    }

    private static double Sq(double v) => v * v;
}
