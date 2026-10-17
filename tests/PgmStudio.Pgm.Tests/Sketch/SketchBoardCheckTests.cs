using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The board's structure read off the ground the sketch builds. The fixture is a team island fanned rot_180
/// across a 30-block strait, compiled to a layout and an intent: the sketch's own reading of it agrees with the
/// plan's, and a quay drawn out from one shore moves the strait out of band with nothing but the drawing to say so.
/// </summary>
public sealed class SketchBoardCheckTests
{
    private const string Banded = """
    { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
      "pieces":[ {"id":"home","role":"spawn","rect":[-2,-5,4,2]} ],
      "zones":[ {"id":"strait","rect":[-2,-3,4,6]} ],
      "placements":{ "spawns":[ {"piece":"home","at":[10,5],"facing":"front"} ],
                     "wools":[ {"piece":"home","at":[5,5]} ] } }
    """;

    private static (string Layout, MapIntent Intent) Compiled(params string[] extraShapes)
    {
        var (layout, intent) = PlanCompiler.Compile(PlanModel.Parse(Banded)!);
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(layout, SketchLayout.Json))!;
        var shapes = node["layers"]![0]!["layout"]!["shapes"]!.AsArray();
        foreach (var shape in extraShapes) shapes.Add(System.Text.Json.Nodes.JsonNode.Parse(shape));
        return (node.ToJsonString(), intent);
    }

    [Test]
    public async Task A_rooms_region_is_its_rooms_ground_and_its_team_is_the_spawns_image()
    {
        var (layout, intent) = Compiled();
        var ground = SketchGround.Of(layout, intent)!;
        var red = intent.Spawns.First(spawn => spawn.Stamp.Image == 0);
        var inside = ((int)Math.Floor(red.Protection[0].MinX), (int)Math.Floor(red.Protection[0].MinZ));

        await Assert.That(ground.Cell).IsEqualTo(1);
        await Assert.That(ground.Filled[inside].K).IsEqualTo(0);
        await Assert.That(ground.SpawnOwners).Contains(ground.Filled[inside].Owner)
            .Because("the spawn's region is a spawn's ground whichever room's id it carries");
        await Assert.That(ground.WoolOwners).Contains(ground.Filled[inside].Owner)
            .Because("the wool shares the spawn's piece, and its ground is a wool's too");
        await Assert.That(ground.Filled.Values.Select(cell => cell.K).Distinct().Order()).IsEquivalentTo(new[] { 0, 1 });
    }

    [Test]
    public async Task The_compiled_board_reads_as_the_plan_does()
    {
        var (layout, intent) = Compiled();

        await Assert.That(SketchBoardCheck.Check(layout, intent).Select(finding => finding.Rule))
            .DoesNotContain(LayoutRules.TeamGapWidth);
    }

    [Test]
    public async Task A_quay_drawn_into_the_strait_narrows_it_out_of_band()
    {
        var (layout, intent) = Compiled(
            """
            {"id":"quay","type":"rectangle","operation":"add","min_x":-10,"min_z":-15,"max_x":10,"max_z":5,"base_height":8}
            """);

        var strait = SketchBoardCheck.Check(layout, intent).Single(finding => finding.Rule == LayoutRules.TeamGapWidth);

        await Assert.That(strait.Measured!.Value).IsLessThan(15);
        await Assert.That(strait.Message).Contains("less than 15 blocks");
    }

    [Test]
    public async Task A_layout_that_builds_nothing_has_no_board_to_read()
    {
        await Assert.That(SketchGround.Of("""{"layers":[]}""", new MapIntent())).IsNull();
        await Assert.That(SketchBoardCheck.Check("""{"layers":[]}""", new MapIntent())).IsEmpty();
    }
}
