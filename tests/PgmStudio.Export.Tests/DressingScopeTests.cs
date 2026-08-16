using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The dressing stage's map-facing half (G161): reading what an author placed, what the pass must leave bare,
/// and how the map is mirrored. <see cref="PgmStudio.Api.Tests.DressingPreviewTests"/> is the sibling for the
/// preview, which is asserted by what it <em>placed</em> rather than by the bytes it drew.
/// </summary>
public sealed class DressingScopeTests
{
    private static string Layout(string body) => $$$"""
        {"setup":{"bbox":{"min_x":-40,"max_x":40,"min_z":-40,"max_z":40},
                  "center":{"cx":0,"cz":0},"mirror_mode":"rot_180"},
         "layers":[{"base_y":0,"layout":{"shapes":[]}}]{{{body}}}}
        """;

    // ── what the author placed ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task An_undressed_sketch_places_nothing()
    {
        // The map that never opened the phase exports exactly as it did before the stage existed.
        await Assert.That(DressingScope.PropsOf(Layout(""))).IsEmpty();
    }

    [Test]
    public async Task Every_kind_of_prop_is_read_back_as_the_kind_it_was_written_as()
    {
        var props = DressingScope.PropsOf(Layout("""
            ,"dressing":{"props":[
              {"kind":"path","id":"p","points":[[0,0],[20,10]],"radius":3,"style":"stones","seed":5,
               "blocks":[{"id":4,"data":0}]},
              {"kind":"tree","id":"t","x":10,"z":12,"species":"birch","height":22,"seed":9},
              {"kind":"boulder","id":"b","x":-8,"z":4,"form":"cairn","size":4,"seed":11},
              {"kind":"flora","id":"f","points":[[0,0],[10,0],[10,10]],"spec":{"coverage":0.8},"seed":13}]}
            """));

        await Assert.That(props.Count).IsEqualTo(4);
        await Assert.That(((PathProp)props[0]).Style).IsEqualTo(PathStyle.Stones);
        await Assert.That(((TreeProp)props[1]).Species).IsEqualTo("birch");
        await Assert.That(((BoulderProp)props[2]).Form).IsEqualTo(BoulderForm.Cairn);
        await Assert.That(((FloraProp)props[3]).Spec.Coverage).IsEqualTo(0.8);
    }

    [Test]
    public async Task The_maps_symmetry_is_what_the_pass_fans_props_through()
    {
        var symmetry = DressingScope.SymmetryOf(Layout(""));
        await Assert.That(symmetry.Mode).IsEqualTo("rot_180");
        await Assert.That(symmetry.Order).IsEqualTo(2);
        await Assert.That(symmetry.ImageCell(3, 4, 1)).IsEqualTo((-4, -5));
    }

    // ── what must be left bare ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Nothing_is_placed_on_what_the_map_is_played_through()
    {
        var world = new VoxelWorld();
        var surface = new Dictionary<(int X, int Z), int>();
        for (var z = 0; z < 40; z++)
        for (var x = 0; x < 40; x++)
        {
            world.SetBlock(x, 7, z, Blocks.Grass);
            surface[(x, z)] = 8;
        }
        // A monument's wool standing on one column — a stamp, which the pass has no business planting on.
        world.SetBlock(30, 7, 30, Blocks.Wool, 14);

        var intent = new MapIntent
        {
            Spawns = [new SpawnIntent { Team = "red", Point = new Pt(5, 8, 5) }],
            Wools = [new WoolIntent { Owner = "red", Color = "red", Spawn = new Pt(20, 8, 20) }],
        };
        var isProtected = DressingScope.ProtectedAt(world, surface, intent);

        await Assert.That(isProtected(5, 5)).IsTrue();      // the spawn
        await Assert.That(isProtected(6, 6)).IsTrue();      // and its margin
        await Assert.That(isProtected(20, 20)).IsTrue();    // the wool
        await Assert.That(isProtected(30, 30)).IsTrue();    // the column a stamp stands on
        await Assert.That(isProtected(15, 34)).IsFalse();   // plain terrain
    }

    [Test]
    public async Task Ground_cover_is_free_over_a_goal_and_the_goal_asks_a_different_question()
    {
        // A destroyable and a core are absent from the placement mask on purpose. Grass under a floating
        // monument is what a hand-built map has; what the goal actually asks for is that nothing HIDES it,
        // which is a wider rect and a different rule.
        var (world, surface) = Ground(-20, 40);
        var core = new MapIntent
        {
            Cores = [new CoreIntent { Owner = "red", Anchor = new Pt(20, 8, 20), Size = 7, Height = 7, Shell = 1 }],
        };

        await Assert.That(DressingScope.ProtectedAt(world, surface, core)(20, 20)).IsFalse();
    }

    [Test]
    public async Task A_goals_ground_reaches_past_its_own_blocks_whatever_size_it_was_authored_at()
    {
        // The clearance is measured from the FOOTPRINT, so it holds at any authored size. A square grown from
        // the marker covers the default sizes by coincidence and stops covering the casing itself the moment a
        // core is authored larger — at size 7 the outer ring of the casing is the goal's own blocks.
        var core = new MapIntent
        {
            Cores = [new CoreIntent { Owner = "red", Anchor = new Pt(20, 8, 20), Size = 7, Height = 7, Shell = 1 }],
        };
        var goalGround = DressingScope.GoalGroundAt(core);

        await Assert.That(goalGround(23, 23)).IsTrue();      // the casing's own far corner
        await Assert.That(goalGround(27, 27)).IsTrue();      // the last block of its clearance (footprint + 4)
        await Assert.That(goalGround(28, 28)).IsFalse();     // and open ground beyond it
    }

    [Test]
    public async Task A_goals_ground_is_the_box_the_stamper_wrote_rather_than_a_square_on_its_marker()
    {
        // A cube-4 leans one block along +X/+Z, so the ground it covers is not centred on its anchor and a
        // symmetric square would hold the near faces while leaving the far ones short (OB8).
        var stamped = new MapIntent
        {
            Destroyables =
            [
                new DestroyableIntent
                {
                    Owner = "red", Name = "red's monument", Style = "cube-4", Materials = "obsidian",
                    Anchor = new Pt(0, 8, 0), Box = new BlockBox(-1, 12, -1, 2, 15, 2),
                },
            ],
        };
        var goalGround = DressingScope.GoalGroundAt(stamped);

        await Assert.That(goalGround(6, 6)).IsTrue();        // the leaning corner plus its clearance
        await Assert.That(goalGround(7, 7)).IsFalse();
        await Assert.That(goalGround(-5, -5)).IsTrue();      // one block nearer on the other side, as the box leans
        await Assert.That(goalGround(-6, -6)).IsFalse();
    }

    // ── OB19: a tree, a boulder or a building inside a goal's clearance ───────────────────────────────
    // A single-block destroyable at (20,20): its GoalGroundAt clearance rect is [16,16]-[24,24] (the
    // footprint grown by GoalClearance = 4), and the prop keep-out reaches further — GoalStandoff = 10 from
    // the marker, so [10,10]-[30,30]. Every test below is read against that one fixed goal.
    private static MapIntent GoalAt20 => new()
    {
        Destroyables =
        [
            new DestroyableIntent
            {
                Owner = "red", Name = "red's monument", Style = "pillar-1", Materials = "obsidian",
                Anchor = new Pt(20, 8, 20), Box = new BlockBox(20, 8, 20, 20, 8, 20),
            },
        ],
    };

    [Test]
    public async Task A_boulder_directly_inside_the_clearance_is_flagged()
    {
        var violations = DressingScope.GoalClearanceViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"boulder\",\"id\":\"b1\",\"x\":18,\"z\":18,\"seed\":1}]}"), GoalAt20);

        await Assert.That(violations.Count).IsEqualTo(1);
        await Assert.That(violations[0].Kind).IsEqualTo("boulder");
        await Assert.That(violations[0].PropId).IsEqualTo("b1");
    }

    [Test]
    public async Task A_building_is_flagged_the_moment_its_footprint_overlaps_the_clearance()
    {
        // The anchor corners sit outside the clearance rect, but the 5×5 footprint they enclose reaches into
        // its [16,16]-[24,24] corner — a building is checked as the whole floor it stamps, not a single point.
        var violations = DressingScope.GoalClearanceViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"house\",\"id\":\"h1\",\"seed\":1,\"wings\":[[[23,23],[27,27]]]}]}"),
            GoalAt20);

        await Assert.That(violations.Count).IsEqualTo(1);
        await Assert.That(violations[0].Kind).IsEqualTo("building");
        await Assert.That(violations[0].PropId).IsEqualTo("h1");
    }

    [Test]
    public async Task A_tree_only_its_mirror_reaches_the_clearance_is_still_flagged()
    {
        // Authored at (-20,-20) — nowhere near the goal at (20,20) — but the map is rot_180 about the origin,
        // so its OTHER image lands at (19,19), inside the clearance rect. The check fans every prop across
        // the same orbit Decorator itself stamps, so a violation only one team's mirror carries is still
        // caught rather than missed because the authored half looks clean.
        var violations = DressingScope.GoalClearanceViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"tree\",\"id\":\"t1\",\"seed\":1,\"x\":-20,\"z\":-20}]}"), GoalAt20);

        await Assert.That(violations.Count).IsEqualTo(1);
        await Assert.That(violations[0].Kind).IsEqualTo("tree");
        await Assert.That(violations[0].X).IsEqualTo(19);
        await Assert.That(violations[0].Z).IsEqualTo(19);
    }

    [Test]
    public async Task The_markers_standoff_reaches_past_the_structures_own_clearance()
    {
        // A tree at (28,20): outside the box-grown clearance (ends at 24) but inside the marker's ten-block
        // standoff (ends at 30) — the author's radius, measured from the marker rather than from however
        // wide the structure under it happens to be. One block past the standoff is clean.
        var inside = DressingScope.GoalClearanceViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"tree\",\"id\":\"t1\",\"seed\":1,\"x\":28,\"z\":20}]}"), GoalAt20);
        await Assert.That(inside.Count).IsEqualTo(1);

        var past = DressingScope.GoalClearanceViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"tree\",\"id\":\"t1\",\"seed\":1,\"x\":31,\"z\":20}]}"), GoalAt20);
        await Assert.That(past).IsEmpty();
    }

    // ── OB21: a tree, a boulder or a building in a door's approach ────────────────────────────────────
    // A spawn at (0,0) with yaw 0 faces +Z, and with no piece it resolves the legacy marker-anchored room
    // ([-5,5] on both axes) — so the approach lane is that face's width, twenty blocks out: z 6..25.
    private static MapIntent SpawnFacingPosZ => new()
    {
        Spawns = [new SpawnIntent { Team = "red", Point = new Pt(0, 8, 0), Yaw = 0 }],
    };

    [Test]
    public async Task A_tree_in_front_of_the_spawn_door_is_flagged_and_one_behind_is_not()
    {
        var ahead = DressingScope.ApproachViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"tree\",\"id\":\"t1\",\"seed\":1,\"x\":0,\"z\":15}]}"),
            SpawnFacingPosZ);
        await Assert.That(ahead.Count).IsEqualTo(1);
        await Assert.That(ahead[0].Kind).IsEqualTo("tree");

        // Behind, and off-axis enough that the rot_180 mirror stays out of the lane too.
        var behind = DressingScope.ApproachViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"tree\",\"id\":\"t2\",\"seed\":1,\"x\":10,\"z\":-15}]}"),
            SpawnFacingPosZ);
        await Assert.That(behind).IsEmpty();

        var past = DressingScope.ApproachViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"tree\",\"id\":\"t3\",\"seed\":1,\"x\":0,\"z\":30}]}"),
            SpawnFacingPosZ);
        await Assert.That(past).IsEmpty();
    }

    [Test]
    public async Task A_boulder_may_stand_in_the_approach_because_low_cover_keeps_the_sightline()
    {
        // The author's frontage rule permits boulders and fauna in the lane — what it forbids is what cuts
        // the line from the door to the objective, and a rock does not.
        var violations = DressingScope.ApproachViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"boulder\",\"id\":\"b1\",\"seed\":1,\"x\":0,\"z\":15}]}"),
            SpawnFacingPosZ);
        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task A_house_in_front_of_a_wool_rooms_entry_is_flagged_at_the_shorter_reach()
    {
        // A legacy wool at (0,0) resolves the default cage with a door on every wall, so each face carries a
        // ten-block approach — a house at z 8..12 stands in the south one; at z 17+ it is past it.
        var wool = new MapIntent
        {
            Wools = [new WoolIntent { Owner = "blue", Color = "blue", Spawn = new Pt(0, 8, 0) }],
        };

        var blocking = DressingScope.ApproachViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"house\",\"id\":\"h1\",\"seed\":1,\"wings\":[[[-2,8],[2,12]]]}]}"),
            wool);
        await Assert.That(blocking.Count).IsEqualTo(1);
        await Assert.That(blocking[0].Kind).IsEqualTo("building");

        var clear = DressingScope.ApproachViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"house\",\"id\":\"h2\",\"seed\":1,\"wings\":[[[-2,17],[2,21]]]}]}"),
            wool);
        await Assert.That(clear).IsEmpty();
    }

    [Test]
    public async Task Ground_cover_never_reaches_this_check()
    {
        // A flora field is generated, not authored the way a tree/boulder/building is — it is turned away
        // only where it grows TALL, and only by AllowsCover inside Decorator itself (decoration.md §3.1). This
        // refusal never sees it at all, whatever ground it covers.
        var violations = DressingScope.GoalClearanceViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"flora\",\"id\":\"f1\",\"seed\":1,\"points\":[[16,16],[24,16],[24,24],[16,24]]}]}"),
            GoalAt20);

        await Assert.That(violations).IsEmpty();
    }

    // ── the point-and-radius foliage render's source ───────────────────────────────────────────────────
    [Test]
    public async Task Every_tree_reports_its_anchor_and_a_positive_measured_radius()
    {
        var points = DressingScope.TreeFootprints(Layout(
            ",\"dressing\":{\"props\":[{\"kind\":\"tree\",\"id\":\"t1\",\"seed\":1,\"x\":30,\"z\":30,"
            + "\"species\":\"oak\",\"height\":16}]}"));

        // The fixture's setup carries rot_180, so one authored tree fans to two images — the same order every
        // other footprint here reads (GoalClearanceViolations).
        await Assert.That(points.Count).IsEqualTo(2);
        var here = points.Single(point => point.X == 30 && point.Z == 30);

        await Assert.That(here.Radius).IsGreaterThan(0);
        // Both images of the same authored tree carry the same measured radius — the crown is a property of
        // the prop, not of where a particular image happens to land.
        await Assert.That(points.Select(point => point.Radius).Distinct()).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Nothing_reaches_the_clearance_from_either_image_is_clean()
    {
        var violations = DressingScope.GoalClearanceViolations(
            Layout(",\"dressing\":{\"props\":[{\"kind\":\"tree\",\"id\":\"t1\",\"seed\":1,\"x\":0,\"z\":0}]}"), GoalAt20);

        await Assert.That(violations).IsEmpty();
    }

    /// <summary>A flat grass field over <paramref name="span"/> blocks from <paramref name="origin"/>, and the
    /// surface map that goes with it — the ground every mask question below is asked against.</summary>
    private static (VoxelWorld World, Dictionary<(int X, int Z), int> Surface) Ground(int origin, int span)
    {
        var world = new VoxelWorld();
        var surface = new Dictionary<(int X, int Z), int>();
        for (var z = origin; z < origin + span; z++)
        for (var x = origin; x < origin + span; x++)
        {
            world.SetBlock(x, 7, z, Blocks.Grass);
            surface[(x, z)] = 8;
        }
        return (world, surface);
    }
}
