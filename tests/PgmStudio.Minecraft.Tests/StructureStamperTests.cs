using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Layout-structure stamps (docs/generator/rules.md ST1–ST4): a room's levelling fill, an entrance redstone
/// row with end torches, a 4×4×4 iron cube on the surface, and a bedrock approach wall to a fixed top height.
/// Each stamp reads a per-column surface top so it sits on the real terrain.
/// </summary>
public sealed class StructureStamperTests
{
    // A flat surface (first air Y) at the given height across a rectangle of columns.
    private static Dictionary<(int X, int Z), int> FlatSurface(int minX, int minZ, int maxX, int maxZ, int top)
    {
        var d = new Dictionary<(int X, int Z), int>();
        for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
            d[(x, z)] = top;
        return d;
    }

    /// <summary>The world a surface map describes: stone from <paramref name="floor"/> up to each column's
    /// own first-air.</summary>
    private static VoxelWorld Ground(IReadOnlyDictionary<(int X, int Z), int> surface, int floor = 0)
    {
        var world = new VoxelWorld();
        foreach (var ((x, z), top) in surface)
            for (var y = floor; y < top; y++)
                world.SetBlock(x, y, z, Blocks.Stone);
        return world;
    }

    [Test]
    public async Task Ground_already_at_the_floor_is_left_exactly_as_it_was()
    {
        var surf = FlatSurface(0, 0, 10, 10, top: 13);
        var w = Ground(surf);
        StructureStamper.StampFoundation(w, surf, minX: 2, minZ: 2, maxX: 6, maxZ: 6);

        // Flat ground already reaches the floor course, so the fill writes nothing: the column keeps the
        // terrain it had, top to bottom, and nothing under a room is sealed against being dug into.
        await Assert.That(w.GetBlock(2, 0, 2).Id).IsEqualTo(Blocks.Stone);
        await Assert.That(w.GetBlock(3, 7, 3).Id).IsEqualTo(Blocks.Stone);
        await Assert.That(w.GetBlock(5, 12, 5).Id).IsEqualTo(Blocks.Stone);   // top solid block
        await Assert.That(w.GetBlock(5, 13, 5).Id).IsEqualTo(Blocks.Air);     // surface cell left open
    }

    [Test]
    public async Task The_fill_is_level_under_a_room_standing_on_sloping_ground()
    {
        // What stands on the fill takes one floor course over its whole frame, read from the frame's
        // highest column, so a fill that followed the ground would stop short of the floor wherever the
        // ground falls away and leave it spanning air.
        var surf = new Dictionary<(int X, int Z), int>();
        for (var x = 0; x <= 10; x++)
        for (var z = 0; z <= 10; z++)
            surf[(x, z)] = 10 + z;                       // first air Y climbs one a row
        var w = Ground(surf);

        StructureStamper.StampFoundation(w, surf, minX: 2, minZ: 2, maxX: 6, maxZ: 6);

        // The highest column of the footprint is z=5, whose first air cell is 15 — so every column of it is
        // solid to 14, the low rows included, and what fills the gap is stone the painter will finish.
        await Assert.That(w.GetBlock(3, 14, 2).Id).IsEqualTo(Blocks.Stone);   // lowest row, filled to the top
        await Assert.That(w.GetBlock(3, 14, 5).Id).IsEqualTo(Blocks.Stone);   // highest row
        await Assert.That(w.GetBlock(3, 15, 5).Id).IsEqualTo(Blocks.Air);     // its surface cell left open
        await Assert.That(w.GetBlock(6, 14, 2).Id).IsEqualTo(Blocks.Air);     // max bound still exclusive

        // And nothing below the ground a cell already had is rewritten — a room is kept from being entered
        // by its region, so there is no plinth sunk under it.
        await Assert.That(w.GetBlock(3, 0, 2).Id).IsEqualTo(Blocks.Stone);
    }

    /// <summary>Ground that floats over void keeps the void under it. A foundation that filled from
    /// <c>y 0</c> hung a pillar under every stamped room on a board of crags — measured on `opus5-aerie` at
    /// `(20, 28)`: solid from y0 to y24 under a wool room whose crag begins at y16, in open sky the whole
    /// way.</summary>
    [Test]
    public async Task Ground_that_floats_keeps_the_void_under_it()
    {
        var surf = FlatSurface(0, 0, 10, 10, top: 30);
        var w = Ground(surf, floor: 20);                 // a slab ten courses thick, hanging over nothing

        StructureStamper.StampFoundation(w, surf, minX: 2, minZ: 2, maxX: 6, maxZ: 6);

        await Assert.That(w.GetBlock(3, 29, 3).Id).IsEqualTo(Blocks.Stone);   // the slab's top course
        await Assert.That(w.GetBlock(3, 20, 3).Id).IsEqualTo(Blocks.Stone);   // its lowest
        await Assert.That(w.GetBlock(3, 19, 3).Id).IsEqualTo(Blocks.Air);     // and the void under it
        await Assert.That(w.GetBlock(3, 0, 3).Id).IsEqualTo(Blocks.Air);
    }

    /// <summary>A cell of the footprint with no ground at all is left alone: there is nothing under it to
    /// level against, and a column raised there stands in open sky.</summary>
    [Test]
    public async Task A_footprint_cell_over_nothing_gets_no_column()
    {
        var surf = new Dictionary<(int X, int Z), int>();
        for (var x = 0; x <= 10; x++)
        for (var z = 0; z <= 10; z++)
            surf[(x, z)] = 10 + z;                       // sloped, so its neighbours do fill
        surf.Remove((3, 3));                             // one cell of the footprint is void
        var w = Ground(surf);

        StructureStamper.StampFoundation(w, surf, minX: 2, minZ: 2, maxX: 6, maxZ: 6);

        await Assert.That(w.GetBlock(3, 14, 3).Id).IsEqualTo(Blocks.Air);
        await Assert.That(w.GetBlock(3, 0, 3).Id).IsEqualTo(Blocks.Air);
        await Assert.That(w.GetBlock(4, 14, 4).Id).IsEqualTo(Blocks.Stone);   // its neighbour is levelled
    }

    [Test]
    public async Task The_goal_plate_is_buried_and_its_chest_stands_on_the_ground()
    {
        // Two different depths, which is why they are two stamps. The plate goes into the terrain, far
        // enough down that a shaft meets rock before daylight; the chest is a supply a defender walks up to,
        // so it stands on the ground beside the monument rather than in the space under it, where whole
        // terrain would cover it. A destroyable takes both.
        var w = new VoxelWorld();
        var surf = FlatSurface(0, 0, 20, 20, top: 21);       // first air Y 21, so the ground's top block is 20
        for (var x = 8; x <= 12; x++)
        for (var z = 8; z <= 12; z++)
        for (var y = 0; y <= 20; y++)
            w.SetBlock(x, y, z, Blocks.Stone);               // ordinary terrain for the plate to be cut into

        StructureStamper.StampPlatform(w, surf, minX: 8, minZ: 8, maxX: 12, maxZ: 12);
        StructureStamper.StampDefenseChest(w, surf, minX: 8, minZ: 8, maxX: 12, maxZ: 12);

        await Assert.That(w.GetBlock(8, 21 - 1 - StructureStamper.PlatformDepth, 8).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(10, 21, 10).Id).IsEqualTo(Blocks.Chest);   // standing on the ground
        await Assert.That(w.GetBlock(10, 22, 10).Id).IsEqualTo(Blocks.Air);     // the lid can open
        // and the ground the chest stands on is whole, rather than a hole where the chest used to sit
        await Assert.That(w.GetBlock(10, 20, 10).Id).IsEqualTo(Blocks.Stone);
        await Assert.That(w.GetBlock(10, 21 - StructureStamper.PlatformDepth, 10).Id).IsEqualTo(Blocks.Stone);
    }

    [Test]
    public async Task Redstone_line_lays_wire_between_torch_ends_on_the_surface()
    {
        var w = new VoxelWorld();
        var surf = FlatSurface(0, 0, 20, 5, top: 9);
        // A row along x from (4,3) to (9,3).
        StructureStamper.StampRedstoneLine(w, surf, x1: 4, z1: 3, x2: 9, z2: 3);

        await Assert.That(w.GetBlock(4, 9, 3).Id).IsEqualTo(Blocks.RedstoneTorch);   // end
        await Assert.That(w.GetBlock(9, 9, 3).Id).IsEqualTo(Blocks.RedstoneTorch);   // end
        await Assert.That(w.GetBlock(5, 9, 3).Id).IsEqualTo(Blocks.RedstoneWire);    // interior
        await Assert.That(w.GetBlock(8, 9, 3).Id).IsEqualTo(Blocks.RedstoneWire);
        // wire is laid at full signal strength (data = power level)
        await Assert.That(w.GetBlock(5, 9, 3).Data).IsEqualTo(15);
        // sits on top of the surface (y = surface top), not below it
        await Assert.That(w.GetBlock(6, 8, 3).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task Iron_cube_is_a_span_cube_resting_on_the_surface_over_its_own_corner()
    {
        var span = RoomFrames.IronSpan;
        var w = new VoxelWorld();
        var surf = FlatSurface(-4, -4, 8, 8, top: 13);
        StructureStamper.StampIronCubeAt(w, surf, minX: 1, minZ: 2, span);

        // The corner is where the resolver seated it, and the cube runs span blocks from there on all three
        // axes with its base on the surface.
        await Assert.That(w.GetBlock(1, 13, 2).Id).IsEqualTo(Blocks.IronBlock);                  // base corner
        await Assert.That(w.GetBlock(span, 13 + span - 1, span + 1).Id).IsEqualTo(Blocks.IronBlock);  // top far
        await Assert.That(w.GetBlock(1, 13 + span, 2).Id).IsEqualTo(Blocks.Air);                 // above it
        await Assert.That(w.GetBlock(0, 13, 2).Id).IsEqualTo(Blocks.Air);                        // outside
        await Assert.That(w.GetBlock(1 + span, 13, 2).Id).IsEqualTo(Blocks.Air);                 // past the far face
    }

    [Test]
    public async Task Wall_rises_bedrock_from_zero_to_the_top_height_inclusive()
    {
        var w = new VoxelWorld();
        // Footprint 2 thick across z, 10 wide across x; top at y=13.
        StructureStamper.StampWall(w, minX: -30, minZ: 39, maxX: -20, maxZ: 41, topY: 13);

        await Assert.That(w.GetBlock(-30, 0, 39).Id).IsEqualTo(Blocks.Bedrock);    // reaches the floor
        await Assert.That(w.GetBlock(-25, 13, 40).Id).IsEqualTo(Blocks.Bedrock);   // top bedrock course
        // 2 thick across the seam (z 39,40); z=41 is the exclusive bound.
        await Assert.That(w.GetBlock(-25, 5, 40).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(-25, 5, 41).Id).IsEqualTo(Blocks.Air);
        // full width across x, exclusive at maxX.
        await Assert.That(w.GetBlock(-21, 5, 39).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(-20, 5, 39).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task Wall_is_capped_by_one_course_of_cobweb_over_its_whole_footprint()
    {
        var w = new VoxelWorld();
        StructureStamper.StampWall(w, minX: -30, minZ: 39, maxX: -20, maxZ: 41, topY: 13);

        for (var x = -30; x < -20; x++)
        for (var z = 39; z < 41; z++)
            await Assert.That(w.GetBlock(x, 14, z).Id).IsEqualTo(Blocks.Cobweb);
        await Assert.That(w.GetBlock(-25, 15, 40).Id).IsEqualTo(Blocks.Air);   // one course, and nothing above it
        await Assert.That(w.GetBlock(-20, 14, 39).Id).IsEqualTo(Blocks.Air);   // exclusive at maxX, like the bedrock
    }

    [Test]
    public async Task Platform_is_a_5x5_bedrock_plate_three_courses_beneath_the_ground()
    {
        var w = new VoxelWorld();
        var surf = FlatSurface(-10, -10, 10, 10, top: 20);   // ground surface (solid) block at y=19
        StructureStamper.StampPlatform(w, surf, minX: -2, minZ: -2, maxX: 2, maxZ: 2);

        // Three courses below the ground's own top block (19 - 3 = 16), full 5×5 footprint.
        for (var x = -2; x <= 2; x++)
        for (var z = -2; z <= 2; z++)
            await Assert.That(w.GetBlock(x, 16, z).Id).IsEqualTo(Blocks.Bedrock);

        // Nothing at the ground's own surface course, and nothing a course further down either — one
        // course is the whole of the plate.
        await Assert.That(w.GetBlock(0, 19, 0).Id).IsEqualTo(Blocks.Air);
        await Assert.That(w.GetBlock(0, 15, 0).Id).IsEqualTo(Blocks.Air);
        // Outside the 5×5 footprint, nothing is touched even at the plate's own course.
        await Assert.That(w.GetBlock(3, 16, 0).Id).IsEqualTo(Blocks.Air);
    }

    /// <summary>The chest stands on the ground at the footprint's centre, beside the monument, with the
    /// course over it carved so the lid can open. The space the plate's depth opens is not where it goes:
    /// three courses down under whole terrain is a supply nobody can see or reach.</summary>
    [Test]
    public async Task The_defence_chest_stands_on_the_ground()
    {
        var w = new VoxelWorld();
        var surf = FlatSurface(-10, -10, 10, 10, top: 20);   // first air Y 20
        StructureStamper.StampDefenseChest(w, surf, minX: -2, minZ: -2, maxX: 2, maxZ: 2);

        await Assert.That(w.GetBlock(0, 20, 0).Id).IsEqualTo(Blocks.Chest);    // on the ground's own surface
        await Assert.That(w.GetBlock(0, 21, 0).Id).IsEqualTo(Blocks.Air);      // the lid's own air
        await Assert.That(w.GetBlock(1, 20, 0).Id).IsEqualTo(Blocks.Air);      // one chest, not a row
    }

    /// <summary><b>The plate and the chest are two stamps, because only one of them is a destroyable's.</b>
    /// A core is won by digging under it until its lava leaks, so bedrock at a fixed depth is a floor laid
    /// across the objective's own rules (the author's ruling) — but every goal a team defends is worth
    /// supplying, so the chest is stamped under both.</summary>
    [Test]
    public async Task The_plate_lays_no_chest_and_the_chest_lays_no_plate()
    {
        var surf = FlatSurface(-10, -10, 10, 10, top: 20);

        var plateOnly = new VoxelWorld();
        StructureStamper.StampPlatform(plateOnly, surf, minX: -2, minZ: -2, maxX: 2, maxZ: 2);
        await Assert.That(plateOnly.GetBlock(0, 16, 0).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(plateOnly.GetBlock(0, 20, 0).Id).IsEqualTo(Blocks.Air)
            .Because("a core takes this stamp's opposite number and no plate");

        var chestOnly = new VoxelWorld();
        StructureStamper.StampDefenseChest(chestOnly, surf, minX: -2, minZ: -2, maxX: 2, maxZ: 2);
        await Assert.That(chestOnly.GetBlock(0, 20, 0).Id).IsEqualTo(Blocks.Chest);
        await Assert.That(chestOnly.GetBlock(0, 16, 0).Id).IsEqualTo(Blocks.Air)
            .Because("the terrain under a core is the dig its float/leak pair asks for");
    }

    [Test]
    public async Task Platform_noops_when_the_terrain_is_too_shallow_to_bury_a_course_under()
    {
        var w = new VoxelWorld();
        var surf = FlatSurface(-10, -10, 10, 10, top: 3);   // ground surface at y=2 — nothing to bury under
        StructureStamper.StampPlatform(w, surf, minX: -2, minZ: -2, maxX: 2, maxZ: 2);

        await Assert.That(w.GetBlock(0, 0, 0).Id).IsEqualTo(Blocks.Air);
        await Assert.That(w.IsEmpty).IsTrue();      // no plate means no chest either
    }
}
