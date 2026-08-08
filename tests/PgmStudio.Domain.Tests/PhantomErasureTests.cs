using PgmStudio.Domain;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// What a map deletes before the first tick (<see cref="PhantomErasure"/>) — the fact that replaces the
/// material guess island detection used to make. The tests fix the two directions that matter: a build floor
/// erased at load is not terrain, and everything else still is.
/// </summary>
public sealed class PhantomErasureTests
{
    private static MapXml MapWith(Destroyable destroyable, ObjectiveMode mode, Region region)
    {
        var map = new MapXml();
        map.Destroyables.Add(destroyable);
        map.Modes.Add(mode);
        map.Regions[region.Id] = region;
        return map;
    }

    private static Region Cuboid(string id, int minX, int minY, int minZ, int maxX, int maxY, int maxZ) =>
        new() { Id = id, Type = "cuboid", MinX = minX, MinY = minY, MinZ = minZ, MaxX = maxX, MaxY = maxY, MaxZ = maxZ };

    private static Destroyable Phantom(string materials, string modeId) =>
        new() { Id = "floor", Name = "floor", Owner = "red", RegionId = "floor-region", Materials = materials, Show = false, Modes = [modeId] };

    [Test]
    public async Task A_build_floor_erased_at_load_is_not_terrain()
    {
        var map = MapWith(Phantom("stained glass", "air-mode"),
                          new ObjectiveMode { Id = "air-mode", After = "0s", Material = "air" },
                          Cuboid("floor-region", 10, 0, 10, 20, 2, 20));
        var erasure = PhantomErasure.From(map);

        await Assert.That(erasure.IsEmpty).IsFalse();
        await Assert.That(erasure.Erases(15, 0, 15, 95)).IsTrue();      // glass inside the region, at load
        await Assert.That(erasure.Erases(15, 0, 15, 1)).IsFalse();      // stone there is not what the mode names
        await Assert.That(erasure.Erases(50, 0, 50, 95)).IsFalse();     // glass outside the region survives
    }

    [Test]
    public async Task A_mode_that_fires_mid_match_erases_nothing()
    {
        // Blocks a mode replaces at 15m are terrain when the match starts, which is the moment island
        // detection describes.
        var map = MapWith(Phantom("stained glass", "late"),
                          new ObjectiveMode { Id = "late", After = "15m", Material = "air" },
                          Cuboid("floor-region", 10, 0, 10, 20, 2, 20));

        await Assert.That(PhantomErasure.From(map).IsEmpty).IsTrue();
    }

    [Test]
    public async Task A_water_lane_adds_blocks_and_so_erases_nothing()
    {
        // The opposite swap: materials `air` becoming water. Nothing is deleted, so nothing is subtracted.
        var map = MapWith(Phantom("air", "water-mode"),
                          new ObjectiveMode { Id = "water-mode", After = "15m", Material = "water" },
                          Cuboid("floor-region", 10, 0, 10, 20, 2, 20));

        await Assert.That(PhantomErasure.From(map).IsEmpty).IsTrue();
    }

    [Test]
    public async Task A_visible_destroyable_is_a_goal_and_erases_nothing()
    {
        var goal = new Destroyable
        {
            Id = "mon", Name = "Red Monument", Owner = "red", RegionId = "floor-region",
            Materials = "obsidian", Show = true, Modes = ["air-mode"],
        };
        var map = MapWith(goal, new ObjectiveMode { Id = "air-mode", After = "0s", Material = "air" },
                          Cuboid("floor-region", 10, 0, 10, 20, 2, 20));

        await Assert.That(PhantomErasure.From(map).IsEmpty).IsTrue();
    }

    [Test]
    public async Task A_phantom_with_no_mode_erases_nothing()
    {
        // No mode means no scheduled swap — the destroyable is a scripted trigger, not a deletion.
        var map = new MapXml();
        map.Destroyables.Add(new Destroyable
        {
            Id = "t", Name = "t", Owner = "red", RegionId = "floor-region", Materials = "iron fence", Show = false,
        });
        map.Regions["floor-region"] = Cuboid("floor-region", 0, 0, 0, 5, 5, 5);

        await Assert.That(PhantomErasure.From(map).IsEmpty).IsTrue();
    }

    [Test]
    public async Task An_unreadable_material_erases_nothing()
    {
        // Failing closed is the safe direction: a block that cannot be named stays exactly as the world has it.
        var map = MapWith(Phantom("some future block", "air-mode"),
                          new ObjectiveMode { Id = "air-mode", After = "0s", Material = "air" },
                          Cuboid("floor-region", 10, 0, 10, 20, 2, 20));

        await Assert.That(PhantomErasure.From(map).IsEmpty).IsTrue();
    }

    [Test]
    public async Task A_region_with_no_vertical_bound_erases_nothing()
    {
        // A shape unbounded in Y would delete whole columns on a claim it never made.
        var map = MapWith(Phantom("stained glass", "air-mode"),
                          new ObjectiveMode { Id = "air-mode", After = "0s", Material = "air" },
                          new Region { Id = "floor-region", Type = "cuboid", MinX = 0, MinZ = 0, MaxX = 9, MaxZ = 9 });

        await Assert.That(PhantomErasure.From(map).IsEmpty).IsTrue();
    }

    [Test]
    public async Task A_union_erases_every_box_under_it()
    {
        var map = MapWith(Phantom("stained glass", "air-mode"),
                          new ObjectiveMode { Id = "air-mode", After = "0s", Material = "air" },
                          new Region { Id = "floor-region", Type = "union", Children = ["west", "east"] });
        map.Regions["west"] = Cuboid("west", 0, 0, 0, 9, 1, 9);
        map.Regions["east"] = Cuboid("east", 100, 0, 0, 109, 1, 9);
        var erasure = PhantomErasure.From(map);

        await Assert.That(erasure.Boxes.Count).IsEqualTo(2);
        await Assert.That(erasure.Erases(5, 0, 5, 95)).IsTrue();
        await Assert.That(erasure.Erases(105, 0, 5, 95)).IsTrue();
        await Assert.That(erasure.Erases(50, 0, 5, 95)).IsFalse();
    }

    [Test]
    public async Task A_cuboid_covers_the_blocks_its_half_open_bounds_name()
    {
        // PGM cuboid bounds are [min, max), so a 0..2 span is the two blocks y=0 and y=1.
        var map = MapWith(Phantom("stained glass", "air-mode"),
                          new ObjectiveMode { Id = "air-mode", After = "0s", Material = "air" },
                          Cuboid("floor-region", 0, 0, 0, 10, 2, 10));
        var erasure = PhantomErasure.From(map);

        await Assert.That(erasure.Erases(0, 0, 0, 95)).IsTrue();
        await Assert.That(erasure.Erases(9, 1, 9, 95)).IsTrue();
        await Assert.That(erasure.Erases(9, 2, 9, 95)).IsFalse();     // one past the last block
        await Assert.That(erasure.Erases(10, 0, 10, 95)).IsFalse();
    }

    [Test]
    public async Task A_map_with_no_destroyables_erases_nothing()
        => await Assert.That(PhantomErasure.From(new MapXml()).IsEmpty).IsTrue();
}
