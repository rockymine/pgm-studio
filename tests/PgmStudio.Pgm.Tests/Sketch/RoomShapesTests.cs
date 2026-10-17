using System.Text.Json.Nodes;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The rooms a layout is read with are the intent's: <see cref="RoomShapes.Project"/> draws a region and a
/// building for each spawn and wool, carrying what the layout's record says about the ground under it, and
/// <see cref="RoomShapes.Fold"/> takes them back out for a store. What must hold is that a layout read, left
/// alone and stored is the layout it was, and that what an edit states about a room's ground survives the
/// trip while its rectangle does not.
/// </summary>
public sealed class RoomShapesTests
{
    private const string Stored = """
        {"layers":[{"id":"ground","base_y":0,"layout":{"shapes":[
            {"id":"land","type":"rectangle","operation":"add","min_x":0,"min_z":0,"max_x":60,"max_z":60}],
          "groups":[]}}],
         "rooms":{"spawn-red":{"base_height":9,"relief_scope":"follow","doors":["-z"]},
                  "spawn-red-building":{"theme":"stone"}}}
        """;

    private static MapIntent Intent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns =
        [
            new SpawnIntent
            {
                Id = "spawn-red", Team = "red", Protection = [new Rect(10, 10, 20, 20)], House = RoomHouse.Over(new Rect(12, 12, 18, 18)),
            },
        ],
        Wools = [new WoolIntent { Id = "wool-red-lime", Owner = "red", Color = "lime", Protection = [new Rect(30, 10, 40, 20)] }],
    };

    private static List<SketchShape> Rooms(string layoutJson) =>
        SketchLayout.Stack(SketchLayout.Parse(layoutJson))[0].Shapes.Where(shape => shape.Role is not null).ToList();

    [Test]
    public async Task Each_room_is_drawn_from_the_intent_with_its_record()
    {
        var rooms = Rooms(RoomShapes.Project(Stored, Intent()));

        await Assert.That(rooms.Select(room => room.Id))
            .IsEquivalentTo(new[] { "spawn-red", "spawn-red-building", "wool-red-lime" });
        var spawn = rooms.Single(room => room.Id == "spawn-red");
        await Assert.That((spawn.MinX, spawn.MinZ, spawn.MaxX, spawn.MaxZ)).IsEqualTo((10.0, 10.0, 20.0, 20.0));
        await Assert.That(spawn.Role).IsEqualTo(StructuralRoles.Spawn);
        await Assert.That(spawn.Color).IsEqualTo("red");
        await Assert.That(spawn.ReliefScope).IsEqualTo(ReliefScopes.Follow);
        await Assert.That(spawn.Doors).IsEquivalentTo(new[] { "-z" });
        await Assert.That(rooms.Single(room => room.Id == "spawn-red-building").Theme).IsEqualTo("stone");
        // A room with no record is ordinary ground the relief does not pin.
        await Assert.That(rooms.Single(room => room.Id == "wool-red-lime").ReliefScope).IsNull();
    }

    [Test]
    public async Task A_layout_read_and_stored_unchanged_is_the_layout_it_was()
    {
        var stored = RoomShapes.Fold(RoomShapes.Project(Stored, Intent()));

        await Assert.That(JsonNode.DeepEquals(JsonNode.Parse(stored), JsonNode.Parse(Stored))).IsTrue();
    }

    [Test]
    public async Task A_layout_holding_no_room_is_stored_byte_for_byte()
    {
        await Assert.That(RoomShapes.Fold(Stored)).IsEqualTo(Stored);
    }

    /// <summary>The height an author corrects on a drawn room is a statement about its ground, so it lands in
    /// the record; the rectangle the canvas held is not stored at all, since the intent says where the room
    /// is.</summary>
    [Test]
    public async Task What_an_edit_states_about_a_rooms_ground_is_kept_and_its_rectangle_is_not()
    {
        var read = JsonNode.Parse(RoomShapes.Project(Stored, Intent()))!;
        var spawn = ((JsonArray)read["layers"]![0]!["layout"]!["shapes"]!).OfType<JsonObject>()
            .Single(shape => shape["id"]!.GetValue<string>() == "spawn-red");
        spawn["base_height"] = 14;
        spawn["height_authored"] = true;
        spawn["min_x"] = 500;

        var stored = SketchLayout.Parse(RoomShapes.Fold(read.ToJsonString()))!;

        await Assert.That(stored.Rooms!["spawn-red"].BaseHeight).IsEqualTo(14);
        await Assert.That(stored.Rooms["spawn-red"].HeightAuthored).IsTrue();
        await Assert.That(SketchLayout.Stack(stored)[0].Shapes.Any(shape => shape.Role is not null)).IsFalse();
        await Assert.That(Rooms(RoomShapes.Project(stored.ToJson(), Intent())).Single(room => room.Id == "spawn-red").MinX)
            .IsEqualTo(10);
    }

    /// <summary>A region stated as a union has no one rectangle the sketch could draw.</summary>
    [Test]
    public async Task A_region_of_several_rectangles_is_not_drawn()
    {
        var intent = Intent();
        intent = intent with
        {
            Spawns = [intent.Spawns[0] with { Protection = [new Rect(10, 10, 20, 20), new Rect(20, 10, 30, 20)] }],
        };

        await Assert.That(Rooms(RoomShapes.Project(Stored, intent)).Select(room => room.Id))
            .IsEquivalentTo(new[] { "wool-red-lime" });
    }

    /// <summary>A room the symmetry fills is a room the world builds, so it is drawn, named the way an
    /// unnamed piece is named.</summary>
    [Test]
    public async Task An_image_the_symmetry_fills_is_drawn_under_its_own_name()
    {
        var intent = Intent() with
        {
            Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 50, CenterZ = 0 },
            Wools = null,
        };

        var rooms = Rooms(RoomShapes.Project(Stored, intent));

        var blue = rooms.Single(room => room.Id == "spawn-blue");
        await Assert.That((blue.MinX, blue.MaxX)).IsEqualTo((80.0, 90.0));
        await Assert.That(blue.Color).IsEqualTo("blue");
    }
}
