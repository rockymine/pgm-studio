using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// What happens to an author-stated room height when a plan is recompiled over it. Unlike a relief
/// (<see cref="SketchReliefCarryTests"/>), a room's height rides on the room's record in the layout, keyed by
/// the room's id — its piece's, which survives a recompile — and only a record the author actually corrected
/// (<c>height_authored</c>) is carried.
/// </summary>
public sealed class RoomGroundCarryTests
{
    // A freshly compiled layout: one plain terrain shape and the record the compiler writes for the authored
    // spawn room, at the plan's flat surface (9). The compiler never sets height_authored.
    private static string Compiled(double baseHeight = 9) => $$"""
    {
      "layers": [{ "id": "ground", "base_y": 0, "layout": {
        "shapes": [
          { "id": "s0", "type": "polygon", "operation": "add", "base_height": 9,
            "vertices": [[0,0],[0,20],[20,20],[20,0]] }
        ],
        "groups": [ { "id": "team", "mirrors": true, "shapeIds": ["s0"] } ]
      } }],
      "rooms": { "spawn-red": { "base_height": {{baseHeight}}, "relief_scope": "follow", "doors": ["-z"] } }
    }
    """;

    // A stored layout carrying an author correction: the room's floor and top moved once real relief existed,
    // flagged height_authored so a recompile knows to keep it.
    private static string StoredWithCorrection(string room = "spawn-red", double floor = 2, double baseHeight = 4) => $$"""
    {
      "layers": [{ "id": "ground", "base_y": 0, "layout": { "shapes": [], "groups": [] } }],
      "rooms": { "{{room}}": { "floor": {{floor}}, "base_height": {{baseHeight}}, "relief_scope": "hold",
                               "height_authored": true } }
    }
    """;

    private static RoomGround Room(string layoutJson, string id) => SketchLayout.Parse(layoutJson)!.Rooms![id];

    [Test]
    public async Task An_authored_height_survives_a_recompile_matched_by_the_rooms_id()
    {
        var spawn = Room(SketchLayout.CarryRoomGround(Compiled(), StoredWithCorrection()), "spawn-red");

        await Assert.That(spawn.Floor).IsEqualTo(2);
        await Assert.That(spawn.BaseHeight).IsEqualTo(4);
        await Assert.That(spawn.HeightAuthored).IsTrue();
        // A corrected height is one the author means against the relief, so carrying it turns the room from
        // following the ground to holding its number; what else the compile stated stands.
        await Assert.That(spawn.ReliefScope).IsEqualTo(ReliefScopes.Hold);
        await Assert.That(spawn.Doors).IsEquivalentTo(new[] { "-z" });
    }

    [Test]
    public async Task An_untouched_room_keeps_tracking_the_plans_own_surface()
    {
        const string stored = """{ "rooms": { "spawn-red": { "base_height": 9, "relief_scope": "follow" } } }""";
        await Assert.That(SketchLayout.CarryRoomGround(Compiled(), stored)).IsEqualTo(Compiled());
    }

    /// <summary>A correction stays with its room: one the compile no longer draws is kept under its id, where
    /// a room given that id again picks it up.</summary>
    [Test]
    public async Task A_correction_on_another_room_does_not_cross_over()
    {
        var merged = SketchLayout.CarryRoomGround(Compiled(), StoredWithCorrection(room: "spawn-blue"));

        await Assert.That(Room(merged, "spawn-red").BaseHeight).IsEqualTo(9);
        await Assert.That(Room(merged, "spawn-blue").HeightAuthored).IsTrue();
    }

    [Test]
    public async Task No_stored_layout_leaves_the_compiled_json_untouched()
    {
        await Assert.That(SketchLayout.CarryRoomGround(Compiled(), null)).IsEqualTo(Compiled());
        await Assert.That(SketchLayout.CarryRoomGround(Compiled(), "")).IsEqualTo(Compiled());
    }

    [Test]
    public async Task Carrying_room_ground_leaves_relief_and_finish_carries_alone()
    {
        // All three carries run together on a recompile (SketchFromPlanEndpoint) and must not interfere.
        const string stored = """
        {
          "layers": [{ "id": "ground", "base_y": 0, "layout": { "shapes": [], "groups": [] } }],
          "rooms": { "spawn-red": { "floor": 2, "base_height": 4, "relief_scope": "hold", "height_authored": true } },
          "mapTheme": "grass",
          "relief": { "team": { "base": 6 } }
        }
        """;
        var merged = SketchLayout.CarryRoomGround(
            SketchLayout.CarryRelief(SketchLayout.CarryFinish(Compiled(), stored), stored), stored);
        var state = SketchLayout.Parse(merged);

        await Assert.That(state!.MapTheme).IsEqualTo("grass");
        await Assert.That(state.Relief!.ContainsKey("team")).IsTrue();
        await Assert.That(state.Rooms!["spawn-red"].Floor).IsEqualTo(2);
        await Assert.That(state.Rooms["spawn-red"].BaseHeight).IsEqualTo(4);
    }

    // A held spawn piece over an island whose relief climbs from 6 to 26 (SketchReliefScopeTests' fixture),
    // proving the carried height is not just JSON that round-trips but a real input to the solve: the
    // rasterizer binds a Role-tagged hold shape to its island by footprint (SketchRasterizer §"groundSoFar"),
    // not by ShapeIds membership, which is exactly the path a spawn/wool annotation takes.
    private const string HeldOverRelief = """
    {
      "layers": [{ "id": "ground", "base_y": 0, "layout": {
        "shapes": [
          { "id": "land", "type": "rectangle", "operation": "add",
            "min_x": 0, "min_z": 0, "max_x": 60, "max_z": 60, "base_height": 5, "floor": 0 },
          { "id": "spawn-red", "type": "rectangle", "operation": "add", "role": "spawn",
            "color": "red",
            "min_x": 22, "min_z": 22, "max_x": 38, "max_z": 38,
            "base_height": HEIGHT, "relief_scope": "hold", "height_authored": true }
        ],
        "groups": [ { "id": "i1", "mirrors": false, "shapeIds": ["land"] } ]
      } }],
      "relief": { "i1": { "base": 6, "marks": [
          { "kind": "point", "at": [3, 3], "h": 6, "r": 3 },
          { "kind": "point", "at": [57, 57], "h": 26, "r": 3 } ] } }
    }
    """;

    [Test]
    public async Task A_carried_correction_moves_where_the_relief_actually_holds_the_ground()
    {
        var beforeCorrection = HeldOverRelief.Replace("HEIGHT", "9");   // the plan's flat surface
        var afterCorrection = HeldOverRelief.Replace("HEIGHT", "16");  // what the author saw and stated instead

        int TopAt(string layoutJson, int x, int z) =>
            SketchRasterizer.RasterizeColumns(layoutJson).Single(c => c.X == x && c.Z == z).YTop;

        await Assert.That(TopAt(beforeCorrection, 30, 30)).IsEqualTo(9);
        await Assert.That(TopAt(afterCorrection, 30, 30)).IsEqualTo(16);
        // The correction is not merely local — the surrounding surface is re-solved knowing the new arrival
        // height, the same "a held shape moves the ground around it" property SketchReliefScopeTests measures.
        await Assert.That(TopAt(beforeCorrection, 5, 30)).IsNotEqualTo(TopAt(afterCorrection, 5, 30));
    }

    // A spawn room on ground that climbs along the axis its door faces: low at -z (ahead of the door, where
    // players leave) and high at +z (behind it). A room is a level rectangle and cannot slope, so the two
    // candidate seatings genuinely disagree — the middle of the footprint sits mid-slope, the door sits on
    // the ground players actually cross.
    private const string RoomOnASlope = """
    {
      "layers": [{ "id": "ground", "base_y": 0, "layout": {
        "shapes": [
          { "id": "land", "type": "rectangle", "operation": "add",
            "min_x": 0, "min_z": 0, "max_x": 60, "max_z": 60, "base_height": 5, "floor": 0 },
          { "id": "spawn-red", "type": "rectangle", "operation": "add", "role": "spawn",
            "color": "red",
            "min_x": 22, "min_z": 22, "max_x": 38, "max_z": 38,
            "base_height": 9, "relief_scope": "follow"DOORS }
        ],
        "groups": [ { "id": "i1", "mirrors": false, "shapeIds": ["land"] } ]
      } }],
      "relief": { "i1": { "base": 10, "marks": [
          { "kind": "line", "points": [[0, 2], [60, 2]], "h": 6, "r": 3 },
          { "kind": "line", "points": [[0, 58], [60, 58]], "h": 30, "r": 3 } ] } }
    }
    """;

    [Test]
    public async Task A_room_is_seated_level_with_the_ground_outside_its_door()
    {
        var atTheDoor = RoomOnASlope.Replace("DOORS", ""","doors": ["-z"]""");
        var underTheRoom = RoomOnASlope.Replace("DOORS", "");

        int TopAt(string layoutJson, int x, int z) =>
            SketchRasterizer.RasterizeColumns(layoutJson).Single(c => c.X == x && c.Z == z).YTop;

        var seated = TopAt(atTheDoor, 30, 30);

        // Flush with the ground a player crosses leaving the door: no wall, no drop.
        await Assert.That(Math.Abs(TopAt(atTheDoor, 30, 21) - seated)).IsLessThanOrEqualTo(1);

        // Still a level rectangle — the seating moves the room, it never tilts it.
        var floor = new List<int>();
        for (var x = 23; x < 38; x++)
            for (var z = 23; z < 38; z++)
                floor.Add(TopAt(atTheDoor, x, z));
        await Assert.That(floor.Distinct().Count()).IsEqualTo(1);

        // And the door is what decided it: seating on the middle of the footprint answers higher on ground
        // that climbs away behind the room, which is a step at the door and the thing being avoided.
        await Assert.That(seated).IsLessThan(TopAt(underTheRoom, 30, 30));
    }

    /// <summary>The <b>word</b> separates the two, and nothing else does. A room that follows gives up the
    /// number its plan carried — stated before any terrain existed — to stay level with the ground it opens
    /// onto; the same room holding keeps that number and lets the relief meet it as a face.</summary>
    [Test]
    public async Task A_following_room_is_seated_on_the_terrain_rather_than_on_the_plans_number()
    {
        var following = HeldOverRelief.Replace("HEIGHT", "9")
                                      .Replace("\"relief_scope\": \"hold\"", "\"relief_scope\": \"follow\"");

        int TopAt(string layoutJson, int x, int z) =>
            SketchRasterizer.RasterizeColumns(layoutJson).Single(c => c.X == x && c.Z == z).YTop;

        var room = TopAt(following, 30, 30);
        await Assert.That(room).IsNotEqualTo(9).Because("the plan's flat number is not a statement about terrain");

        // It is still a room: flat across its whole floor, and level with the ground it opens onto.
        var floor = new List<int>();
        for (var x = 23; x < 38; x++)
            for (var z = 23; z < 38; z++)
                floor.Add(TopAt(following, x, z));
        await Assert.That(floor.Distinct().Count()).IsEqualTo(1).Because("a followed room is flat");
        await Assert.That(Math.Abs(TopAt(following, 30, 20) - room)).IsLessThanOrEqualTo(1)
            .Because("a player walks out of the door rather than into it");

        // And the same room holding keeps the number instead, whatever the relief wants — which is the
        // difference the two words exist to state, and the face is the author's to accept.
        var holding = HeldOverRelief.Replace("HEIGHT", "9");
        await Assert.That(TopAt(holding, 30, 30)).IsEqualTo(9);
    }
}
