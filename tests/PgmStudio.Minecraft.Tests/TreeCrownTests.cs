using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The ground a standing tree holds is the disc its crown covers, not the columns its own blocks fill. A
/// crown is leaves with gaps in it, so a claim made cell by cell lets a second trunk seat between them and
/// the two crowns grow through each other — <c>DR-CLAIM</c> is what refuses that, once the claim is the disc.
/// </summary>
public sealed class TreeCrownTests
{
    private static (VoxelWorld World, Dictionary<(int X, int Z), int> Top) Meadow()
    {
        var world = new VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        for (var z = 0; z < 80; z++)
        for (var x = 0; x < 80; x++)
        {
            for (var y = 0; y < 7; y++) world.SetBlock(x, y, z, Blocks.Stone);
            world.SetBlock(x, 7, z, Blocks.Grass);
            top[(x, z)] = 8;
        }
        return (world, top);
    }

    private static DressingContext Context(
        Dictionary<(int X, int Z), int> top, IReadOnlyList<PlacedProp> props)
        => new(top, props, (_, _) => null, DressingSymmetry.None);

    private static TreeProp Oak(string id, int x, int z) => new() { Id = id, X = x, Z = z, Seed = 11 };

    [Test]
    public async Task A_tree_holds_the_disc_its_crown_covers()
    {
        var (world, top) = Meadow();
        var placed = Decorator.Decorate(world, Context(top, [Oak("first", 40, 40)]));

        await Assert.That(placed.Trees).IsEqualTo(1);
        var reach = (int)Math.Ceiling(Decorator.CanopyRadius(Oak("first", 40, 40)));
        await Assert.That(reach).IsGreaterThan(1);

        // Every cell of the disc is claimed, gaps in the foliage included.
        var held = placed.Placements.Single(claim => claim.Owner.Unit == "first").Cells;
        for (var dz = -reach; dz <= reach; dz++)
        for (var dx = -reach; dx <= reach; dx++)
            if (dx * dx + dz * dz <= reach * reach)
                await Assert.That(held).Contains((40 + dx, 40 + dz));
    }

    /// <summary>Two trunks a crown apart put one crown inside the other, which is the collision the claim
    /// exists to refuse: the second tree is declined and the finding names the first.</summary>
    [Test]
    public async Task A_second_trunk_inside_the_first_crown_is_declined()
    {
        var (world, top) = Meadow();
        var reach = (int)Math.Ceiling(Decorator.CanopyRadius(Oak("first", 40, 40)));

        var placed = Decorator.Decorate(world, Context(top,
            [Oak("first", 40, 40), Oak("second", 40 + reach - 1, 40)]));

        await Assert.That(placed.Trees).IsEqualTo(1);
        var decline = placed.Declines.Single(finding => finding.SubjectIds.Contains("second"));
        await Assert.That(decline.Rule).IsEqualTo(DressingRules.GroundTaken);
        await Assert.That(decline.Message).Contains("first");
    }

    /// <summary>Two crowns apart, both stand — the spacing the rule buys is the two radii, so a wood is a
    /// wood rather than one mass of leaves.</summary>
    [Test]
    public async Task Two_trees_their_two_crowns_apart_both_stand()
    {
        var (world, top) = Meadow();
        var reach = (int)Math.Ceiling(Decorator.CanopyRadius(Oak("first", 40, 40)));

        var placed = Decorator.Decorate(world, Context(top,
            [Oak("first", 20, 40), Oak("second", 20 + 2 * reach + 1, 40)]));

        await Assert.That(placed.Trees).IsEqualTo(2);
        await Assert.That(placed.Declines.Count(finding => finding.SubjectIds.Contains("second"))).IsEqualTo(0);
    }

    /// <summary>The radius is this tree's own build rather than a species nominal, so a copied body is
    /// measured. Both forms answer it, which is what lets one claim cover both.</summary>
    [Test]
    public async Task The_radius_is_the_tree_s_own_farthest_leaf()
    {
        var small = Decorator.CanopyRadius(new TreeProp { X = 0, Z = 0, Seed = 3, Style = new TreeStyle { Height = 6 } });
        var large = Decorator.CanopyRadius(new TreeProp { X = 0, Z = 0, Seed = 3, Style = new TreeStyle { Height = 18 } });

        await Assert.That(small).IsGreaterThan(0.0);
        await Assert.That(large).IsGreaterThanOrEqualTo(small);
    }
}
