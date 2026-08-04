namespace PgmStudio.Geom.Algorithms;

/// <summary>One limb of a tree: a smoothed centerline that thins from <paramref name="StartRadius"/> to
/// <paramref name="EndRadius"/> along its length. <paramref name="Level"/> is 0 for the trunk and rises with
/// each generation of branching, which is what a caller reads to know whether a limb is structural wood or a
/// twig.</summary>
public readonly record struct TreeLimb(IReadOnlyList<Vec3> Path, double StartRadius, double EndRadius, int Level);

/// <summary>A limb's outer end, where a leaf cluster sits: the point and the direction the limb was travelling
/// when it got there, so the cluster can be pushed out along the branch rather than dropped on it.</summary>
public readonly record struct TreeTip(Vec3 Position, Vec3 Direction);

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
public sealed record TreeShape(
    double Height = 20,
    int Stems = 1,
    int Levels = 2,
    double BranchAngle = 0.75,
    double Gnarl = 0.30,
    double Flow = 0.35,
    double Leader = 0.5)
{
    /// <summary>Where along a parent the first child leaves it — children never sprout from the base.</summary>
    public double ChildStart { get; init; } = 0.30;
    /// <summary>How much of its start radius a limb keeps at its end.</summary>
    public double Taper { get; init; } = 0.62;
    /// <summary>How much of its parent's radius a child starts at.</summary>
    public double ChildScale { get; init; } = 0.6;
    /// <summary>How much of its parent's length a child gets.</summary>
    public double LengthFactor { get; init; } = 0.62;

    /// <summary>The tree's size as a fraction of the largest one grown, which is what the thinner stem and
    /// sparser branching of a small tree are scaled by.</summary>
    public double Size => Math.Clamp((Height - 4) / 40.0, 0.05, 1);

    /// <summary>The trunk's radius at the ground.</summary>
    public double TrunkRadius => 0.7 + Size * 1.5;
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
            var lateralCount = Math.Max(1, (int)Math.Round(1 + shape.Leader * 2 + shape.Size * 2));
            var highest = 0.66 + 0.24 * shape.Leader;
            for (var k = 0; k < lateralCount; k++)
            {
                var along = shape.ChildStart + (highest - shape.ChildStart) * ((k + 0.5) / lateralCount);
                var index = At(control, along);
                var parent = Heading(control, index);
                // Laterals spiral around the axis rather than alternating in one plane, which is what stops a
                // tree from reading as flat when it is finally seen from another angle.
                var spread = k * 2.399963 + PatternNoise.Unit(k, (int)key, seed) * 0.8;
                var angle = shape.BranchAngle * (0.7 + 0.7 * PatternNoise.Unit(k * 3, (int)key, seed + 9));
                var length2 = length * shape.LengthFactor * (0.72 + 0.4 * PatternNoise.Unit(k, (int)key, seed + 4)) * (1 - 0.35 * along);
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
            tips.Add(new TreeTip(top, topHeading));
        }

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
                pitch += (Math.PI / 2 - pitch) * 0.10 - 0.02 * level;         // upright pull, drooping higher up
                point += Vec3.FromYawPitch(yaw, pitch) * (length / steps);
                control.Add(point);
            }
            var endRadius = Math.Max(0.55, startRadius * shape.Taper);
            limbs.Add(Smoothed(control, startRadius, endRadius, level));

            // A branch shorter than a step is terminal, which is why a small tree carries fewer of them rather
            // than the same count scaled down.
            if (level >= shape.Levels || length < 3.5)
            {
                tips.Add(new TreeTip(control[^1], Heading(control, control.Count - 1)));
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

    /// <summary>Turn a direction by <paramref name="angle"/>, in the plane picked by <paramref name="spread"/>
    /// around it — how a child leaves its parent at an angle and at its own place around the parent's girth.</summary>
    private static Vec3 Steer(Vec3 direction, double spread, double angle)
    {
        var (yaw, pitch) = YawPitch(direction);
        // Decompose the turn into the parent's own frame: the spread says which way round it goes, the angle
        // how far. Doing it in yaw/pitch rather than as a rotation matrix keeps the upright pull one term.
        return Vec3.FromYawPitch(yaw + Math.Cos(spread) * angle, pitch - Math.Abs(Math.Sin(spread)) * angle * 0.6);
    }

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
