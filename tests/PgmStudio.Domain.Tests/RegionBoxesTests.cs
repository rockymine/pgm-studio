using PgmStudio.Domain;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// <see cref="RegionBoxes.FootprintXZ"/> — the XZ-only reduction a <c>rectangle</c> region needs, since it
/// carries no Y bounds for <see cref="RegionBoxes.Of"/> to use. Mirrors the shape the studio's own build
/// regions are drawn in (<c>BuildGenerator</c>): individual rectangles unioned, then wrapped in a negative so
/// an apply rule can gate the void everywhere else.
/// </summary>
public sealed class RegionBoxesTests
{
    private static Region Rectangle(string id, int minX, int minZ, int maxX, int maxZ) =>
        new() { Id = id, Type = "rectangle", MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ };

    [Test]
    public async Task A_rectangle_reduces_to_its_XZ_span_regardless_of_height()
    {
        var registry = new Dictionary<string, Region> { ["build-area-1"] = Rectangle("build-area-1", 10, 20, 15, 22) };

        var boxes = RegionBoxes.FootprintXZ(registry, "build-area-1");

        await Assert.That(boxes.Count).IsEqualTo(1);
        await Assert.That(boxes[0].MinX).IsEqualTo(10);
        await Assert.That(boxes[0].MaxX).IsEqualTo(14);   // PGM bounds are [min, max) on X/Z
        await Assert.That(boxes[0].MinZ).IsEqualTo(20);
        await Assert.That(boxes[0].MaxZ).IsEqualTo(21);
    }

    [Test]
    public async Task Of_gives_nothing_for_a_rectangle_because_it_has_no_Y_bounds()
    {
        // The gap FootprintXZ exists to fill: Of() only handles shapes it can state as a full BlockBox, and
        // a rectangle never carries Y at all.
        var registry = new Dictionary<string, Region> { ["r"] = Rectangle("r", 0, 0, 5, 5) };

        await Assert.That(RegionBoxes.Of(registry, "r").Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_union_of_rectangles_contributes_every_child()
    {
        var registry = new Dictionary<string, Region>
        {
            ["build-area-1"] = Rectangle("build-area-1", 0, 0, 5, 5),
            ["build-area-2"] = Rectangle("build-area-2", 100, 100, 105, 105),
            ["build-area"] = new() { Id = "build-area", Type = "union", Children = ["build-area-1", "build-area-2"] },
        };

        var boxes = RegionBoxes.FootprintXZ(registry, "build-area");

        await Assert.That(boxes.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_negative_over_a_buildable_union_yields_the_buildable_footprint_not_the_complement()
    {
        // BuildGenerator's own shape: not-build-area = negative(build-area). The negative wraps the region
        // an apply rule gates outside of; walking it hands back the buildable footprint itself (the "rest is
        // subtracted" convention Of() already documents), not an attempt at the true (unbounded) complement.
        var registry = new Dictionary<string, Region>
        {
            ["build-area-1"] = Rectangle("build-area-1", 0, 0, 5, 5),
            ["build-area"] = new() { Id = "build-area", Type = "union", Children = ["build-area-1"] },
            ["not-build-area"] = new() { Id = "not-build-area", Type = "negative", Children = ["build-area"] },
        };

        var boxes = RegionBoxes.FootprintXZ(registry, "not-build-area");

        await Assert.That(boxes.Count).IsEqualTo(1);
        await Assert.That(boxes[0].MinX).IsEqualTo(0);
        await Assert.That(boxes[0].MaxX).IsEqualTo(4);
    }

    [Test]
    public async Task A_complement_drops_its_hole_children_the_same_way_Of_drops_them()
    {
        var registry = new Dictionary<string, Region>
        {
            ["build-area"] = Rectangle("build-area", 0, 0, 10, 10),
            ["build-hole-1"] = Rectangle("build-hole-1", 2, 2, 4, 4),
            ["buildable"] = new() { Id = "buildable", Type = "complement", Children = ["build-area", "build-hole-1"] },
        };

        var boxes = RegionBoxes.FootprintXZ(registry, "buildable");

        await Assert.That(boxes.Count).IsEqualTo(1);
        await Assert.That(boxes[0].MaxX).IsEqualTo(9);   // the hole is not subtracted from the footprint
    }

    [Test]
    public async Task A_shape_that_does_not_reduce_contributes_nothing()
    {
        var registry = new Dictionary<string, Region>
        {
            ["circle"] = new() { Id = "circle", Type = "circle", CenterX = 0, CenterZ = 0, Radius = 10 },
        };

        await Assert.That(RegionBoxes.FootprintXZ(registry, "circle").Count).IsEqualTo(0);
    }

    [Test]
    public async Task An_unknown_region_id_contributes_nothing()
        => await Assert.That(RegionBoxes.FootprintXZ(new Dictionary<string, Region>(), "missing").Count).IsEqualTo(0);
}
