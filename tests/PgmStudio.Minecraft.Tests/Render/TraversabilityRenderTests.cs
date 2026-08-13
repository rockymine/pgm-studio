using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The traversability stage image: navigable columns (ground + two blocks headroom) split into
/// connected components.</summary>
public sealed class TraversabilityRenderTests
{
    [Test]
    public async Task Two_platforms_with_no_ground_between_them_are_separate_components()
    {
        var world = new VoxelWorld();
        for (var x = 0; x <= 1; x++)
            for (var z = 0; z <= 1; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);
        for (var x = 5; x <= 6; x++)
            for (var z = 0; z <= 1; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);
        // x = 2..4 stays void: nothing joins the two platforms.

        var result = TraversabilityRender.Render(AnvilRegion.FromWorld(world), markers: []);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ComponentCount).IsEqualTo(2);
        await Assert.That(result.NavigableCount).IsEqualTo(8);
    }

    [Test]
    public async Task A_bridge_of_ground_joins_two_platforms_into_one_component()
    {
        var world = new VoxelWorld();
        for (var x = 0; x <= 6; x++)
            world.SetBlock(x, 5, 0, Blocks.Stone);   // one continuous run

        var result = TraversabilityRender.Render(AnvilRegion.FromWorld(world), markers: []);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ComponentCount).IsEqualTo(1);
    }

    [Test]
    public async Task Ground_with_something_standing_directly_on_it_is_not_navigable()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(0, 6, 0, Blocks.Log);   // a trunk stood right on the ground blocks the headroom above it

        var result = TraversabilityRender.Render(AnvilRegion.FromWorld(world), markers: []);

        // Ground is found (the stone the trunk stands on), but the headroom check reads the trunk itself —
        // stepped past to find the ground, not stepped past again to clear the two cells above it — so the
        // column reads as blocked rather than navigable.
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.NavigableCount).IsEqualTo(0);
    }
}
