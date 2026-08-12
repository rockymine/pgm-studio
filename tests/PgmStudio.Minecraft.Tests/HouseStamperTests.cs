using PgmStudio.Domain;
using PgmStudio.Geom.Algorithms;

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

        await Assert.That(Highest(world, 11, 9) - FloorY).IsEqualTo(style.TopLayerOver(11, 9));
    }

    [Test]
    [Arguments(RoofForm.Gable, RoomEdge.NegZ)]
    [Arguments(RoofForm.Gable, RoomEdge.NegX)]
    [Arguments(RoofForm.Hip, RoomEdge.PosX)]
    [Arguments(RoofForm.Shed, RoomEdge.NegZ)]
    [Arguments(RoofForm.Shed, RoomEdge.NegX)]
    [Arguments(RoofForm.Shed, RoomEdge.PosX)]
    [Arguments(RoofForm.Saltbox, RoomEdge.NegZ)]
    [Arguments(RoofForm.Saltbox, RoomEdge.PosX)]
    [Arguments(RoofForm.Gambrel, RoomEdge.NegX)]
    public async Task The_reserved_height_follows_the_wall_the_doors_are_cut_through(RoofForm form, RoomEdge front)
    {
        // A shed and a saltbox fall toward the front, so their ridge climbs with the span perpendicular to that
        // wall — on this footprint 11 one way and 9 the other. Reserving against a fixed edge is short by the
        // difference, and a roof reserved short is clipped at the world ceiling instead of lowered. The
        // symmetric forms are here to hold the other half of the claim: that the front changes nothing for them.
        var style = new HouseStyle { Form = form, Pitch = 2 };
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, 0, 0, 11, 9, FloorY, style, doors: [new RoomDoor(front, 4, 2)]);

        await Assert.That(Highest(world, 11, 9) - FloorY).IsEqualTo(style.TopLayerOver(11, 9, front));
    }

    [Test]
    [Arguments(RoofForm.Gable)]
    [Arguments(RoofForm.Hip)]
    [Arguments(RoofForm.Gambrel)]
    [Arguments(RoofForm.Shed)]
    [Arguments(RoofForm.Saltbox)]
    public async Task No_roof_climbs_with_the_long_side_of_a_building(RoofForm form)
    {
        // The same hall, roofed from an end wall and then from a side one. A gable, a hip and a gambrel are
        // measured across the shorter side and never saw the long one; a shed and a saltbox fall toward the
        // front, so before they were held to the short side the first of these stood three times the second.
        var style = new HouseStyle { Form = form, Pitch = 2 };
        var fromTheEnd = new VoxelWorld();
        HouseStamper.Stamp(fromTheEnd, 0, 0, 24, 8, FloorY, style, doors: [new RoomDoor(RoomEdge.NegX, 3, 2)]);
        var fromTheSide = new VoxelWorld();
        HouseStamper.Stamp(fromTheSide, 0, 0, 24, 8, FloorY, style, doors: [new RoomDoor(RoomEdge.NegZ, 3, 2)]);

        await Assert.That(Highest(fromTheEnd, 24, 8)).IsEqualTo(Highest(fromTheSide, 24, 8));

        // And the height that survives is the short side's, not the long one's: a 24x8 hall is never taller
        // than the same roof over an 8x8 shed of it.
        var square = new VoxelWorld();
        HouseStamper.Stamp(square, 0, 0, 8, 8, FloorY, style, doors: [new RoomDoor(RoomEdge.NegX, 3, 2)]);
        await Assert.That(Highest(fromTheEnd, 24, 8) - FloorY).IsLessThanOrEqualTo(Highest(square, 8, 8) - FloorY);
    }

    /// <summary>The topmost course any block of the building occupies, read generously around it.</summary>
    private static int Highest(VoxelWorld world, int width, int depth)
    {
        var highest = FloorY;
        for (var x = -4; x < width + 4; x++)
            for (var z = -4; z < depth + 4; z++)
                for (var y = FloorY; y < FloorY + 60; y++)
                    if (world.GetBlock(x, y, z).Id != Blocks.Air) highest = Math.Max(highest, y);
        return highest;
    }

    [Test]
    public async Task A_bound_gable_faces_the_triangle_and_leaves_the_wall_below_it_alone()
    {
        // The classic look the wall's own stack cannot say: a timbered gable over a plain wall.
        var world = House(13, 9, new HouseStyle
        {
            Wall = RoomPart.Of(new SolidMaterial(Blocks.QuartzBlock), 5),
            Gable = new SolidMaterial(Blocks.Obsidian),
            Overhang = 0,
        });

        // The triangle above the wall top is the gable's material...
        await Assert.That(world.GetBlock(0, FloorY + 7, 4).Id).IsEqualTo(Blocks.Obsidian);
        await Assert.That(world.GetBlock(12, FloorY + 7, 4).Id).IsEqualTo(Blocks.Obsidian);
        // ...and the wall below it is untouched, gable and wall being different parts of one building.
        await Assert.That(world.GetBlock(0, FloorY + 3, 4).Id).IsEqualTo(Blocks.QuartzBlock);
        // Nothing of it lands on the long walls, which a gable roof does not leave a triangle on.
        await Assert.That(world.GetBlock(6, FloorY + 7, 0).Id).IsNotEqualTo(Blocks.Obsidian);
    }

    [Test]
    public async Task An_unbound_gable_is_the_walls_top_course_and_not_the_next_one_in_its_stack()
    {
        // A stack longer than the wall it fills: the gable takes what actually tops the wall, not the course
        // the stack would have gone on to had the wall been taller. Naming the part changed nothing here — a
        // style that never binds it builds exactly what it always did.
        var stack = new RoomPart(
            [new RoomCourse(new SolidMaterial(Blocks.Cobblestone)),
             new RoomCourse(new SolidMaterial(Blocks.QuartzBlock)),
             new RoomCourse(new SolidMaterial(Blocks.Obsidian))], 2);
        var world = House(13, 9, new HouseStyle { Wall = stack, Post = null, Overhang = 0 });

        await Assert.That(world.GetBlock(0, FloorY + 2, 4).Id).IsEqualTo(Blocks.QuartzBlock);   // the wall's top
        await Assert.That(world.GetBlock(0, FloorY + 4, 4).Id).IsEqualTo(Blocks.QuartzBlock);   // the gable
        await Assert.That(world.GetBlock(0, FloorY + 4, 4).Id).IsNotEqualTo(Blocks.Obsidian);
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
    public async Task A_porch_sits_at_porch_height_however_tall_the_building_behind_it_is()
    {
        // The canopy is seated by its own lowest course clearing the doorway, not by the eave overhead. Seating
        // it under the eave is the same answer on a house and absurd on a tower, where it would ride the wall
        // up and leave a colonnade over a door open to the sky.
        static int CanopyTop(int wallExtent)
        {
            var world = House(13, 11, new HouseStyle
            {
                Wall = RoomPart.Of(new SolidMaterial(Blocks.Cobblestone), wallExtent),
                Porch = new PorchStyle { Depth = 2 },
                Door = DoorMaterial.Air,
            });
            var top = 0;
            for (var x = 1; x < 12; x++)
                for (var y = FloorY + 1; y < FloorY + 40; y++)
                    if (world.GetBlock(x, y, 0).Id != Blocks.Air) top = Math.Max(top, y - FloorY);
            return top;
        }

        // A five-course house and a twenty-four-course tower wear the same porch at the same height.
        var house = CanopyTop(5);
        await Assert.That(house).IsEqualTo(6);
        foreach (var extent in new[] { 3, 8, 12, 24 })
            await Assert.That(CanopyTop(extent)).IsEqualTo(house);
    }

    [Test]
    public async Task A_porch_leaves_the_way_out_of_its_doorway_walkable()
    {
        // The invariant the seating rule exists for, at every wall height: the two courses a player occupies
        // stepping out of the door and off the deck are clear of the canopy over them.
        foreach (var extent in new[] { 3, 4, 5, 8, 24 })
        {
            var world = House(13, 11, new HouseStyle
            {
                Wall = RoomPart.Of(new SolidMaterial(Blocks.Cobblestone), extent),
                Porch = new PorchStyle { Depth = 2 },
                Door = DoorMaterial.Air,
            });
            for (var z = 0; z <= 2; z++)                       // off the deck, across it, and the doorway itself
                for (var y = FloorY + 1; y <= FloorY + 2; y++)
                    await Assert.That(world.GetBlock(6, y, z).Id).IsEqualTo(Blocks.Air);
        }
    }

    [Test]
    public async Task A_tower_is_a_tall_wall_and_nothing_else()
    {
        // Height is the wall part's extent and its last course repeats, so a tower needs no form of its own —
        // and a hip over a square footprint comes to a point, which is the cap one wants.
        var style = new HouseStyle
        {
            Wall = RoomPart.Of(new SolidMaterial(Blocks.Cobblestone), 24),
            Form = RoofForm.Hip, Door = DoorMaterial.StainedGlass,
        };
        var world = House(5, 5, style);

        await Assert.That(Leaks(world, 5, 5)).IsFalse();

        var highest = FloorY;
        for (var x = -3; x < 8; x++)
            for (var z = -3; z < 8; z++)
                for (var y = FloorY; y < FloorY + 40; y++)
                    if (world.GetBlock(x, y, z).Id != Blocks.Air) highest = Math.Max(highest, y);
        await Assert.That(highest - FloorY).IsEqualTo(style.TopLayerOver(5, 5));
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

    [Test]
    public async Task The_beams_run_two_log_ends_out_of_every_corner()
    {
        // In plan the seam reads as a hash: the walls are the square in the middle and eight ends stand
        // outside it, one along each axis at each of the four corners. Each shows its sawn end, which is what
        // the end of a log is.
        var style = new HouseStyle
        {
            Storeys = [new Storey { Clear = 3 }, new Storey { Clear = 3 }],
            Beams = new BeamStyle { Block = Blocks.Log, Data = 0, Reach = 1 },
        };
        var world = House(7, 9, style);
        var seam = FloorY + 4;                                  // three clear plus the slab over them

        var ends = 0;
        for (var x = -2; x < 9; x++)
            for (var z = -2; z < 11; z++)
            {
                var outside = x is < 0 or > 6 || z is < 0 or > 8;
                if (world.GetBlock(x, seam, z).Id != Blocks.Log || !outside) continue;
                ends++;
                // Lying along the axis it runs out on, so the face pointing away from the building is sawn.
                var axis = world.GetBlock(x, seam, z).Data >> 2;
                await Assert.That(axis).IsEqualTo(x is < 0 or > 6 ? 1 : 2);
            }
        await Assert.That(ends).IsEqualTo(8);
    }

    [Test]
    public async Task A_building_that_asks_for_no_beams_writes_nothing_outside_its_walls()
    {
        // The beams are the one thing a house lays past its own footprint, so a style naming none has to leave
        // the ring around it exactly as it found it — that is the invariant the rest of the stamper keeps.
        var style = new HouseStyle { Storeys = [new Storey { Clear = 3 }, new Storey { Clear = 3 }], Overhang = 0 };
        var world = House(7, 9, style);

        for (var x = -1; x < 8; x++)
            for (var z = -1; z < 10; z++)
                if (x is < 0 or > 6 || z is < 0 or > 8)
                    for (var y = FloorY + 1; y < FloorY + 20; y++)
                        await Assert.That(world.GetBlock(x, y, z).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task A_gable_window_is_cut_once_per_face_and_only_where_there_is_a_gable_to_cut()
    {
        // A gable is a triangle, so the middle is the one place a window certainly fits — one per face and
        // centred, where a wall takes as many as its run will hold.
        var style = new HouseStyle
        {
            Form = RoofForm.Gable, Pitch = 2, Wall = RoomPart.Of(new SolidMaterial(Blocks.Planks), 5),
            GableWindows = new WindowStyle { Form = WindowForm.Pane, Block = Blocks.GlassPane, Width = 2, Height = 2, Sill = 1 },
        };
        var world = House(11, 9, style);
        var wallTop = FloorY + 5;

        // The slope is taken across the shorter side, which here is z — so the two z walls are the eaves, the
        // ridge runs along x, and the gable triangles stand on the two x walls. A pane on an eave wall would
        // be a window cut into thin air above it.
        var onGables = Panes(world, RoomEdge.NegX, 11, 9, wallTop) + Panes(world, RoomEdge.PosX, 11, 9, wallTop);
        var onSlopes = Panes(world, RoomEdge.NegZ, 11, 9, wallTop) + Panes(world, RoomEdge.PosZ, 11, 9, wallTop);

        await Assert.That(onGables).IsEqualTo(8);      // two faces, 2x2 each
        await Assert.That(onSlopes).IsEqualTo(0);
    }

    [Test]
    public async Task A_small_gable_takes_a_small_window_and_refuses_a_larger_one()
    {
        // A gable narrows as it rises, so a window that technically lands on gable can still run hard into the
        // slope above it and leave the face as a hole with a rim. This face is five cells at its base and three
        // a course up: it carries one cell in the middle and not a two-by-two.
        var wall = RoomPart.Of(new SolidMaterial(Blocks.Planks), 5);
        var open = new WindowStyle { Form = WindowForm.Open, Width = 1, Height = 1, Sill = 2 };
        var wallTop = FloorY + 5;

        var small = House(7, 9, new HouseStyle { Form = RoofForm.Gable, Pitch = 1, Wall = wall, GableWindows = open });
        var large = House(7, 9, new HouseStyle
        {
            Form = RoofForm.Gable, Pitch = 1, Wall = wall,
            GableWindows = open with { Width = 2, Height = 2, Sill = 1 },
        });

        // The one cell it does carry, dead centre of the triangle with gable either side of it.
        await Assert.That(small.GetBlock(3, wallTop + 2, 0).Id).IsEqualTo(Blocks.Air);
        await Assert.That(small.GetBlock(2, wallTop + 2, 0).Id).IsNotEqualTo(Blocks.Air);
        await Assert.That(small.GetBlock(4, wallTop + 2, 0).Id).IsNotEqualTo(Blocks.Air);

        // And the one it does not: the face is left whole rather than opened at all.
        foreach (var along in new[] { 3, 4 })
            foreach (var course in new[] { 1, 2 })
                await Assert.That(large.GetBlock(along, wallTop + course, 0).Id).IsNotEqualTo(Blocks.Air);
    }

    [Test]
    public async Task A_gable_too_shallow_to_hold_the_window_is_left_whole()
    {
        // Asking for one on a hip, which leaves no gable anywhere, cuts nothing rather than cutting a hole in
        // the wall below where the gable would have been.
        var style = new HouseStyle
        {
            Form = RoofForm.Hip, Pitch = 1, Wall = RoomPart.Of(new SolidMaterial(Blocks.Planks), 5),
            GableWindows = new WindowStyle { Form = WindowForm.Pane, Block = Blocks.GlassPane, Width = 2, Height = 2, Sill = 1 },
        };
        var world = House(11, 9, style);
        var wallTop = FloorY + 5;
        foreach (var edge in new[] { RoomEdge.NegZ, RoomEdge.PosZ, RoomEdge.NegX, RoomEdge.PosX })
            await Assert.That(Panes(world, edge, 11, 9, wallTop)).IsEqualTo(0);
    }

    /// <summary>How many panes stand in one face above the wall top — the gable's own courses.</summary>
    private static int Panes(VoxelWorld world, RoomEdge edge, int width, int depth, int wallTop)
    {
        var found = 0;
        for (var along = 0; along < (edge is RoomEdge.NegZ or RoomEdge.PosZ ? width : depth); along++)
            for (var y = wallTop + 1; y < wallTop + 12; y++)
            {
                var (x, z) = edge switch
                {
                    RoomEdge.NegZ => (along, 0),
                    RoomEdge.PosZ => (along, depth - 1),
                    RoomEdge.NegX => (0, along),
                    _ => (width - 1, along),
                };
                if (world.GetBlock(x, y, z).Id == Blocks.GlassPane) found++;
            }
        return found;
    }

    [Test]
    public async Task A_self_cut_door_keeps_a_block_of_wall_clear_of_both_posts()
    {
        // A door hard against the corner post reads as a hole knocked through the frame rather than a way in.
        var world = House(9, 7, new HouseStyle { Post = new SolidMaterial(Blocks.Log), DoorWidth = 2 });

        // The door is cut on a long side, so it runs along x. Its cells are air; the two beside the posts are
        // not.
        var open = Enumerable.Range(0, 9).Where(x => world.GetBlock(x, FloorY + 1, 0).Id == Blocks.Air).ToList();
        await Assert.That(open).IsNotEmpty();
        await Assert.That(open.Min()).IsGreaterThanOrEqualTo(2);
        await Assert.That(open.Max()).IsLessThanOrEqualTo(6);
    }

    [Test]
    public async Task A_face_too_narrow_for_a_two_wide_door_narrows_the_door_rather_than_the_margin()
    {
        // Five across leaves one cell once both margins are kept, so the opening narrows to it. The door is
        // what gives way, never the block of wall carrying the post — a narrow shed says it is narrow.
        var world = House(5, 5, new HouseStyle { DoorWidth = 2 });
        var open = Enumerable.Range(0, 5).Where(x => world.GetBlock(x, FloorY + 1, 0).Id == Blocks.Air).ToList();

        await Assert.That(open).IsEquivalentTo(new[] { 2 });
    }

    [Test]
    public async Task A_door_on_a_narrow_wall_still_clears_the_post()
    {
        // The case that started this: a two-wide door on a five-wide face used to sit at cells 2 and 3, and
        // cell 3 is hard against the post at 4.
        // Depth five so every width here is the long side and the house fronts on z — a house fronts on its
        // longer side, and a door on the other wall would simply not be at z = 0 to look for.
        foreach (var width in new[] { 5, 6, 7, 9, 11 })
        {
            var world = House(width, 5, new HouseStyle { DoorWidth = 2 });
            var open = Enumerable.Range(0, width)
                .Where(x => world.GetBlock(x, FloorY + 1, 0).Id == Blocks.Air).ToList();

            await Assert.That(open).IsNotEmpty();
            await Assert.That(open.Min()).IsGreaterThanOrEqualTo(2);
            await Assert.That(open.Max()).IsLessThanOrEqualTo(width - 3);
        }
    }

    /// <summary>A frame's door is the entry contract and keeps the wall the frame chose — but a frame knows the
    /// room and not the building, so where the style stands a post the opening is still fitted clear of it. This
    /// is the path a library preview and a wool room take, and it was the one seating doors against the post.</summary>
    [Test]
    [Arguments(7)]
    [Arguments(9)]
    [Arguments(11)]
    public async Task A_handed_in_door_clears_the_post_a_frame_knew_nothing_about(int width)
    {
        var style = new HouseStyle { Post = new SolidMaterial(Blocks.Log), DoorWidth = 2 };
        var world = new VoxelWorld();
        // Hard against the corner cell, which is where a frame that never heard of a post would put it.
        HouseStamper.Stamp(world, 0, 0, width, 7, FloorY, style, doors: [new RoomDoor(RoomEdge.NegZ, 1, 2)]);

        var open = Enumerable.Range(0, width)
            .Where(x => world.GetBlock(x, FloorY + 1, 0).Id == Blocks.Air).ToList();
        await Assert.That(open).IsNotEmpty();
        await Assert.That(open.Min()).IsGreaterThanOrEqualTo(2);
    }

    /// <summary>And a building with <b>no</b> post keeps the same margin, because the margin is not the post's.
    /// A corner is where two walls meet and turn, and an opening hard against that turn reads as a wall that
    /// failed rather than as a way through, in a plain shell exactly as in a framed house. Making it conditional
    /// would also mean one building gained and lost the margin as its corners were bound and unbound, which is a
    /// style deciding where a door goes.</summary>
    [Test]
    public async Task A_postless_shell_keeps_the_same_margin_as_a_framed_one()
    {
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, 0, 0, 9, 7, FloorY, new HouseStyle { Post = null },
            doors: [new RoomDoor(RoomEdge.NegZ, 1, 2)]);

        var open = Enumerable.Range(0, 9)
            .Where(x => world.GetBlock(x, FloorY + 1, 0).Id == Blocks.Air).ToList();
        await Assert.That(open).IsNotEmpty();
        await Assert.That(open.Min()).IsGreaterThanOrEqualTo(2);
        await Assert.That(8 - open.Max()).IsGreaterThanOrEqualTo(2);
    }

    /// <summary>The margin a wool cage's own doors already keep is the one this rule asks for, so making it
    /// general costs them nothing. WX7 holds a door to at least one block narrower than the interior on each
    /// side, which is exactly the seat run — so a frame's door fits it without being narrowed, and only one
    /// pushed hard against a corner moves at all.</summary>
    [Test]
    [Arguments(6, 2)]
    [Arguments(8, 4)]
    [Arguments(9, 3)]
    [Arguments(10, 4)]
    public async Task A_frame_door_sized_by_WX7_is_never_narrowed_by_the_margin(int span, int doorWidth)
    {
        var world = new VoxelWorld();
        // Centred on the wall, which is where a frame puts a door it has room for.
        var lo = (span - doorWidth) / 2;
        HouseStamper.Stamp(world, 0, 0, span, 7, FloorY, new HouseStyle { Post = null },
            doors: [new RoomDoor(RoomEdge.NegZ, lo, doorWidth)]);

        var open = Enumerable.Range(0, span)
            .Where(x => world.GetBlock(x, FloorY + 1, 0).Id == Blocks.Air).ToList();
        await Assert.That(open.Count).IsEqualTo(doorWidth);
        await Assert.That(open.Min()).IsEqualTo(lo);
    }

    /// <summary>A style may name the wall it fronts on, and it outranks the proportions. It is what lets a hall
    /// be entered at its gable end so the row of windows down its long walls survives — windows and doorways are
    /// both centred on a wall's run, and a seat a door meets is dropped rather than shifted.</summary>
    [Test]
    public async Task A_style_may_choose_the_wall_it_fronts_on()
    {
        var hall = new HouseStyle { DoorEdge = RoomEdge.NegX, DoorWidth = 2 };
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, 0, 0, 21, 9, FloorY, hall);

        // The long side is where the proportions would have put it, and it is whole.
        var longSide = Enumerable.Range(0, 21)
            .Where(x => world.GetBlock(x, FloorY + 1, 0).Id == Blocks.Air).ToList();
        await Assert.That(longSide).IsEmpty();

        // The gable end carries it instead.
        var end = Enumerable.Range(0, 9)
            .Where(z => world.GetBlock(0, FloorY + 1, z).Id == Blocks.Air).ToList();
        await Assert.That(end).IsNotEmpty();
    }

    /// <summary>A material that inks a cell whose ring bends at least <paramref name="Angle"/> degrees, so a
    /// stamped wall reports the turn it was painted with. It is what <see cref="WallFrameMaterial"/> reads, on
    /// its own: a frame also inks the courses at the top and bottom of a wall, which would ink every cell here
    /// and say nothing about the corner.</summary>
    private sealed record TurnProbe(int Angle) : TerrainMaterial
    {
        public override (int Id, int Data) Resolve(in BucketContext ctx)
            => (ctx.PerimeterTurn >= Angle ? Blocks.Stone : Blocks.Dirt, 0);
    }

    /// <summary>A wall reads the bend at its corners exactly as the terrain beside it reads the same outline —
    /// one walk of one ring, so a building and the plateau it stands on cannot frame differently.
    ///
    /// <para>The narrow spans are the ones that carry it. A closed form sees only the corner nearest a cell, so
    /// on a wall shorter than twice the measuring window it reports one bend where the ring turns two: over a
    /// five-wide side it disagrees with the walk by 57° at the middle, and a five-deep house then frames six of
    /// its fourteen inked cells differently at a threshold of ninety. The wider spans hold the other half of
    /// the claim — that reading the ring off a walk leaves an ordinary building's corners where they were.</para>
    /// </summary>
    [Test]
    [Arguments(11, 5, 60)]      // both corners of the short side inside one window
    [Arguments(11, 5, 90)]
    [Arguments(7, 9, 45)]
    [Arguments(7, 9, 60)]
    [Arguments(21, 9, 45)]
    [Arguments(13, 13, 45)]
    [Arguments(9, 9, 60)]
    public async Task A_wall_bends_where_the_traced_ring_does(int width, int depth, int angle)
    {
        var style = new HouseStyle { Wall = RoomPart.Of(new TurnProbe(angle), 5), Post = null };
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, 0, 0, width, depth, FloorY, style);

        // Any course of the wall answers, so a cell the doorway took lower down is still read at the top.
        var inked = new HashSet<(int X, int Z)>();
        foreach (var (x, z) in Perimeter(width, depth))
            for (var y = FloorY + 1; y <= FloorY + style.WallCourses; y++)
                if (world.GetBlock(x, y, z).Id == Blocks.Stone) inked.Add((x, z));

        var ring = GridBoundary.TracePerimeter(Cells(width, depth));
        var expected = GridBoundary.Turns(ring, GridBoundary.CornerWindow)
            .Where(cell => (int)Math.Round(cell.Value) >= angle)
            .Select(cell => cell.Key).ToHashSet();

        await Assert.That(inked.OrderBy(c => c.X).ThenBy(c => c.Z))
            .IsEquivalentTo(expected.OrderBy(c => c.X).ThenBy(c => c.Z));
    }

    private static IEnumerable<(int X, int Z)> Cells(int width, int depth)
    {
        for (var x = 0; x < width; x++)
            for (var z = 0; z < depth; z++)
                yield return (x, z);
    }

    private static IEnumerable<(int X, int Z)> Perimeter(int width, int depth)
        => Cells(width, depth).Where(c => c.X == 0 || c.X == width - 1 || c.Z == 0 || c.Z == depth - 1);
}
