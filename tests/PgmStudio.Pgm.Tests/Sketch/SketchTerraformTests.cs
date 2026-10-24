using System.Globalization;
using PgmStudio.Geom;
using PgmStudio.Pgm.Sketch;

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
                               double radius = 10, double rise = 6, bool mirror = false) =>
        $$"""
        { "id": "{{id}}", "op": "mound", "rise": {{Num(rise)}}, {{extra}}
          "mask": { "areas": [ { "kind": "ellipse", "at": [{{Num(x)}}, {{Num(z)}}], "rx": {{Num(radius)}}, "rz": {{Num(radius)}} } ],
                    "mirror": {{(mirror ? "true" : "false")}} } }
        """;

    private static SketchLayout Layout(string modifiers, string mode = "none", string roomShapes = "", string otherLayers = "") =>
        SketchLayout.Parse($$"""
        { "setup": { "mirror_mode": "{{mode}}", "center": { "cx": 20, "cz": 20 } },
          "layers": [ {{Ground.Replace("{0}", roomShapes.Length == 0 ? "" : "," + roomShapes)}} {{otherLayers}} ],
          "modifiers": [ {{modifiers}} ] }
        """)!;

    private static int Top(SketchLayout layout, int x, int z, string layer = "ground") =>
        SketchRasterizer.RasterizeColumns(layout).Single(column => column.X == x && column.Z == z && column.Layer == layer).YTop;

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

    [Test]
    public async Task The_ground_under_a_play_room_holds()
    {
        var layout = Layout(Hill(), roomShapes: Spawn);

        foreach (var (x, z) in new[] { (18, 18), (20, 20), (21, 21), (18, 21) })
            await Assert.That(Top(layout, x, z)).IsEqualTo(8);
        await Assert.That(Top(layout, 22, 20)).IsGreaterThan(8);
        await Assert.That(Top(layout, 20, 22)).IsGreaterThan(8);
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
}
