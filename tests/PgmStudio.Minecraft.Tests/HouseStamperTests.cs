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
                    if (y <= FloorY + new HouseStyle().Wall.Extent) continue;      // walls and gable, not roof
                    await Assert.That(BlockRoles.IsFullCube(id)).IsTrue();
                }
    }

    [Test]
    [Arguments(RoofForm.Flat)]
    [Arguments(RoofForm.Gable)]
    [Arguments(RoofForm.Hip)]
    [Arguments(RoofForm.Shed)]
    [Arguments(RoofForm.Gambrel)]
    [Arguments(RoofForm.Saltbox)]
    public async Task Every_roof_form_closes_the_building_under_it(RoofForm form)
    {
        // The claim the height field is worth having for: six formulas, one loop, and none of them leaves the
        // house open. A hole is invisible in a block count and obvious standing under it, so closure is tested
        // by trying to escape rather than by counting what was placed.
        var world = House(11, 9, new HouseStyle
        {
            Form = form, Door = DoorMaterial.StainedGlass, RoofHole = false, Overhang = 2,
        });
        await Assert.That(Leaks(world, 11, 9)).IsFalse();
    }

    [Test]
    [Arguments(RoofForm.Gable, 1)]
    [Arguments(RoofForm.Hip, 1)]
    [Arguments(RoofForm.Shed, 1)]
    [Arguments(RoofForm.Gambrel, 2)]
    [Arguments(RoofForm.Saltbox, 1)]
    public async Task The_height_a_style_reserves_is_the_height_it_actually_builds_to(RoofForm form, int pitch)
    {
        // A caller clamps headroom against this and a preview draws to it, so a form whose reserved height and
        // built height disagree is a house cut off at the top of its own card.
        var style = new HouseStyle { Form = form, Pitch = pitch };
        var world = House(11, 9, style);

        var highest = FloorY;
        for (var x = -4; x < 15; x++)
            for (var z = -4; z < 13; z++)
                for (var y = FloorY; y < FloorY + 40; y++)
                    if (world.GetBlock(x, y, z).Id != Blocks.Air) highest = Math.Max(highest, y);

        await Assert.That(highest - FloorY).IsEqualTo(style.TopLayerOver(11, 9));
    }

    [Test]
    public async Task A_ridge_cap_lays_the_line_the_slopes_meet_on_in_the_verge()
    {
        var capped = House(11, 9, new HouseStyle { RidgeCap = true, Verge = new SolidMaterial(Blocks.Obsidian) });
        var plain = House(11, 9, new HouseStyle { Verge = new SolidMaterial(Blocks.Obsidian) });

        var ridge = FloorY + new HouseStyle().TopLayerOver(11, 9);
        await Assert.That(capped.GetBlock(5, ridge, 4).Id).IsEqualTo(Blocks.Obsidian);
        await Assert.That(plain.GetBlock(5, ridge, 4).Id).IsNotEqualTo(Blocks.Obsidian);
    }

    [Test]
    public async Task A_floor_is_zoned_from_the_walls_inward()
    {
        var world = House(13, 13, new HouseStyle
        {
            Surface = new FloorSurface
            {
                Border = new SolidMaterial(Blocks.Obsidian),
                Field = new SolidMaterial(Blocks.QuartzBlock),
                Inlay = new SolidMaterial(Blocks.GoldBlock),
                BorderWidth = 2,
                InlayInset = 4,
            },
        });

        // Ring 0 is the wall line and is the floor part's, not the surface's: the walls stand on it.
        await Assert.That(world.GetBlock(0, FloorY, 6).Id).IsNotEqualTo(Blocks.Obsidian);
        await Assert.That(world.GetBlock(2, FloorY, 6).Id).IsEqualTo(Blocks.Obsidian);      // ring 2 — border
        await Assert.That(world.GetBlock(3, FloorY, 6).Id).IsEqualTo(Blocks.QuartzBlock);   // ring 3 — field
        await Assert.That(world.GetBlock(6, FloorY, 6).Id).IsEqualTo(Blocks.GoldBlock);     // ring 6 — inlay
    }

    [Test]
    public async Task A_style_with_no_floor_zones_leaves_the_floor_part_showing()
    {
        var world = House(11, 9);
        var (id, _) = new HouseStyle().Floor.At(0).Material.Resolve(
            new BucketContext(5, FloorY, 4, TerrainBucket.Fill, 0));
        await Assert.That(world.GetBlock(5, FloorY, 4).Id).IsEqualTo(id);
    }

    [Test]
    public async Task A_porch_is_taken_out_of_the_footprint_and_never_added_to_it()
    {
        // The footprint comes from the piece and a style may not change it (WX1), so the walls stand back and
        // the strip they gave up becomes the deck. Nothing is written outside the footprint but the sill.
        var world = House(13, 11, new HouseStyle { Porch = new PorchStyle { Depth = 3 } });

        var standing = 0;
        for (var x = 0; x < 13; x++)
        {
            if (world.GetBlock(x, FloorY + 3, 0).Id != Blocks.Air) standing++;              // the old front wall
            await Assert.That(world.GetBlock(x, FloorY, 0).Id).IsNotEqualTo(Blocks.Air);    // still floored
        }
        // Only the deck's posts stand on that line now — a 13-wide deck carries four, none of them a wall.
        await Assert.That(standing).IsEqualTo(4);
        // The wall now stands three blocks back, on the deck's inner line — read clear of the doorway,
        // which is centred on it.
        await Assert.That(world.GetBlock(2, FloorY + 3, 3).Id).IsNotEqualTo(Blocks.Air);
    }

    [Test]
    public async Task A_porch_carries_the_doorway_onto_the_wall_it_moved()
    {
        var world = House(13, 11, new HouseStyle { Porch = new PorchStyle { Depth = 2 }, Door = DoorMaterial.Air });
        var open = 0;
        for (var x = 1; x < 12; x++) if (world.GetBlock(x, FloorY + 1, 2).Id == Blocks.Air) open++;
        await Assert.That(open).IsEqualTo(2);
    }

    [Test]
    public async Task A_porch_keeps_its_deck_walkable_and_its_rail_open_in_front_of_the_door()
    {
        var style = new HouseStyle { Porch = new PorchStyle { Depth = 2 }, Door = DoorMaterial.Air };
        var world = House(13, 11, style);

        // The canopy clears the doorway it fronts rather than closing it.
        for (var y = FloorY + 1; y <= FloorY + Math.Max(3, style.DoorHeight); y++)
            await Assert.That(world.GetBlock(6, y, 0).Id).IsEqualTo(Blocks.Air);

        // The rail runs the deck's open edges and breaks where the door's own run crosses it.
        await Assert.That(world.GetBlock(3, FloorY + 1, 0).Id).IsEqualTo(Blocks.OakFence);
        await Assert.That(world.GetBlock(6, FloorY + 1, 0).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task A_porch_gives_way_to_the_room_behind_it_rather_than_the_other_way_round()
    {
        // A style asked for a building and a porch; half a building is neither, so the porch is trimmed to
        // what the room can spare — and where the room can spare nothing, there is no porch.
        var deep = House(11, 9, new HouseStyle { Porch = new PorchStyle { Depth = 8 } });
        await Assert.That(deep.GetBlock(1, FloorY + 3, 6).Id).IsNotEqualTo(Blocks.Air);    // walls kept 3 blocks

        var narrow = House(9, 3, new HouseStyle { Porch = new PorchStyle { Depth = 4 }, Door = DoorMaterial.StainedGlass });
        await Assert.That(Leaks(narrow, 9, 3)).IsFalse();
        await Assert.That(narrow.GetBlock(1, FloorY + 2, 0).Id).IsNotEqualTo(Blocks.Air);  // front wall still there
    }

    [Test]
    public async Task A_house_with_windows_and_a_porch_is_still_sealed()
    {
        var world = House(13, 11, new HouseStyle
        {
            Form = RoofForm.Gambrel,
            Door = DoorMaterial.StainedGlass,
            Windows = WindowStyle.Glazed,
            Porch = new PorchStyle { Depth = 2 },
            Surface = new FloorSurface { Border = new SolidMaterial(Blocks.Obsidian) },
        });
        await Assert.That(Leaks(world, 13, 11)).IsFalse();
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
