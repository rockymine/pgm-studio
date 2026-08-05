using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>
/// The vanilla tree. What has to hold is that the three profiles are three <em>silhouettes</em> and not three
/// labels on one shape — which is exactly what parameterising the grower per species produced, and the reason
/// this builder exists at all.
/// </summary>
public sealed class TreeTemplateTests
{
    [Test]
    public async Task A_conifer_narrows_as_it_rises_and_a_blob_does_not()
    {
        var cone = Build(CanopyProfile.Cone);
        var blob = Build(CanopyProfile.Blob);

        // Measured across the crown's own courses: a cone is at its widest where it meets the trunk and ends in
        // a spire, while a blob rounds in at the bottom as well as the top.
        await Assert.That(WidthAt(cone, Lowest(cone))).IsEqualTo(Widest(cone));
        await Assert.That(WidthAt(cone, Highest(cone))).IsLessThan(WidthAt(cone, Lowest(cone)));
        await Assert.That(WidthAt(blob, Lowest(blob))).IsLessThan(Widest(blob));
        await Assert.That(WidthAt(blob, Highest(blob))).IsLessThan(Widest(blob));
    }

    [Test]
    public async Task A_conifer_is_notched_rather_than_smoothly_tapered()
    {
        // The read that says spruce is the step between layers. A radius that shrinks a little every course is
        // a party hat, so the profile steps in pairs and the silhouette has to bulge back out at least twice.
        var cone = Build(CanopyProfile.Cone);
        var widths = Courses(cone).Select(y => WidthAt(cone, y)).ToList();

        var flares = 0;
        for (var i = 1; i < widths.Count; i++) if (widths[i] > widths[i - 1]) flares++;
        await Assert.That(flares).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task An_umbrella_is_wider_than_it_is_deep()
    {
        // An acacia is a flat disc on a stalk. Three courses is the whole canopy, and it reaches further out
        // than any of the rounded species.
        var umbrella = Build(CanopyProfile.Umbrella, radius: 4);

        await Assert.That(Courses(umbrella).Count).IsLessThanOrEqualTo(3);
        await Assert.That(Widest(umbrella)).IsGreaterThan(6);
    }

    [Test]
    public async Task A_leaning_trunk_carries_its_crown_out_over_the_lean()
    {
        // Half the read of an acacia is that the canopy is not over the roots. A crown that stayed centred on
        // the foot would make the lean look like a mistake.
        var straight = TreeTemplate.Build(new TemplateShape(6, 4, CanopyProfile.Umbrella), 5);
        var leaning = TreeTemplate.Build(new TemplateShape(6, 4, CanopyProfile.Umbrella, Lean: 4), 5);

        await Assert.That(Middle(leaning.Leaves) - Middle(straight.Leaves)).IsGreaterThan(2);
        static double Middle(IReadOnlyList<(int X, int Y, int Z)> cells) => cells.Average(cell => (double)cell.X);
    }

    [Test]
    public async Task A_wide_trunk_is_four_columns_and_a_plain_one_is_a_single_column()
    {
        var slim = TreeTemplate.Build(new TemplateShape(6, 3), 5);
        var wide = TreeTemplate.Build(new TemplateShape(6, 3, WideTrunk: true), 5);

        await Assert.That(Columns(slim.Wood)).IsEqualTo(1);
        await Assert.That(Columns(wide.Wood)).IsEqualTo(4);
        static int Columns(IReadOnlyList<(int X, int Y, int Z)> wood)
            => wood.Select(cell => (cell.X, cell.Z)).Distinct().Count();
    }

    [Test]
    public async Task Leaves_never_take_a_cell_the_trunk_holds_and_never_reach_the_ground()
    {
        var tree = TreeTemplate.Build(new TemplateShape(6, 3.5), 5);
        var wood = tree.Wood.ToHashSet();

        await Assert.That(tree.Leaves.Any(cell => wood.Contains(cell))).IsFalse();
        await Assert.That(tree.Leaves.Min(cell => cell.Y)).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task A_seed_always_builds_the_same_tree()
    {
        // The whole dressing stage re-exports identically because nothing in it rolls dice.
        var once = TreeTemplate.Build(new TemplateShape(6, 3.5), 9);
        var again = TreeTemplate.Build(new TemplateShape(6, 3.5), 9);
        var other = TreeTemplate.Build(new TemplateShape(6, 3.5), 10);

        await Assert.That(once.Leaves).IsEquivalentTo(again.Leaves);
        await Assert.That(once.Leaves.Count).IsNotEqualTo(other.Leaves.Count);
    }

    private static TemplateTree Build(CanopyProfile profile, double radius = 3.4)
        => TreeTemplate.Build(new TemplateShape(6, radius, profile), 5);

    private static List<int> Courses(TemplateTree tree)
        => [.. tree.Leaves.Select(cell => cell.Y).Distinct().Order()];

    private static int WidthAt(TemplateTree tree, int y)
    {
        var row = tree.Leaves.Where(cell => cell.Y == y).ToList();
        return row.Count == 0 ? 0 : row.Max(cell => cell.X) - row.Min(cell => cell.X) + 1;
    }

    /// <summary>The widest a crown gets, in blocks — several courses may tie, so this is the width itself
    /// rather than the course it happened to be measured on.</summary>
    private static int Widest(TemplateTree tree) => Courses(tree).Max(y => WidthAt(tree, y));

    private static int Lowest(TemplateTree tree) => Courses(tree)[0];
    private static int Highest(TemplateTree tree) => Courses(tree)[^1];
}
