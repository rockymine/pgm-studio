using System.Text.Json;
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
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static string Ser(object o) => JsonSerializer.Serialize(o, Web);

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
    public async Task Right_facing_is_absolute_plus_x_on_the_authored_unit_and_rotates_on_the_orbit_image()
    {
        // facing is absolute (front=−z, back=+z, left=−x, right=+x): an authored `right` spawn faces +x (east,
        // yaw 270) on team 0, independent of where the piece sits, and its rot_180 image faces −x (west, yaw 90).
        var p = Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5},
          "pieces":[ {"id":"lane","role":"piece","rect":[1,5,2,6]} ],
          "placements":{ "spawns":[ {"piece":"lane","at":[1,5],"facing":"right"} ] } }
        """);
        var (_, intent) = PlanCompiler.Compile(p);
        var red = intent.Spawns.Single(s => s.Team == "red");
        await Assert.That(red.Point.X).IsEqualTo(10);
        await Assert.That(red.Point.Z).IsEqualTo(50);
        await Assert.That(red.Yaw).IsEqualTo(270);      // +x faces east
        var blue = intent.Spawns.Single(s => s.Team == "blue");
        await Assert.That(blue.Yaw).IsEqualTo(90);      // rot_180 image faces −x (west)
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
    public async Task A_buffer_overlapping_terrain_compiles_identically_to_the_plan_without_it()
    {
        // A buffer is non-generating: even one overlapping a terrain piece must leave the compiled pair byte-
        // identical — same layout shapes + fanned bbox, same intent (teams/spawns/wools/build/structures).
        const string unit = """
            "pieces":[ {"id":"lane","role":"lane","rect":[1,5,2,6]}, {"id":"wr","role":"wool-room","rect":[-3,5,2,2]} ],
            "placements":{ "spawns":[ {"piece":"lane","at":[1,5],"facing":"front"} ],
                           "wools":[ {"piece":"wr","at":[1,1]} ] }
            """;
        var (baseLayout, baseIntent) = PlanCompiler.Compile(Plan($$"""{ "plan":1, "globals":{"symmetry":"rot_180","cell":5}, {{unit}} }"""));
        var (bufLayout, bufIntent) = PlanCompiler.Compile(Plan($$"""
            { "plan":1, "globals":{"symmetry":"rot_180","cell":5},
              "pieces":[ {"id":"lane","role":"lane","rect":[1,5,2,6]}, {"id":"wr","role":"wool-room","rect":[-3,5,2,2]},
                         {"id":"buffer","role":"buffer","rect":[1,5,2,2]} ],
              "placements":{ "spawns":[ {"piece":"lane","at":[1,5],"facing":"front"} ],
                             "wools":[ {"piece":"wr","at":[1,1]} ] } }
            """));

        await Assert.That(Ser(bufLayout)).IsEqualTo(Ser(baseLayout));
        await Assert.That(Ser(bufIntent)).IsEqualTo(Ser(baseIntent));
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

    [Test]
    public async Task Disjoint_same_surface_patches_bridged_by_another_surface_each_get_a_shape()
    {
        // one component: a surface-9 bridge shares a land border with two surface-11 patches that do not touch
        // each other (a gap between them). The surface-11 group is disjoint yet both patches must surface — the
        // union must not drop the smaller one.
        var p = Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5,"surface":9},
          "pieces":[ {"id":"bridge","role":"piece","rect":[0,0,6,2]},
                     {"id":"a","role":"piece","rect":[0,2,2,2],"surface":11},
                     {"id":"b","role":"piece","rect":[4,2,2,2],"surface":11} ] }
        """);
        var (layout, _) = PlanCompiler.Compile(p);
        // one island (all three pieces one component), one surface-9 shape, two surface-11 shapes
        await Assert.That(layout.Layout!.Islands.Count).IsEqualTo(1);
        var surface11 = layout.Layout!.Shapes.Where(s => s.BaseHeight == 11).ToList();
        await Assert.That(surface11.Count).IsEqualTo(2);
        // the two patches are A's block box x[0,10]z[10,20] and B's x[20,30]z[10,20]
        var boxes = surface11.Select(s => Bbox(s.Vertices!)).ToHashSet();
        await Assert.That(boxes.SetEquals(new[] { (0, 10, 10, 20), (20, 10, 30, 20) })).IsTrue();
    }

    // The integer bounding box (minX, minZ, maxX, maxZ) of a ring's vertices.
    private static (int, int, int, int) Bbox(IReadOnlyList<double[]> ring) =>
        ((int)ring.Min(v => v[0]), (int)ring.Min(v => v[1]), (int)ring.Max(v => v[0]), (int)ring.Max(v => v[1]));

    [Test]
    public async Task A_staircase_of_narrow_steps_compiles_to_one_island_with_a_shape_per_plateau()
    {
        // the author's idiom: a 1x3 bar plus three stepped 1x1 pieces (distinct surfaces), each meeting the
        // others over a 5-block (narrow) border. Narrow seams connect, so all four join one component and union
        // into a single island; the union splits into one shape per distinct surface (a stacked plateau).
        var p = Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5,"surface":9},
          "pieces":[ {"id":"bar","role":"piece","rect":[0,0,1,3]},
                     {"id":"step1","role":"piece","rect":[1,0,1,1]},
                     {"id":"step2","role":"piece","rect":[1,1,1,1],"surface":11},
                     {"id":"step3","role":"piece","rect":[1,2,1,1],"surface":13} ] }
        """);
        var (layout, _) = PlanCompiler.Compile(p);
        await Assert.That(layout.Layout!.Islands.Count).IsEqualTo(1);
        // one shape per plateau: base 9 (bar + step1 unioned), 11, 13.
        await Assert.That(layout.Layout!.Shapes.Count).IsEqualTo(3);
        await Assert.That(layout.Layout!.Shapes.Select(s => s.BaseHeight!.Value).OrderBy(h => h).ToList())
            .IsEquivalentTo(new[] { 9d, 11d, 13d });
    }
}
