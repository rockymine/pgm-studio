using PgmStudio.Domain;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The dressing pass's rule ids as the catalogue serves them. The standoff's numbers live once, on the prop
/// kinds (<see cref="PlacedProp.RouteStandoff"/>) — so the sentence <c>GET /api/rules</c> answers for
/// DR-ROAD, which is this assembly's docstring, must carry those values and not a stale transcription.
/// </summary>
public sealed class DressingRulesTests
{
    [Test]
    public async Task DR_ROADs_catalogue_sentence_states_the_standoffs_the_props_actually_keep()
    {
        var road = RuleCatalog.Read([typeof(DressingRules).Assembly])
            .Single(rule => rule.Rule == DressingRules.RoadStandoff);

        await Assert.That(road.Means).Contains($"a tree {new TreeProp().RouteStandoff} blocks");
        await Assert.That(road.Means).Contains($"a boulder {new BoulderProp().RouteStandoff}");
        await Assert.That(road.Fix).IsNotNull();
    }

    [Test]
    public async Task DR_PASSs_catalogue_sentence_states_the_width_the_check_keeps()
    {
        var passAround = RuleCatalog.Read([typeof(DressingRules).Assembly])
            .Single(rule => rule.Rule == DressingRules.PassAround);

        await Assert.That(passAround.Means).Contains($"{DressingRules.PassAroundWidth} blocks of passable ground");
        await Assert.That(passAround.Fix).IsNotNull();
    }

    [Test]
    public async Task A_standoff_drop_cites_the_rule_id_the_catalogue_answers()
    {
        // The census is prose, so the id in the reason is what connects a drop to GET /api/rules. The same
        // geometry as the decorator standoff test: a trunk two blocks off a z=20, radius-2 road.
        var world = new Anvil.VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        for (var z = 0; z < 40; z++)
        for (var x = 0; x < 40; x++)
        {
            for (var y = 0; y < 7; y++) world.SetBlock(x, y, z, Palette.Blocks.Stone);
            world.SetBlock(x, 7, z, Palette.Blocks.Grass);
            top[(x, z)] = 8;
        }

        var tally = Decorator.Decorate(world, new DressingContext(top,
        [
            new PathProp
            {
                Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Seed = 5,
                Pave = new Painting.SolidMaterial(Palette.Blocks.Gravel),
            },
            new TreeProp { Id = "t", X = 20, Z = 23, Species = "oak", Height = 14, Seed = 5 },
        ]));

        await Assert.That(tally.Dropped!.Single().Reason).Contains($"({DressingRules.RoadStandoff})");
    }
}
