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
            Foundation = new Foundation
            {
                Surface = new FloorSurface
                {
                    Border = new SolidMaterial(Blocks.Obsidian),
                    Field = new SolidMaterial(Blocks.QuartzBlock),
                    Inlay = new SolidMaterial(Blocks.GoldBlock),
                    BorderWidth = 2,
                    InlayInset = 4,
                },
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
        var (id, _) = new HouseStyle().Foundation.Deck.Resolve(
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
            Foundation = new Foundation { Surface = new FloorSurface { Border = new SolidMaterial(Blocks.Obsidian) } },
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

    /// <summary>The claim <c>DressingScope.StructureFootprints</c> rests on: every block a stamp writes
    /// lands inside <see cref="HouseStamper.StampedExtent"/>, over the overhang, the beam ends and the porch
    /// together — and the wall rectangle alone is <b>not</b> enough, which is the bug this guards against. Proved
    /// by construction rather than by one style: overhang, beam reach and a porch are each varied past the plain
    /// wall footprint, so a caller reading only the wall rectangle would miss real, intentionally-placed blocks
    /// in every case below.</summary>
    [Test]
    [Arguments(1, 0, false)]
    [Arguments(3, 0, false)]      // a deep overhang, no beams
    [Arguments(1, 4, true)]       // beams reaching further than the overhang, plus a porch
    [Arguments(0, 2, false)]      // overhang asked for zero — the sill's own one-block margin still applies
    public async Task Every_block_a_stamp_writes_lands_inside_its_stamped_extent(int overhang, int beamReach, bool porch)
    {
        const int width = 11, depth = 9;
        var style = new HouseStyle
        {
            Overhang = overhang,
            Storeys = [new Storey { Clear = 3 }, new Storey { Clear = 3 }],
            Beams = beamReach > 0 ? new BeamStyle { Block = Blocks.Log, Reach = beamReach } : new BeamStyle { Block = -1 },
            Porch = porch ? new PorchStyle { Depth = 2 } : null,
        };
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, 0, 0, width, depth, FloorY, style);

        var (minX, minZ, maxX, maxZ) = HouseStamper.StampedExtent((0, 0, width - 1, depth - 1), style);

        // The extent is a real bound, not a vacuous one: something stands strictly outside the plain wall
        // rectangle, which is exactly the ground the old claim (the wall rect alone) used to miss.
        var sawOutsideWalls = false;
        for (var x = minX - 3; x <= maxX + 3; x++)
            for (var z = minZ - 3; z <= maxZ + 3; z++)
                for (var y = FloorY - 1; y < FloorY + 25; y++)
                {
                    if (world.GetBlock(x, y, z).Id == Blocks.Air) continue;
                    var insideWalls = x is >= 0 and < width && z is >= 0 and < depth;
                    if (!insideWalls) sawOutsideWalls = true;
                    await Assert.That(x).IsGreaterThanOrEqualTo(minX);
                    await Assert.That(x).IsLessThanOrEqualTo(maxX);
                    await Assert.That(z).IsGreaterThanOrEqualTo(minZ);
                    await Assert.That(z).IsLessThanOrEqualTo(maxZ);
                }
        await Assert.That(sawOutsideWalls).IsTrue();
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

    // ── a house on more than one wing ───────────────────────────────────────────────────────────────

    /// <summary>An L: a hall along −z with a wing running north off its west end, and the fixture every pass
    /// <b>below the eave</b> is asked about — the sill, the posts, the wall runs, the notch and the floor. The
    /// wing is deeper than it is wide, so its ridge runs across the hall's and the two abut over the whole of
    /// the wing's end, which is what makes the pair a building at all
    /// (<see cref="WingJoints"/>). Nothing here asserts anything about the roof; the junction fixtures that do
    /// are <see cref="EllMarch"/> and <see cref="EllProject"/>.</summary>
    private static Footprint Ell() => new([new Wing(0, 0, 10, 6), new Wing(0, 7, 5, 13)]);

    /// <summary>Everything below the eave follows the plan rather than the box drawn round it. The notch is the
    /// cell that tells them apart: a pass reading a min and a max writes into it, and the building never stood
    /// there.</summary>
    [Test]
    public async Task A_house_on_two_wings_builds_nothing_in_the_notch()
    {
        var plan = Ell();
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, plan, FloorY, new HouseStyle { Door = DoorMaterial.StainedGlass });

        // Deep in the notch, clear of the block of sill that legitimately rings the building.
        foreach (var (x, z) in new[] { (9, 11), (10, 12), (8, 12) })
        {
            await Assert.That(plan.Holds(x, z)).IsFalse();
            await Assert.That(plan.Borders(x, z)).IsFalse();
            for (var y = FloorY - 2; y <= FloorY + 2; y++)
                await Assert.That(world.GetBlock(x, y, z).Id).IsEqualTo(Blocks.Air);
        }

        // The floor is laid over every cell the plan does hold, the crook of the two wings included.
        foreach (var (x, z) in plan.Cells())
            await Assert.That(world.GetBlock(x, FloorY, z).Id).IsNotEqualTo(Blocks.Air);
    }

    /// <summary>The sill runs one block proud of the outline, so it follows the plan into the crook instead of
    /// squaring the building off. It is the pass a bounding box gets most visibly wrong.</summary>
    [Test]
    public async Task The_sill_rings_the_outline_rather_than_the_box()
    {
        var plan = Ell();
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, plan, FloorY, new HouseStyle());

        var sill = new HashSet<(int X, int Z)>();
        for (var x = plan.MinX - 2; x <= plan.MaxX + 2; x++)
            for (var z = plan.MinZ - 2; z <= plan.MaxZ + 2; z++)
                if (!plan.Holds(x, z) && world.GetBlock(x, FloorY, z).Id != Blocks.Air) sill.Add((x, z));

        var proud = plan.Cells()
            .SelectMany(cell => new[] { -1, 0, 1 }.SelectMany(dx => new[] { -1, 0, 1 }
                .Select(dz => (X: cell.X + dx, Z: cell.Z + dz))))
            .Where(cell => plan.Borders(cell.X, cell.Z)).ToHashSet();

        await Assert.That(sill.OrderBy(c => c.X).ThenBy(c => c.Z))
            .IsEquivalentTo(proud.OrderBy(c => c.X).ThenBy(c => c.Z));
    }

    /// <summary>The shell closes on more than one wing too. A roof drawn per plan cell is easy to leave gaps in,
    /// and the walls of a plan that turns a corner are the pass most likely to leave one.</summary>
    [Test]
    public async Task A_house_on_two_wings_is_sealed()
    {
        var plan = Ell();
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, plan, FloorY, new HouseStyle { Door = DoorMaterial.StainedGlass });

        // Started in the crook, which is the cell furthest from any one wing's own walls.
        var start = (4, FloorY + 1, 5);
        var seen = new HashSet<(int X, int Y, int Z)> { start };
        var queue = new Queue<(int X, int Y, int Z)>([start]);
        var escaped = false;
        while (queue.Count > 0 && !escaped)
        {
            var (x, y, z) = queue.Dequeue();
            if (x < plan.MinX - 4 || x > plan.MaxX + 4 || z < plan.MinZ - 4 || z > plan.MaxZ + 4
                || y > FloorY + 30) { escaped = true; break; }
            foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
            {
                var next = (x + dx, y + dy, z + dz);
                if (world.GetBlock(next.Item1, next.Item2, next.Item3).Id != Blocks.Air) continue;
                if (seen.Add(next)) queue.Enqueue(next);
            }
        }
        await Assert.That(escaped).IsFalse();
    }

    /// <summary>Six posts on an L, not five: the cell where two wings meet is a corner the walls run into, and
    /// it takes a post exactly as the five the building turns away at do.</summary>
    [Test]
    public async Task An_ell_stands_on_six_posts()
    {
        var plan = Ell();
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, plan, FloorY, new HouseStyle());

        var posts = plan.Cells()
            .Where(cell => world.GetBlock(cell.X, FloorY + 3, cell.Z).Id == Blocks.Log).ToList();
        await Assert.That(posts.Count).IsEqualTo(6);
        await Assert.That(posts).Contains((X: 5, Z: 6));      // the turn, where the two wings meet
    }

    /// <summary>The building is closed <b>diagonally</b> as well as squarely, which is a different question from
    /// whether air escapes it.
    ///
    /// <para>Where two wings meet, the two walls running into the turn touch along a single vertical edge and
    /// nothing else. Leave the cell behind that edge open and the building has no block where it turns and the
    /// room shows through the seam — and a flood fill walks past it without a word, because nothing can step
    /// diagonally. So the question is asked of the geometry instead: no cell inside the house may touch the
    /// outside at all, corners included.</para></summary>
    [Test]
    public async Task No_room_touches_the_outside_across_a_diagonal()
    {
        var plan = Ell();
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, plan, FloorY, new HouseStyle { Door = DoorMaterial.StainedGlass });

        var course = FloorY + 3;
        foreach (var (x, z) in plan.Cells())
        {
            if (world.GetBlock(x, course, z).Id != Blocks.Air) continue;      // a wall or a post, not a room
            for (var dx = -1; dx <= 1; dx++)
                for (var dz = -1; dz <= 1; dz++)
                    if (!plan.Holds(x + dx, z + dz))
                        await Assert.That(world.GetBlock(x + dx, course, z + dz).Id)
                            .IsNotEqualTo(Blocks.Air);
        }
    }

    /// <summary>A plan with no cell off its own wall has no room in it, whatever shape it is — the refusal a
    /// span under three blocks used to be.</summary>
    [Test]
    public async Task A_plan_with_no_inside_is_refused()
    {
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, new Footprint(0, 0, 9, 1), FloorY, new HouseStyle());
        await Assert.That(world.GetBlock(0, FloorY, 0).Id).IsEqualTo(Blocks.Air);
    }

    /// <summary>A wing may stop below the one beside it, and the storey above then walls itself along the line
    /// they shared. Downstairs the two are one room and that line carries no wall; upstairs it is the taller
    /// wing's own outline, so the wall is built by the ordinary storey pass rather than by a rule about
    /// neighbours.</summary>
    [Test]
    public async Task A_storey_above_a_stopped_wing_walls_the_line_they_shared()
    {
        var plan = new Footprint([new Wing(0, 0, 10, 6, new WingSpec(StoreysHigh: 2)), new Wing(0, 7, 6, 12, new WingSpec(StoreysHigh: 1))]);
        var style = new HouseStyle
        {
            Storeys = [new Storey { Clear = 4 }, new Storey { Clear = 4 }],
            Door = DoorMaterial.StainedGlass,
        };
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, plan, FloorY, style);

        // The shared line, clear of the corners at either end of it.
        foreach (var x in new[] { 2, 3, 4 })
        {
            await Assert.That(world.GetBlock(x, FloorY + 2, 6).Id).IsEqualTo(Blocks.Air);      // one room below
            await Assert.That(world.GetBlock(x, FloorY + 8, 6).Id).IsNotEqualTo(Blocks.Air);   // walled above
        }

        // And the wing itself carries no upper storey: no floor was laid over it for a room nobody built.
        await Assert.That(world.GetBlock(3, FloorY + 5, 10).Id).IsEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(3, FloorY + 5, 3).Id).IsNotEqualTo(Blocks.Air);       // the hall's slab
    }

    // ── the roof over more than one wing ────────────────────────────────────────────────────────────

    /// <summary>A T: a hall along −z with a cross wing running north out of the middle of it. The wing is
    /// narrower than it is deep, so its ridge runs the other way from the hall's — which is what puts one of
    /// its gable ends against the hall and the other out in the open. The two <b>abut</b>: the wing's last row
    /// is the one before the hall's first, which is the whole of how a junction is drawn.</summary>
    private static Footprint Tee() => new([new Wing(0, 6, 9, 10), new Wing(2, 0, 6, 5)]);

    /// <summary>An L on the same hall, with the wing set against its corner rather than its middle. Its ridge
    /// runs across the hall's, which is what makes the meeting a junction at all — see <see cref="Ell"/>, whose
    /// two ridges are parallel and which therefore exercises none of this.</summary>
    private static Footprint EllMarch() => new([new Wing(0, 6, 9, 10), new Wing(0, 0, 4, 5)]);

    /// <summary>The same L, and the same two rectangles, with the wing asking to carry its roof across the hall
    /// instead of stopping in it — so its second gable stands in the open on the far side. <b>Nothing about the
    /// plan differs</b>: a projection is the one thing about a junction the rectangles cannot say, which is why
    /// the wing says it.</summary>
    private static Footprint EllProject() =>
        new([new Wing(0, 6, 9, 10), new Wing(0, 0, 4, 5, new WingSpec(Projects: true))]);

    /// <summary>
    /// <b>A wing's end that stands against its neighbour is not a gable.</b> It is a doorway between two halves
    /// of one building — open at the storey, and open above it. Filled anyway it walls the wing's attic off
    /// from the hall's, so a building that reads as one shape from outside comes out with two sealed lofts in
    /// it. A marching T therefore carries <b>three</b> gable faces and a projecting one carries four, which is
    /// the difference between the two junctions stated as something countable.
    ///
    /// <para>An earlier form of this test asserted the opposite — that the two ends are the same gable — and
    /// passed while the attic was in two pieces. The end against the hall is the one thing about a wing that
    /// its open end cannot stand in for.</para>
    /// </summary>
    [Test]
    public async Task A_wings_end_against_its_neighbour_carries_no_gable()
    {
        // The gable takes a material of its own, because the default style walls and roofs in the same block
        // and a face cannot be told from a slope that is made of the same thing.
        var style = new HouseStyle
        {
            Form = RoofForm.Gable, Pitch = 1, Gable = new SolidMaterial(Blocks.EndStone),
        };
        var world = Built(Tee(), style);
        var wing = Tee().Wings[1];

        var open = 0;
        for (var x = wing.MinX; x <= wing.MaxX; x++)
            for (var y = FloorY + style.WallCourses + 1; y <= FloorY + 24; y++)
            {
                // MaxZ is the end standing on the hall's wall; MinZ is the end out in the open.
                if (world.GetBlock(x, y, wing.MinZ).Id == Blocks.EndStone) open++;
                var against = world.GetBlock(x, y, wing.MaxZ).Id;
                await Assert.That((x, y, against)).IsNotEqualTo((x, y, Blocks.EndStone));
            }

        // The open end does carry one, or the comparison above is measuring nothing.
        await Assert.That(open).IsGreaterThan(0);
    }

    /// <summary>
    /// <b>The attic over a junction is one space.</b> Above the eave only walls and gable faces rise, and the
    /// roof at each course closes a single outline with them — so every course that has an attic at all has
    /// exactly <b>one</b> pocket of air inside it. Two wings of one building share their loft; they do not each
    /// get their own.
    ///
    /// <para>Counted by flood fill from outside the plan, which is the only reading that cannot be satisfied by
    /// a roof with a hole in its body: a seal test passes on one, and this does not.</para>
    /// </summary>
    [Test]
    [MethodDataSource(nameof(Junctions))]
    public async Task An_attic_over_a_junction_is_one_space(string name, Footprint plan)
    {
        var style = new HouseStyle();
        var world = Built(plan, style);
        const int lo = -4, hi = 16;

        var attics = 0;
        for (var y = FloorY + style.WallCourses + 1; y <= FloorY + 24; y++)
        {
            bool Solid(int x, int z) => world.GetBlock(x, y, z).Id != Blocks.Air;
            var seen = new HashSet<(int, int)>();
            var outside = new Queue<(int X, int Z)>();
            for (var x = lo; x <= hi; x++) { outside.Enqueue((x, lo)); outside.Enqueue((x, hi)); }
            for (var z = lo; z <= hi; z++) { outside.Enqueue((lo, z)); outside.Enqueue((hi, z)); }
            while (outside.Count > 0)
            {
                var (x, z) = outside.Dequeue();
                if (x < lo || x > hi || z < lo || z > hi || Solid(x, z) || !seen.Add((x, z))) continue;
                outside.Enqueue((x + 1, z)); outside.Enqueue((x - 1, z));
                outside.Enqueue((x, z + 1)); outside.Enqueue((x, z - 1));
            }

            var pockets = 0;
            var counted = new HashSet<(int, int)>();
            for (var x = lo; x <= hi; x++)
                for (var z = lo; z <= hi; z++)
                {
                    if (Solid(x, z) || seen.Contains((x, z)) || counted.Contains((x, z))) continue;
                    pockets++;
                    var fill = new Queue<(int X, int Z)>();
                    fill.Enqueue((x, z));
                    while (fill.Count > 0)
                    {
                        var (a, b) = fill.Dequeue();
                        if (a < lo || a > hi || b < lo || b > hi || Solid(a, b) || !counted.Add((a, b))) continue;
                        fill.Enqueue((a + 1, b)); fill.Enqueue((a - 1, b));
                        fill.Enqueue((a, b + 1)); fill.Enqueue((a, b - 1));
                    }
                }

            await Assert.That((name, y, pockets)).IsEqualTo((name, y, pockets == 0 ? 0 : 1));
            if (pockets == 1) attics++;
        }

        // A building with no enclosed course at all would satisfy the loop and mean nothing.
        await Assert.That(attics).IsGreaterThan(0);
    }

    /// <summary>A 5 × 5 wing on the same hall, crossing only because it says so. Left to its proportions it
    /// ties and takes the along-x ridge, which is the hall's, and the two roofs meet in a gutter. It shares the
    /// hall's near row exactly as the other junction fixtures do — a wing that merely abuts one has no ground in
    /// common with it to open through.</summary>
    private static Footprint EllSquare() =>
        new([new Wing(0, 6, 9, 10), new Wing(0, 1, 4, 5, new WingSpec(Ridge: RidgeAxis.AlongZ))]);

    public static IEnumerable<(string, Footprint)> Junctions() =>
    [
        ("T marched", Tee()), ("T projected", Crossed()),
        ("L marched", EllMarch()), ("L projected", EllProject()),
        ("L squared, ridge stated", EllSquare()),
    ];

    /// <summary>
    /// <b>A wing may state which way its ridge runs, because its own proportions cannot know whether it
    /// crosses anything.</b> Whether two wings make a junction at all is whether their ridges cross, and read
    /// from each wing alone they easily do not — a 10 × 5 hall and a 7 × 6 wing are both wider than deep, so
    /// both ridges run along x and the roofs meet in a gutter rather than a valley. A <b>square</b> wing is the
    /// sharper case: it has no longer side at all, the comparison ties, and it can therefore never cross
    /// anything whatever an author wanted. Stated, it crosses — and the same 5 × 5 then answers every law a
    /// junction is held to, which is what <see cref="Junctions"/> puts it through.
    /// </summary>
    [Test]
    public async Task A_square_wing_cannot_cross_by_its_proportions_and_may_say_so()
    {
        var square = new Wing(0, 1, 4, 5);
        await Assert.That(square.RidgeAlongX).IsTrue();                       // 4 <= 4 — the tie
        await Assert.That(square.GableEnds).IsEqualTo((0, 4));                // its ±x walls

        var stated = square with { Spec = square.Spec with { Ridge = RidgeAxis.AlongZ } };
        await Assert.That(stated.RidgeAlongX).IsFalse();
        await Assert.That(stated.GableEnds).IsEqualTo((1, 5));                // now its ±z walls

        // The hall it stands against is wider than it is deep, so it keeps the along-x ridge either way, and
        // the two cross only because the wing said which way it runs.
        await Assert.That(new Wing(0, 6, 9, 10).RidgeAlongX).IsTrue();

        // A stated axis is what the roof is actually built on, not merely what the wing reports.
        var style = new HouseStyle();
        var world = Built(EllSquare(), style);
        var ridge = FloorY + style.WallCourses + 3;
        var alongZ = 0;
        for (var z = 1; z <= 5; z++) if (world.GetBlock(2, ridge, z).Id != Blocks.Air) alongZ++;
        var alongX = 0;
        for (var x = 0; x <= 4; x++) if (world.GetBlock(x, ridge, 3).Id != Blocks.Air) alongX++;
        await Assert.That(alongZ).IsGreaterThan(alongX);
    }

    /// <summary>
    /// <b>A verge is the outer rim of a roof, so no cell inside the outline is one.</b> A building of several
    /// wings has a single outline however many rectangles drew it, and a march's first step lands exactly on
    /// the wing's own overhang line — asked of the wing rather than of the building it stamps verge in the
    /// middle of the roof it has just run into. Counted at the ridge course, where a gable's verge is one cell
    /// per end that stands in the open: <b>three</b> on a marching T and four on a projecting one.
    /// </summary>
    [Test]
    [Arguments("T marched", 3)]
    [Arguments("T projected", 4)]
    [Arguments("L marched", 3)]
    [Arguments("L projected", 4)]
    [Arguments("L squared, ridge stated", 3)]
    public async Task Verge_at_the_ridge_course_counts_the_ends_that_stand_open(string name, int ends)
    {
        var style = new HouseStyle();
        var plan = Junctions().Single(pair => pair.Item1 == name).Item2;
        var world = Built(plan, style);
        var verge = (SolidMaterial)style.Verge;

        var ridge = FloorY + style.WallCourses + 3;
        var found = new List<(int X, int Z)>();
        for (var x = -4; x <= 16; x++)
            for (var z = -4; z <= 16; z++)
            {
                var (id, data) = world.GetBlock(x, ridge, z);
                if (id == verge.Id && data == verge.Data) found.Add((x, z));
            }

        await Assert.That((name, found.Count)).IsEqualTo((name, ends));
    }

    /// <summary>
    /// <b>An eave never fills the triangle a verge hangs open.</b> A verge climbs, so the cells under it are
    /// air; an eave does not, so its overhang is one solid course running the length. Where a wing's eave
    /// overhang reaches the column another wing's gable oversails, the verge stands higher and the eave gives
    /// way — an eave under a verge has no slope above it to catch anything from.
    ///
    /// <para>Read against the hall stamped <b>alone</b>, which is the only oracle that does not beg the
    /// question of how high its own gable stands there.</para>
    /// </summary>
    [Test]
    public async Task An_eave_overhang_leaves_a_neighbours_verge_triangle_open()
    {
        var style = new HouseStyle();
        var world = Built(EllProject(), style);

        var lone = new VoxelWorld();
        HouseStamper.Stamp(lone, new Footprint(0, 6, 9, 10), FloorY, style);

        var checkedCells = 0;
        for (var z = 6; z <= 10; z++)
        {
            var crown = FloorY - 1;
            for (var y = FloorY + 24; y >= FloorY; y--)
                if (lone.GetBlock(-1, y, z).Id != Blocks.Air) { crown = y; break; }
            await Assert.That((z, crown)).IsEqualTo((z, crown));

            for (var y = FloorY + style.WallCourses; y < crown; y++)
            {
                await Assert.That((z, y, world.GetBlock(-1, y, z).Id)).IsEqualTo((z, y, Blocks.Air));
                checkedCells++;
            }
        }

        // The hall's gable has to actually oversail this column, or nothing above was asserted.
        await Assert.That(checkedCells).IsGreaterThan(0);
    }

    /// <summary>The T's wing carrying its roof across the hall, so the cell past its second gable end is
    /// outside every wing and that end stands in the open — which is the only way a wing gets a second gable.
    /// The roof reaches the hall's far wall or it does not project at all: a gable landing mid-slope is a shape
    /// the stamper never builds, because the extension is taken to that wall rather than to a distance.</summary>
    private static Footprint Crossed() =>
        new([new Wing(0, 6, 9, 10), new Wing(2, 0, 6, 5, new WingSpec(Projects: true))]);

    /// <summary>
    /// <b>A wing's two gable ends carry the same triangle.</b> A wing drawn
    /// through its neighbour ends on that neighbour's far wall, and its gable there — face, slope and verge —
    /// is the gable that closes the building's other end, mirrored.
    ///
    /// <para><b>Below the eave they differ, and should.</b> The wing's own end is a corner the building turns
    /// away at and stands on posts; the end on the neighbour's wall is a stretch of that wall, and the building
    /// turns at the neighbour's corners instead — several blocks further along. What has to match is the
    /// <b>triangle</b>, and it does; no post is added where a wall runs straight on, because that would not be
    /// a corner. The difference below the line is asserted rather than trimmed out, so that the day it moves,
    /// something says so.</para>
    /// </summary>
    [Test]
    public async Task A_wings_two_gable_ends_carry_the_same_triangle()
    {
        var plan = Crossed();
        var wing = plan.Wings[1];
        // A projecting wing's far gable stands on the hall's far wall, which its own rectangle stops well
        // short of: the roof is what carries across, and the wall it arrives on is the hall's.
        var farWall = plan.Wings[0].MaxZ;
        var style = new HouseStyle { Form = RoofForm.Gable, Pitch = 1 };
        var world = Built(plan, style);
        var eave = FloorY + style.WallCourses;

        var seen = 0;
        for (var x = wing.MinX; x <= wing.MaxX; x++)
            for (var y = eave + 1; y <= FloorY + 24; y++)
            {
                var through = world.GetBlock(x, y, farWall);        // the end on the hall's far wall
                var open = world.GetBlock(x, y, wing.MinZ);         // the end that closes the building
                await Assert.That((x, y, through.Id, through.Data)).IsEqualTo((x, y, open.Id, open.Data));
                if (through.Id != Blocks.Air) seen++;
            }
        await Assert.That(seen).IsGreaterThan(0);

        // And below it they part exactly where the building's own corners are, which is the corner columns.
        var turns = Enumerable.Range(wing.MinX, wing.Width)
            .Where(x => world.GetBlock(x, eave, wing.MinZ) != world.GetBlock(x, eave, farWall))
            .ToList();
        await Assert.That(turns).IsEquivalentTo(new[] { wing.MinX, wing.MaxX });
    }

    /// <summary>
    /// <b>A wing that marches makes a T; one that projects makes a +.</b> It is the same rule either way and
    /// never a mode: where two ridges cross the higher one stands. A marching wing carries its ridge into the
    /// hall only as far as the hall's own ridge and stops there — nothing penetrates — so the tallest course
    /// reads as a T. The same two rectangles with the wing projecting carry its ridge the whole way to the
    /// hall's far wall, and the two cross, which reads as a +.
    /// </summary>
    [Test]
    public async Task A_wing_stopping_short_makes_a_T_and_one_drawn_through_makes_a_plus()
    {
        var style = new HouseStyle { Form = RoofForm.Gable, Pitch = 1 };
        var eave = FloorY + style.WallCourses;

        int Surface(Footprint plan, int x, int z)
        {
            var world = Built(plan, style);
            for (var y = FloorY + 20; y >= eave; y--)
                if (world.GetBlock(x, y, z).Id != Blocks.Air) return y - eave;
            return -1;
        }

        // The hall's ridge sits at z=8 and the wing's at x=4, both three courses over the eave.
        const int hallRidge = 8, wingRidge = 4, top = 3;

        // Stopped short: the wing's ridge reaches the hall's and goes no further.
        await Assert.That(Surface(Tee(), wingRidge, hallRidge)).IsEqualTo(top);
        await Assert.That(Surface(Tee(), wingRidge, hallRidge + 1)).IsEqualTo(top - 1);

        // Projecting: it carries on past, and out over the hall's own overhang.
        await Assert.That(Surface(Crossed(), wingRidge, hallRidge + 1)).IsEqualTo(top);
        await Assert.That(Surface(Crossed(), wingRidge, hallRidge + 3)).IsEqualTo(top);

        // Either way the hall's own ridge survives the crossing whole.
        foreach (var plan in new[] { Tee(), Crossed() })
            for (var x = 0; x <= 9; x++)
                await Assert.That(Surface(plan, x, hallRidge)).IsEqualTo(top);
    }

    /// <summary>
    /// <b>A march that can never be struck still stops.</b> The wing's ridge marches on the hall's wall exactly
    /// as <see cref="A_wing_stopping_short_makes_a_T_and_one_drawn_through_makes_a_plus"/> does, but this wing's
    /// own pitch is steeper than the hall's — so its ridge stands taller than the hall's ever gets, and nothing
    /// in the hall's own roof is ever tall enough for the march to hit. Left unbounded that runs the march the
    /// whole length of the hall and out its far overhang, the drawn-through shape of a wing whose footprint was
    /// never drawn that far. Bounded by the wing's own distance from its own eave instead, the march reaches a
    /// few courses in and stops there — short of the hall's far wall, whatever the hall's own roof is doing.
    /// </summary>
    [Test]
    public async Task A_steeper_wings_march_stops_short_of_a_shallower_halls_far_wall()
    {
        var plan = new Footprint([
            new Wing(0, 5, 9, 9, new WingSpec(Form: RoofForm.Gable, Pitch: 1)),
            new Wing(2, 0, 6, 5, new WingSpec(Form: RoofForm.Gable, Pitch: 2)),
        ]);
        var style = new HouseStyle();
        var world = Built(plan, style);

        // The hall exactly as it would have stood with no wing at all, to read what its own roof does on its
        // own — the only oracle that does not beg the question of what the march ought to leave behind.
        var lone = new VoxelWorld();
        HouseStamper.Stamp(lone, new Footprint(0, 5, 9, 9), FloorY, style);

        var wing = plan.Wings[1];
        var untouched = 0;
        for (var x = wing.MinX; x <= wing.MaxX; x++)
        {
            // A course this many blocks from its own wall is the furthest the march may ever reach for it.
            var reachable = Math.Min(x - wing.MinX, wing.MaxX - x);
            for (var z = wing.MaxZ + reachable + 1; z <= 12; z++)
                for (var y = FloorY; y <= FloorY + 20; y++)
                {
                    var marched = world.GetBlock(x, y, z);
                    var alone = lone.GetBlock(x, y, z);
                    await Assert.That((marched.Id, marched.Data)).IsEqualTo((alone.Id, alone.Data));
                    if (alone.Id != Blocks.Air) untouched++;
                }
        }
        // The bound has to actually be crossed somewhere, or the comparison holds vacuously.
        await Assert.That(untouched).IsGreaterThan(0);
    }

    /// <summary>
    /// <b>The same stop, where the hall has no ridge to strike at all.</b> A flat lid never rises, so a probe
    /// waiting to be "hit" by the hall's own roof waits forever: every course of the wing's gable is eventually
    /// taller than the flat lid's one constant course, and the old march ran every one of them the length of
    /// the hall. The distance bound does not care that the hall is flat — it never asked the hall anything.
    /// </summary>
    [Test]
    public async Task A_march_against_a_flat_roofed_hall_stops_without_a_ridge_to_strike()
    {
        var plan = new Footprint([
            new Wing(0, 5, 9, 9, new WingSpec(Form: RoofForm.Flat)),
            new Wing(2, 0, 6, 5, new WingSpec(Form: RoofForm.Gable, Pitch: 1)),
        ]);
        var style = new HouseStyle();
        var world = Built(plan, style);

        var lone = new VoxelWorld();
        HouseStamper.Stamp(lone, new Footprint(0, 5, 9, 9), FloorY, style with { Form = RoofForm.Flat });

        var wing = plan.Wings[1];
        var untouched = 0;
        for (var x = wing.MinX; x <= wing.MaxX; x++)
        {
            var reachable = Math.Min(x - wing.MinX, wing.MaxX - x);
            for (var z = wing.MaxZ + reachable + 1; z <= 12; z++)
                for (var y = FloorY; y <= FloorY + 20; y++)
                {
                    var marched = world.GetBlock(x, y, z);
                    var alone = lone.GetBlock(x, y, z);
                    await Assert.That((marched.Id, marched.Data)).IsEqualTo((alone.Id, alone.Data));
                    if (alone.Id != Blocks.Air) untouched++;
                }
        }
        await Assert.That(untouched).IsGreaterThan(0);
    }

    private static VoxelWorld Built(Footprint plan, HouseStyle style)
    {
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, plan, FloorY, style);
        return world;
    }

    /// <summary>Every cell the plan stands on is roofed. The cut is what makes this worth asking: it takes the
    /// roof a projecting wing pushes into out of the way, and taken one column too wide it opens a hole nothing
    /// fills — the verge that was to sit there is itself standing over the hall's wall, so the rule that keeps
    /// roof out from under a wall keeps it out too, and the building is left open to the sky down both sides of
    /// the wing. A flood fill from inside finds it; a plan of the highest block over each column shows it at a
    /// glance, which is how it was found.</summary>
    [Test]
    public async Task A_cross_gable_leaves_no_hole_where_it_cut()
    {
        var plan = Crossed();
        var style = new HouseStyle { Form = RoofForm.Gable, Pitch = 1 };
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, plan, FloorY, style);

        var eave = FloorY + style.WallCourses;
        foreach (var (x, z) in plan.Cells())
        {
            var roofed = Enumerable.Range(eave, 20).Any(y => world.GetBlock(x, y, z).Id != Blocks.Air);
            await Assert.That(roofed).IsTrue();
        }
    }

    /// <summary>A roof reaches its own walls and its own overhang, and stops. A wing may not hang a stub of
    /// itself over ground no wall of the building stands on.</summary>
    [Test]
    public async Task No_roof_stands_further_out_than_a_wings_own_overhang()
    {
        var plan = Tee();
        var style = new HouseStyle { Form = RoofForm.Gable, Pitch = 1 };
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, plan, FloorY, style);

        var reach = Math.Max(0, style.Overhang);
        for (var x = plan.MinX - 4; x <= plan.MaxX + 4; x++)
            for (var z = plan.MinZ - 4; z <= plan.MaxZ + 4; z++)
            {
                if (plan.Near(x, z, reach)) continue;
                for (var y = FloorY; y <= FloorY + 24; y++)
                    await Assert.That(world.GetBlock(x, y, z).Id).IsEqualTo(Blocks.Air);
            }
    }

    /// <summary>No roof block below the wall top of whatever covers that cell — under a wall is inside the
    /// building. It is what makes a wing that stops lower stop <em>against</em> its taller neighbour instead of
    /// pushing a slope through its standing wall.</summary>
    [Test]
    public async Task A_shorter_wings_roof_stops_against_its_taller_neighbour()
    {
        var plan = new Footprint([new Wing(0, 0, 10, 6, new WingSpec(StoreysHigh: 2)), new Wing(0, 7, 6, 12, new WingSpec(StoreysHigh: 1))]);
        var style = new HouseStyle
        {
            Storeys = [new Storey { Clear = 4 }, new Storey { Clear = 4 }],
            Form = RoofForm.Gable, Pitch = 1,
        };
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, plan, FloorY, style);

        // The hall's upper storey wall, along the line the wing gave up. Every course of it is wall, from the
        // slab it stands on to its own eave: the wing's roof reaches the line and gets no further.
        for (var course = 1; course <= 4; course++)
            foreach (var x in new[] { 2, 4 })
                await Assert.That(world.GetBlock(x, FloorY + 5 + course, 6).Id).IsNotEqualTo(Blocks.Air);
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
