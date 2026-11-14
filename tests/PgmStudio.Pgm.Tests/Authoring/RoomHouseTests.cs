using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// A room's house: wings whose corners are their first and last cells, the first of them the room. A house over
/// a max-exclusive rectangle reads back as that rectangle, the bounds run round every wing, and a mapping moves
/// each wing while keeping what it states about itself and carrying the chests and seats it states with them.
/// </summary>
public sealed class RoomHouseTests
{
    [Test]
    public async Task A_house_over_a_rectangle_is_one_wing_of_its_first_and_last_cells()
    {
        var house = RoomHouse.Over(new Rect(2, 3, 8, 9));

        await Assert.That(house.Wings.Single().Corners.Select(corner => (corner[0], corner[1])))
            .IsEquivalentTo(new[] { (2.0, 3.0), (7.0, 8.0) });
        await Assert.That(house.Room).IsEqualTo(new Rect(2, 3, 8, 9));
        await Assert.That(house.Bounds).IsEqualTo(new Rect(2, 3, 8, 9));
    }

    [Test]
    public async Task The_room_is_the_first_wing_and_the_bounds_run_round_them_all()
    {
        var house = new RoomHouse { Wings = [.. RoomHouse.Over(new Rect(0, 0, 6, 6)).Wings, .. RoomHouse.Over(new Rect(6, 2, 10, 6)).Wings] };

        await Assert.That(house.Room).IsEqualTo(new Rect(0, 0, 6, 6));
        await Assert.That(house.Bounds).IsEqualTo(new Rect(0, 0, 10, 6));
        await Assert.That(new RoomHouse().Room).IsNull();
    }

    [Test]
    public async Task A_mapping_moves_every_wing_and_keeps_its_spec()
    {
        var tall = RoomHouse.Over(new Rect(0, 0, 6, 6)).Wings[0] with { Spec = new WingSpec(StoreysHigh: 2) };
        var house = new RoomHouse { Wings = [tall] };

        var moved = house.Mapped(rect => new Rect(rect.MinX + 3, rect.MinZ - 1, rect.MaxX + 3, rect.MaxZ - 1));

        await Assert.That(moved.Room).IsEqualTo(new Rect(3, -1, 9, 5));
        await Assert.That(moved.Wings[0].Spec.StoreysHigh).IsEqualTo(2);
    }

    [Test]
    public async Task A_wings_words_are_written_as_words_whatever_the_options()
    {
        var wing = RoomHouse.Over(new Rect(0, 0, 6, 6)).Wings[0]
            with { Spec = new WingSpec(Form: RoofForm.Hip, Ridge: RidgeAxis.AlongX) };
        var json = System.Text.Json.JsonSerializer.Serialize(new RoomHouse { Wings = [wing] },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        await Assert.That(json).Contains("\"ridge\":\"alongX\"");
        await Assert.That(json).Contains("\"form\":\"hip\"");
    }

    [Test]
    public async Task A_stated_null_style_reads_back_as_null_and_an_absent_one_stays_absent()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        var open = System.Text.Json.JsonSerializer.Deserialize<RoomHouse>("""{"wings":[],"style":null}""", options)!;
        var unstated = System.Text.Json.JsonSerializer.Deserialize<RoomHouse>("""{"wings":[]}""", options)!;

        await Assert.That(open.Style.ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Null);
        await Assert.That(System.Text.Json.JsonSerializer.Serialize(open, options)).Contains("\"style\":null");
        await Assert.That(unstated.Style.ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Undefined);
        await Assert.That(System.Text.Json.JsonSerializer.Serialize(unstated, options)).DoesNotContain("style");
    }

    [Test]
    public async Task A_mapping_states_each_cell_again_from_the_mapped_rooms_corner()
    {
        var house = RoomHouse.Over(new Rect(0, 0, 6, 8)) with { Chests = [[1, 1], [4, 6]] };

        var mirrored = house.Mapped(rect => new Rect(-rect.MaxX, rect.MinZ, -rect.MinX, rect.MaxZ));

        await Assert.That(mirrored.Room).IsEqualTo(new Rect(-6, 0, 0, 8));
        await Assert.That(mirrored.Chests!.Select(cell => (cell[0], cell[1]))).IsEquivalentTo(new[] { (4, 1), (1, 6) })
            .Because("board cell 1 mirrors onto cell -2, which is cell 4 from the mirrored room's corner at -6");
        await Assert.That(mirrored.Monuments).IsNull();
    }

    [Test]
    [Arguments("mirror_x", 1)]
    [Arguments("mirror_z", 1)]
    [Arguments("rot_180", 1)]
    [Arguments("rot_90", 1)]
    [Arguments("rot_90", 2)]
    [Arguments("rot_90", 3)]
    public async Task The_nth_stated_chest_and_seat_of_a_room_and_of_its_image_are_images(string mode, int step)
    {
        // A walled 12 by 13 room, its marker on a grid line and its door on -z, stating chests and seats at
        // ties and off them; its image is the house carried the way the orbit fill carries it.
        var house = RoomHouse.Over(new Rect(-14, -20, -2, -7)) with
        {
            Chests = [[1, 1], [5, 11], [10, 6], [1, 6]],
            Monuments = [[10, 6], [1, 1], [5, 11], [3, 4], [6, 2]],
        };
        var image = house.Mapped(rect =>
        {
            var (minX, minZ, maxX, maxZ) = Symmetry.Rect(rect.MinX, rect.MinZ, rect.MaxX, rect.MaxZ, mode, 0, 0, step);
            return new Rect(Math.Round(minX), Math.Round(minZ), Math.Round(maxX), Math.Round(maxZ));
        });
        RoomEdge Turned(RoomEdge edge)
        {
            var (outX, outZ) = edge.Outward();
            var (turnedX, turnedZ) = Symmetry.Point(outX, outZ, mode, 0, 0, step);
            return RoomEdges.Nearest(((int)Math.Round(turnedX), (int)Math.Round(turnedZ)), RoomEdges.All)!.Value;
        }
        RoomFrame Frame(RoomHouse of, (double X, double Z) marker, RoomEdge door, bool reflected)
        {
            var room = of.Room!.Value;
            var cells = new BlockRect((int)room.MinX, (int)room.MinZ, (int)room.MaxX, (int)room.MaxZ);
            return RoomFrames.Resolve(cells, cells, shellBound: true, marker.X, marker.Z, [new AskedDoor(door)],
                spawn: true, out _)! with { Reflected = reflected };
        }

        var authored = RoomHouse.Interior(house, Frame(house, (-8, -14), RoomEdge.NegZ, false), out var refusal);
        var carried = RoomHouse.Interior(image,
            Frame(image, Symmetry.Point(-8, -14, mode, 0, 0, step), Turned(RoomEdge.NegZ), Symmetry.Reflects(mode, step)),
            out var imageRefusal);

        await Assert.That(refusal).IsNull();
        await Assert.That(imageRefusal).IsNull();
        string Imaged(int x, int z, RoomEdge edge)
        {
            var cell = Symmetry.Cell(x, z, mode, 0, 0, step);
            return $"{cell.X},{cell.Z},{Turned(edge)}";
        }
        await Assert.That(string.Join(" ", carried.Chests.Select(chest => $"{chest.X},{chest.Z},{chest.Facing}")))
            .IsEqualTo(string.Join(" ", authored.Chests.Select(chest => Imaged(chest.X, chest.Z, chest.Facing))));
        await Assert.That(string.Join(" ", carried.Monuments.Select(seat => $"{seat.X},{seat.Z},{seat.Wall}")))
            .IsEqualTo(string.Join(" ", authored.Monuments.Select(seat => Imaged(seat.X, seat.Z, seat.Wall))));
    }
}
