using PgmStudio.Domain;
using PgmStudio.Minecraft.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The traversability stage image: navigable columns (ground + two blocks headroom, or a void column
/// the map's own buildable-region wiring makes bridgeable) split into connected components.</summary>
public sealed class TraversabilityRenderTests
{
    /// <summary><b>A building is not walked over.</b> A roof carries two clear blocks of headroom like any
    /// other surface, so before the rise bound the flood climbed the wall and the route ran across the
    /// building — a road blocked by a house read as one whole component, which is the direction that looks
    /// like an improvement. Past <see cref="Walk.WallRise"/> a face is something a player goes round
    /// (<c>WS17</c>).</summary>
    [Test]
    public async Task A_building_standing_across_a_road_splits_the_walk()
    {
        var world = new VoxelWorld();
        // A road nine cells long, one cell wide.
        for (var x = 0; x <= 8; x++) world.SetBlock(x, 5, 0, Blocks.Stone);

        // A solid block of building standing on the middle of it, seven courses tall — a rise of seven over
        // the road on each side, past the bound.
        for (var y = 6; y <= 12; y++)
            for (var x = 3; x <= 5; x++)
                world.SetBlock(x, y, 0, Blocks.Stone);

        var result = TraversabilityRender.Render(AnvilRegion.FromWorld(world), markers: []);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ComponentCount).IsEqualTo(3)
            .Because("the road either side of the building, and the roof, are three separate places");
    }

    /// <summary>A rise inside the bound is ground: a bank, a ramp or a flight of steps still joins what it
    /// climbs between, so the bound separates a wall from a slope rather than flattening every step.</summary>
    [Test]
    public async Task A_bank_within_the_bound_still_joins_what_it_climbs_between()
    {
        var world = new VoxelWorld();
        for (var x = 0; x <= 3; x++) world.SetBlock(x, 5, 0, Blocks.Stone);
        for (var x = 4; x <= 8; x++) world.SetBlock(x, 10, 0, Blocks.Stone);   // a rise of five, at the bound

        var result = TraversabilityRender.Render(AnvilRegion.FromWorld(world), markers: []);

        await Assert.That(result!.ComponentCount).IsEqualTo(1)
            .Because("five blocks is the tallest rise that is still ground");
    }

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

    // The two platforms from the tests above (x=0..1 and x=5..6, z=0..1), with the same x=2..4 void gap.
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

    /// <summary>The columns x 2..4, z <paramref name="minZ"/>..<paramref name="maxZ"/> — the gap between the
    /// two platforms, or part of it.</summary>
    private static HashSet<(int X, int Z)> Gap(int minZ = 0, int maxZ = 1)
    {
        var cells = new HashSet<(int X, int Z)>();
        for (var x = 2; x <= 4; x++)
            for (var z = minZ; z <= maxZ; z++)
                cells.Add((x, z));
        return cells;
    }

    /// <summary><b>The render draws exactly the void columns it is handed.</b> Which columns a board opens to
    /// bridging is the caller's answer, so the picture bridges the handed gap and nothing past it, and a
    /// handed column that already has ground keeps its own reading.</summary>
    [Test]
    public async Task The_render_bridges_exactly_the_void_columns_it_is_handed()
    {
        var handed = Gap();
        handed.Add((0, 0));                                   // ground: stays ground

        var result = TraversabilityRender.Render(AnvilRegion.FromWorld(TwoPlatforms()), markers: [], handed);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.BridgeableCount).IsEqualTo(6);
        await Assert.That(result.ComponentCount).IsEqualTo(1);
    }

    /// <summary>A handed set covering only one row of the gap still joins the platforms, and bridges only
    /// that row.</summary>
    [Test]
    public async Task A_partial_bridge_is_drawn_as_the_part_handed()
    {
        var result = TraversabilityRender.Render(
            AnvilRegion.FromWorld(TwoPlatforms()), markers: [], Gap(minZ: 0, maxZ: 0));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.BridgeableCount).IsEqualTo(3);
        await Assert.That(result.ComponentCount).IsEqualTo(1);
    }

    /// <summary><b>The render does not read the map's filters.</b> A map wiring a build area the template way
    /// opens nothing unless the caller hands the columns in: a second reader of the same filters here is a
    /// second answer to a question <c>Editability</c> owns.</summary>
    [Test]
    public async Task A_map_stating_a_build_area_bridges_nothing_the_caller_did_not_hand_in()
    {
        var map = new MapXml();
        map.Regions["build-area-1"] = new Region { Id = "build-area-1", Type = "rectangle", MinX = 2, MinZ = -1, MaxX = 5, MaxZ = 3 };
        map.Regions["not-build-area"] = new Region { Id = "not-build-area", Type = "negative", Children = ["build-area-1"] };
        map.Filters["is-void"] = new Filter { Id = "is-void", Type = "void" };
        map.Filters["no-void"] = new Filter { Id = "no-void", Type = "not", Child = "is-void" };
        map.ApplyRules.Add(new ApplyRule { BlockPlaceFilter = "no-void", RegionId = "not-build-area" });

        var result = TraversabilityRender.Read([.. AnvilRegion.FromWorld(TwoPlatforms())], map, bridgeable: null);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.BridgeableCount).IsEqualTo(0);
        await Assert.That(result.ComponentCount).IsEqualTo(2);
    }

    [Test]
    public async Task Run_appends_a_scale_legend_that_grows_the_written_png()
    {
        var outPng = Path.Combine(Path.GetTempPath(), $"traversability-legend-{Guid.NewGuid():N}.png");
        try
        {
            var exit = TraversabilityRender.Run(
                [.. AnvilRegion.FromWorld(TwoPlatforms())], outPng, map: null, bridgeable: null, scale: 2);
            await Assert.That(exit).IsEqualTo(0);

            var (width, height) = PngTestUtil.Dimensions(File.ReadAllBytes(outPng));
            await Assert.That(width).IsEqualTo(14);       // x=0..6 at scale 2
            await Assert.That(height).IsGreaterThan(4);   // z=0..1 at scale 2, plus the legend strip
        }
        finally { File.Delete(outPng); }
    }
}
