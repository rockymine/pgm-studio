using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// Where the intent's areas, lanes, walls and doors meet the ground the sketch builds. The fixture is a team
/// island fanned rot_180 across a 30-block strait, with a quay drawn beside it 30 blocks wide and 20 deep: the
/// compiled board breaks what its plan breaks, and each test draws one thing wrong onto it.
/// </summary>
public sealed class SketchSeamCheckTests
{
    private const string Banded = """
    { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
      "pieces":[ {"id":"home","role":"spawn","rect":[-2,-5,4,2]} ],
      "zones":[ {"id":"strait","rect":[-2,-3,4,6]} ],
      "placements":{ "spawns":[ {"piece":"home","at":[10,5],"facing":"front"} ],
                     "wools":[ {"piece":"home","at":[5,5]} ] } }
    """;

    private const string Quay = """
    {"id":"quay","type":"rectangle","operation":"add","min_x":30,"min_z":-10,"max_x":60,"max_z":10,"base_height":8}
    """;

    private static (string Layout, MapIntent Intent) Compiled()
    {
        var (layout, intent) = PlanCompiler.Compile(PlanModel.Parse(Banded)!);
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(layout, SketchLayout.Json))!;
        node["layers"]![0]!["layout"]!["shapes"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse(Quay));
        return (node.ToJsonString(), intent);
    }

    private static List<Finding> Check(Func<MapIntent, MapIntent> change)
    {
        var (layout, intent) = Compiled();
        return [.. SketchSeamCheck.Check(layout, change(intent), SketchLayout.Parse(layout)?.Setup?.Board)];
    }

    private static MapIntent WithArea(MapIntent intent, params PieceArea[] areas) =>
        intent with { Build = (intent.Build ?? new BuildIntent()) with { Areas = [.. intent.Build?.Areas ?? [], .. areas] } };

    private static MapIntent WithWall(MapIntent intent, int x1, int z1, int x2, int z2) =>
        intent with { Structures = (intent.Structures ?? new StructureIntent()) with { Walls = [new WallStructure(x1, z1, x2, z2, 12, Id: "wall")] } };

    [Test]
    public async Task The_compiled_board_breaks_the_seams_its_plan_breaks()
    {
        var plan = PlanValidator.Check(PlanModel.Parse(Banded)!).Where(finding => SketchSeamCheck.Asks.Contains(finding.Rule));

        await Assert.That(Check(intent => intent).Select(finding => finding.Rule).Order())
            .IsEquivalentTo(plan.Select(finding => finding.Rule).Order())
            .Because("the strait runs up to both spawns' regions, which is the plan's BZ5 too");
    }

    [Test]
    public async Task A_region_narrower_than_a_corridor_is_named_with_its_width()
    {
        var finding = Check(intent => WithArea(intent, new PieceArea(30, -40, 35, -20, "narrow")))
            .Single(finding => finding.Rule == LayoutRules.CorridorWidth);

        await Assert.That(finding.Message).Contains("'narrow' is 5 blocks across its shorter side");
    }

    [Test]
    public async Task A_region_against_a_spawns_region_touches_it_and_one_a_corner_away_does_not()
    {
        var against = Check(intent => WithArea(intent, new PieceArea(10, -25, 20, -15, "against")));
        var corner = Check(intent => WithArea(intent, new PieceArea(10, -40, 20, -25, "corner")));

        await Assert.That(against.Where(finding => finding.Rule == LayoutRules.ZoneTouchesSpawn).SelectMany(finding => finding.Subjects!))
            .Contains("against");
        await Assert.That(corner.Where(finding => finding.Rule == LayoutRules.ZoneTouchesSpawn).SelectMany(finding => finding.Subjects!))
            .DoesNotContain("corner");
    }

    [Test]
    public async Task Two_regions_that_fill_one_rectangle_are_stitched_and_an_l_is_not()
    {
        var stitched = Check(intent => WithArea(intent, new PieceArea(30, 20, 40, 30, "left"), new PieceArea(40, 20, 50, 30, "right")));
        var ell = Check(intent => WithArea(intent, new PieceArea(30, 20, 40, 40, "left"), new PieceArea(40, 20, 50, 30, "right")));

        await Assert.That(stitched.Single(finding => finding.Rule == LayoutRules.StitchedZones).Message).Contains("'left' and 'right'");
        await Assert.That(ell.Select(finding => finding.Rule)).DoesNotContain(LayoutRules.StitchedZones);
    }

    [Test]
    public async Task A_lane_over_the_quay_overlaps_ground_and_one_over_the_void_does_not()
    {
        MapIntent Lane(MapIntent intent, double minX) =>
            intent with { WaterLanes = new WaterLaneIntent { Rects = [new PieceArea(minX, -5, minX + 10, 5, "lane")] } };

        await Assert.That(Check(intent => Lane(intent, 35)).Select(finding => finding.Rule)).Contains(LayoutRules.WaterLaneOverGround);
        await Assert.That(Check(intent => Lane(intent, 65)).Select(finding => finding.Rule)).DoesNotContain(LayoutRules.WaterLaneOverGround);
    }

    [Test]
    public async Task A_wall_beside_a_wools_region_is_refused()
    {
        var finding = Check(intent => WithWall(intent, -10, -15, 10, -15)).Single(finding => finding.Rule == PlanRules.WallOnWoolRoom);

        await Assert.That(finding.Severity).IsEqualTo(Severity.Refusal);
    }

    [Test]
    public async Task A_wall_across_the_whole_quay_bars_its_depth_and_stands_clear_at_both_ends()
    {
        var rules = Check(intent => WithWall(intent, 40, -15, 40, 15)).Select(finding => finding.Rule).ToList();

        await Assert.That(rules).DoesNotContain(PlanRules.WallAtJunction)
            .Because("both ends stand out over the void, though the line is longer than the ground it crosses");
        await Assert.That(rules).DoesNotContain(LayoutRules.ApproachWallEdgeLength);
    }

    [Test]
    public async Task A_wall_ending_inside_the_quay_has_ground_past_its_end_and_too_short_a_mouth()
    {
        var findings = Check(intent => WithWall(intent, 40, -3, 40, 3));

        await Assert.That(findings.Select(finding => finding.Rule)).Contains(PlanRules.WallAtJunction);
        await Assert.That(findings.Single(finding => finding.Rule == LayoutRules.ApproachWallEdgeLength).Message)
            .Contains("6 blocks across, less than 10 blocks");
    }

    [Test]
    public async Task A_spawn_whose_door_opens_on_the_void_has_no_ground_ahead()
    {
        var findings = Check(intent => intent with
        {
            Spawns = [.. intent.Spawns.Select(spawn => spawn.Stamp.Image == 0 ? spawn with { Doors = ["-z"] } : spawn)],
        });

        await Assert.That(findings.Single(finding => finding.Rule == LayoutRules.SpawnDoorGround).Message)
            .Contains("0 blocks of ground ahead of its -z door");
    }
}
