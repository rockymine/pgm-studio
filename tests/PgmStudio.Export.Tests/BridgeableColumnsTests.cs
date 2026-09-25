using PgmStudio.Analysis.Playability;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Render;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Which void columns a world's map document opens to bridging, answered once by <see cref="Editability"/>
/// and drawn by the reach picture: the two cannot disagree, because the picture is handed the answer.
/// </summary>
public sealed class BridgeableColumnsTests
{
    /// <summary>Two platforms of ground down to y=0 — x 0..1 and x 5..6, z 0..1 — with a void gap between.</summary>
    private static List<AnvilRegion.Chunk> TwoPlatforms()
    {
        var world = new VoxelWorld();
        foreach (var x in (int[])[0, 1, 5, 6])
            for (var z = 0; z <= 1; z++)
                for (var y = 0; y <= 5; y++)
                    world.SetBlock(x, y, z, Blocks.Stone);
        return [.. AnvilRegion.FromWorld(world)];
    }

    /// <summary>A document carrying a build area the way the template writes it — <c>block-place</c> =
    /// <c>not(void)</c> and the break exception beside it, over <c>not-build-area</c> — wired by the
    /// generator that writes it.</summary>
    private static Dict TemplateBuildArea(Rect area)
    {
        var doc = new Dict
        {
            ["regions"] = new Dict(), ["filters"] = new Dict(), ["apply_rules"] = new List<object?>(),
        };
        BuildGenerator.Apply(doc, new MapIntent { Build = new BuildIntent { Areas = [area] } });
        return doc;
    }

    [Test]
    public async Task A_template_build_area_over_the_gap_joins_the_platforms_and_opens_nothing_else()
    {
        var chunks = TwoPlatforms();
        var doc = TemplateBuildArea(new Rect(2, -1, 5, 3));

        var bridgeable = BridgeableColumns.Of(chunks, doc);
        var reach = TraversabilityRender.Render(chunks, markers: [], bridgeable);

        await Assert.That(reach).IsNotNull();
        await Assert.That(reach!.ComponentCount).IsEqualTo(1);
        await Assert.That(bridgeable.Contains((3, 0))).IsTrue().Because("inside the drawn area");
        await Assert.That(bridgeable.Contains((10, 10))).IsFalse()
            .Because("outside it placing over the void is denied, whatever the break exception allows");
        await Assert.That(bridgeable.All(cell => cell is { X: >= 2 and <= 4, Z: >= -1 and <= 2 })).IsTrue();
    }

    [Test]
    public async Task The_reach_picture_bridges_the_void_columns_editability_opens()
    {
        var chunks = TwoPlatforms();
        var doc = TemplateBuildArea(new Rect(2, -1, 5, 3));

        var zones = Editability.Compute(doc, [], (-16, -16, 24, 24));
        var reach = TraversabilityRender.Render(chunks, markers: [], BridgeableColumns.Of(chunks, doc));

        var ground = new HashSet<(int, int)> { (0, 0), (0, 1), (1, 0), (1, 1), (5, 0), (5, 1), (6, 0), (6, 1) };
        await Assert.That(reach!.BridgeableCount)
            .IsEqualTo(zones.BridgeableCells().Count(cell => !ground.Contains(cell)));
    }

    [Test]
    public async Task A_build_area_that_misses_the_gap_leaves_the_platforms_separate()
    {
        var chunks = TwoPlatforms();
        var reach = TraversabilityRender.Render(
            chunks, markers: [], BridgeableColumns.Of(chunks, TemplateBuildArea(new Rect(50, 50, 55, 55))));

        // The far-off area is its own small component; the two platforms stay apart.
        await Assert.That(reach!.ComponentCount).IsEqualTo(3);
    }

    [Test]
    public async Task A_void_rule_on_the_block_scope_opens_the_same_columns_as_one_on_the_place_scope()
    {
        var chunks = TwoPlatforms();
        var placeScoped = TemplateBuildArea(new Rect(2, -1, 5, 3));
        var bothScoped = TemplateBuildArea(new Rect(2, -1, 5, 3));
        var rule = (Dict)((List<object?>)bothScoped["apply_rules"]!)[0]!;
        rule["block"] = rule["block_place"];
        rule.Remove("block_place");
        rule.Remove("block_break");

        var placed = BridgeableColumns.Of(chunks, placeScoped);

        await Assert.That(placed.Count).IsGreaterThan(0);
        await Assert.That(placed.SetEquals(BridgeableColumns.Of(chunks, bothScoped))).IsTrue();
    }

    [Test]
    public async Task A_region_over_the_gap_with_no_rule_opens_nothing()
    {
        // The water-lane shape: a region drawn over the gap and nothing applied to it — a lane opens later by
        // a timed fill, not at kickoff.
        var doc = TemplateBuildArea(new Rect(2, -1, 5, 3));
        doc["apply_rules"] = new List<object?>();

        await Assert.That(BridgeableColumns.Of(TwoPlatforms(), doc)).IsEmpty();
    }
}
