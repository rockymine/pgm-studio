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
    /// merges every cluster into one mass; too high and the crown falls apart into separate balls. It is wide
    /// because the clusters are small: a puff two blocks across needs a seam of comparable width before the
    /// eye reads two of them rather than one lump.</summary>
    private const double SeamMargin = 0.68;

    /// <summary>How full a cluster is, and it is mostly air. A hand-built crown measures **24% block** over
    /// its own volume, with every one of its leaves carrying air on at least one side — there is no interior
    /// to it at all. Foliage is lace the light comes through, not a brush stroke with holes in it, and a
    /// cluster filled two-thirds is the single thing that made a generated crown read as one solid mass
    /// however the seams between clusters were widened.</summary>
    private const double Density = 0.45;

    /// <summary>How far along the branch's own direction a cluster is carried past its tip, as a share of the
    /// cluster's radius. It is a share rather than a distance so that the tip stays inside the cluster at every
    /// size: a crown seated beyond the branch it hangs on is foliage attached to nothing, which measures as a
    /// tenth of the wood contact an author's crown has and, on a small tree, as a crown that floats away
    /// entirely.</summary>
    private const double TipCarry = 0.35;

    /// <summary>Place a cluster on each tip. <paramref name="leafSize"/> scales them and
    /// <paramref name="treeSize"/> shrinks them with the tree, so a sapling carries a few small clusters on a
    /// thin stalk rather than a scaled-down big tree's crown.
    ///
    /// <para>Clusters are <b>small and many</b> rather than large and few. A hand-built crown is lace — 1.7% of
    /// its leaves are enclosed on all six faces and each carries about six occupied neighbours — and a puff a
    /// couple of blocks across is lace by construction, where a wide one is a solid whatever is done to its
    /// surface. It also leaves the seam something to separate: clusters that overlap everywhere have no seams
    /// left to show.</para></summary>
    public static List<LeafCluster> Clusters(IReadOnlyList<TreeTip> tips, double leafSize, double treeSize, uint seed)
    {
        var clusters = new List<LeafCluster>(tips.Count);
        for (var i = 0; i < tips.Count; i++)
        {
            var tip = tips[i];
            var radius = (2.0 + leafSize * 2.5) * (0.80 + 0.25 * treeSize)
                * (0.55 + 0.45 * tip.Spread) + PatternNoise.Unit(i, 2, seed) * 0.8;
            var height = radius * (0.62 + PatternNoise.Unit(i, 5, seed) * 0.14);   // an oblate disc, not a ball
            var outward = tip.Direction.Normalized;
            var center = tip.Position + outward * (radius * TipCarry);
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

    /// <summary>The foliage that is actually held: the leaves reaching wood through a chain of leaves, and no
    /// others. A cluster's rim and the seams between clusters both shed the odd cell that touches nothing, and
    /// a single perforated cell on a rim is a leaf block floating in the air once the tree is placed. Filtering
    /// on reachability rather than tuning the fill until specks are rare is what makes the guarantee absolute:
    /// an author's 75 trees carry 11 stranded leaves between them, and a generator can carry none.</summary>
    public static HashSet<(int X, int Y, int Z)> Rooted(
        IEnumerable<(int X, int Y, int Z)> leaves, IReadOnlySet<(int X, int Y, int Z)> wood)
    {
        var foliage = leaves as IReadOnlySet<(int X, int Y, int Z)> ?? new HashSet<(int X, int Y, int Z)>(leaves);
        var held = new HashSet<(int X, int Y, int Z)>();
        var frontier = new Queue<(int X, int Y, int Z)>();
        foreach (var leaf in foliage)
            if (Around(leaf).Any(wood.Contains) && held.Add(leaf)) frontier.Enqueue(leaf);
        while (frontier.Count > 0)
            foreach (var next in Around(frontier.Dequeue()))
                if (foliage.Contains(next) && held.Add(next)) frontier.Enqueue(next);
        return held;
    }

    private static IEnumerable<(int X, int Y, int Z)> Around((int X, int Y, int Z) cell)
    {
        for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dy != 0 || dz != 0) yield return (cell.X + dx, cell.Y + dy, cell.Z + dz);
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
