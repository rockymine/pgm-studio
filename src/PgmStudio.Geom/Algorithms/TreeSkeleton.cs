namespace PgmStudio.Geom.Algorithms;

/// <summary>One limb of a tree: a smoothed centerline that thins from <paramref name="StartRadius"/> to
/// <paramref name="EndRadius"/> along its length. <paramref name="Level"/> is 0 for the trunk and rises with
/// each generation of branching, which is what a caller reads to know whether a limb is structural wood or a
/// twig.</summary>
public readonly record struct TreeLimb(IReadOnlyList<Vec3> Path, double StartRadius, double EndRadius, int Level);

/// <summary>A limb's outer end, where a leaf cluster sits: the point and the direction the limb was travelling
/// when it got there, so the cluster can be pushed out along the branch rather than dropped on it.
/// <paramref name="Spread"/> is how much foliage the branch can carry, as a share of a full-length limb's —
/// a short branch holds a small clump. It is what makes a whorled tree a cone rather than a column, since its
/// upper rings are the short ones, and it stops a twig on any tree from wearing the same crown as a bough.</summary>
public readonly record struct TreeTip(Vec3 Position, Vec3 Direction, double Spread = 1);

/// <summary>A grown tree as pure geometry — limb centerlines and the tips their foliage hangs on, with no
/// block, palette or world in sight. Sweeping the limbs into wood and the tips into leaves is the caller's
/// half.</summary>
public sealed record GrownTree(IReadOnlyList<TreeLimb> Limbs, IReadOnlyList<TreeTip> Tips);

/// <summary>
/// The knobs a tree is grown from. Two of them carry most of the read and are worth naming plainly:
/// <paramref name="Leader"/> is how far the central axis climbs before it gives out — low, and the trunk
/// dissolves into a spreading fork the way an oak does; high, and it carries on up through the crown as one
/// dominant spine with branches staggered along it, the way a birch or a conifer does. <paramref name="Flow"/>
/// is how much that axis wanders as it rises; zero is a flagpole.
/// </summary>
/// <param name="Height">The tree's height in blocks. Not a uniform scale — a smaller tree also gets a thinner
/// stem and fewer branches, because a scaled-down big tree reads as a model of a tree, not a sapling.</param>
/// <param name="Stems">Base setups a real tree has: one stem, a double, a triple.</param>
/// <param name="Levels">How many generations of branching after the laterals off the trunk.</param>
/// <param name="BranchAngle">Radians a child leaves its parent by.</param>
/// <param name="Gnarl">Per-step wander on a lateral — the sharper jitter that keeps a limb from being a ruler.</param>
/// <param name="Flow">Slow sway on the central axis.</param>
/// <param name="Leader">0–1; how far the central axis climbs into the crown.</param>
/// <param name="Whorled">Whether the laterals are gathered into whorls — a ring of them at one height, the next
/// ring a few courses up, each ring shorter than the one below. It is the conifer, and it is a different tree
/// rather than a setting of the same one: measured against a hand-built corpus, four families of fourteen build
/// their crowns in 3–7 tiers with the widest tenth in the bottom third, and the other ten never exceed two tiers
/// and carry their bulk higher, with no overlap between the two groups on either measure.</param>
public sealed record TreeShape(
    double Height = 20,
    int Stems = 1,
    int Levels = 2,
    double BranchAngle = 1.1,
    double Gnarl = 0.30,
    double Flow = 0.35,
    double Leader = 0.5,
    bool Whorled = false)
{
    /// <summary>Courses between one whorl and the next. Every conifer family in the corpus spaces its tiers
    /// 4.6 to 5.8 courses apart, whatever its height — 7 tiers in 32 courses, 4 in 20, 4 in 23, 3 in 14 — so the
    /// spacing is a constant and the tier count is what a taller tree gains.</summary>
    public const double WhorlSpacing = 5.2;

    /// <summary>Where along a parent the first child leaves it — children never sprout from the base.</summary>
    public double ChildStart { get; init; } = 0.30;
    /// <summary>How much of its start radius a limb keeps at its end.</summary>
    public double Taper { get; init; } = 0.62;
    /// <summary>How much of its parent's radius a child starts at.</summary>
    public double ChildScale { get; init; } = 0.5;
    /// <summary>How much of its parent's length a child gets.</summary>
    public double LengthFactor { get; init; } = 0.62;

    /// <summary>The tree's size as a fraction of the largest one grown, which is what the thinner stem and
    /// sparser branching of a small tree are scaled by.</summary>
    public double Size => Math.Clamp((Height - 4) / 40.0, 0.05, 1);

    /// <summary>The trunk's radius at the ground. A hand-built bole carries about three blocks across its
    /// course, so a big tree's is under two: wood swept fatter than that reads as a column, and it is what
    /// puts half again as many occupied neighbours on every block of a generated limb as on an author's.</summary>
    public double TrunkRadius => 0.65 + Size * 1.15;
}

/// <summary>
/// Grows a tree's skeleton: a continuous central axis with lateral branches staggered up it, every one of them
/// a stepped path smoothed into a flowing curve.
///
/// <para>Both halves of that matter, and each alone fails. A purely recursive brancher reads as a fractal —
/// self-similar forks, nothing that looks like it grew; one clean spline reads as a mast. What reads as a tree
/// is recursion for the <em>structure</em> and a smoothed wander for every <em>limb</em>: taper, curve, and
/// foliage gathered at the tips. The trunk is a continuous spine rather than a stub that forks and stops,
/// because a trunk that ends where its first branches begin is the single most obvious tell of a generated
/// tree.</para>
///
/// <para>Everything is hash-keyed off the seed and the limb's index, never RNG, so a seed always grows the
/// same tree — the same discipline the terrain patterns hold, and what lets a map re-export identically.</para>
/// </summary>
public static class TreeSkeleton
{
    /// <summary>Grow a tree at the origin, its base at y = 0 and its axis running up +Y.</summary>
    public static GrownTree Grow(TreeShape shape, uint seed)
    {
        var limbs = new List<TreeLimb>();
        var tips = new List<TreeTip>();
        var grower = new Growth(shape, seed, limbs, tips);

        var stems = Math.Max(1, shape.Stems);
        var axisLength = shape.Height * (0.5 + 0.34 * shape.Leader);
        for (var s = 0; s < stems; s++)
        {
            // Multiple stems lean out from a shared base, each a slightly shorter axis of its own.
            var offset = stems == 1 ? 0 : (double)s / (stems - 1) - 0.5;
            var yaw = stems == 1 ? 0 : offset * 4;
            var basePoint = new Vec3(Math.Cos(yaw) * offset * 2.5, 0, Math.Sin(yaw) * offset * 2.5);
            grower.GrowAxis(basePoint, yaw, Math.PI / 2 - Math.Abs(offset) * 0.55,
                axisLength * (stems == 1 ? 1 : 0.9), shape.TrunkRadius, (uint)(1 + s * 131));
        }
        return new GrownTree(limbs, tips);
    }

    /// <summary>The mutable half of one grow, kept out of the public surface: the two recursive walks and the
    /// hash-keyed jitter they share.</summary>
    private sealed class Growth(TreeShape shape, uint seed, List<TreeLimb> limbs, List<TreeTip> tips)
    {
        /// <summary>The central axis: one long spline climbing into the crown, swaying and thinning, with
        /// laterals staggered up its length and a small fan plus a cluster at the very top.</summary>
        public void GrowAxis(Vec3 origin, double yaw, double pitch, double length, double startRadius, uint key)
        {
            var steps = Math.Max(6, (int)Math.Round(length / 2.6));
            var control = new List<Vec3> { origin };
            var point = origin;
            for (var s = 1; s <= steps; s++)
            {
                // A slow sway keyed on height, so the axis twists as it rises instead of jittering in place.
                yaw += (PatternNoise.Value((int)Math.Round(point.Y), (int)key, seed, 9) - 0.5) * shape.Flow * 2;
                pitch += (PatternNoise.Value((int)Math.Round(point.Y), (int)key + 977, seed, 9) - 0.5) * shape.Flow;
                pitch += (Math.PI / 2 - pitch) * 0.05;                       // stays generally upright
                point += Vec3.FromYawPitch(yaw, pitch) * (length / steps);
                control.Add(point);
            }
            var endRadius = Math.Max(0.5, startRadius * (0.42 - 0.14 * shape.Leader));
            limbs.Add(Smoothed(control, startRadius, endRadius, level: 0));

            // Laterals up the axis — fewer on a small tree, and reaching higher when the leader is strong.
            // A big tree carries many of them because that is what the crown is built from: one small cluster
            // per tip, so a tree with three tips can only be foliated by three clusters wide enough to be
            // solids. The author's own tree ends in about thirty limbs.
            var lateralCount = Math.Max(2, (int)Math.Round(2 + shape.Leader * 2 + shape.Size * 6));
            // A whorled tree's rings stop well below the apex, which is what leaves a spire above the top
            // ring instead of a bush around it.
            var highest = shape.Whorled ? 0.52 + 0.16 * shape.Leader : 0.66 + 0.24 * shape.Leader;

            // A conifer's laterals are gathered into whorls — a ring at one height, the next ring a fixed few
            // courses up — and each ring is shorter than the one below it, which is the whole of why the
            // silhouette is a cone and why the crown carries its bulk in its bottom third. A broadleaf's are
            // staggered up the axis one at a time instead, never whorled.
            var tiers = shape.Whorled
                ? Math.Max(3, (int)Math.Round(length * (highest - shape.ChildStart) / TreeShape.WhorlSpacing))
                : 0;
            var perTier = shape.Whorled ? Math.Max(2, (int)Math.Round(lateralCount / (double)tiers) + 1) : 0;
            if (shape.Whorled) lateralCount = tiers * perTier;

            for (var k = 0; k < lateralCount; k++)
            {
                var tier = shape.Whorled ? k / perTier : 0;
                var upTheTiers = shape.Whorled && tiers > 1 ? tier / (double)(tiers - 1) : 0;
                var along = shape.ChildStart + (highest - shape.ChildStart)
                    * (shape.Whorled ? upTheTiers : (k + 0.5) / lateralCount);
                var index = At(control, along);
                var parent = Heading(control, index);
                // Laterals spiral around the axis rather than alternating in one plane, which is what stops a
                // tree from reading as flat when it is finally seen from another angle. A whorl divides its
                // ring evenly and turns each ring against the last, so the tiers do not stack into a fin.
                var spread = shape.Whorled
                    ? (k % perTier) * (Math.PI * 2 / perTier) + tier * 0.7 + PatternNoise.Unit(k, (int)key, seed) * 0.3
                    : k * 2.399963 + PatternNoise.Unit(k, (int)key, seed) * 0.8;
                var angle = shape.BranchAngle * (0.7 + 0.7 * PatternNoise.Unit(k * 3, (int)key, seed + 9));
                var length2 = length * shape.LengthFactor * (0.72 + 0.4 * PatternNoise.Unit(k, (int)key, seed + 4)) * (1 - 0.35 * along);
                // A whorl's ring shortens as it climbs, from full at the foot to a third at the top.
                if (shape.Whorled) length2 *= 1 - 0.72 * upTheTiers;
                var radius = Math.Max(0.6, (startRadius + (endRadius - startRadius) * along) * shape.ChildScale);
                GrowLimb(control[index], Steer(parent, spread, angle), length2, radius, level: 1, key: key * 17 + (uint)k * 4 + 1);
            }

            // The top forks a little and then ends in a cluster of its own — a sapling is too short to fork.
            var top = control[^1];
            var topHeading = Heading(control, control.Count - 1);
            if (length * shape.LengthFactor * 0.5 > 2.6)
                for (var k = 0; k < 2; k++)
                    GrowLimb(top, Steer(topHeading, k * Math.PI, shape.BranchAngle * 0.5),
                        length * shape.LengthFactor * 0.5, Math.Max(0.6, endRadius * 0.85), level: 1, key: key * 91 + (uint)k);
            tips.Add(new TreeTip(top, topHeading, Carry(length * shape.LengthFactor * 0.6)));
        }

        /// <summary>How much foliage a limb of this length carries, against a full-grown branch's.</summary>
        private static double Carry(double length) => Math.Clamp(length / 6.0, 0.4, 1.25);

        /// <summary>A lateral branch: sharper per-step jitter than the axis, pulled gently upright, branching
        /// on until it runs out of levels or gets too short to be worth one.</summary>
        private void GrowLimb(Vec3 origin, Vec3 direction, double length, double startRadius, int level, uint key)
        {
            var steps = Math.Max(3, (int)Math.Round(length / 2.2));
            var control = new List<Vec3> { origin };
            var point = origin;
            var (yaw, pitch) = YawPitch(direction);
            for (var s = 1; s <= steps; s++)
            {
                yaw += (PatternNoise.Unit((int)key * 7 + s, level + 1, seed) - 0.5) * shape.Gnarl * 2;
                pitch += (PatternNoise.Unit((int)key * 7 + s, level + 401, seed) - 0.5) * shape.Gnarl;
                // A weak upright pull, weaker again further out. A strong one straightens every limb back
                // toward the axis within a few steps, which is what puts a generated branch at 41° off
                // vertical where an author's leaves the trunk at 59° and its children at 67°.
                pitch += (Math.PI / 2 - pitch) * 0.04 - 0.03 * level;
                point += Vec3.FromYawPitch(yaw, pitch) * (length / steps);
                control.Add(point);
            }
            var endRadius = Math.Max(0.55, startRadius * shape.Taper);
            limbs.Add(Smoothed(control, startRadius, endRadius, level));

            // A branch shorter than a step is terminal, which is why a small tree carries fewer of them rather
            // than the same count scaled down. A whorled tree's branches never fork at all: a ring of forking
            // limbs fills the gap above it and the tiers stop reading as tiers.
            if (level >= shape.Levels || length < 3.5 || shape.Whorled)
            {
                tips.Add(new TreeTip(control[^1], Heading(control, control.Count - 1), Carry(length)));
                return;
            }

            for (var k = 0; k < 2; k++)
            {
                var along = shape.ChildStart + (1 - shape.ChildStart) * ((k + 0.6) / 2);
                var index = At(control, along);
                var parent = Heading(control, index);
                var spread = k * Math.PI + PatternNoise.Unit((int)key + k, 7, seed) * 0.8;
                var angle = shape.BranchAngle * (0.6 + 0.8 * PatternNoise.Unit((int)key * 3 + k, 9, seed));
                var childLength = length * shape.LengthFactor * (0.78 + 0.44 * PatternNoise.Unit((int)key + k, 4, seed));
                var childRadius = Math.Max(0.6, (startRadius + (endRadius - startRadius) * along) * shape.ChildScale);
                GrowLimb(control[index], Steer(parent, spread, angle), childLength, childRadius, level + 1, key * 4 + (uint)k + 1);
            }
        }

        // A stepped control polyline becomes a limb by being smoothed — the step is where the wander is
        // decided, the curve is what the eye reads.
        private static TreeLimb Smoothed(List<Vec3> control, double startRadius, double endRadius, int level)
            => new(Spline(control), startRadius, endRadius, level);

        private static int At(List<Vec3> control, double along)
            => Math.Clamp((int)Math.Round(along * (control.Count - 1)), 1, control.Count - 1);

        private static Vec3 Heading(List<Vec3> control, int index)
            => (control[index] - control[Math.Max(0, index - 1)]).Normalized;
    }

    /// <summary>Turn a direction by <paramref name="angle"/>, about the place on the parent's girth picked by
    /// <paramref name="spread"/> — how a child leaves its parent at an angle and at its own place around it.
    ///
    /// <para>The turn is taken in the parent's own frame rather than in world yaw and pitch, so that
    /// <paramref name="angle"/> is the angle the child actually leaves by and <paramref name="spread"/> only
    /// decides which way round. Spending it on yaw instead is silent for exactly the parent that matters most:
    /// a trunk is vertical, a vertical direction has no meaningful yaw, and a whole radian of branch angle
    /// there turns a limb by nothing at all. That is what put generated laterals at 41° off vertical against
    /// the 59° an author leaves the trunk by, however far the knob was turned up.</para></summary>
    private static Vec3 Steer(Vec3 direction, double spread, double angle)
    {
        var forward = direction.Normalized;
        // Any axis not parallel to the limb, so the frame is well defined for a vertical trunk as well.
        var reference = Math.Abs(forward.Y) > 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0);
        var side = Cross(forward, reference).Normalized;
        var over = Cross(forward, side);
        var away = side * Math.Cos(spread) + over * Math.Sin(spread);
        return (forward * Math.Cos(angle) + away * Math.Sin(angle)).Normalized;
    }

    private static Vec3 Cross(Vec3 a, Vec3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static (double Yaw, double Pitch) YawPitch(Vec3 direction)
    {
        var d = direction.Normalized;
        return (Math.Atan2(d.Z, d.X), Math.Asin(Math.Clamp(d.Y, -1, 1)));
    }

    /// <summary>The centripetal Catmull-Rom of <see cref="CatmullRom"/>, per axis pair — the spline is planar
    /// there and a limb is not, so each of the three coordinates is smoothed against the same parameter.</summary>
    private static List<Vec3> Spline(IReadOnlyList<Vec3> control)
    {
        if (control.Count < 3) return [.. control];
        var xy = CatmullRom.Spline([.. control.Select(p => new[] { p.X, p.Y })], 6);
        var zy = CatmullRom.Spline([.. control.Select(p => new[] { p.Z, p.Y })], 6);
        var count = Math.Min(xy.Count, zy.Count);
        var path = new List<Vec3>(count);
        for (var i = 0; i < count; i++) path.Add(new Vec3(xy[i][0], xy[i][1], zy[i][0]));
        return path;
    }
}
