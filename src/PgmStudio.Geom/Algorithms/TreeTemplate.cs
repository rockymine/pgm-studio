namespace PgmStudio.Geom.Algorithms;

/// <summary>The shape a vanilla canopy takes. Each is a radius per course rather than a code path — see
/// <see cref="CanopyProfiles"/> — which is what lets a species be a data row.</summary>
public enum CanopyProfile
{
    /// <summary>A rounded mass wrapping the top of the trunk: oak, birch, jungle, dark oak.</summary>
    Blob,
    /// <summary>Courses stepping in as they rise, in pairs, so the silhouette is notched rather than smooth —
    /// the read that says conifer.</summary>
    Cone,
    /// <summary>Three courses of a wide flat disc sitting on a leaning trunk: an acacia.</summary>
    Umbrella,
}

/// <summary> A vanilla tree's proportions. This is the <em>other</em> tree, and it is deliberately not the grown
/// one: a trunk of a known height under a canopy of a known shape, which is the tree almost every map wants and
/// the only tree a player reads as "an oak". </summary>
/// <param name="Profile">The canopy's silhouette — which radius table the crown is cut from.</param>
/// <param name="TrunkHeight">Courses of bare trunk under the canopy.</param>
/// <param name="CanopyRadius">How far the canopy reaches from the trunk at its widest.</param>
/// <param name="Lean">How far the trunk's top is displaced from its foot, in blocks. Only an acacia
/// leans.</param>
/// <param name="WideTrunk">A 2×2 trunk rather than a single column — a dark oak.</param>
public sealed record TemplateShape(
    double TrunkHeight = 5,
    double CanopyRadius = 3.2,
    CanopyProfile Profile = CanopyProfile.Blob,
    double Lean = 0,
    bool WideTrunk = false);

/// <summary>A template tree as pure geometry: the cells that are wood and the cells that are leaf, in the
/// tree's own frame with its foot at the origin. Which blocks those become is the caller's half, exactly as
/// it is for <see cref="GrownTree"/>.</summary>
public sealed record TemplateTree(
    IReadOnlyList<(int X, int Y, int Z)> Wood,
    IReadOnlyList<(int X, int Y, int Z)> Leaves);

/// <summary>
/// Builds the vanilla tree — trunk column, canopy of a named profile.
///
/// <para>It exists beside <see cref="TreeSkeleton"/> rather than as a setting on it because the two make
/// different things. The grower produces a wandering spline skeleton with foliage gathered at its tips: that
/// reads as an oak or a birch, and it cannot produce a notched conifer or a flat acacia umbrella at all —
/// there is no knob for a canopy that steps in as it rises. Parameterising the grower per species would
/// therefore promise six silhouettes and build one, which is the failure a drawn picker exists to prevent.</para>
///
/// <para>Every irregularity is hash-keyed off the seed, never RNG, so a seed always builds the same tree —
/// the discipline the whole dressing stage holds so a map re-exports identically.</para>
/// </summary>
public static class TreeTemplate
{
    /// <summary>How ragged a canopy's outer surface is: the fraction of a block the edge is pushed in or out
    /// by. A canopy cut at exactly its radius is a geometric solid and reads as one.</summary>
    private const double EdgeBite = 0.9;

    /// <summary>Build the tree with its foot at the origin and its trunk running up +Y.</summary>
    public static TemplateTree Build(TemplateShape shape, uint seed)
    {
        var trunkTop = Math.Max(1, (int)Math.Round(shape.TrunkHeight));
        var radius = Math.Max(1, shape.CanopyRadius);
        var wood = Trunk(shape, trunkTop);
        var (fromCourse, radii) = CanopyProfiles.Of(shape.Profile, radius);

        var leaves = new List<(int X, int Y, int Z)>();
        var woodAt = new HashSet<(int X, int Y, int Z)>(wood);
        for (var course = 0; course < radii.Count; course++)
        {
            var y = trunkTop + fromCourse + course;
            if (y < 1) continue;                              // a canopy never reaches the ground
            var courseRadius = radii[course];
            if (courseRadius <= 0) continue;

            // The canopy follows the trunk, so a leaning tree carries its crown out over the lean.
            var axis = AxisAt(shape, trunkTop, Math.Min(y, trunkTop));
            var reach = (int)Math.Ceiling(courseRadius);
            for (var dz = -reach; dz <= reach; dz++)
            for (var dx = -reach; dx <= reach; dx++)
            {
                var (x, z) = ((int)Math.Round(axis.X) + dx, (int)Math.Round(axis.Z) + dz);
                if (woodAt.Contains((x, y, z))) continue;
                var distance = Math.Sqrt(dx * dx + dz * dz);
                if (distance > courseRadius + (PatternNoise.Unit(x, y, z, seed) - 0.5) * EdgeBite) continue;
                leaves.Add((x, y, z));
            }
        }
        return new TemplateTree(wood, leaves);
    }

    /// <summary>The trunk: one column, or four for a wide trunk, leaning as it rises.
    ///
    /// <para>A leaning trunk fills the step it takes sideways as well as the course it rises, or the lean comes
    /// out as a diagonal of blocks touching only at their corners — which reads as a broken trunk rather than a
    /// leaning one, and is not a thing the game would ever grow.</para></summary>
    private static List<(int X, int Y, int Z)> Trunk(TemplateShape shape, int trunkTop)
    {
        var cells = new List<(int X, int Y, int Z)>();
        var previous = (int)Math.Round(AxisAt(shape, trunkTop, 0).X);
        for (var y = 0; y <= trunkTop; y++)
        {
            var x = (int)Math.Round(AxisAt(shape, trunkTop, y).X);
            for (var step = Math.Min(previous, x); step <= Math.Max(previous, x); step++) Column(cells, step, y);
            previous = x;
        }
        return cells;

        void Column(List<(int X, int Y, int Z)> into, int x, int y)
        {
            into.Add((x, y, 0));
            if (!shape.WideTrunk) return;
            into.Add((x + 1, y, 0));
            into.Add((x, y, 1));
            into.Add((x + 1, y, 1));
        }
    }

    /// <summary>Where the trunk's centre sits at a course — the lean, applied in proportion to the climb.</summary>
    private static Vec3 AxisAt(TemplateShape shape, int trunkTop, int y)
        => new(shape.Lean * y / Math.Max(1, trunkTop), y, 0);
}

/// <summary>The radii behind <see cref="CanopyProfile"/> — profile-as-data, the same seam
/// <c>BoulderShapes</c> uses for a rock's form. A profile is a shape, so it is a table rather than a
/// branch.</summary>
public static class CanopyProfiles
{
    /// <summary>The canopy's radius at each course, and which course above the trunk's top it starts at
    /// (negative — every canopy engulfs the last of the trunk rather than perching on it).</summary>
    public static (int FromCourse, IReadOnlyList<double> Radii) Of(CanopyProfile profile, double radius) => profile switch
    {
        CanopyProfile.Cone => (-(int)Math.Round(radius * 0.35), Cone(radius)),
        CanopyProfile.Umbrella => (0, [radius * 0.72, radius, radius * 0.72]),
        _ => (-(int)Math.Round(radius * 0.5), Ellipsoid(radius)),
    };

    /// <summary>How far above the trunk's top a canopy of this profile reaches. A species subtracts it from its
    /// height to know how much of that is trunk, so a tree comes out as tall as the picker said it was — and
    /// so the two numbers cannot drift apart the way a hand-tuned pair of factors would.</summary>
    public static double Rise(CanopyProfile profile, double radius)
    {
        var (fromCourse, radii) = Of(profile, radius);
        return fromCourse + radii.Count - 1;
    }

    /// <summary>A crown's width up its own height, bottom course first, as fractions of its radius. Written
    /// out rather than evaluated from a formula because the two obvious formulas both draw something else: a
    /// sphere's slices hold full width for one course and taper away from it, which is a lens, and anything
    /// flatter holds full width everywhere, which is a cylinder — a rectangle from the side. A crown is neither.
    /// It is widest a third of the way up, carries that width for a while, and closes in faster above than
    /// below, which is what these five numbers say.</summary>
    private static readonly double[] CrownWidths = [0.72, 1.0, 1.0, 0.78, 0.42];

    /// <summary>A rounded canopy: <see cref="CrownWidths"/> stretched over as many courses as the radius
    /// warrants.</summary>
    private static IReadOnlyList<double> Ellipsoid(double radius)
    {
        var courses = Math.Max(2, (int)Math.Round(radius * 1.6));
        var radii = new double[courses + 1];
        for (var i = 0; i <= courses; i++)
        {
            var along = (double)i / courses * (CrownWidths.Length - 1);
            var step = Math.Min((int)along, CrownWidths.Length - 2);
            radii[i] = radius * (CrownWidths[step] + (CrownWidths[step + 1] - CrownWidths[step]) * (along - step));
        }
        return radii;
    }

    /// <summary>A conifer: courses stepping in as they rise, but <b>in pairs</b>, the lower course of each pair
    /// one wider than the upper. The notch that leaves is the whole read — a smooth linear taper is a party
    /// hat, and nobody has ever mistaken one for a spruce.</summary>
    private static IReadOnlyList<double> Cone(double radius)
    {
        var courses = (int)Math.Round(radius * 2.4);
        var radii = new double[courses + 1];
        for (var i = 0; i <= courses; i++)
        {
            var climbed = (double)i / (courses + 1);
            radii[i] = radius * (1 - climbed) * (i % 2 == 0 ? 1 : 0.62);
        }
        radii[^1] = Math.Min(radii[^1], 0.9);                 // the spire is a single block
        return radii;
    }
}
