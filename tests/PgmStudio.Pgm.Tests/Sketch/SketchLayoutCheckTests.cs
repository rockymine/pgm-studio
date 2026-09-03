using PgmStudio.Domain;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The document gate for a sketch layout. Every case here was first put through the real build and observed
/// to pass silently — a shape kind nobody has, a two-vertex polygon, a mirror mode nobody has and a relief
/// keyed to no group all produce a world, with less in it than the document asked for. The rasterizer is set
/// algebra, so an unreadable shape contributes no ground rather than failing; this is what says so.
/// </summary>
public sealed class SketchLayoutCheckTests
{
    private static string Layout(string shapes, string extra = "", string mode = "rot_180") =>
        "{\"setup\":{\"mirror_mode\":\"" + mode + "\",\"center\":{\"cx\":0,\"cz\":0}},"
        + "\"layers\":[{\"base_y\":0,\"layout\":{\"shapes\":[" + shapes + "],"
        + "\"groups\":[{\"id\":\"i\",\"name\":\"I\",\"shapeIds\":[\"s1\"]}]}}]" + extra + "}";

    private const string Rect =
        """{"id":"s1","type":"rectangle","operation":"add","min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}""";

    [Test]
    public async Task A_layout_that_builds_what_it_says_has_nothing_to_report()
    {
        await Assert.That(SketchLayoutCheck.Check(Layout(Rect))).IsEmpty();
    }

    [Test]
    [Arguments("""{"id":"s1","type":"trapezoid","min_x":-5,"max_x":5,"min_z":-5,"max_z":5}""", "kind 'trapezoid'")]
    [Arguments("""{"id":"s1","type":"polygon","vertices":[[0,0],[10,0]]}""", "2 vertices")]
    [Arguments("""{"id":"s1","type":"circle","center_x":0,"center_z":0,"radius":0}""", "circle of radius 0")]
    [Arguments("""{"id":"s1","type":"rectangle","min_x":5,"max_x":5,"min_z":-5,"max_z":5}""", "no area")]
    // A polygon of three vertices or more with every point on one line: enough vertices to clear the count
    // and no area to draw, which is the fault the same rule already names for a rectangle (TS67).
    [Arguments("""{"id":"s1","type":"polygon","vertices":[[7,-10],[7,0],[7,10]]}""", "enclosing no area")]
    [Arguments("""{"id":"s1","type":"lasso","vertices":[[-7,-10],[-7,-3],[-7,4],[-7,10]]}""", "enclosing no area")]
    public async Task A_shape_that_draws_no_ground_is_named_with_the_reason_it_draws_none(string shape, string says)
    {
        var findings = SketchLayoutCheck.Check(Layout(shape));

        // A complaint, never a refusal: the board built, it just built less than was drawn.
        await Assert.That(findings.Refuses).IsFalse();
        await Assert.That(findings.Single().Message).Contains(says);
        await Assert.That(findings.Single().SubjectIds).IsEquivalentTo(new[] { "s1" });
    }

    [Test]
    public async Task A_mirror_mode_nobody_has_is_named_because_the_board_is_built_unmirrored()
    {
        // Symmetry.Order answers 2 for an unknown mode and the transform is the identity, so the second image
        // lands on the first: the map states two halves and stands on one, silently.
        var findings = SketchLayoutCheck.Check(Layout(Rect, mode: "rot_37"));

        var finding = findings.Single();
        await Assert.That(finding.Rule).IsEqualTo(SketchRules.NamesNothing);
        await Assert.That(finding.Field).IsEqualTo("setup.mirror_mode");
        await Assert.That(finding.Message).Contains("built unmirrored");
    }

    [Test]
    public async Task An_island_listing_a_shape_the_layout_does_not_carry_is_named()
    {
        var findings = SketchLayoutCheck.Check(Layout(
            """{"id":"other","type":"rectangle","min_x":-5,"max_x":5,"min_z":-5,"max_z":5}"""));

        var finding = findings.Single(f => f.Message.Contains("group 'i'"));
        await Assert.That(finding.Rule).IsEqualTo(SketchRules.NamesNothing);
        await Assert.That(finding.Message).Contains("lists shape 's1'");
    }

    [Test]
    public async Task A_shape_no_group_lists_is_named_because_it_is_built_without_its_mirror_image()
    {
        // The fan is read off each mirroring group's shapeIds, so a shape no list names stands once. Nothing
        // else says so: it rasterizes where it was drawn and the drawn group outline still covers it.
        var findings = SketchLayoutCheck.Check(Layout(
            Rect + ""","""
            + """{"id":"s2","type":"rectangle","operation":"add","min_x":10,"max_x":40,"min_z":-20,"max_z":20,"floor":8,"base_height":12}"""));

        var finding = findings.Single(f => f.Rule == SketchRules.ShapeInNoGroup);
        await Assert.That(findings.Refuses).IsFalse();
        await Assert.That(finding.SubjectIds).IsEquivalentTo(new[] { "s2" });
        await Assert.That(finding.Message).Contains("no image on the other side");
    }

    [Test]
    public async Task A_board_that_does_not_mirror_loses_no_image_to_an_unlisted_shape()
    {
        var findings = SketchLayoutCheck.Check(Layout(
            Rect + ""","""
            + """{"id":"s2","type":"rectangle","operation":"add","min_x":10,"max_x":40,"min_z":-20,"max_z":20,"floor":8,"base_height":12}""",
            mode: "none"));

        await Assert.That(findings.Any(f => f.Rule == SketchRules.ShapeInNoGroup)).IsFalse();
    }

    [Test]
    public async Task A_role_tagged_room_piece_is_never_listed_in_a_group_and_is_not_reported_for_it()
    {
        // A room places no terrain of its own, so it is deliberately absent from every group's shapeIds.
        var findings = SketchLayoutCheck.Check(Layout(
            Rect + ""","""
            + """{"id":"spawn-red","role":"spawn","type":"rectangle","operation":"add","min_x":-5,"max_x":5,"min_z":-5,"max_z":5}"""));

        await Assert.That(findings.Any(f => f.Rule == SketchRules.ShapeInNoGroup)).IsFalse();
    }

    [Test]
    public async Task A_placement_naming_a_recipe_the_document_does_not_state_is_refused_and_a_strokes_edge_word_is_not()
    {
        // A tree's `style` is a key into the registry; a stroke's `style` is the word for its edge and names
        // nothing, so a road drawn `rough` beside an unstated recipe is one refusal, about the tree.
        var findings = SketchLayoutCheck.Check(Layout(Rect, """
            ,"dressing":{"props":[
              {"kind":"stroke","id":"road","points":[[0,0],[10,0]],"radius":2,"style":"rough"},
              {"kind":"tree","id":"t","x":0,"z":0,"style":"cut-oak"}],
             "styles":{}}
            """));
        var refusal = findings.Refusals.Single();
        await Assert.That(refusal.Rule).IsEqualTo(SketchRules.RecipeNotStated);
        await Assert.That(refusal.Message).Contains("'t' names the recipe 'cut-oak'");
    }

    [Test]
    public async Task A_relief_keyed_to_an_island_that_is_not_there_is_named()
    {
        var findings = SketchLayoutCheck.Check(Layout(Rect, ""","relief":{"group-2":{"kind":"hills"}}"""));

        var finding = findings.Single();
        await Assert.That(finding.Field).IsEqualTo("relief.group-2");
        await Assert.That(finding.Message).Contains("is not built");
    }

    [Test]
    [Arguments(300, 12, "reaches y=312")]
    [Arguments(-40, 12, "floor at y=-40")]
    public async Task A_column_the_world_cannot_hold_is_named_as_the_height_it_states(double floor, double height, string says)
    {
        var shape = "{\"id\":\"s1\",\"type\":\"rectangle\",\"min_x\":-20,\"max_x\":20,\"min_z\":-20,"
                  + "\"max_z\":20,\"floor\":" + floor + ",\"base_height\":" + height + "}";
        var finding = SketchLayoutCheck.Check(Layout(shape)).Single();

        await Assert.That(finding.Rule).IsEqualTo(SketchRules.UnbuildableHeight);
        await Assert.That(finding.Message).Contains(says);
        await Assert.That(finding.Severity).IsEqualTo(Severity.Complaint);
    }

    [Test]
    public async Task A_board_past_what_the_studio_will_realize_is_refused_and_one_inside_it_is_not()
    {
        // The case that took a machine down: 4000×4000 does not fail, it walks 16 million columns.
        var huge = SketchLayoutCheck.Check(Layout(
            """{"id":"s1","type":"rectangle","min_x":-2000,"max_x":2000,"min_z":-2000,"max_z":2000}"""));

        await Assert.That(huge.Refuses).IsTrue();
        var refusal = huge.Refusals.Single();
        await Assert.That(refusal.Rule).IsEqualTo(SketchRules.BoardTooLarge);
        // It says the span it measured — the board the author actually drew.
        await Assert.That(refusal.Message).Contains("4,000×4,000");
        // And never the ceiling: a stated one is a target, so an agent reading this learns that it drew too
        // much and not how much it may draw. The number lives in the constant and nowhere a caller reads.
        await Assert.That(refusal.Message).DoesNotContain(SketchRules.MaxBoardColumns.ToString("N0"));

        // 1000×1000 is a million columns — four times the size of anything authored, and it stands.
        var big = SketchLayoutCheck.Check(Layout(
            """{"id":"s1","type":"rectangle","min_x":-500,"max_x":500,"min_z":-500,"max_z":500}"""));
        await Assert.That(big.Refuses).IsFalse();
    }

    [Test]
    public async Task The_extent_is_measured_across_the_orbit_not_across_the_drawn_shape()
    {
        // A 1400-wide board drawn 1400 off-centre spans 4200 once its rot_180 image is counted, which is the
        // ground the build has to walk — measuring the drawn rectangle alone would let it through.
        var offCentre = SketchLayoutCheck.Check(Layout(
            """{"id":"s1","type":"rectangle","min_x":700,"max_x":2100,"min_z":-700,"max_z":700}"""));

        await Assert.That(offCentre.Refuses).IsTrue();
        await Assert.That(offCentre.Refusals.Single().Rule).IsEqualTo(SketchRules.BoardTooLarge);
    }

    [Test]
    public async Task A_board_carrying_none_of_a_finish_is_told_so_and_one_carrying_any_of_it_is_not()
    {
        // The silence the other gates cannot cover: each of them needs something stated to disagree with, so
        // a board stating no theme, no relief and no props slips between them and exports as raw stone.
        // Any one of the three is a board being finished on purpose, and says nothing.
        await Assert.That(SketchLayoutCheck.Unfinished(SketchLayout.Stated(Layout(Rect)))!.Rule)
            .IsEqualTo(SketchRules.NoFinish);

        foreach (var carried in (string[])[
            ""","themes":{"t":{}}""",
            ""","relief":{"i":{"base":10}}""",
            ""","dressing":{"props":[{"kind":"boulder","id":"b","x":0,"z":0}]}""",
        ])
            await Assert.That(SketchLayoutCheck.Unfinished(SketchLayout.Stated(Layout(Rect, carried))))
                .IsNull().Because($"a board carrying {carried} is finished");

        // An empty registry is not a finish: a key holding nothing says the same as no key.
        await Assert.That(SketchLayoutCheck.Unfinished(
            SketchLayout.Stated(Layout(Rect, ""","themes":{},"dressing":{"props":[]}"""))))
            .IsNotNull();
    }

    [Test]
    public async Task The_bare_board_is_a_complaint_naming_all_three_and_never_a_refusal()
    {
        // A board of bare ground is legitimate — a test piece, a shape being tried — so this stops nothing.
        var bare = SketchLayoutCheck.Unfinished(SketchLayout.Stated(Layout(Rect)))!;

        await Assert.That(bare.Refuses).IsFalse();
        await Assert.That(bare.Severity).IsEqualTo(Severity.Complaint);
        foreach (var absent in (string[])["theme registry", "relief", "nothing placed on it"])
            await Assert.That(bare.Message).Contains(absent);
    }

    [Test]
    public async Task A_body_that_is_not_a_layout_is_not_this_gates_to_report()
    {
        // An unreadable body is the request's own fault (RQ1), answered where the body is read — this gate
        // says nothing rather than inventing a second answer for it.
        await Assert.That(SketchLayoutCheck.Check("this is not json")).IsEmpty();
        await Assert.That(SketchLayoutCheck.Check("""{"hello":"world"}""")).IsEmpty();
    }

    // ── SK9: a layer holds one span per column ───────────────────────────────────────────────────────────
    private const string Gallery =
        """{"id":"gallery","type":"rectangle","operation":"add","min_x":-30,"max_x":24,"min_z":-6,"max_z":6,"floor":0,"base_height":4}""";
    private const string Roof =
        """{"id":"roof","type":"rectangle","operation":"add","min_x":-30,"max_x":24,"min_z":-6,"max_z":6,"floor":16,"base_height":6}""";

    /// <summary>A floor with a roof drawn over it on one layer builds as the roof alone — the taller add
    /// replaces the shorter outright, floor included. The board reads as authored and the gallery is gone, so
    /// this is what says the input did not survive.</summary>
    [Test]
    public async Task A_second_span_on_one_layer_is_declined_by_name()
    {
        var findings = SketchLayoutCheck.Check(Layout(Gallery + "," + Roof));
        var stacked = findings.Where(finding => finding.Rule == SketchRules.StackedInOneLayer).ToList();

        await Assert.That(stacked.Count).IsEqualTo(1);
        await Assert.That(stacked[0].Severity).IsEqualTo(Severity.Decline);
        await Assert.That(stacked[0].SubjectIds).IsEquivalentTo(new[] { "gallery", "roof" });
        await Assert.That(stacked[0].Message).Contains("gallery");
    }

    /// <summary>The order the two are drawn in does not change which one the world keeps, so it does not
    /// change what is reported either.</summary>
    [Test]
    public async Task The_draw_order_does_not_change_what_is_declined()
    {
        var reversed = SketchLayoutCheck.Check(Layout(Roof + "," + Gallery))
            .Where(finding => finding.Rule == SketchRules.StackedInOneLayer).ToList();
        await Assert.That(reversed.Count).IsEqualTo(1);
        await Assert.That(reversed[0].SubjectIds).IsEquivalentTo(new[] { "gallery", "roof" });
    }

    /// <summary>Two adds at one floor are ordinary ground and the taller winning is what "height is the
    /// tallest add" means, so an overlap that is not a stack stays silent.</summary>
    [Test]
    public async Task Two_adds_at_one_floor_are_not_a_stack()
    {
        const string tall =
            """{"id":"tall","type":"rectangle","operation":"add","min_x":-30,"max_x":24,"min_z":-6,"max_z":6,"floor":0,"base_height":20}""";
        await Assert.That(SketchLayoutCheck.Check(Layout(Gallery + "," + tall))
            .Where(finding => finding.Rule == SketchRules.StackedInOneLayer)).IsEmpty();
    }

    /// <summary>A group id is the key a relief is stored under, so a board carrying it twice has no single
    /// group for that terrain to belong to. Measured on `pgm-studio-mapgen`'s `opus5-ravensmere`, where the
    /// canvas split one board into three groups and gave all three the saved id: the relief keyed to it
    /// reached one of them and the board's whole surface came back flat.</summary>
    [Test]
    public async Task Two_islands_answering_to_one_id_are_declined()
    {
        var found = SketchLayoutCheck.Check(TwoIslands("i", "i"))
            .Where(finding => finding.Rule == SketchRules.GroupIdTwice).ToList();
        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].Message).Contains("2 groups answer to the id 'i'");
    }

    /// <summary>Two groups with their own ids is the ordinary shape of a board with two landmasses, and it
    /// stays silent.</summary>
    [Test]
    public async Task Two_islands_with_their_own_ids_are_not_declined()
    {
        await Assert.That(SketchLayoutCheck.Check(TwoIslands("i", "j"))
            .Where(finding => finding.Rule == SketchRules.GroupIdTwice)).IsEmpty();
    }

    /// <summary>Two rectangles wide apart, one group named over each.</summary>
    private static string TwoIslands(string first, string second) =>
        "{\"setup\":{\"mirror_mode\":\"rot_180\",\"center\":{\"cx\":0,\"cz\":0}},"
        + "\"layers\":[{\"base_y\":0,\"layout\":{\"shapes\":[" + Rect + ","
        + "{\"id\":\"s2\",\"type\":\"rectangle\",\"operation\":\"add\",\"min_x\":60,\"max_x\":70,"
        + "\"min_z\":60,\"max_z\":70,\"floor\":8,\"base_height\":12}],"
        + "\"groups\":[{\"id\":\"" + first + "\",\"name\":\"I\",\"shapeIds\":[\"s1\"]},"
        + "{\"id\":\"" + second + "\",\"name\":\"J\",\"shapeIds\":[\"s2\"]}]}}]}";

    /// <summary>Walls clamped around a tucked-in floor is how a roofed gallery is built, and the shapes do
    /// not contest a cell — so the way that works is not reported as the way that does not.</summary>
    [Test]
    public async Task Clamped_walls_beside_a_floor_are_not_a_stack()
    {
        const string wallNorth =
            """{"id":"wall-n","type":"rectangle","operation":"add","min_x":-30,"max_x":24,"min_z":7,"max_z":20,"floor":0,"base_height":20}""";
        await Assert.That(SketchLayoutCheck.Check(Layout(Gallery + "," + wallNorth))
            .Where(finding => finding.Rule == SketchRules.StackedInOneLayer)).IsEmpty();
    }

    // ── SK14: a relief solves through an override add ────────────────────────────────────────────────────
    private static string Relieved(string shapes, string ids) =>
        "{\"setup\":{\"mirror_mode\":\"rot_180\",\"center\":{\"cx\":0,\"cz\":0}},"
        + "\"layers\":[{\"id\":\"ground\",\"base_y\":0,\"layout\":{\"shapes\":[" + shapes + "],"
        + "\"groups\":[{\"id\":\"i\",\"name\":\"I\",\"shapeIds\":[" + ids + "]}]}}],"
        + "\"relief\":{\"i\":{\"base\":8,\"reach\":16,\"step\":1,\"marks\":[]}}}";

    private const string Ground =
        """{"id":"g","type":"rectangle","operation":"add","min_x":-40,"max_x":40,"min_z":-40,"max_z":40,"floor":0,"base_height":9}""";
    private const string Wall =
        """{"id":"wall","type":"rectangle","operation":"add","override":true,"theme":"stone","min_x":-40,"max_x":40,"min_z":10,"max_z":14,"floor":0,"base_height":22}""";

    /// <summary>A made thing is an override add: the column is its own, floor and all. A relief replaces the
    /// top of every column of its group, so a wall carrying no `height_mode` builds to the field and its
    /// twenty-two courses are nowhere in the world — with nothing else on the board to say so.</summary>
    [Test]
    public async Task An_override_add_a_relief_solves_through_is_named()
    {
        var findings = SketchLayoutCheck.Check(Relieved(Ground + "," + Wall, "\"g\",\"wall\""))
            .Where(finding => finding.Rule == SketchRules.ReliefOverStatedTop).ToList();

        await Assert.That(findings.Count).IsEqualTo(1);
        await Assert.That(findings[0].SubjectIds).IsEquivalentTo(new[] { "wall" });
        await Assert.That(findings[0].Message).Contains("y22");
        await Assert.That(findings[0].Message).Contains("height_mode");
    }

    /// <summary>The two words that hold a stated top, and the plain ground that is not this: a relief shaping
    /// ordinary terrain is what a relief is for, so only the override is the statement being overruled.</summary>
    [Test]
    public async Task A_shape_that_stands_out_of_the_field_or_is_not_an_override_is_not_named()
    {
        foreach (var held in new[] { "\"height_mode\":\"level\",\"skirt\":0", "\"relief_scope\":\"exclude\"" })
        {
            var wall = Wall.Replace("\"override\":true", "\"override\":true," + held);
            await Assert.That(SketchLayoutCheck.Check(Relieved(Ground + "," + wall, "\"g\",\"wall\""))
                .Where(finding => finding.Rule == SketchRules.ReliefOverStatedTop)).IsEmpty();
        }
        var plain = Wall.Replace("\"override\":true,", "");
        await Assert.That(SketchLayoutCheck.Check(Relieved(Ground + "," + plain, "\"g\",\"wall\""))
            .Where(finding => finding.Rule == SketchRules.ReliefOverStatedTop)).IsEmpty();
    }

    /// <summary>A top has to be stated to be discarded. An override add carrying no height at all is a
    /// footprint holding a theme, and the ground the relief solves under it is the ground it wanted.</summary>
    [Test]
    public async Task An_override_add_that_states_no_top_is_not_named()
    {
        var paint =
            """{"id":"scree","type":"rectangle","operation":"add","override":true,"theme":"scree","min_x":-8,"max_x":8,"min_z":8,"max_z":16}""";
        await Assert.That(SketchLayoutCheck.Check(Relieved(Ground + "," + paint, "\"g\",\"scree\""))
            .Where(finding => finding.Rule == SketchRules.ReliefOverStatedTop)).IsEmpty();
    }

    /// <summary>A relief is keyed on a group, so a board that carries none has nothing to overrule.</summary>
    [Test]
    public async Task An_island_with_no_relief_overrules_nothing()
    {
        var flat = Relieved(Ground + "," + Wall, "\"g\",\"wall\"")
            .Replace("\"relief\":{\"i\":{\"base\":8,\"reach\":16,\"step\":1,\"marks\":[]}}", "\"relief\":{}");
        await Assert.That(SketchLayoutCheck.Check(flat)
            .Where(finding => finding.Rule == SketchRules.ReliefOverStatedTop)).IsEmpty();
    }

    /// <summary>A shape in a mirroring group stands on the board once per axis of the orbit, so what a patch
    /// contests is as often another patch's reflection as the patch itself.</summary>
    [Test]
    public async Task A_shape_painted_by_another_shapes_image_is_named()
    {
        // A mound laid clear of the raised court on the half it is drawn on, whose rot_180 image lands in it:
        // smaller, so it wins the paint, and shorter, so the court's own ground is what stands there.
        var court = """{"id":"court","type":"rectangle","operation":"add","override":true,"theme":"flags","min_x":-16,"max_x":16,"min_z":4,"max_z":20,"floor":0,"base_height":13}""";
        var mound = """{"id":"mound","type":"rectangle","operation":"add","override":true,"theme":"turf","min_x":-14,"max_x":-6,"min_z":-18,"max_z":-10,"floor":0,"base_height":10}""";
        var board = "{\"setup\":{\"mirror_mode\":\"rot_180\",\"center\":{\"cx\":0,\"cz\":0}},"
                  + "\"layers\":[{\"id\":\"ground\",\"base_y\":0,\"layout\":{\"shapes\":[" + court + "," + mound + "],"
                  + "\"groups\":[{\"id\":\"i\",\"name\":\"I\",\"mirrors\":true,\"shapeIds\":[\"court\",\"mound\"]}]}}]}";

        var findings = SketchLayoutCheck.Check(board)
            .Where(finding => finding.Rule == SketchRules.PaintedByAnotherShape).ToList();

        await Assert.That(findings.Count).IsEqualTo(1);
        await Assert.That(findings[0].SubjectIds).IsEquivalentTo(new[] { "court", "mound" });

        // The same two on a group that is not fanned contest nothing: the image is what they meet in.
        await Assert.That(SketchLayoutCheck.Check(board.Replace("\"mirrors\":true", "\"mirrors\":false"))
            .Where(finding => finding.Rule == SketchRules.PaintedByAnotherShape)).IsEmpty();
    }

    // ── SK15: one shape builds the column and another paints it ──────────────────────────────────────────
    private const string Mound =
        """{"id":"mound","type":"rectangle","operation":"add","override":true,"theme":"grass","min_x":-8,"max_x":8,"min_z":8,"max_z":16,"floor":0,"base_height":11}""";

    /// <summary>The taller add wins the column and the smaller wins the theme, so a mound's ring crossing a
    /// wall leaves the wall standing to its own courses and painted in the mound's material.</summary>
    [Test]
    public async Task A_shape_built_by_one_and_painted_by_another_is_named()
    {
        var findings = SketchLayoutCheck.Check(Layout(Wall + "," + Mound))
            .Where(finding => finding.Rule == SketchRules.PaintedByAnotherShape).ToList();

        await Assert.That(findings.Count).IsEqualTo(1);
        await Assert.That(findings[0].SubjectIds).IsEquivalentTo(new[] { "wall", "mound" });
        await Assert.That(findings[0].Message).Contains("grass");
        await Assert.That(findings[0].Message).Contains("stone");
    }

    /// <summary>Two shapes at one height are a theme scoped to a patch, which is what scoping is for; and two
    /// sharing a theme cannot disagree about paint. Neither is this.</summary>
    [Test]
    public async Task A_theme_scoped_to_a_patch_is_not_this()
    {
        var patch = Mound.Replace("\"base_height\":11", "\"base_height\":22");
        await Assert.That(SketchLayoutCheck.Check(Layout(Wall + "," + patch))
            .Where(finding => finding.Rule == SketchRules.PaintedByAnotherShape)).IsEmpty();

        var sameTheme = Mound.Replace("\"theme\":\"grass\"", "\"theme\":\"stone\"");
        await Assert.That(SketchLayoutCheck.Check(Layout(Wall + "," + sameTheme))
            .Where(finding => finding.Rule == SketchRules.PaintedByAnotherShape)).IsEmpty();
    }

    /// <summary>And two that do not share a column have nothing to contest.</summary>
    [Test]
    public async Task Shapes_that_do_not_meet_are_not_named()
    {
        var apart = Mound.Replace("\"min_z\":8,\"max_z\":16", "\"min_z\":24,\"max_z\":32");
        await Assert.That(SketchLayoutCheck.Check(Layout(Wall + "," + apart))
            .Where(finding => finding.Rule == SketchRules.PaintedByAnotherShape)).IsEmpty();
    }
}

/// <summary>
/// A placement names a recipe, and the document that carries it has to state it.
///
/// <para>Every read of the dressing refuses a key that names nothing, so a world is never built from one. What
/// the gate adds is <b>when</b>: the layout is stored and finished without its dressing being parsed, so a
/// document written by a driver was taken twice with a 200 and only said no at the export — the fault sitting
/// in the map in between.</para>
/// </summary>
public sealed class SketchRecipeGateTests
{
    private static string WithDressing(string dressing) =>
        "{\"setup\":{\"mirror_mode\":\"rot_180\",\"center\":{\"cx\":0,\"cz\":0}},"
        + "\"layers\":[{\"base_y\":0,\"layout\":{\"shapes\":[{\"id\":\"s1\",\"type\":\"rectangle\","
        + "\"operation\":\"add\",\"min_x\":-20,\"max_x\":20,\"min_z\":-20,\"max_z\":20,\"floor\":8,"
        + "\"base_height\":12}],\"groups\":[{\"id\":\"i\",\"name\":\"I\",\"shapeIds\":[\"s1\"]}]}}],"
        + "\"dressing\":" + dressing + "}";

    [Test]
    public async Task A_placement_naming_a_recipe_the_document_does_not_state_is_refused()
    {
        var findings = SketchLayoutCheck.Check(WithDressing(
            """{"props":[{"kind":"tree","id":"t1","x":0,"z":0,"style":"maple-9"}],"styles":{}}"""));

        var finding = findings.Single(f => f.Rule == SketchRules.RecipeNotStated);
        await Assert.That(finding.Severity).IsEqualTo(Severity.Refusal);
        await Assert.That(finding.Message).Contains("maple-9");
        await Assert.That(finding.Subjects).Contains("t1");
        await Assert.That(findings.Refuses).IsTrue();
    }

    [Test]
    public async Task A_placement_naming_a_recipe_the_document_states_says_nothing()
    {
        var findings = SketchLayoutCheck.Check(WithDressing(
            """
            {"props":[{"kind":"tree","id":"t1","x":0,"z":0,"style":"oak-10"}],
             "styles":{"oak-10":{"kind":"tree","form":"template","species":"oak","height":10}}}
            """));

        await Assert.That(findings.Any(f => f.Rule == SketchRules.RecipeNotStated)).IsFalse();
    }

    /// <summary>A prop put down before a recipe was picked builds the kind's own default, the way a sketch
    /// binding no room style stamps the built-in shell. Refusing it would make a board unsaveable halfway
    /// through being authored.</summary>
    [Test]
    public async Task A_placement_naming_nothing_at_all_is_not_this()
    {
        var findings = SketchLayoutCheck.Check(WithDressing(
            """{"props":[{"kind":"tree","id":"t1","x":0,"z":0,"style":""}]}"""));

        await Assert.That(findings.Any(f => f.Rule == SketchRules.RecipeNotStated)).IsFalse();
    }

    [Test]
    public async Task A_layout_with_no_dressing_at_all_is_not_this()
    {
        var findings = SketchLayoutCheck.Check(WithDressing("null"));
        await Assert.That(findings.Any(f => f.Rule == SketchRules.RecipeNotStated)).IsFalse();
    }

    /// <summary>The placement is named by its own id where it has one, so a refusal points at the entry to
    /// fix rather than at the document.</summary>
    [Test]
    public async Task An_unnamed_placement_is_reported_by_where_it_sits()
    {
        var findings = SketchLayoutCheck.Check(WithDressing(
            """{"props":[{"kind":"boulder","x":0,"z":0,"style":"gone"}],"styles":{}}"""));

        await Assert.That(findings.Single(f => f.Rule == SketchRules.RecipeNotStated).Subjects).Contains("#0");
    }

    // ── SK20: the list order and base_y disagree about which layer is on top ──────────

    /// <summary>A stack whose layers are listed in the order their ground stands in.</summary>
    private static string Stack(params (string Id, double BaseY, string? Kind)[] layers) =>
        """{"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layers":["""
        + string.Join(",", layers.Select(layer =>
            $$"""{"id":"{{layer.Id}}","base_y":{{layer.BaseY}}"""
            + (layer.Kind is null ? "" : $$""","kind":"{{layer.Kind}}" """.TrimEnd())
            + ""","layout":{"shapes":[],"groups":[]}}"""))
        + "]}";

    [Test]
    public async Task A_stack_listed_in_the_order_its_ground_stands_in_says_nothing()
    {
        var findings = SketchLayoutCheck.Check(Stack(("ground", 0, null), ("terrace", 14, null), ("roof", 20, null)));
        await Assert.That(findings.Where(finding => finding.Rule == SketchRules.StackOutOfOrder)).IsEmpty();
    }

    [Test]
    public async Task A_layer_drawn_after_one_whose_ground_starts_higher_is_a_complaint()
    {
        var findings = SketchLayoutCheck.Check(Stack(("ground", 0, null), ("terrace", 20, null), ("span", 14, null)));
        var reported = findings.Single(finding => finding.Rule == SketchRules.StackOutOfOrder);
        await Assert.That(reported.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(reported.Subjects).IsEquivalentTo(new[] { "terrace", "span" });
    }

    [Test]
    public async Task A_made_things_slices_are_outside_the_order_and_do_not_break_the_plain_run()
    {
        var findings = SketchLayoutCheck.Check(Stack(
            ("ground", 0, null), ("balloon-top", 40, "made"), ("balloon-basket", 24, "made"), ("lid", 17, null)));
        await Assert.That(findings.Where(finding => finding.Rule == SketchRules.StackOutOfOrder)).IsEmpty();
    }
}
