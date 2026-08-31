using System.Text.Json;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

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
    public async Task Protection_and_room_are_the_marker_pieces_full_footprint()
    {
        var (_, intent) = PlanCompiler.Compile(Plan($$"""{ "plan":1, "globals":{"symmetry":"rot_180"}, {{Unit}} }"""));
        // Spawn protection = the whole 'lane' piece (rect [1,5,2,6] → blocks x 5..15, z 25..55), not the
        // smaller stamped cube around the spawn point.
        var prot = intent.Spawns.Single(s => s.Team == "red").Protection.Single();
        await Assert.That((prot.MinX, prot.MinZ, prot.MaxX, prot.MaxZ)).IsEqualTo((5d, 25d, 15d, 55d));
        // Wool room = the whole 'wr' piece (rect [-3,5,2,2] → blocks x -15..-5, z 25..35).
        var room = intent.Wools!.First(w => w.Owner == "red").Room.Single();
        await Assert.That((room.MinX, room.MinZ, room.MaxX, room.MaxZ)).IsEqualTo((-15d, 25d, -5d, 35d));
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

    /// <summary>The observer's height still comes off the plan's nominal surface — it is a camera position on
    /// a flat board, and there is no built terrain when this runs. The build ceiling deliberately does not:
    /// it is left unset here, because the answer is twenty blocks over the ground the world build produces and
    /// that ground does not exist yet. A number asserted here would be the one the export overwrites.</summary>
    [Test]
    public async Task Observer_defaults_to_surface_plus_fifteen_and_the_ceiling_is_left_to_the_world_build()
    {
        var (_, intent) = PlanCompiler.Compile(Plan($$"""
        { "plan":1, "globals":{"symmetry":"rot_180","surface":9}, {{Unit}} }
        """));
        await Assert.That(intent.Observer!.Point.Y).IsEqualTo(24);
        await Assert.That(intent.Build!.MaxHeight).IsNull();
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
        await Assert.That(SketchLayout.Stack(layout)[0].Shapes.Count).IsEqualTo(1);
        await Assert.That(SketchLayout.Stack(layout)[0].Groups.Count).IsEqualTo(1);
        await Assert.That(SketchLayout.Stack(layout)[0].Groups[0].Mirrors).IsTrue();
    }

    // A spawn-role piece abutting a wool-room-role piece at one surface — S25's motivating case, since the
    // single-height generator fuses same-plane pieces into one polygon.
    private const string StructuralUnit = """
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5,"surface":9},
          "pieces":[ {"id":"sp","role":"spawn","rect":[0,0,2,2]}, {"id":"wr","role":"wool-room","rect":[2,0,2,2]} ],
          "placements":{ "spawns":[ {"piece":"sp","at":[1,1],"facing":"front"} ],
                         "wools":[ {"piece":"wr","at":[1,1]} ] } }
        """;

    [Test]
    public async Task Structural_pieces_surface_as_locked_role_rectangles_distinct_from_the_fused_terrain()
    {
        var (layout, _) = PlanCompiler.Compile(Plan(StructuralUnit));
        var shapes = SketchLayout.Stack(layout)[0].Shapes;
        var spawns  = shapes.Where(s => s.Role == "spawn").ToList();
        var wools   = shapes.Where(s => s.Role == "woolRoom").ToList();
        var terrain = shapes.Where(s => s.Role is null).ToList();

        // Both teams' spawn + wool room surface as their own labelled rectangles, linked + coloured for the
        // sketch (spawn keyed by team, wool by owner:colour).
        await Assert.That(spawns.Count).IsEqualTo(2);
        await Assert.That(wools.Count).IsEqualTo(2);
        await Assert.That(spawns.All(s => s.Type == "rectangle")).IsTrue();
        await Assert.That(spawns.Select(s => s.IntentRef!)).IsEquivalentTo(new[] { "red", "blue" });
        await Assert.That(spawns.Single(s => s.IntentRef == "red").Color).IsEqualTo("red");
        await Assert.That(wools.Select(w => w.IntentRef!)).IsEquivalentTo(new[] { "red:red", "blue:blue" });

        // The terrain the pieces sit on has fused to fewer polygons than there are structural pieces — the very
        // case the annotation exists to survive.
        await Assert.That(terrain.Count).IsLessThan(spawns.Count + wools.Count);
    }

    [Test]
    public async Task Structural_role_shapes_contribute_no_terrain_cells()
    {
        var (layout, _) = PlanCompiler.Compile(Plan(StructuralUnit));
        var withRoles = SketchRasterizer.Rasterize(layout.ToJson()).ToHashSet();

        // Strip the structural shapes and rasterize again — an identical footprint proves the rasterizer never
        // turned a locked annotation into ground (they overlap the fused terrain but add nothing to it).
        SketchLayout.Stack(layout)[0].Shapes.RemoveAll(s => s.Role is not null);
        var terrainOnly = SketchRasterizer.Rasterize(layout.ToJson()).ToHashSet();

        await Assert.That(terrainOnly.Count).IsGreaterThan(0);
        await Assert.That(withRoles.SetEquals(terrainOnly)).IsTrue();
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
        await Assert.That(SketchLayout.Stack(layout)[0].Groups.Count).IsEqualTo(1);
        var surface11 = SketchLayout.Stack(layout)[0].Shapes.Where(s => s.BaseHeight == 11).ToList();
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
        await Assert.That(SketchLayout.Stack(layout)[0].Groups.Count).IsEqualTo(1);
        // one shape per plateau: base 9 (bar + step1 unioned), 11, 13.
        await Assert.That(SketchLayout.Stack(layout)[0].Shapes.Count).IsEqualTo(3);
        await Assert.That(SketchLayout.Stack(layout)[0].Shapes.Select(s => s.BaseHeight!.Value).OrderBy(h => h).ToList())
            .IsEquivalentTo(new[] { 9d, 11d, 13d });
    }

    // The compiled layout's solid (x,z) footprint — what the world is actually built from.
    private static HashSet<(int, int)> Footprint(SketchLayout layout) =>
        [.. SketchRasterizer.Rasterize(JsonSerializer.Serialize(layout, SketchLayout.Json))];

    [Test]
    public async Task A_ring_of_pieces_keeps_its_enclosed_void()
    {
        // Four pieces frame a one-cell hole at cell (1,1) — blocks 5..10 on both axes. The patch's outline is
        // only its outer boundary, so without the declared void the hole would rasterize as solid ground.
        var (layout, _) = PlanCompiler.Compile(Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5,"surface":9},
          "pieces":[ {"id":"n","role":"piece","rect":[0,0,3,1]}, {"id":"s","role":"piece","rect":[0,2,3,1]},
                     {"id":"w","role":"piece","rect":[0,1,1,1]}, {"id":"e","role":"piece","rect":[2,1,1,1]} ] }
        """));
        var hole = SketchLayout.Stack(layout)[0].Shapes.Single(s => s.Operation == "subtract");
        await Assert.That(Bbox(hole.Vertices!)).IsEqualTo((5, 5, 10, 10));
        // it joins the island of the body that encloses it, so it mirrors with that body
        await Assert.That(SketchLayout.Stack(layout)[0].Groups.Single().ShapeIds.Contains(hole.Id)).IsTrue();

        var cells = Footprint(layout);
        await Assert.That(cells.Contains((7, 7))).IsFalse();      // inside the hole — void
        await Assert.That(cells.Contains((2, 2))).IsTrue();       // on the frame — ground
        await Assert.That(cells.Contains((-7, -7))).IsFalse();    // the rot_180 copy's hole, void too
    }

    [Test]
    public async Task Every_colour_the_compiler_picks_is_a_dye_PGM_resolves()
    {
        // A wool that states no colour has one picked: the team's colour for a team's first, then a cursor
        // over distinct dyes. Both sources are lists the compiler holds rather than the dye set itself, so
        // what has to hold is that the words they hand out are words PGM resolves — a colour it cannot
        // resolve is a map that will not load, and nothing between here and the server would say so.
        // rot_90 is the widest fan there is (four teams), so it exercises the whole team half of the choice.
        var (_, intent) = PlanCompiler.Compile(Plan("""
        { "plan":1, "globals":{"symmetry":"rot_90","cell":5,"surface":9},
          "pieces":[ {"id":"w1","role":"wool-room","rect":[2,2,2,2]},
                     {"id":"w2","role":"wool-room","rect":[6,2,2,2]},
                     {"id":"w3","role":"wool-room","rect":[2,6,2,2]} ],
          "placements":{ "wools":[ {"piece":"w1","at":[1,1]},
                                   {"piece":"w2","at":[1,1]},
                                   {"piece":"w3","at":[1,1]} ] } }
        """));
        await Assert.That(intent.Wools.Count).IsEqualTo(12);
        foreach (var wool in intent.Wools)
            await Assert.That(WoolColors.IsColor(wool.Color)).IsTrue();
    }

    [Test]
    public async Task A_buffer_subtracts_only_the_ground_no_piece_covers()
    {
        // a buffer half over a lane, half off its end: the covered half is inert, the open half is negative
        // space that was never terrain — so the lane keeps every block it generates.
        var (layout, _) = PlanCompiler.Compile(Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5,"surface":9},
          "pieces":[ {"id":"lane","role":"piece","rect":[0,0,2,2]},
                     {"id":"over","role":"buffer","rect":[1,0,2,2]} ] }
        """));
        var cells = Footprint(layout);
        await Assert.That(cells.Contains((7, 2))).IsTrue();       // lane block under the buffer — kept
        await Assert.That(cells.Contains((2, 2))).IsTrue();       // lane block clear of it — kept
        await Assert.That(cells.Contains((12, 2))).IsFalse();     // past the lane's end — void either way
    }
}
