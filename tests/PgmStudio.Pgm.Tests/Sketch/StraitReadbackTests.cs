using System.Text.Json;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The CTW strait re-read off the drawn board (<c>CT12</c>). The plan measures it over rectangles before a
/// shape exists, and a finish is free to move it — so the same pairs are measured again on the raster, and
/// the same 15–40 band decides.
///
/// <para>The fixture is the plan the validator's own CT12 case passes: a team island fanned rot_180 across a
/// 30-block strait. Compiled it stays 30 and says nothing; a shape drawn across the gap closes it and is the
/// finding.</para>
/// </summary>
public sealed class StraitReadbackTests
{
    /// <summary>A two-team wool board whose team islands stand 30 blocks apart — inside the band, so the
    /// plan's own CT12 passes and there is a verdict for the raster to move.</summary>
    private const string Banded = """
    { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
      "pieces":[ {"id":"home","role":"spawn","rect":[-2,-5,4,2]} ],
      "zones":[ {"id":"strait","rect":[-2,-3,4,6]} ],
      "placements":{ "spawns":[ {"piece":"home","at":[10,5],"facing":"front"} ],
                     "wools":[ {"piece":"home","at":[5,5]} ] } }
    """;

    private static (PlanModel Plan, string Layout) Compiled(params string[] extraShapes)
    {
        var plan = PlanModel.Parse(Banded)!;
        var (layout, _) = PlanCompiler.Compile(plan);
        var json = JsonSerializer.Serialize(layout, SketchLayout.Json);
        if (extraShapes.Length == 0) return (plan, json);

        // The finish, as an author writes one: shapes appended to the compiled ground layer.
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var shapes = node["layers"]![0]!["layout"]!["shapes"]!.AsArray();
        foreach (var shape in extraShapes) shapes.Add(System.Text.Json.Nodes.JsonNode.Parse(shape));
        return (plan, node.ToJsonString());
    }

    [Test]
    public async Task A_compiled_board_whose_strait_the_plan_passed_says_nothing()
    {
        var (plan, layout) = Compiled();

        await Assert.That(StraitReadback.Check(plan, layout)).IsEmpty();
    }

    [Test]
    public async Task A_shape_drawn_across_the_strait_joins_the_two_teams_and_is_reported()
    {
        // A bridge over the whole gap: the two team islands become one landmass, so the strait the plan was
        // checked against is not in the ground at all.
        var (plan, layout) = Compiled("""
        {"id":"bridge","type":"rectangle","operation":"add","min_x":-10,"min_z":-20,"max_x":10,"max_z":20,"base_height":8}
        """);

        var finding = StraitReadback.Check(plan, layout).Single();
        await Assert.That(finding.Rule).IsEqualTo("CT12");
        await Assert.That(finding.Severity).IsEqualTo(Vocabulary.Severity.Complaint);
        await Assert.That(finding.Message).Contains("one landmass");
    }

    [Test]
    public async Task A_shape_that_narrows_the_strait_out_of_band_is_reported_with_both_numbers()
    {
        // A quay pushed out from one team's shore: the two masses stay apart and the crossing no longer is
        // one — the case the plan cannot see, since it was measured before the quay was drawn.
        var (plan, layout) = Compiled(
            """
            {"id":"quay","type":"rectangle","operation":"add","min_x":-10,"min_z":-15,"max_x":10,"max_z":5,"base_height":8}
            """);

        var finding = StraitReadback.Check(plan, layout).Single();
        await Assert.That(finding.Rule).IsEqualTo("CT12");
        await Assert.That(finding.Message).Contains("30 blocks apart");
        await Assert.That(finding.Message).Contains("15–40");
    }

    [Test]
    public async Task A_board_with_no_wool_is_not_this_rules_to_judge()
    {
        var plan = PlanModel.Parse(Banded.Replace("""
            "wools":[ {"piece":"home","at":[5,5]} ]
        """.Trim(), """ "wools":[] """))!;
        var (layout, _) = PlanCompiler.Compile(plan);

        await Assert.That(StraitReadback.Check(plan, JsonSerializer.Serialize(layout, SketchLayout.Json))).IsEmpty();
    }

    [Test]
    public async Task No_plan_and_no_layout_are_both_nothing_to_measure()
    {
        var (plan, layout) = Compiled();
        await Assert.That(StraitReadback.Check(null, layout)).IsEmpty();
        await Assert.That(StraitReadback.Check(plan, null)).IsEmpty();
    }
}
