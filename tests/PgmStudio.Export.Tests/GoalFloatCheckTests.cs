using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Export;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The float check over a built world (<c>OB37</c>, <c>OB38</c>): every column under a monument or core has its float
/// of air before whatever stands below and a block at y 0, and a goal switching its <c>floatCheck</c> off is not
/// asked.
/// </summary>
public sealed class GoalFloatCheckTests
{
    // Ground of bedrock from y 0 to 4 under (0..2, 0..2), and a 3×3×3 monument from y 10: five blocks of air under it.
    private static VoxelWorld Floating()
    {
        var world = new VoxelWorld();
        for (var x = 0; x <= 2; x++)
        for (var z = 0; z <= 2; z++)
        {
            for (var y = 0; y <= 4; y++) world.SetBlock(x, y, z, Blocks.Bedrock);
            for (var y = 10; y <= 12; y++) world.SetBlock(x, y, z, Blocks.Obsidian);
        }
        return world;
    }

    private static MapIntent Monument(int floats, string[]? off = null) => new()
    {
        Destroyables =
        [
            new DestroyableIntent
            {
                Id = "destroyable-red", Owner = "red", Float = floats, StampsOff = off,
                Box = new BlockBox(0, 10, 0, 2, 12, 2),
            },
        ],
    };

    [Test]
    public async Task A_monument_with_its_float_of_air_under_every_column_passes()
    {
        await Assert.That(MapExportComposer.CheckGoalsFloat(Floating(), Monument(5)).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Something_standing_under_a_monument_cuts_its_float_short()
    {
        var world = Floating();
        world.SetBlock(1, 8, 1, Blocks.Stone);

        var findings = MapExportComposer.CheckGoalsFloat(world, Monument(3));

        await Assert.That(findings.Single().Rule).IsEqualTo(ObjectiveRules.FloatShort);
        await Assert.That(findings.Single().Measured!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_column_with_nothing_at_y_zero_is_void_under_the_goal()
    {
        var world = Floating();
        world.SetBlock(2, 0, 2, Blocks.Air);

        var findings = MapExportComposer.CheckGoalsFloat(world, Monument(3));

        await Assert.That(findings.Single().Rule).IsEqualTo(ObjectiveRules.VoidUnderGoal);
    }

    [Test]
    public async Task A_goal_with_its_float_check_off_is_not_asked()
    {
        var world = Floating();
        world.SetBlock(1, 8, 1, Blocks.Stone);
        world.SetBlock(2, 0, 2, Blocks.Air);

        await Assert.That(MapExportComposer.CheckGoalsFloat(world, Monument(3, [StampSwitches.FloatCheck])).Count).IsEqualTo(0);
    }
}
