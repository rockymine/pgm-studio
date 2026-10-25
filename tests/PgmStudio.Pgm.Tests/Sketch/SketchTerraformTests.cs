using System.Globalization;
using PgmStudio.Geom;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The modifier stack as the build sees it: run over the ground the layers built, in list order, before anything
/// is seated on it, leaving made things and play rooms where they are. Every test reads the board through
/// <see cref="SketchRasterizer.RasterizeColumns(SketchLayout?)"/> — the one place every read of the ground comes from.
/// </summary>
public sealed class SketchTerraformTests
{
    // A square ground eight thick, 40 on a side, folding about its middle.
    private const string Ground = """
        { "id": "ground", "base_y": 0, "layout": { "shapes": [
          { "id": "board", "type": "rectangle", "operation": "add", "min_x": 0, "min_z": 0, "max_x": 40, "max_z": 40, "base_height": 8 }
          {0}
        ], "groups": [] } }
        """;

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Hill(string id = "hill", string extra = "", double x = 20.5, double z = 20.5,
                               double radius = 10, double rise = 6, bool mirror = false, string leaves = "") =>
        $$"""
        { "id": "{{id}}", "op": "mound", "rise": {{Num(rise)}}, {{extra}}
          "mask": { "areas": [ { "kind": "ellipse", "at": [{{Num(x)}}, {{Num(z)}}], "rx": {{Num(radius)}}, "rz": {{Num(radius)}} } ],
                    "mirror": {{(mirror ? "true" : "false")}}{{leaves}} } }
        """;

    private static string LayoutJson(string modifiers, string mode = "none", string roomShapes = "", string otherLayers = "") =>
        $$"""
        { "setup": { "mirror_mode": "{{mode}}", "center": { "cx": 20, "cz": 20 } },
          "layers": [ {{Ground.Replace("{0}", roomShapes.Length == 0 ? "" : "," + roomShapes)}} {{otherLayers}} ],
          "modifiers": [ {{modifiers}} ] }
        """;

    private static SketchLayout Layout(string modifiers, string mode = "none", string roomShapes = "", string otherLayers = "") =>
        SketchLayout.Parse(LayoutJson(modifiers, mode, roomShapes, otherLayers))!;

    private static int Top(SketchLayout layout, int x, int z, string layer = "ground") =>
        SketchRasterizer.RasterizeColumns(layout).Single(column => column.X == x && column.Z == z && column.Layer == layer).YTop;

    private static Dictionary<(int X, int Z), int> Surfaces(SketchLayout layout) =>
        SketchRasterizer.WaterOf(layout).ToDictionary(column => column.Key, column => column.Value.Surface);

    private static Dictionary<(int X, int Z), int> Tops(SketchLayout layout) =>
        SketchRasterizer.RasterizeColumns(layout).Where(column => column.Layer == "ground")
                        .ToDictionary(column => column.Cell, column => column.YTop);

    private const string Spawn = """
        { "id": "spawn-1", "type": "rectangle", "operation": "add", "role": "spawn",
          "min_x": 18, "min_z": 18, "max_x": 22, "max_z": 22, "base_height": 8 }
        """;

    [Test]
    public async Task A_hill_raises_the_middle_column_and_leaves_a_corner()
    {
        var layout = Layout(Hill());

        await Assert.That(Top(layout, 20, 20)).IsEqualTo(14);
        await Assert.That(Top(layout, 0, 0)).IsEqualTo(8);
        await Assert.That(Top(layout, 20, 20)).IsGreaterThan(Top(layout, 26, 20));
        await Assert.That(Top(layout, 26, 20)).IsGreaterThan(8);
    }

    [Test]
    public async Task A_layout_with_no_modifier_builds_the_ground_its_layers_drew()
    {
        var layout = Layout("");

        await Assert.That(Top(layout, 20, 20)).IsEqualTo(8);
        await Assert.That(SketchRasterizer.RasterizeColumns(layout).Count).IsEqualTo(40 * 40);
    }

    [Test]
    public async Task A_modifier_that_is_off_changes_nothing()
    {
        var plain = SketchRasterizer.RasterizeColumns(Layout(""));
        var off = SketchRasterizer.RasterizeColumns(Layout(Hill(extra: "\"off\": true,")));

        await Assert.That(off.SequenceEqual(plain)).IsTrue();
    }

    [Test]
    public async Task A_modifier_the_build_does_not_know_changes_nothing()
    {
        var plain = SketchRasterizer.RasterizeColumns(Layout(""));
        var unknown = SketchRasterizer.RasterizeColumns(Layout(
            """{ "id": "x", "op": "bogus", "mask": { "areas": [{ "kind": "board" }], "mirror": false } }"""));

        await Assert.That(unknown.SequenceEqual(plain)).IsTrue();
    }

    [Test]
    public async Task The_footprint_is_the_one_the_layers_drew()
    {
        var plain = SketchRasterizer.RasterizeColumns(Layout("")).Select(column => column.Cell).ToHashSet();
        var raised = SketchRasterizer.RasterizeColumns(Layout(Hill())).Select(column => column.Cell).ToHashSet();

        await Assert.That(raised.SetEquals(plain)).IsTrue();
    }

    [Test]
    public async Task A_mirrored_hill_raises_both_images_equally()
    {
        var tops = Tops(Layout(Hill(x: 8.5, z: 11.5, radius: 5, mirror: true), mode: "rot_180"));
        var image = Symmetry.ApplyCell(8, 11, "rot_180", 20, 20);

        await Assert.That(tops[(8, 11)]).IsEqualTo(14);
        await Assert.That(tops[image]).IsEqualTo(14);
        foreach (var (cell, top) in tops)
            await Assert.That(tops[Symmetry.ApplyCell(cell.X, cell.Z, "rot_180", 20, 20)]).IsEqualTo(top);
    }

    [Test]
    public async Task An_unmirrored_hill_works_only_where_it_is_drawn()
    {
        var layout = Layout(Hill(x: 8.5, z: 11.5, radius: 5, mirror: false), mode: "rot_180");
        var (imageX, imageZ) = Symmetry.ApplyCell(8, 11, "rot_180", 20, 20);

        await Assert.That(Top(layout, 8, 11)).IsEqualTo(14);
        await Assert.That(Top(layout, imageX, imageZ)).IsEqualTo(8);
    }

    private static readonly (int X, int Z)[] RoomCells = [(18, 18), (20, 20), (21, 21), (18, 21), (21, 18)];

    [Test]
    public async Task A_play_room_stands_level_on_the_ground_the_stack_left()
    {
        var layout = Layout(Hill(), roomShapes: Spawn.Replace(", \"base_height\": 8", ""));
        var tops = RoomCells.Select(cell => Top(layout, cell.X, cell.Z)).ToList();

        await Assert.That(tops.Distinct().Count()).IsEqualTo(1).Because("a room moves as one, so its floor stays level");
        await Assert.That(tops[0]).IsGreaterThan(8).Because("the hill under the room lifted the ground it stands on");
        await Assert.That(tops[0]).IsLessThanOrEqualTo(Top(Layout(Hill()), 20, 20));
    }

    [Test]
    public async Task A_play_room_stating_hold_keeps_the_ground_it_was_drawn_on()
    {
        var layout = Layout(Hill(), roomShapes: Spawn.Replace("\"base_height\": 8", "\"base_height\": 8, \"relief_scope\": \"hold\""));

        foreach (var (x, z) in RoomCells)
            await Assert.That(Top(layout, x, z)).IsEqualTo(8);
        await Assert.That(Top(layout, 22, 20)).IsGreaterThan(8);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task A_play_room_stating_a_height_stands_at_it(bool hill)
    {
        var authored = Spawn.Replace("\"base_height\": 8", "\"base_height\": 12, \"height_authored\": true");
        var layout = Layout(hill ? Hill() : "", roomShapes: authored);

        foreach (var (x, z) in RoomCells)
            await Assert.That(Top(layout, x, z)).IsEqualTo(12);
    }

    [Test]
    public async Task A_made_thing_stays_where_it_was_drawn()
    {
        const string sculpture = """
            { "id": "tower", "kind": "made", "base_y": 30, "layout": { "shapes": [
              { "id": "block", "type": "rectangle", "operation": "add", "min_x": 16, "min_z": 16, "max_x": 24, "max_z": 24, "base_height": 4 }
            ], "groups": [] } }
            """;
        var layout = Layout(Hill(), otherLayers: "," + sculpture);

        await Assert.That(Top(layout, 20, 20, "tower")).IsEqualTo(34);
        await Assert.That(Top(layout, 20, 20)).IsEqualTo(14);
    }

    [Test]
    public async Task A_thing_seated_on_the_ground_settles_on_the_ground_the_stack_left()
    {
        const string perched = """
            { "id": "perch", "kind": "made", "seat": "ground", "base_y": 0, "layout": { "shapes": [
              { "id": "block", "type": "rectangle", "operation": "add", "min_x": 19, "min_z": 19, "max_x": 22, "max_z": 22,
                "floor": 30, "base_height": 4 }
            ], "groups": [] } }
            """;
        var plain = SketchRasterizer.RasterizeColumns(Layout("", otherLayers: "," + perched))
                                    .Where(column => column.Layer == "perch").Min(column => column.YFloor);
        var raised = SketchRasterizer.RasterizeColumns(Layout(Hill(), otherLayers: "," + perched))
                                     .Where(column => column.Layer == "perch").Min(column => column.YFloor);

        await Assert.That(raised).IsGreaterThan(plain);
    }

    [Test]
    public async Task A_top_lowered_below_its_segments_floor_stops_at_the_floor()
    {
        var slab = """
            { "id": "ground", "base_y": 0, "layout": { "shapes": [
              { "id": "board", "type": "rectangle", "operation": "add", "min_x": 0, "min_z": 0, "max_x": 40, "max_z": 40,
                "floor": 6, "base_height": 2 }
            ], "groups": [] } }
            """;
        var layout = SketchLayout.Parse($$"""
            { "setup": { "mirror_mode": "none", "center": { "cx": 20, "cz": 20 } }, "layers": [ {{slab}} ],
              "modifiers": [ { "id": "dig", "op": "crater", "floor": 0,
                "mask": { "areas": [ { "kind": "ellipse", "at": [20.5, 20.5], "rx": 6, "rz": 6 } ], "mirror": false } } ] }
            """)!;

        var middle = SketchRasterizer.RasterizeColumns(layout).Single(column => column.X == 20 && column.Z == 20);
        await Assert.That(middle.YFloor).IsEqualTo(6);
        await Assert.That(middle.YTop).IsEqualTo(6);
    }

    [Test]
    public async Task Modifiers_run_in_list_order_and_over_each_others_result()
    {
        const string level = """{ "id": "flat", "op": "level", "top": 10, "mask": { "areas": [{ "kind": "board" }], "mirror": false } }""";
        var hillThenLevel = Layout(Hill() + "," + level);
        var levelThenHill = Layout(level + "," + Hill());

        await Assert.That(Top(hillThenLevel, 20, 20)).IsEqualTo(10);
        await Assert.That(Top(levelThenHill, 20, 20)).IsEqualTo(16);
    }

    [Test]
    public async Task Heights_are_rounded_once_after_the_whole_stack()
    {
        var layout = Layout(Hill("a", rise: 0.4) + "," + Hill("b", rise: 0.4));

        await Assert.That(Top(layout, 20, 20)).IsEqualTo(9);       // 8 + 0.4 + 0.4 = 8.8; rounded each time it would stay 8
    }

    private static string Pad(string extra) => $$"""
        { "id": "pad", "type": "rectangle", "operation": "add", "min_x": 18, "min_z": 18, "max_x": 22, "max_z": 22,
          "base_height": 8 {{extra}} }
        """;

    [Test]
    public async Task A_shape_stating_hold_keeps_its_columns_out_of_the_stack()
    {
        var layout = Layout(Hill(), roomShapes: Pad(", \"relief_scope\": \"hold\""));

        foreach (var (x, z) in new[] { (18, 18), (20, 20), (21, 21), (18, 21) })
            await Assert.That(Top(layout, x, z)).IsEqualTo(8);
        await Assert.That(Top(layout, 22, 20)).IsGreaterThan(8);
        await Assert.That(Top(layout, 20, 22)).IsGreaterThan(8);
    }

    [Test]
    public async Task A_shape_stating_no_scope_follows_the_stack()
    {
        var layout = Layout(Hill(), roomShapes: Pad(""));

        await Assert.That(Top(layout, 20, 20)).IsEqualTo(14);
    }

    [Test]
    public async Task A_word_the_build_does_not_know_follows_the_stack()
    {
        var layout = Layout(Hill(), roomShapes: Pad(", \"relief_scope\": \"exclude\""));

        await Assert.That(Top(layout, 20, 20)).IsEqualTo(14);
    }

    [Test]
    public async Task A_shape_standing_at_its_own_height_mode_is_held()
    {
        var layout = Layout(Hill(), roomShapes: Pad(", \"height_mode\": \"level\", \"base_height\": 11"));

        foreach (var (x, z) in new[] { (18, 18), (20, 20), (21, 21) })
            await Assert.That(Top(layout, x, z)).IsEqualTo(11);
        await Assert.That(Top(layout, 22, 20)).IsGreaterThan(8);
    }

    [Test]
    public async Task A_held_shape_is_held_at_its_image_under_the_symmetry()
    {
        const string held = """
            { "id": "pad", "type": "rectangle", "operation": "add", "min_x": 6, "min_z": 9, "max_x": 11, "max_z": 14,
              "base_height": 8, "relief_scope": "hold" }
            """;
        var tops = Tops(Layout(Hill(x: 8.5, z: 11.5, radius: 5, mirror: true), mode: "rot_180", roomShapes: held));
        var image = Symmetry.ApplyCell(8, 11, "rot_180", 20, 20);

        await Assert.That(tops[(8, 11)]).IsEqualTo(8);
        await Assert.That(tops[image]).IsEqualTo(8);
        await Assert.That(tops[(12, 11)]).IsGreaterThan(8);
        await Assert.That(tops[Symmetry.ApplyCell(12, 11, "rot_180", 20, 20)]).IsGreaterThan(8);
    }

    private const string Wall = """
        { "id": "wall", "type": "rectangle", "operation": "add", "min_x": 12, "min_z": 20, "max_x": 30, "max_z": 21,
          "base_height": 2, "height_mode": "drape" }
        """;

    [Test]
    public async Task A_draped_shape_climbs_the_ground_the_stack_raised_under_it()
    {
        var bare = Layout(Hill());
        var draped = Layout(Hill(), roomShapes: Wall);

        foreach (var x in new[] { 12, 16, 20, 24, 29 })
            await Assert.That(Top(draped, x, 20)).IsEqualTo(Top(bare, x, 20) + 2);
        await Assert.That(Top(draped, 20, 20)).IsGreaterThan(Top(draped, 12, 20));
    }

    [Test]
    public async Task A_raised_shape_moves_with_the_ground_under_it_as_one_flat_thing()
    {
        var tops = Tops(Layout(Hill(), roomShapes: Pad(", \"height_mode\": \"raise\", \"base_height\": 3")));
        var plinth = new[] { (18, 18), (20, 20), (21, 21), (18, 21) }.Select(cell => tops[cell]).Distinct().ToList();

        await Assert.That(plinth.Count).IsEqualTo(1);
        await Assert.That(plinth[0]).IsGreaterThan(Tops(Layout(""))[(20, 20)] + 3);
    }

    private static string Stepped(string modifiers, int step) =>
        SketchSteps.Through(LayoutJson(modifiers), step);

    private static Dictionary<(int X, int Z), int> TopsOf(string layoutJson) =>
        SketchRasterizer.RasterizeColumns(layoutJson).Where(column => column.Layer == "ground")
                        .ToDictionary(column => column.Cell, column => column.YTop);

    private const string BoardLevel =
        """{ "id": "flat", "op": "level", "top": 10, "mask": { "areas": [{ "kind": "board" }], "mirror": false } }""";

    [Test]
    public async Task Step_zero_is_the_ground_the_shapes_alone_build()
    {
        var stack = Hill() + "," + BoardLevel;

        await Assert.That(TopsOf(Stepped(stack, 0)).SequenceEqual(Tops(Layout("")))).IsTrue();
    }

    [Test]
    public async Task Step_n_is_the_ground_after_the_first_n_modifiers()
    {
        var stack = Hill() + "," + BoardLevel;

        await Assert.That(TopsOf(Stepped(stack, 1)).SequenceEqual(Tops(Layout(Hill())))).IsTrue();
        await Assert.That(TopsOf(Stepped(stack, 2)).SequenceEqual(Tops(Layout(stack)))).IsTrue();
        await Assert.That(TopsOf(Stepped(stack, 9)).SequenceEqual(Tops(Layout(stack)))).IsTrue();
    }

    [Test]
    public async Task A_modifier_switched_off_is_not_a_step()
    {
        var off = Hill("idle", extra: "\"off\": true,", x: 8.5, z: 8.5, radius: 4);
        var stack = off + "," + Hill() + "," + BoardLevel;

        await Assert.That(TopsOf(Stepped(stack, 1)).SequenceEqual(Tops(Layout(Hill())))).IsTrue();
        await Assert.That(TopsOf(Stepped(stack, 0)).SequenceEqual(Tops(Layout("")))).IsTrue();
    }

    [Test]
    public async Task Cutting_the_stack_changes_nothing_else_in_the_document()
    {
        var cut = System.Text.Json.Nodes.JsonNode.Parse(Stepped(Hill() + "," + BoardLevel, 1))!;
        var whole = System.Text.Json.Nodes.JsonNode.Parse(LayoutJson(Hill() + "," + BoardLevel))!;

        await Assert.That(cut["modifiers"]!.AsArray().Count).IsEqualTo(1);
        await Assert.That(cut["layers"]!.ToJsonString()).IsEqualTo(whole["layers"]!.ToJsonString());
        await Assert.That(cut["setup"]!.ToJsonString()).IsEqualTo(whole["setup"]!.ToJsonString());
    }

    [Test]
    public async Task Each_modifier_counts_the_columns_its_own_run_moved()
    {
        var off = Hill("idle", extra: "\"off\": true,");
        var layout = Layout(Hill() + "," + off + "," + BoardLevel);

        var changes = SketchRasterizer.ModifierChanges(layout);

        var hilled = Tops(Layout(Hill()));
        var levelled = Tops(layout);
        await Assert.That(changes.Select(change => change.Id).SequenceEqual(new[] { "hill", "idle", "flat" })).IsTrue();
        await Assert.That(changes[0].Columns).IsEqualTo(hilled.Count(column => column.Value != 8));
        await Assert.That(changes[1].Columns).IsEqualTo(0);
        await Assert.That(changes[2].Columns).IsEqualTo(hilled.Count(column => levelled[column.Key] != column.Value));
        await Assert.That(changes[2].Columns).IsGreaterThan(0);
    }

    [Test]
    public async Task A_layout_with_no_modifier_has_no_counts()
    {
        await Assert.That(SketchRasterizer.ModifierChanges(Layout("")).Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_shape_stating_ride_keeps_its_form_and_moves_by_the_median_change_under_it()
    {
        var bare = Tops(Layout(Hill()));
        var padCells = Enumerable.Range(18, 4).SelectMany(x => Enumerable.Range(18, 4).Select(z => (x, z))).ToList();
        var median = padCells.Select(cell => bare[cell]).Order().ElementAt(padCells.Count / 2);

        var ridden = Tops(Layout(Hill(), roomShapes: Pad(", \"relief_scope\": \"ride\"")));

        await Assert.That(padCells.Select(cell => ridden[cell]).Distinct().Single()).IsEqualTo(median);
        await Assert.That(ridden[(18, 18)]).IsNotEqualTo(bare[(18, 18)]);
        await Assert.That(ridden[(30, 20)]).IsEqualTo(bare[(30, 20)]);
    }

    [Test]
    public async Task A_shape_stating_ride_rides_at_its_image_under_the_symmetry()
    {
        const string rider = """
            { "id": "pad", "type": "rectangle", "operation": "add", "min_x": 6, "min_z": 9, "max_x": 11, "max_z": 14,
              "base_height": 8, "relief_scope": "ride" }
            """;
        var tops = Tops(Layout(Hill(x: 8.5, z: 11.5, radius: 5, mirror: true), mode: "rot_180", roomShapes: rider));
        var image = Symmetry.ApplyCell(8, 11, "rot_180", 20, 20);
        var imageCells = Enumerable.Range(6, 5).SelectMany(x => Enumerable.Range(9, 5).Select(z => Symmetry.ApplyCell(x, z, "rot_180", 20, 20)));

        await Assert.That(imageCells.Select(cell => tops[cell]).Distinct().Count()).IsEqualTo(1);
        await Assert.That(tops[image]).IsEqualTo(tops[(8, 11)]);
    }

    [Test]
    public async Task A_modifier_leaves_out_the_shapes_it_names()
    {
        var left = Layout(Hill(leaves: ", \"leaves\": [\"pad\"]"), roomShapes: Pad(""));
        var tops = Tops(left);
        var plain = Tops(Layout(Hill(), roomShapes: Pad("")));

        foreach (var (x, z) in new[] { (18, 18), (20, 20), (21, 21), (18, 21) })
            await Assert.That(tops[(x, z)]).IsEqualTo(8);
        await Assert.That(tops[(23, 20)]).IsEqualTo(plain[(23, 20)]);
        await Assert.That(plain[(20, 20)]).IsEqualTo(14);
    }

    [Test]
    public async Task A_leave_out_binds_only_the_modifier_that_names_it()
    {
        var stack = Hill(leaves: ", \"leaves\": [\"pad\"]") + "," + Hill("second", rise: 4);
        var tops = Tops(Layout(stack, roomShapes: Pad("")));

        await Assert.That(tops[(20, 20)]).IsEqualTo(12);
    }

    [Test]
    public async Task A_leave_out_holds_at_the_images_of_the_shape()
    {
        const string pad = """
            { "id": "pad", "type": "rectangle", "operation": "add", "min_x": 6, "min_z": 9, "max_x": 11, "max_z": 14, "base_height": 8 }
            """;
        var tops = Tops(Layout(Hill(x: 8.5, z: 11.5, radius: 5, mirror: true, leaves: ", \"leaves\": [\"pad\"]"),
                               mode: "rot_180", roomShapes: pad));
        var image = Symmetry.ApplyCell(8, 11, "rot_180", 20, 20);

        await Assert.That(tops[(8, 11)]).IsEqualTo(8);
        await Assert.That(tops[image]).IsEqualTo(8);
        await Assert.That(tops[(12, 11)]).IsGreaterThan(8);
    }

    [Test]
    public async Task A_leave_out_naming_no_shape_changes_nothing()
    {
        var named = Tops(Layout(Hill(leaves: ", \"leaves\": [\"nothing\"]")));

        await Assert.That(named.SequenceEqual(Tops(Layout(Hill())))).IsTrue();
    }

    [Test]
    public async Task A_left_out_shape_is_not_counted_as_moved()
    {
        var changes = SketchRasterizer.ModifierChanges(Layout(Hill(leaves: ", \"leaves\": [\"pad\"]"), roomShapes: Pad("")));
        var all = SketchRasterizer.ModifierChanges(Layout(Hill(), roomShapes: Pad(""))).Single().Columns;

        await Assert.That(changes.Single().Columns).IsEqualTo(all - 16);
    }

    private static string River(string id = "river", int depth = 2, int water = 1, string extra = "", bool mirror = false) =>
        $$"""
        { "id": "{{id}}", "op": "river", "depth": {{depth}}, "water": {{water}}, "bank": 3, {{extra}}
          "mask": { "areas": [ { "kind": "line", "points": [[0.5, 20.5], [39.5, 20.5]], "width": 4 } ],
                    "mirror": {{(mirror ? "true" : "false")}} } }
        """;

    private static string Lake(string extra = "") =>
        $$"""
        { "id": "lake", "op": "lake", "depth": 3, "shore": 3, {{extra}}
          "mask": { "areas": [ { "kind": "ellipse", "at": [20.5, 20.5], "rx": 8, "rz": 8 } ], "mirror": false } }
        """;

    [Test]
    public async Task A_river_cuts_its_bed_under_the_ground_and_wets_the_columns_it_cut()
    {
        var layout = Layout(River());
        var water = Surfaces(layout);

        await Assert.That(Top(layout, 20, 20)).IsEqualTo(6);
        await Assert.That(water[(20, 20)]).IsEqualTo(7);
        await Assert.That(Top(layout, 20, 10)).IsEqualTo(8);
        await Assert.That(water.ContainsKey((20, 10))).IsFalse();
        await Assert.That(water.Count).IsEqualTo(5 * 40).Because("the channel is five rows across the whole board");
        foreach (var (cell, surface) in water)
            await Assert.That(Top(layout, cell.X, cell.Z)).IsLessThan(surface);
    }

    [Test]
    public async Task A_lake_cuts_a_bowl_under_its_level_and_wets_it()
    {
        var layout = Layout(Lake("\"level\": 7,"));
        var water = Surfaces(layout);

        await Assert.That(water[(20, 20)]).IsEqualTo(7);
        await Assert.That(Top(layout, 20, 20)).IsEqualTo(4).Because("three blocks of water over the bed in the middle");
        await Assert.That(water.ContainsKey((20, 34))).IsFalse();
        await Assert.That(water.Keys.All(cell => Top(layout, cell.X, cell.Z) < 7)).IsTrue();
    }

    [Test]
    public async Task Ground_a_later_modifier_raises_to_the_surface_is_dry()
    {
        const string plateau = """{ "id": "plateau", "op": "level", "top": 12, "mask": { "areas": [ { "kind": "ellipse", "at": [20.5, 20.5], "rx": 6, "rz": 6 } ], "mirror": false } }""";
        var layout = Layout(River() + "," + plateau);
        var water = Surfaces(layout);

        await Assert.That(Top(layout, 20, 20)).IsEqualTo(12);
        await Assert.That(water.ContainsKey((20, 20))).IsFalse();
        await Assert.That(water.ContainsKey((3, 20))).IsTrue();
        await Assert.That(water.ContainsKey((38, 20))).IsTrue();
    }

    [Test]
    public async Task A_river_drawn_after_another_replaces_its_water_where_they_cross()
    {
        var layout = Layout(River() + "," + River("deeper", depth: 4, water: 2));

        await Assert.That(Surfaces(layout)[(20, 20)]).IsEqualTo(Top(layout, 20, 20) + 2);
    }

    [Test]
    public async Task A_mirrored_river_wets_the_columns_of_its_image_too()
    {
        const string bend = """
            { "id": "stream", "op": "river", "depth": 2, "water": 1, "bank": 3,
              "mask": { "areas": [ { "kind": "line", "points": [[2.5, 6.5], [18.5, 10.5]], "width": 4 } ], "mirror": true } }
            """;
        var layout = Layout(bend, mode: "rot_180");
        var water = Surfaces(layout);
        var image = Symmetry.ApplyCell(10, 8, "rot_180", 20, 20);

        await Assert.That(water.ContainsKey((10, 8))).IsTrue();
        await Assert.That(water.ContainsKey(image)).IsTrue();
        foreach (var (cell, surface) in water)
            await Assert.That(water[Symmetry.ApplyCell(cell.X, cell.Z, "rot_180", 20, 20)]).IsEqualTo(surface);
    }

    [Test]
    public async Task A_river_switched_off_makes_no_water_and_no_cut()
    {
        var layout = Layout(River(extra: "\"off\": true,"));

        await Assert.That(SketchRasterizer.WaterOf(layout)).IsEmpty();
        await Assert.That(Top(layout, 20, 20)).IsEqualTo(8);
    }

    [Test]
    public async Task A_line_with_no_width_or_a_single_point_draws_nothing()
    {
        const string widthless = """{ "id": "r", "op": "river", "mask": { "areas": [ { "kind": "line", "points": [[0.5, 20.5], [39.5, 20.5]] } ], "mirror": false } }""";
        const string single = """{ "id": "r", "op": "river", "mask": { "areas": [ { "kind": "line", "points": [[0.5, 20.5]], "width": 4 } ], "mirror": false } }""";

        foreach (var modifier in new[] { widthless, single })
        {
            var layout = Layout(modifier);
            await Assert.That(SketchRasterizer.WaterOf(layout)).IsEmpty();
            await Assert.That(Top(layout, 20, 20)).IsEqualTo(8);
        }
    }

    [Test]
    public async Task Water_is_part_of_the_cached_build_and_follows_the_layout_it_was_built_from()
    {
        await Assert.That(SketchRasterizer.WaterOf(Layout(River())).Count).IsGreaterThan(0);
        await Assert.That(SketchRasterizer.WaterOf(Layout("")).Count).IsEqualTo(0);
        await Assert.That(SketchRasterizer.WaterOf(Layout(River())).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task A_ridge_a_scarp_and_a_canyon_move_the_built_ground()
    {
        const string ridge = """{ "id": "r", "op": "ridge", "rise": 5, "wander": 0, "mask": { "areas": [ { "kind": "line", "points": [[0.5, 10.5], [39.5, 10.5]], "width": 6 } ], "mirror": false } }""";
        const string canyon = """{ "id": "c", "op": "canyon", "depth": 5, "mask": { "areas": [ { "kind": "line", "points": [[0.5, 30.5], [39.5, 30.5]], "width": 8 } ], "mirror": false } }""";
        var layout = Layout(ridge + "," + canyon);

        await Assert.That(Top(layout, 20, 10)).IsEqualTo(13);
        await Assert.That(Top(layout, 20, 30)).IsEqualTo(3);
        await Assert.That(Top(layout, 20, 20)).IsEqualTo(8);
    }

    [Test]
    public async Task A_coast_floods_its_area_to_the_sea_and_leaves_a_beach_above_it()
    {
        var layout = Layout("""
            { "id": "coast", "op": "coast", "level": 7, "shelf": 6, "depth": 4,
              "mask": { "areas": [ { "kind": "polygon", "points": [[0, 0], [12, 0], [12, 40], [0, 40]] } ], "mirror": false } }
            """);
        var water = Surfaces(layout);

        await Assert.That(water[(4, 20)]).IsEqualTo(7);
        await Assert.That(Top(layout, 4, 20)).IsLessThan(7);
        await Assert.That(water.ContainsKey((14, 20))).IsFalse();
        await Assert.That(Top(layout, 12, 20)).IsEqualTo(8).Because("the shore column is a beach one over the sea");
        foreach (var (cell, surface) in water)
            await Assert.That(Top(layout, cell.X, cell.Z)).IsLessThan(surface);
    }

    private const string LineThroughTheHill =
        """{ "kind": "line", "points": [[2.5, 20.5], [20.5, 22.5], [38.5, 20.5]], "width": 8 }""";

    private const string AreaOnTheHill = """{ "kind": "ellipse", "at": [20.5, 20.5], "rx": 10, "rz": 10 }""";

    [Test]
    [Arguments(ModifierOps.Ridge, LineThroughTheHill)]
    [Arguments(ModifierOps.Butte, AreaOnTheHill)]
    [Arguments(ModifierOps.Spire, AreaOnTheHill)]
    [Arguments(ModifierOps.Scarp, LineThroughTheHill)]
    [Arguments(ModifierOps.Canyon, LineThroughTheHill)]
    [Arguments(ModifierOps.River, LineThroughTheHill)]
    [Arguments(ModifierOps.Lake, AreaOnTheHill)]
    [Arguments(ModifierOps.Road, LineThroughTheHill)]
    [Arguments(ModifierOps.Gullies, AreaOnTheHill)]
    [Arguments(ModifierOps.Warp, """{ "kind": "board" }""")]
    [Arguments(ModifierOps.Profile, LineThroughTheHill)]
    [Arguments(ModifierOps.Coast, AreaOnTheHill)]
    [Arguments(ModifierOps.Lumps, AreaOnTheHill)]
    [Arguments(ModifierOps.Shatter, """{ "kind": "board" }""")]
    public async Task Every_landform_word_the_vocabulary_names_runs_over_the_ground_a_hill_left(string op, string area)
    {
        var hill = Hill(radius: 14, rise: 10);
        var stacked = $$"""{ "id": "x", "op": "{{op}}", "seed": 4, "mask": { "areas": [ {{area}} ], "mirror": false } }""";

        var alone = Tops(Layout(hill));
        var with = Tops(Layout(hill + "," + stacked));

        await Assert.That(with.Any(column => column.Value != alone[column.Key])).IsTrue();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task No_water_stands_over_a_dry_bank_where_a_river_meets_a_lake(bool riverFirst)
    {
        var layout = Layout(riverFirst ? River() + "," + Lake() : Lake() + "," + River());
        var water = Surfaces(layout);
        var tops = Tops(layout);

        await Assert.That(water.Count).IsGreaterThan(0);
        foreach (var ((x, z), surface) in water)
            for (var dx = -1; dx <= 1; dx++)
                for (var dz = -1; dz <= 1; dz++)
                {
                    var beside = (x + dx, z + dz);
                    if (water.ContainsKey(beside) || !tops.TryGetValue(beside, out var top)) continue;
                    await Assert.That(top).IsGreaterThanOrEqualTo(surface).Because($"{beside} borders water at {surface}");
                }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task A_river_down_a_slope_keeps_its_falls_and_stands_its_own_depth_everywhere(bool intoLake)
    {
        var layout = Layout(Hill(x: 0.5, z: 20.5, radius: 26, rise: 14) + "," + River() + (intoLake ? "," + Lake() : ""));
        var water = SketchRasterizer.WaterOf(layout);
        var tops = Tops(layout);
        var river = water.Where(column => column.Value.Body == "river").ToList();

        await Assert.That(river.Select(column => column.Value.Surface).Distinct().Count()).IsGreaterThan(1)
                    .Because("the river falls as the hill does");
        foreach (var ((x, z), column) in river)
            await Assert.That(column.Surface - tops[(x, z)]).IsLessThan(5)
                        .Because($"({x}, {z}) holds its own reach's water, not the height of a reach upstream");
    }

    [Test]
    public async Task A_river_run_down_a_hill_only_cuts_and_never_banks_its_water_up()
    {
        var hill = Hill(x: 0.5, z: 20.5, radius: 26, rise: 14);
        var alone = Tops(Layout(hill));
        var withRiver = Tops(Layout(hill + "," + River()));

        foreach (var (cell, top) in withRiver)
            await Assert.That(top).IsLessThanOrEqualTo(alone[cell]).Because($"{cell} stands higher than the hill left it");
    }

    [Test]
    public async Task A_river_along_a_slope_eases_its_banks_into_it()
    {
        var hill = Hill(x: 20.5, z: 2.5, radius: 22, rise: 12);
        var alone = Tops(Layout(hill));
        var layout = Layout(hill + "," + River());
        var tops = Tops(layout);
        var water = SketchRasterizer.WaterOf(layout);

        foreach (var ((x, z), top) in tops)
        {
            if (x < 4 || x > 36 || Math.Abs(z - 20) > 12 || water.ContainsKey((x, z))) continue;
            foreach (var beside in new[] { (x, z + 1), (x + 1, z) })
            {
                if (water.ContainsKey(beside) || !tops.TryGetValue(beside, out var next)) continue;
                var was = Math.Abs(alone[(x, z)] - alone[beside]);
                await Assert.That(Math.Abs(top - next)).IsLessThanOrEqualTo(Math.Max(1, was))
                            .Because($"the bank between {(x, z)} and {beside} steps more than the ground did");
            }
        }
    }

    [Test]
    public async Task A_lake_on_a_slope_eases_its_shore_into_it()
    {
        var hill = Hill(x: 20.5, z: 2.5, radius: 24, rise: 12);
        var alone = Tops(Layout(hill));
        var layout = Layout(hill + "," + Lake());
        var tops = Tops(layout);
        var water = SketchRasterizer.WaterOf(layout);

        foreach (var ((x, z), top) in tops)
        {
            if (water.ContainsKey((x, z))) continue;
            foreach (var beside in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            {
                if (water.TryGetValue(beside, out var wet))
                    await Assert.That(top).IsLessThanOrEqualTo(wet.Surface + 1).Because($"{(x, z)} stands over the water beside it as a wall");
                else if (tops.TryGetValue(beside, out var next) && double.Hypot(x + 0.5 - 20.5, z + 0.5 - 20.5) < 18)
                    await Assert.That(Math.Abs(top - next)).IsLessThanOrEqualTo(Math.Max(1, Math.Abs(alone[(x, z)] - alone[beside])))
                                .Because($"the shore between {(x, z)} and {beside} steps more than the ground did");
            }
        }
    }

    [Test]
    public async Task A_river_leaving_a_lake_leaves_at_its_surface()
    {
        const string outflow = """
            { "id": "outflow", "op": "river", "depth": 2, "water": 1, "bank": 3,
              "mask": { "areas": [ { "kind": "line", "points": [[20.5, 20.5], [39.5, 20.5]], "width": 4 } ], "mirror": false } }
            """;
        var layout = Layout(Lake("\"level\": 7,") + "," + outflow);
        var tops = Tops(layout);

        for (var x = 32; x < 39; x++)
            await Assert.That(tops[(x, 20)]).IsGreaterThanOrEqualTo(7 - 2)
                        .Because($"({x}, 20) is past the lake's shore, and the river's bed lies its depth under the surface it left at");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Water_that_touches_stands_at_one_surface(bool riverFirst)
    {
        var water = Surfaces(Layout(riverFirst ? River() + "," + Lake() : Lake() + "," + River()));

        foreach (var ((x, z), surface) in water)
            foreach (var beside in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
                if (water.TryGetValue(beside, out var other))
                    await Assert.That(other).IsEqualTo(surface).Because($"{(x, z)} and {beside} are one body of water");
    }

    [Test]
    public async Task Each_wet_column_belongs_to_the_modifier_that_wet_it()
    {
        var river = SketchRasterizer.WaterOf(Layout(River()));
        var lake = SketchRasterizer.WaterOf(Layout(Lake("\"level\": 7,")));

        await Assert.That(river.Count).IsGreaterThan(0);
        await Assert.That(river.Values.All(column => column.Body == "river")).IsTrue();
        await Assert.That(lake.Count).IsGreaterThan(0);
        await Assert.That(lake.Values.All(column => column.Body == "lake")).IsTrue();
    }

    [Test]
    public async Task A_river_drawn_after_another_owns_the_columns_it_replaces_water_in()
    {
        var water = SketchRasterizer.WaterOf(Layout(River() + "," + River("deeper", depth: 4, water: 2)));

        await Assert.That(water[(20, 20)].Body).IsEqualTo("deeper");
    }

    [Test]
    public async Task A_lake_that_meets_a_higher_river_takes_its_surface_and_its_modifier()
    {
        var water = SketchRasterizer.WaterOf(Layout(River() + "," + Lake("\"level\": 6,")));

        await Assert.That(water[(20, 26)]).IsEqualTo(new WetColumn(7, "river"))
            .Because("the lake is one body with the river it meets, and stands at the river's surface");
        await Assert.That(water.Values.Distinct().Single()).IsEqualTo(new WetColumn(7, "river"));
    }

    [Test]
    public async Task A_coast_owns_the_sea_it_floods()
    {
        var water = SketchRasterizer.WaterOf(Layout("""
            { "id": "sea", "op": "coast", "level": 7, "shelf": 6, "depth": 4,
              "mask": { "areas": [ { "kind": "polygon", "points": [[0, 0], [12, 0], [12, 40], [0, 40]] } ], "mirror": false } }
            """));

        await Assert.That(water.Count).IsGreaterThan(0);
        await Assert.That(water.Values.All(column => column.Body == "sea")).IsTrue();
    }

    [Test]
    public async Task A_bed_material_a_modifier_states_survives_a_read_and_a_write_of_the_layout()
    {
        var layout = Layout(River(extra: "\"bed_material\": { \"kind\": \"solid\", \"id\": 13, \"data\": 0 },"));

        var again = SketchLayout.Parse(layout.ToJson())!;

        await Assert.That(again.Modifiers![0].BedMaterial!.Value.GetProperty("id").GetInt32()).IsEqualTo(13);
        await Assert.That(Layout(River()).Modifiers![0].BedMaterial).IsNull();
    }
}
