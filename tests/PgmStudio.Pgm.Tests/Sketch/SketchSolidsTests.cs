using System.Globalization;
using PgmStudio.Geom;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The shapes that stand in the block grid, as the build sees them: each is the runs of blocks its body makes in
/// the layer it is on, it holds against the modifier stack unless it says otherwise, it joins the ground it is
/// bedded in, and the symmetry fans it into exact images.
/// </summary>
public sealed class SketchSolidsTests
{
    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    private const string Board =
        """{ "id": "board", "type": "rectangle", "operation": "add", "min_x": 0, "min_z": 0, "max_x": 40, "max_z": 40, "base_height": 8 }""";

    private static string Layer(string id, int baseY, params string[] shapes) =>
        $$"""{ "id": "{{id}}", "base_y": {{baseY}}, "layout": { "shapes": [ {{string.Join(",", shapes)}} ], "groups": [] } }""";

    private static SketchLayout Layout(string layers, string mode = "none", string modifiers = "") =>
        SketchLayout.Parse($$"""
            { "setup": { "mirror_mode": "{{mode}}", "center": { "cx": 20, "cz": 20 } },
              "layers": [ {{layers}} ], "modifiers": [ {{modifiers}} ] }
            """)!;

    private static List<ColumnSegment> Of(SketchLayout layout, string layer) =>
        [.. SketchRasterizer.RasterizeColumns(layout).Where(segment => segment.Layer == layer)];

    private const string Hill =
        """
        { "id": "hill", "op": "mound", "rise": 6,
          "mask": { "areas": [ { "kind": "ellipse", "at": [20.5, 20.5], "rx": 14, "rz": 14 } ], "mirror": false } }
        """;

    [Test]
    public async Task A_sphere_on_a_raised_layer_stands_from_the_layers_base()
    {
        var layout = Layout(Layer("ball", 20,
            """{ "id": "s", "type": "sphere", "center_x": 10.5, "center_z": 10.5, "radius": 3 }"""));

        var columns = Of(layout, "ball");
        var middle = columns.Single(segment => segment.X == 10 && segment.Z == 10);
        await Assert.That((middle.YFloor, middle.YTop)).IsEqualTo((20, 26)).Because("the diameter, up from base_y");
        await Assert.That(columns.Count).IsBetween(16, 36);
        await Assert.That(columns.Select(segment => segment.Cell).Distinct().Count()).IsEqualTo(columns.Count)
            .Because("a sphere is convex, so a column holds one run of it");
    }

    [Test]
    public async Task A_sphere_bedded_in_the_ground_joins_it_without_a_hollow_under_the_sphere()
    {
        var layout = Layout(Layer("ground", 0, Board,
            """{ "id": "s", "type": "sphere", "center_x": 10.5, "center_z": 10.5, "radius": 4, "floor": 5 }"""));

        var columns = Of(layout, "ground");
        var middle = columns.Single(segment => segment.X == 10 && segment.Z == 10);
        await Assert.That((middle.YFloor, middle.YTop)).IsEqualTo((0, 13)).Because("the ground's span and the sphere's are one mass");
        await Assert.That(columns.Single(segment => segment.X == 30 && segment.Z == 30).YTop).IsEqualTo(8);
    }

    [Test]
    public async Task A_sunk_box_has_its_lowest_point_under_its_floor()
    {
        var layout = Layout(Layer("pier", 0,
            """{ "id": "b", "type": "box", "min_x": 10, "min_z": 10, "max_x": 14, "max_z": 14, "floor": 10, "base_height": 6, "sunk": 4 }"""));

        var middle = Of(layout, "pier").Single(segment => segment.X == 11 && segment.Z == 11);
        await Assert.That((middle.YFloor, middle.YTop)).IsEqualTo((6, 12));
    }

    [Test]
    public async Task A_solid_holds_against_a_hill_raised_over_it_and_a_follow_word_lets_the_hill_reshape_it()
    {
        string Box(string scope) => $$"""
            { "id": "b", "type": "box", "min_x": 18, "min_z": 18, "max_x": 23, "max_z": 23, "base_height": 12{{scope}} }
            """;
        int Top(SketchLayout layout) => Of(layout, "ground").Single(segment => segment.X == 20 && segment.Z == 20).YTop;

        var held = Layout(Layer("ground", 0, Board, Box("")), modifiers: Hill);
        var followed = Layout(Layer("ground", 0, Board, Box(""", "relief_scope": "follow" """)), modifiers: Hill);
        var rectangle = Layout(Layer("ground", 0, Board,
            """{ "id": "b", "type": "rectangle", "operation": "add", "min_x": 18, "min_z": 18, "max_x": 23, "max_z": 23, "base_height": 12 }"""),
            modifiers: Hill);

        await Assert.That(Top(held)).IsEqualTo(12);
        await Assert.That(Top(followed)).IsGreaterThan(12).Because("follow is the ground's own relation to the hill");
        await Assert.That(Top(rectangle)).IsEqualTo(Top(followed)).Because("a rectangle follows without saying so");
        await Assert.That(Of(held, "ground").Single(segment => segment.X == 20 && segment.Z == 28).YTop)
            .IsGreaterThan(8).Because("the ground beside the held solid is raised");
    }

    [Test]
    [Arguments("rot_180", false)] [Arguments("rot_90", false)] [Arguments("mirror_x", false)] [Arguments("mirror_d1", false)]
    [Arguments("rot_180", true)] [Arguments("rot_90", true)] [Arguments("mirror_x", true)] [Arguments("mirror_z", true)]
    [Arguments("mirror_d1", true)] [Arguments("mirror_d2", true)]
    public async Task Every_image_of_a_solid_is_the_exact_mirror_of_it(string mode, bool grouped)
    {
        // Grouped, each image is the shape itself carried through the axis; ungrouped, the layer's columns are.
        var layer = Layer("sculpture", 4,
            """{ "id": "w", "type": "wedge", "min_x": 22, "min_z": 6, "max_x": 30, "max_z": 12, "base_height": 7, "turn": 25, "tilt": 15 }""",
            """{ "id": "t", "type": "torus", "center_x": 8, "center_z": 30, "radius": 6, "tube": 2, "turn": 40, "tilt": 70 }""",
            """{ "id": "b", "type": "box", "min_x": 28, "min_z": 28, "max_x": 34, "max_z": 33, "base_height": 5, "turn": 30, "tilt": 20 }""");
        if (grouped)
            layer = layer.Replace("\"groups\": []", "\"groups\": [ { \"id\": \"all\", \"shapeIds\": [\"w\", \"t\", \"b\"], \"mirrors\": true } ]");
        var layout = Layout(layer, mode);

        var spans = new Dictionary<(int X, int Z), List<(int Floor, int Top)>>();
        foreach (var segment in Of(layout, "sculpture"))
        {
            if (!spans.TryGetValue(segment.Cell, out var here)) spans[segment.Cell] = here = [];
            here.Add((segment.YFloor, segment.YTop));
        }
        await Assert.That(spans.Count).IsGreaterThan(100);

        foreach (var axis in Symmetry.OrbitAxes(mode))
            foreach (var ((x, z), here) in spans)
            {
                var image = Symmetry.ApplyCell(x, z, axis, 20, 20);
                await Assert.That(spans.TryGetValue(image, out var there)).IsTrue()
                    .Because($"({x}, {z}) has an image at {image} under {axis}");
                await Assert.That(there!.Order().ToList()).IsEquivalentTo(here.Order().ToList())
                    .Because($"({x}, {z}) and {image} stand alike under {axis}");
            }
    }

    [Test]
    public async Task A_torus_on_its_edge_puts_two_runs_of_its_layer_in_a_column_through_its_hole()
    {
        var layout = Layout(Layer("ring", 0,
            """{ "id": "r", "type": "torus", "center_x": 20.5, "center_z": 20.5, "radius": 6, "tube": 2, "floor": 10, "tilt": 90 }"""));

        var column = Of(layout, "ring").Where(segment => segment.X == 20 && segment.Z == 20).OrderBy(segment => segment.YFloor).ToList();
        await Assert.That(column.Count).IsEqualTo(2);
        await Assert.That(column[0].YTop).IsLessThan(column[1].YFloor).Because("air between the arcs");
        await Assert.That((column[0].YFloor, column[1].YTop)).IsEqualTo((10, 22));
    }

    [Test]
    public async Task A_torus_standing_on_the_ground_of_its_own_layer_leaves_the_ground_under_its_lower_arc()
    {
        var layout = Layout(Layer("ground", 0, Board.Replace("\"base_height\": 8", "\"base_height\": 10"),
            """{ "id": "r", "type": "torus", "center_x": 20.5, "center_z": 20.5, "radius": 6, "tube": 2, "floor": 10, "tilt": 90 }"""));

        var column = Of(layout, "ground").Where(segment => segment.X == 20 && segment.Z == 20)
                                         .OrderBy(segment => segment.YFloor).Select(segment => (segment.YFloor, segment.YTop)).ToList();
        await Assert.That(column).IsEquivalentTo([(0, 14), (18, 22)]).Because("the lower arc joins the ground it stands on; the upper arc is the layer's other span");
    }

    [Test]
    public async Task A_sphere_hung_over_the_ground_of_its_own_layer_does_not_take_the_ground_away()
    {
        var layout = Layout(Layer("ground", 0, Board.Replace("\"base_height\": 8", "\"base_height\": 10"),
            """{ "id": "s", "type": "sphere", "center_x": 20.5, "center_z": 20.5, "radius": 3, "floor": 14 }"""));

        var column = Of(layout, "ground").Where(segment => segment.X == 20 && segment.Z == 20)
                                         .OrderBy(segment => segment.YFloor).Select(segment => (segment.YFloor, segment.YTop)).ToList();
        await Assert.That(column).IsEquivalentTo([(0, 10), (14, 20)]);
        await Assert.That(SketchRasterizer.StackedInOneLayer(layout)).IsEmpty().Because("a solid loses nothing to a taller shape");
    }

    [Test]
    public async Task A_subtract_takes_both_runs_of_a_column_a_torus_makes()
    {
        var ring = """{ "id": "r", "type": "torus", "center_x": 20.5, "center_z": 20.5, "radius": 6, "tube": 2, "floor": 10, "tilt": 90 }""";
        var cut = """{ "id": "cut", "type": "rectangle", "operation": "subtract", "min_x": 19, "min_z": 19, "max_x": 22, "max_z": 22 }""";
        var layout = Layout(Layer("ring", 0, ring, cut));

        await Assert.That(Of(layout, "ring").Any(segment => segment.X == 20 && segment.Z == 20)).IsFalse();
        await Assert.That(Of(layout, "ring").Any(segment => segment.X == 24 && segment.Z == 20)).IsTrue();
    }

    [Test]
    public async Task An_arch_is_a_deck_over_a_gap_with_its_feet_on_the_ground()
    {
        var arch = Layout(Layer("ground", 0, Board.Replace("\"base_height\": 8", "\"base_height\": 4")) + ","
            + Layer("arch", 0,
                """{ "id": "a", "type": "arch", "vertices": [[4, 20], [36, 20]], "radius": 3, "deck": [14, 5, 1], "thick": [3, 10, 1] }"""));

        var columns = Of(arch, "arch").ToDictionary(segment => segment.Cell);
        await Assert.That(columns[(20, 20)].YTop).IsEqualTo(15).Because("the deck at the crown, one block of surface over y14");
        await Assert.That(columns[(20, 20)].YFloor).IsEqualTo(11).Because("three blocks of rock under the deck at the crown");
        await Assert.That(columns[(20, 20)].YFloor).IsGreaterThan(4).Because("clear air between the underside and the ground");
        await Assert.That(columns[(5, 20)].YTop).IsLessThan(columns[(12, 20)].YTop).Because("the deck climbs towards the crown");
        await Assert.That(columns[(12, 20)].YTop).IsLessThan(columns[(20, 20)].YTop);
        await Assert.That(columns[(5, 20)].YFloor).IsLessThan(4).Because("a foot reaches down into the ground");
    }

    [Test]
    public async Task The_gate_knows_every_solid_and_names_one_that_draws_nothing()
    {
        var layout = Layout(Layer("g", 0,
            """{ "id": "a", "type": "box", "min_x": 1, "min_z": 1, "max_x": 5, "max_z": 5 }""",
            """{ "id": "b", "type": "wedge", "min_x": 6, "min_z": 1, "max_x": 9, "max_z": 5 }""",
            """{ "id": "c", "type": "cylinder", "center_x": 14, "center_z": 4, "radius": 3 }""",
            """{ "id": "d", "type": "sphere", "center_x": 20, "center_z": 4, "radius": 3 }""",
            """{ "id": "e", "type": "torus", "center_x": 28, "center_z": 4, "radius": 4 }""",
            """{ "id": "f", "type": "arch", "vertices": [[1, 20], [20, 20]], "radius": 3 }""",
            """{ "id": "nothing", "type": "sphere", "center_x": 30, "center_z": 30, "radius": 0 }""",
            """{ "id": "towering", "type": "sphere", "center_x": 36, "center_z": 30, "radius": 3, "floor": 254 }"""));

        var findings = SketchLayoutCheck.Check(layout, LayoutReading.Document);
        await Assert.That(findings.Any(finding => finding.Rule == SketchRules.NamesNothing)).IsFalse()
            .Because("every solid is a kind the gate knows");
        await Assert.That(findings.Any(finding => finding.Rule == SketchRules.DrawsNothing && finding.Subjects?.Contains("nothing") == true)).IsTrue();
        await Assert.That(findings.Any(finding => finding.Rule == SketchRules.UnbuildableHeight && finding.Subjects?.Contains("towering") == true)).IsTrue()
            .Because("the sphere's own top, not its base_height, is what the world has to hold");
        await Assert.That(findings.Count(finding => finding.Subjects?.Contains("a") == true)).IsEqualTo(0);
    }

    [Test]
    public async Task Every_solid_word_is_a_shape_kind_and_the_drawn_ones_still_are()
    {
        await Assert.That(ShapeKinds.All).IsEquivalentTo(
            ["rectangle", "circle", "polygon", "lasso", "polyline", "box", "wedge", "cylinder", "sphere", "torus", "arch"]);
        await Assert.That(ReliefScopes.All).IsEquivalentTo(["hold", "ride", "follow"]);
    }
}
