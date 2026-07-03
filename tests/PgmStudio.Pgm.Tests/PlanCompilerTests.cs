using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The plan compiler's building blocks: team defs from the orbit order, facing→yaw, orbit fanning of spawns
/// and wools, auto/explicit wool colours, fanned+deduped build areas, and the framed bbox. Full seed
/// regression lives in <see cref="PlanSeedGoldenTests"/>.
/// </summary>
public sealed class PlanCompilerTests
{
    private static PlanModel Plan(string json) => PlanModel.Parse(json)!;

    private const string Unit = """
        "pieces":[ {"id":"lane","role":"lane","rect":[1,5,2,6]}, {"id":"wr","role":"wool-room","rect":[-3,5,2,2]} ],
        "placements":{ "spawns":[ {"piece":"lane","at":[1,5],"facing":"front"} ],
                       "wools":[ {"piece":"wr","at":[1,1]} ] }
        """;

    [Test]
    public async Task Rot_180_yields_two_teams_red_and_blue()
    {
        var (_, intent) = PlanCompiler.Compile(Plan($$"""{ "plan":1, "globals":{"symmetry":"rot_180"}, {{Unit}} }"""));
        await Assert.That(intent.Teams!.Select(t => t.Id)).IsEquivalentTo(new[] { "red", "blue" });
        await Assert.That(intent.Spawns.Count).IsEqualTo(2);
        await Assert.That(intent.Wools!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Rot_90_yields_four_teams_in_orbit_order()
    {
        var (_, intent) = PlanCompiler.Compile(Plan($$"""{ "plan":1, "globals":{"symmetry":"rot_90"}, {{Unit}} }"""));
        await Assert.That(intent.Teams!.Select(t => t.Id).ToList()).IsEquivalentTo(new[] { "red", "blue", "yellow", "green" });
        await Assert.That(intent.Spawns.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Front_facing_spawn_faces_the_centre()
    {
        var (_, intent) = PlanCompiler.Compile(Plan($$"""{ "plan":1, "globals":{"symmetry":"rot_180"}, {{Unit}} }"""));
        var red = intent.Spawns.Single(s => s.Team == "red");
        // team-0 spawn resolves to block (10,50); front (toward 0,0) quantizes to -Z → yaw 180
        await Assert.That(red.Point.X).IsEqualTo(10);
        await Assert.That(red.Point.Z).IsEqualTo(50);
        await Assert.That(red.Yaw).IsEqualTo(180);
        var blue = intent.Spawns.Single(s => s.Team == "blue");
        await Assert.That(blue.Yaw).IsEqualTo(0);       // rot_180 image faces +Z
    }

    [Test]
    public async Task First_wool_takes_the_team_colour()
    {
        var (_, intent) = PlanCompiler.Compile(Plan($$"""{ "plan":1, "globals":{"symmetry":"rot_180"}, {{Unit}} }"""));
        await Assert.That(intent.Wools!.Single(w => w.Owner == "red").Color).IsEqualTo("red");
        await Assert.That(intent.Wools!.Single(w => w.Owner == "blue").Color).IsEqualTo("blue");
    }

    [Test]
    public async Task An_explicit_wool_colour_is_respected()
    {
        var p = Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180"},
          "pieces":[ {"id":"lane","role":"lane","rect":[1,5,2,6]} ],
          "placements":{ "wools":[ {"piece":"lane","at":[1,1],"color":"magenta"} ] } }
        """);
        var (_, intent) = PlanCompiler.Compile(p);
        await Assert.That(intent.Wools!.All(w => w.Color == "magenta")).IsTrue();
    }

    [Test]
    public async Task Build_areas_fan_across_the_orbit_and_dedupe_self_images()
    {
        var p = Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5},
          "pieces":[ {"id":"lane","role":"lane","rect":[1,5,2,6]} ],
          "zones":[ {"id":"mid","rect":[-3,-5,6,10]}, {"id":"bridge","rect":[3,7,2,2]} ] }
        """);
        var (_, intent) = PlanCompiler.Compile(p);
        // the centred band is its own rot_180 image → one area; the bridge fans to two
        await Assert.That(intent.Build!.Areas.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Observer_defaults_to_surface_plus_fifteen_and_max_height_to_surface_plus_headroom()
    {
        var (_, intent) = PlanCompiler.Compile(Plan($$"""
        { "plan":1, "globals":{"symmetry":"rot_180","surface":9,"headroom":11}, {{Unit}} }
        """));
        await Assert.That(intent.Observer!.Point.Y).IsEqualTo(24);
        await Assert.That(intent.Build!.MaxHeight).IsEqualTo(20);
    }

    [Test]
    public async Task A_half_cell_marker_offset_resolves_to_a_2_5_block_coordinate()
    {
        // a .5 offset on the half-cell lattice lands the marker on a 2.5-block half-cell: piece origin block
        // (0,0) + 0.5·5. The raw fractional coordinate flows through un-rounded (downstream floors it).
        var p = Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5},
          "pieces":[ {"id":"lane","role":"lane","rect":[0,0,2,2]} ],
          "placements":{ "wools":[ {"piece":"lane","at":[0.5,0.5]} ] } }
        """);
        var (_, intent) = PlanCompiler.Compile(p);
        await Assert.That(intent.Wools!.Any(w => w.Spawn.X == 2.5 && w.Spawn.Z == 2.5)).IsTrue();
    }

    [Test]
    public async Task Land_connected_pieces_union_into_one_shape()
    {
        // two abutting same-surface bars → one unioned shape, one island
        var p = Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5,"surface":9},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,2,4]}, {"id":"b","role":"lane","rect":[0,4,2,4]} ] }
        """);
        var (layout, _) = PlanCompiler.Compile(p);
        await Assert.That(layout.Layout!.Shapes.Count).IsEqualTo(1);
        await Assert.That(layout.Layout!.Islands.Count).IsEqualTo(1);
        await Assert.That(layout.Layout!.Islands[0].Mirrors).IsTrue();
    }
}
