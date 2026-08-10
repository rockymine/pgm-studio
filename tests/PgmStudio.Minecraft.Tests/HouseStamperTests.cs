using PgmStudio.Domain;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The house shell: a sill, posted walls, a gabled roof and a doorway. The load-bearing assertion is that the
/// building is <b>closed</b> — a roof drawn as one block per plan cell is easy to leave gaps in, and a gap is
/// invisible in a block count but obvious standing under it, so closure is tested by trying to escape the
/// house rather than by counting what was placed.
/// </summary>
public sealed class HouseStamperTests
{
    private const int FloorY = 64;

    private static VoxelWorld House(int width, int depth, HouseStyle? style = null)
    {
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, 0, 0, width, depth, FloorY, style ?? new HouseStyle());
        return world;
    }

    /// <summary>Whether air escapes the house from the inside — the test of a shell. Walks air in six
    /// directions from a cell just above the floor and reports whether it ever reaches beyond a box drawn
    /// generously around the building.</summary>
    private static bool Leaks(VoxelWorld world, int width, int depth, int reach = 6)
    {
        var start = (width / 2, FloorY + 1, depth / 2);
        var seen = new HashSet<(int X, int Y, int Z)> { start };
        var queue = new Queue<(int X, int Y, int Z)>([start]);
        while (queue.Count > 0)
        {
            var (x, y, z) = queue.Dequeue();
            if (x < -reach || x > width + reach || z < -reach || z > depth + reach || y > FloorY + 30) return true;
            foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
            {
                var next = (x + dx, y + dy, z + dz);
                if (world.GetBlock(next.Item1, next.Item2, next.Item3).Id != Blocks.Air) continue;
                if (seen.Add(next)) queue.Enqueue(next);
            }
        }
        return false;
    }

    [Test]
    [Arguments(11, 9, 1)]
    [Arguments(11, 10, 1)]      // even span — the ridge is two blocks wide
    [Arguments(11, 9, 2)]       // a steep pitch steps two courses at a time
    [Arguments(7, 13, 1)]       // deeper than wide — the ridge runs the other way
    [Arguments(5, 5, 1)]
    [Arguments(3, 3, 1)]        // the smallest house there is
    public async Task A_house_with_its_door_filled_is_sealed(int width, int depth, int pitch)
    {
        // A doorway is a hole on purpose, so closure is asked of a house whose door is glazed.
        var world = House(width, depth, new HouseStyle { Pitch = pitch, Door = DoorMaterial.StainedGlass });
        await Assert.That(Leaks(world, width, depth)).IsFalse();
    }

    [Test]
    public async Task An_open_doorway_is_the_only_way_out()
    {
        // The same house with the door open must leak, or the doorway was never cut.
        var world = House(11, 9, new HouseStyle { Door = DoorMaterial.Air });
        await Assert.That(Leaks(world, 11, 9)).IsTrue();
    }

    [Test]
    public async Task The_four_corners_are_posts_and_the_sides_between_them_are_not()
    {
        var world = House(11, 9);
        foreach (var (x, z) in new[] { (0, 0), (10, 0), (0, 8), (10, 8) })
            await Assert.That(world.GetBlock(x, FloorY + 2, z).Id).IsEqualTo(Blocks.Log);

        await Assert.That(world.GetBlock(2, FloorY + 2, 0).Id).IsNotEqualTo(Blocks.Log);   // wall, clear of the door
        await Assert.That(world.GetBlock(0, FloorY + 2, 4).Id).IsNotEqualTo(Blocks.Log);
    }

    [Test]
    public async Task The_ridge_is_one_block_over_an_odd_span_and_two_over_an_even_one()
    {
        static int RidgeWidth(int depth)
        {
            var world = House(11, depth);
            var top = 0;
            for (var y = FloorY; y < FloorY + 30; y++)
                for (var z = -2; z < depth + 2; z++)
                    if (world.GetBlock(5, y, z).Id != Blocks.Air) top = Math.Max(top, y);
            var count = 0;
            for (var z = -2; z < depth + 2; z++) if (world.GetBlock(5, top, z).Id != Blocks.Air) count++;
            return count;
        }

        await Assert.That(RidgeWidth(9)).IsEqualTo(1);
        await Assert.That(RidgeWidth(10)).IsEqualTo(2);
    }

    [Test]
    [Arguments(1, 1)]
    [Arguments(1, 2)]
    [Arguments(2, 1)]
    public async Task The_slope_falls_by_its_pitch_every_block_out_including_the_overhang(int pitch, int overhang)
    {
        // The property a pair of magic coordinates cannot state: a gable is one angle from ridge to eave, and
        // the overhang is part of the slope rather than a lip tacked onto the end of it. Levelling the last
        // course with the wall line — the obvious way to seat the roof on the wall — runs the edge flat and is
        // exactly the defect this catches.
        var world = House(11, 9, new HouseStyle { Pitch = pitch, Overhang = overhang });

        int? RoofTop(int z)
        {
            for (var y = FloorY + 25; y > FloorY; y--)
                if (world.GetBlock(5, y, z).Id != Blocks.Air) return y;
            return null;
        }

        var climb = new List<int>();
        for (var z = -overhang; z <= 4; z++)          // outer eave to ridge, along the middle of the building
        {
            var top = RoofTop(z);
            await Assert.That(top).IsNotNull();
            climb.Add(top!.Value);
        }
        for (var step = 1; step < climb.Count; step++)
            await Assert.That(climb[step] - climb[step - 1]).IsEqualTo(pitch);

        await Assert.That(RoofTop(-overhang - 1)).IsNull();     // and nothing past the eave
    }

    [Test]
    public async Task Nothing_is_written_below_the_sill()
    {
        var world = House(11, 9);
        for (var x = -3; x < 14; x++)
            for (var z = -3; z < 12; z++)
                await Assert.That(world.GetBlock(x, FloorY - 1, z).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task A_footprint_too_small_to_hold_walls_and_an_inside_builds_nothing()
    {
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, 0, 0, 2, 9, FloorY, new HouseStyle());
        for (var x = -2; x < 5; x++)
            for (var y = FloorY - 1; y < FloorY + 12; y++)
                for (var z = -2; z < 12; z++)
                    await Assert.That(world.GetBlock(x, y, z).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    [Arguments(11, 9)]
    [Arguments(11, 10)]
    [Arguments(7, 13)]
    public async Task A_slope_that_climbs_a_whole_block_is_laid_in_whole_blocks(int width, int depth)
    {
        // The defect a flood fill cannot see. A slab is a solid block to anything that walks air, but it fills
        // only the lower half of its cube, so a course of slabs stepping up a whole block leaves an open half
        // between every pair and the roof can be seen through. Asked of fullness, not of occupancy.
        var world = House(width, depth);
        for (var x = -2; x < width + 2; x++)
            for (var z = -2; z < depth + 2; z++)
                for (var y = FloorY + 1; y < FloorY + 20; y++)
                {
                    var id = world.GetBlock(x, y, z).Id;
                    if (id == Blocks.Air) continue;
                    if (y <= FloorY + new HouseStyle().WallHeight) continue;      // walls and gable, not roof
                    await Assert.That(BlockRoles.IsFullCube(id)).IsTrue();
                }
    }

    [Test]
    public async Task The_doorway_is_two_wide_and_three_tall_however_it_is_asked_for()
    {
        // A single-width opening is a gap in a wall, not a door — a room an objective is carried out of has to
        // be walked through. Asking for less gets the floor, not the number asked for.
        var world = House(11, 9, new HouseStyle { DoorWidth = 1, DoorHeight = 1 });

        var open = new List<(int X, int Y)>();
        for (var x = 1; x < 10; x++)
            for (var y = FloorY + 1; y <= FloorY + 4; y++)
                if (world.GetBlock(x, y, 0).Id == Blocks.Air) open.Add((x, y));

        await Assert.That(open.Select(cell => cell.X).Distinct().Count()).IsEqualTo(2);
        await Assert.That(open.Select(cell => cell.Y).Distinct().Count()).IsEqualTo(3);
    }
}
