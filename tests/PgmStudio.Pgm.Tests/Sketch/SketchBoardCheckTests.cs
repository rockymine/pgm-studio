using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

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

    private static readonly string[] Routes = [LayoutRules.NoBuildRegion, PlanRules.WoolUnreachable, LayoutRules.SpawnOnWoolRoute];

    [Test]
    public async Task The_compiled_board_has_a_build_area_and_every_wool_a_route()
    {
        var (layout, intent) = Compiled();

        await Assert.That(SketchBoardCheck.Check(layout, intent).Select(finding => finding.Rule).Intersect(Routes)).IsEmpty();
    }

    /// <summary>With the strait unbuildable each team's ground is an island, so neither spawn walks to the other's
    /// wool.</summary>
    [Test]
    public async Task A_board_with_no_build_area_says_so_and_leaves_each_wool_out_of_the_other_teams_reach()
    {
        var (layout, intent) = Compiled();
        var findings = SketchBoardCheck.Check(layout, intent with { Build = null }).ToList();

        await Assert.That(findings.Count(finding => finding.Rule == LayoutRules.NoBuildRegion)).IsEqualTo(1);
        await Assert.That(findings.Count(finding => finding.Rule == PlanRules.WoolUnreachable)).IsEqualTo(2)
            .Because("each team's wool is cut off from the one other team");
        await Assert.That(findings.Any(finding => finding.Rule == LayoutRules.SpawnOnWoolRoute)).IsFalse()
            .Because("with no build area there is no front line to walk from");
    }

    /// <summary>A wool room on ground behind its spawn's room, away from the strait, is reached from the front line
    /// only through that room.</summary>
    [Test]
    public async Task A_wool_behind_its_spawns_room_has_no_front_line_route_round_it()
    {
        var (_, compiled) = Compiled();
        var red = compiled.Spawns.Single(spawn => spawn.Stamp.Image == 0).Protection[0];
        var farMinZ = Math.Abs(red.MinZ) > Math.Abs(red.MaxZ);
        var annex = farMinZ
            ? new Geom.Rect(red.MinX, red.MinZ - 10, red.MaxX, red.MinZ)
            : new Geom.Rect(red.MinX, red.MaxZ, red.MaxX, red.MaxZ + 10);
        var (layout, intent) = Compiled(
            $$"""{"id":"annex","type":"rectangle","operation":"add","min_x":{{annex.MinX}},"min_z":{{annex.MinZ}},"max_x":{{annex.MaxX}},"max_z":{{annex.MaxZ}},"base_height":9}""");
        var wools = intent.Wools!.Select(wool => wool.Stamp.Image == 0
            ? wool with { Protection = [annex], Spawn = wool.Spawn with { X = (annex.MinX + annex.MaxX) / 2, Z = (annex.MinZ + annex.MaxZ) / 2 } }
            : wool).ToList();

        var findings = SketchBoardCheck.Check(layout, intent with { Wools = wools }).ToList();
        var behind = intent.Wools!.Single(wool => wool.Stamp.Image == 0).Id;

        await Assert.That(findings.Where(finding => finding.Rule == LayoutRules.SpawnOnWoolRoute).SelectMany(finding => finding.Subjects!))
            .IsEquivalentTo(new[] { behind });
        await Assert.That(findings.Any(finding => finding.Rule == PlanRules.WoolUnreachable)).IsFalse()
            .Because("a spawn's room is ground a capturing team may cross");
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
    public async Task A_hop_bridged_to_more_of_the_units_ground_is_held_to_its_band()
    {
        List<Finding> Hop(int gap)
        {
            var (layout, intent) = Compiled(
                $$"""
                {"id":"islet","type":"rectangle","operation":"add","min_x":{{10 + gap}},"min_z":-25,"max_x":{{30 + gap}},"max_z":-15,"base_height":8}
                """);
            intent = intent with
            {
                Build = intent.Build! with { Areas = [.. intent.Build!.Areas, new PieceArea(10, -25, 10 + gap, -15, "bridge")] },
            };
            return [.. SketchBoardCheck.Check(layout, intent, SketchLayout.Parse(layout)?.Setup?.Board)];
        }

        await Assert.That(Hop(25).Single(finding => finding.Rule == LayoutRules.VoidHop).Message)
            .Contains("a gap of 25 blocks between them, more than 20 blocks");
        await Assert.That(Hop(15).Select(finding => finding.Rule)).DoesNotContain(LayoutRules.VoidHop);
    }

    [Test]
    public async Task A_layout_that_builds_nothing_has_no_board_to_read()
    {
        await Assert.That(SketchGround.Of("""{"layers":[]}""", new MapIntent())).IsNull();
        await Assert.That(SketchBoardCheck.Check("""{"layers":[]}""", new MapIntent())).IsEmpty();
    }
}
