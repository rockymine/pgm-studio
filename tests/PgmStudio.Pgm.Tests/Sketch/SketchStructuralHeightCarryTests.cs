using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// What happens to an author-stated structural height when a plan is recompiled over it. Unlike a relief
/// (<see cref="SketchReliefCarryTests"/>), a structural shape's height rides on the shape itself, and the
/// compiler regenerates that shape's id and rect on every compile — so the carry cannot match by either. It
/// matches by <c>intentRef</c>, the team/owner:colour identity a spawn or wool room keeps across a recompile,
/// and only for a shape the author actually corrected (<c>height_authored</c>).
/// </summary>
public sealed class SketchStructuralHeightCarryTests
{
    // A freshly compiled layout: one structural spawn annotation at the plan's flat surface (9), one plain
    // terrain shape it sits on. The compiler never sets height_authored.
    private static string Compiled(string intentRef = "red", double baseHeight = 9) => $$"""
    {
      "layout": {
        "shapes": [
          { "id": "s0", "type": "polygon", "operation": "add", "base_height": 9,
            "vertices": [[0,0],[0,20],[20,20],[20,0]] },
          { "id": "spawn-red", "type": "rectangle", "operation": "add", "role": "spawn",
            "intentRef": "{{intentRef}}", "color": "red",
            "min_x": 0, "min_z": 0, "max_x": 10, "max_z": 10,
            "base_height": {{baseHeight}}, "relief_scope": "hold" }
        ],
        "islands": [ { "id": "team", "mirrors": true, "shapeIds": ["s0"] } ]
      }
    }
    """;

    // A stored layout carrying an author correction: the spawn piece's floor/base_height moved once real
    // relief existed, flagged height_authored so a recompile knows to keep it.
    private static string StoredWithCorrection(string intentRef = "red", double floor = 2, double baseHeight = 4) => $$"""
    {
      "layout": {
        "shapes": [
          { "id": "spawn-red", "type": "rectangle", "operation": "add", "role": "spawn",
            "intentRef": "{{intentRef}}", "color": "red",
            "min_x": 0, "min_z": 0, "max_x": 10, "max_z": 10,
            "floor": {{floor}}, "base_height": {{baseHeight}}, "relief_scope": "hold",
            "height_authored": true }
        ],
        "islands": [ { "id": "team", "mirrors": true, "shapeIds": [] } ]
      }
    }
    """;

    [Test]
    public async Task An_authored_height_survives_a_recompile_matched_by_intent_ref()
    {
        var merged = SketchLayout.CarryStructuralHeight(Compiled(), StoredWithCorrection());
        var state = SketchLayout.Parse(merged);
        var spawn = state!.Layout!.Shapes.Single(s => s.Id == "spawn-red");

        await Assert.That(spawn.Floor).IsEqualTo(2);
        await Assert.That(spawn.BaseHeight).IsEqualTo(4);
        await Assert.That(spawn.HeightAuthored).IsTrue();
        // The compiler's own rect/relief_scope are untouched — only the height fields move.
        await Assert.That(spawn.MinX).IsEqualTo(0);
        await Assert.That(spawn.ReliefScope).IsEqualTo("hold");
    }

    [Test]
    public async Task An_untouched_piece_keeps_tracking_the_plans_own_surface()
    {
        // Stored has no height_authored flag at all — nothing to carry, the fresh plan value stands.
        const string stored = """
        {
          "layout": {
            "shapes": [
              { "id": "spawn-red", "type": "rectangle", "operation": "add", "role": "spawn",
                "intentRef": "red", "color": "red",
                "min_x": 0, "min_z": 0, "max_x": 10, "max_z": 10, "base_height": 9, "relief_scope": "hold" }
            ],
            "islands": [ { "id": "team", "mirrors": true, "shapeIds": [] } ]
          }
        }
        """;
        var merged = SketchLayout.CarryStructuralHeight(Compiled(), stored);
        await Assert.That(merged).IsEqualTo(Compiled());
    }

    [Test]
    public async Task A_correction_on_a_different_intent_ref_does_not_cross_over()
    {
        // The stored correction belongs to "blue"; the compiled layout only has a "red" spawn piece.
        var merged = SketchLayout.CarryStructuralHeight(Compiled(), StoredWithCorrection(intentRef: "blue"));
        await Assert.That(merged).IsEqualTo(Compiled());
    }

    [Test]
    public async Task No_stored_layout_leaves_the_compiled_json_untouched()
    {
        await Assert.That(SketchLayout.CarryStructuralHeight(Compiled(), null)).IsEqualTo(Compiled());
        await Assert.That(SketchLayout.CarryStructuralHeight(Compiled(), "")).IsEqualTo(Compiled());
    }

    [Test]
    public async Task Terrain_shapes_without_a_role_are_never_matched()
    {
        // A plain terrain shape sharing no intentRef can't accidentally pick up a correction — role gates it.
        var merged = SketchLayout.CarryStructuralHeight(Compiled(), StoredWithCorrection());
        var state = SketchLayout.Parse(merged);
        var terrain = state!.Layout!.Shapes.Single(s => s.Id == "s0");
        await Assert.That(terrain.BaseHeight).IsEqualTo(9);
        await Assert.That(terrain.HeightAuthored).IsNull();
    }

    [Test]
    public async Task Carrying_structural_height_leaves_relief_and_finish_carries_alone()
    {
        // All three carries run together on a recompile (SketchFromPlanEndpoint) and must not interfere.
        const string stored = """
        {
          "layout": {
            "shapes": [
              { "id": "spawn-red", "type": "rectangle", "operation": "add", "role": "spawn",
                "intentRef": "red", "color": "red",
                "min_x": 0, "min_z": 0, "max_x": 10, "max_z": 10,
                "floor": 2, "base_height": 4, "relief_scope": "hold", "height_authored": true }
            ],
            "islands": [ { "id": "team", "mirrors": true, "shapeIds": [] } ]
          },
          "mapTheme": "grass",
          "relief": { "team": { "base": 6 } }
        }
        """;
        var compiled = Compiled();
        var merged = SketchLayout.CarryStructuralHeight(
            SketchLayout.CarryRelief(SketchLayout.CarryFinish(compiled, stored), stored), stored);
        var state = SketchLayout.Parse(merged);

        await Assert.That(state!.MapTheme).IsEqualTo("grass");
        await Assert.That(state.Relief!.ContainsKey("team")).IsTrue();
        var spawn = state.Layout!.Shapes.Single(s => s.Id == "spawn-red");
        await Assert.That(spawn.Floor).IsEqualTo(2);
        await Assert.That(spawn.BaseHeight).IsEqualTo(4);
    }

    // A held spawn piece over an island whose relief climbs from 6 to 26 (SketchReliefScopeTests' fixture),
    // proving the carried height is not just JSON that round-trips but a real input to the solve: the
    // rasterizer binds a Role-tagged hold shape to its island by footprint (SketchRasterizer §"groundSoFar"),
    // not by ShapeIds membership, which is exactly the path a spawn/wool annotation takes.
    private const string HeldOverRelief = """
    {
      "layout": {
        "shapes": [
          { "id": "land", "type": "rectangle", "operation": "add",
            "min_x": 0, "min_z": 0, "max_x": 60, "max_z": 60, "base_height": 5, "floor": 0 },
          { "id": "spawn-red", "type": "rectangle", "operation": "add", "role": "spawn",
            "intentRef": "red", "color": "red",
            "min_x": 22, "min_z": 22, "max_x": 38, "max_z": 38,
            "base_height": HEIGHT, "relief_scope": "hold", "height_authored": true }
        ],
        "islands": [ { "id": "i1", "mirrors": false, "shapeIds": ["land"] } ]
      },
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

    [Test]
    public async Task An_uncorrected_room_is_seated_on_the_terrain_rather_than_on_the_plans_number()
    {
        // The flag is what separates the two. A height the author has stated against real ground is theirs;
        // one carried over from a plan was stated before any terrain existed, and holding a room there is
        // what leaves a spawn door facing a wall the relief built around it.
        var uncorrected = HeldOverRelief.Replace("HEIGHT", "9").Replace(""", "height_authored": true""", "");

        int TopAt(string layoutJson, int x, int z) =>
            SketchRasterizer.RasterizeColumns(layoutJson).Single(c => c.X == x && c.Z == z).YTop;

        var room = TopAt(uncorrected, 30, 30);
        await Assert.That(room).IsNotEqualTo(9).Because("the plan's flat number is not a statement about terrain");

        // It is still a room: flat across its whole floor, and level with the ground it opens onto.
        var floor = new List<int>();
        for (var x = 23; x < 38; x++)
            for (var z = 23; z < 38; z++)
                floor.Add(TopAt(uncorrected, x, z));
        await Assert.That(floor.Distinct().Count()).IsEqualTo(1).Because("a held room is flat");
        await Assert.That(Math.Abs(TopAt(uncorrected, 30, 20) - room)).IsLessThanOrEqualTo(1)
            .Because("a player walks out of the door rather than into it");
    }
}
