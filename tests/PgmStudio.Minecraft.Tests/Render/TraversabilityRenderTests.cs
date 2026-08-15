using PgmStudio.Domain;
using PgmStudio.Minecraft.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The traversability stage image: navigable columns (ground + two blocks headroom, or a void column
/// the map's own buildable-region wiring makes bridgeable) split into connected components.</summary>
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

    // The two platforms from the tests above (x=0..1 and x=5..6, z=0..1), with the same x=2..4 void gap —
    // now read against a map document rather than bare markers, to exercise the buildable-region wiring.
    private static VoxelWorld TwoPlatforms()
    {
        var world = new VoxelWorld();
        for (var x = 0; x <= 1; x++)
            for (var z = 0; z <= 1; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);
        for (var x = 5; x <= 6; x++)
            for (var z = 0; z <= 1; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);
        return world;
    }

    // BuildGenerator's own shape: a rectangle (or a union of them) wrapped in a negative, gated by a filter
    // that chases down to "void" — the wiring PGM actually enforces at kickoff, not a name convention.
    private static MapXml MapWithBuildRegion(int minX, int minZ, int maxX, int maxZ)
    {
        var map = new MapXml();
        map.Regions["build-area-1"] = new Region { Id = "build-area-1", Type = "rectangle", MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ };
        map.Regions["not-build-area"] = new Region { Id = "not-build-area", Type = "negative", Children = ["build-area-1"] };
        map.Filters["is-void"] = new Filter { Id = "is-void", Type = "void" };
        map.Filters["no-void"] = new Filter { Id = "no-void", Type = "not", Child = "is-void" };
        map.ApplyRules.Add(new ApplyRule { BlockFilter = "no-void", RegionId = "not-build-area" });
        return map;
    }

    [Test]
    public async Task A_void_gap_inside_the_declared_buildable_region_joins_the_platforms_into_one_component()
    {
        var map = MapWithBuildRegion(minX: 2, minZ: -1, maxX: 5, maxZ: 3);   // covers the x=2..4 gap
        var bridgeable = TraversabilityRender.BridgeableColumns(map);

        var result = TraversabilityRender.Render(AnvilRegion.FromWorld(TwoPlatforms()), markers: [], bridgeable);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ComponentCount).IsEqualTo(1);
        await Assert.That(result.BridgeableCount).IsGreaterThan(0);
    }

    [Test]
    public async Task A_buildable_region_that_misses_the_gap_leaves_the_platforms_separate()
    {
        var map = MapWithBuildRegion(minX: 50, minZ: 50, maxX: 55, maxZ: 55);   // nowhere near the gap
        var bridgeable = TraversabilityRender.BridgeableColumns(map);

        var result = TraversabilityRender.Render(AnvilRegion.FromWorld(TwoPlatforms()), markers: [], bridgeable);

        // The far-off buildable patch is itself now navigable (it reads as its own small component, +1), but
        // it touches neither platform — three components total, the two platforms still apart from each other.
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ComponentCount).IsEqualTo(3);
        await Assert.That(result.BridgeableCount).IsGreaterThan(0);
    }

    [Test]
    public async Task A_region_over_the_gap_with_no_void_gating_apply_rule_is_not_bridged()
    {
        // The water-lane shape: a region named/drawn over the gap, but nothing applies a "not void" filter
        // to it — the lane opens later by a timed fill, not by this wiring, so it reads as void here exactly
        // like any other gap the map has not declared buildable at kickoff.
        var map = new MapXml();
        map.Regions["lane"] = new Region { Id = "lane", Type = "rectangle", MinX = 2, MinZ = -1, MaxX = 5, MaxZ = 3 };

        var bridgeable = TraversabilityRender.BridgeableColumns(map);
        var result = TraversabilityRender.Render(AnvilRegion.FromWorld(TwoPlatforms()), markers: [], bridgeable);

        await Assert.That(bridgeable.Count).IsEqualTo(0);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ComponentCount).IsEqualTo(2);
    }

    [Test]
    public async Task An_inline_deny_void_shorthand_is_read_the_same_as_a_registered_not_void_filter()
    {
        // A hand-authored map can write PGM's inline filter shorthand directly into the block attribute
        // without ever registering a filter for it.
        var map = new MapXml();
        map.Regions["build-area-1"] = new Region { Id = "build-area-1", Type = "rectangle", MinX = 2, MinZ = -1, MaxX = 5, MaxZ = 3 };
        map.ApplyRules.Add(new ApplyRule { BlockFilter = "deny(void)", RegionId = "build-area-1" });

        var bridgeable = TraversabilityRender.BridgeableColumns(map);

        await Assert.That(bridgeable.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task An_apply_rule_gated_on_an_unrelated_filter_bridges_nothing()
    {
        var map = new MapXml();
        map.Regions["spawn-area"] = new Region { Id = "spawn-area", Type = "rectangle", MinX = 2, MinZ = -1, MaxX = 5, MaxZ = 3 };
        map.Filters["red-only"] = new Filter { Id = "red-only", Type = "team", Team = "red" };
        map.ApplyRules.Add(new ApplyRule { BlockFilter = "red-only", RegionId = "spawn-area" });

        await Assert.That(TraversabilityRender.BridgeableColumns(map).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Run_appends_a_scale_legend_that_grows_the_written_png()
    {
        var outPng = Path.Combine(Path.GetTempPath(), $"traversability-legend-{Guid.NewGuid():N}.png");
        try
        {
            var exit = TraversabilityRender.Run(TwoPlatforms(), outPng, map: null, scale: 2);
            await Assert.That(exit).IsEqualTo(0);

            var (width, height) = PngTestUtil.Dimensions(File.ReadAllBytes(outPng));
            await Assert.That(width).IsEqualTo(14);       // x=0..6 at scale 2
            await Assert.That(height).IsGreaterThan(4);   // z=0..1 at scale 2, plus the legend strip
        }
        finally { File.Delete(outPng); }
    }
}
