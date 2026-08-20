using PgmStudio.Domain;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The document gate for a sketch layout. Every case here was first put through the real build and observed
/// to pass silently — a shape kind nobody has, a two-vertex polygon, a mirror mode nobody has and a relief
/// keyed to no island all produce a world, with less in it than the document asked for. The rasterizer is set
/// algebra, so an unreadable shape contributes no ground rather than failing; this is what says so.
/// </summary>
public sealed class SketchLayoutCheckTests
{
    private static string Layout(string shapes, string extra = "", string mode = "rot_180") =>
        "{\"setup\":{\"mirror_mode\":\"" + mode + "\",\"center\":{\"cx\":0,\"cz\":0}},"
        + "\"layers\":[{\"base_y\":0,\"layout\":{\"shapes\":[" + shapes + "],"
        + "\"islands\":[{\"id\":\"i\",\"name\":\"I\",\"shapeIds\":[\"s1\"]}]}}]" + extra + "}";

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

        var finding = findings.Single(f => f.Message.Contains("island 'i'"));
        await Assert.That(finding.Rule).IsEqualTo(SketchRules.NamesNothing);
        await Assert.That(finding.Message).Contains("lists shape 's1'");
    }

    [Test]
    public async Task A_relief_keyed_to_an_island_that_is_not_there_is_named()
    {
        var findings = SketchLayoutCheck.Check(Layout(Rect, ""","relief":{"island-2":{"kind":"hills"}}"""));

        var finding = findings.Single();
        await Assert.That(finding.Field).IsEqualTo("relief.island-2");
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
    public async Task A_body_that_is_not_a_layout_is_not_this_gates_to_report()
    {
        // An unreadable body is the request's own fault (RQ1), answered where the body is read — this gate
        // says nothing rather than inventing a second answer for it.
        await Assert.That(SketchLayoutCheck.Check("this is not json")).IsEmpty();
        await Assert.That(SketchLayoutCheck.Check("""{"hello":"world"}""")).IsEmpty();
    }
}
