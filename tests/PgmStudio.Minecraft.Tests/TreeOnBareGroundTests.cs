using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// DR-ROOT: a tree standing on ground nothing grows out of. The block read is the one under the trunk's
/// standing level, asked before the trunk is written, and the answer is <c>DressingPalette.RootsInto</c> —
/// grass and the three dirts, where the flora pass's own <c>SoilShare</c> also takes sand and gravel.
/// </summary>
public sealed class TreeOnBareGroundTests
{
    /// <summary>A flat board eight courses deep, surfaced in one block.</summary>
    private static (VoxelWorld World, Dictionary<(int X, int Z), int> Top) Ground(int surfaceId, int data = 0)
    {
        var world = new VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        for (var z = 0; z < 40; z++)
        for (var x = 0; x < 40; x++)
        {
            for (var y = 0; y < 7; y++) world.SetBlock(x, y, z, Blocks.Stone);
            world.SetBlock(x, 7, z, surfaceId, data);
            top[(x, z)] = 8;
        }
        return (world, top);
    }

    private static DressingContext Context(
        Dictionary<(int X, int Z), int> top, IReadOnlyList<PlacedProp> props)
        => new(top, props, (_, _) => null, DressingSymmetry.None);

    private static TreeProp Oak(string id, int x, int z) =>
        new() { Id = id, X = x, Z = z, Seed = 11 };

    private static Finding? Root(DressingPlacement placed) =>
        placed.Declined?.FirstOrDefault(finding => finding.Rule == DressingRules.TreeOnBareGround);

    [Test]
    public async Task A_tree_on_stone_is_named_with_the_block_it_stands_on()
    {
        var (world, top) = Ground(Blocks.Stone);
        var placed = Decorator.Decorate(world, Context(top, [Oak("rowan", 20, 20)]));

        var finding = Root(placed);
        await Assert.That(finding).IsNotNull();
        await Assert.That(finding!.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(finding.Message).Contains("tree 'rowan' stands at (20, 20)");
        await Assert.That(finding.Message).Contains("Stone");
        await Assert.That(finding.SubjectIds).IsEquivalentTo(new[] { "rowan" });
    }

    /// <summary>A complaint, not a decline: the wood builds and the author is told where it is.</summary>
    [Test]
    public async Task The_tree_is_still_in_the_world()
    {
        var (world, top) = Ground(Blocks.Stone);
        var placed = Decorator.Decorate(world, Context(top, [Oak("rowan", 20, 20)]));

        await Assert.That(Root(placed)).IsNotNull();
        await Assert.That(placed.Trees).IsEqualTo(1);
    }

    [Test]
    public async Task Grass_and_all_three_dirts_hold_a_tree()
    {
        foreach (var (id, data) in new[]
                 { (Blocks.Grass, 0), (Blocks.Dirt, 0), (Blocks.Dirt, 1), (Blocks.Dirt, 2) })
        {
            var (world, top) = Ground(id, data);
            var placed = Decorator.Decorate(world, Context(top, [Oak("rowan", 20, 20)]));

            await Assert.That(Root(placed)).IsNull();
            await Assert.That(placed.Trees).IsEqualTo(1);
        }
    }

    /// <summary>The ground a board is most often painted with, and the reason the rule exists: every one of
    /// these grows a tuft or nothing, and none of them roots a trunk.</summary>
    [Test]
    public async Task Gravel_clay_stone_and_the_diorites_do_not()
    {
        foreach (var (id, data) in new[]
                 { (Blocks.Gravel, 0), (172, 0), (Blocks.Stone, 0), (Blocks.Stone, 3), (Blocks.Sand, 0) })
        {
            var (world, top) = Ground(id, data);
            await Assert.That(Root(Decorator.Decorate(world, Context(top, [Oak("rowan", 20, 20)])))).IsNotNull();
        }
    }

    /// <summary>A tree the pass turned away is standing nowhere, so it has nothing to be rooted in and the
    /// rule says nothing about it: one prop, one finding, and that finding is the decline.</summary>
    [Test]
    public async Task A_tree_that_did_not_land_is_not_asked_what_it_stands_on()
    {
        var (world, top) = Ground(Blocks.Stone);
        // The stroke claims the ground first, and a tree placing later meets that claim.
        var placed = Decorator.Decorate(world, Context(top,
        [
            new StrokeProp
            {
                Id = "road", Points = [[10, 20], [30, 20]], Radius = 2, Seed = 5, ClaimsGround = true,
                Pave = new SolidMaterial(Blocks.Gravel),
            },
            Oak("rowan", 20, 20),
        ]));

        await Assert.That(placed.Trees).IsEqualTo(0);
        await Assert.That(Root(placed)).IsNull();
        await Assert.That(placed.Declines.Count(finding => finding.SubjectIds.Contains("rowan"))).IsEqualTo(1);
    }

    /// <summary>Sand and gravel are the two the flora pass and this rule disagree about, and the
    /// disagreement is deliberate — a tuft of grass in a shingle is ordinary and a trunk out of it is not.
    /// </summary>
    [Test]
    public async Task What_grows_on_a_surface_and_what_a_tree_roots_in_are_two_questions()
    {
        await Assert.That(DressingPalette.SoilShare(Blocks.Sand, 0)).IsGreaterThan(0);
        await Assert.That(DressingPalette.SoilShare(Blocks.Gravel, 0)).IsGreaterThan(0);

        await Assert.That(DressingPalette.RootsInto(Blocks.Sand)).IsFalse();
        await Assert.That(DressingPalette.RootsInto(Blocks.Gravel)).IsFalse();
        await Assert.That(DressingPalette.RootsInto(Blocks.Grass)).IsTrue();
        await Assert.That(DressingPalette.RootsInto(Blocks.Dirt)).IsTrue();
    }
}
