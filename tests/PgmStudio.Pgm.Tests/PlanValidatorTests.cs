using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The two-tier validator. Structural errors (different-surface overlaps, placements outside a piece,
/// unreachable wool, a wool reachable only through a spawn, a wall off any land interface) block a compile;
/// rule lint cites a provisional layout-rule id and never blocks. Narrow seams are legal connecting geometry
/// (no per-seam width lint); a bare corner between separate areas lints PC-C. Each rule is exercised firing and
/// not firing on synthetic fixtures; the three seed plans must be error-free.
/// </summary>
public sealed class PlanValidatorTests
{
    private static PlanModel Plan(string json) => PlanModel.Parse(json)!;
    private static bool Err(PlanModel p, string needle) =>
        PlanValidator.Check(p).Any(f => f.Severity == Severity.Refusal && f.Message.Contains(needle));

    /// <summary>Whether a plan is refused by a named rule. Preferred over <see cref="Err"/> wherever the rule
    /// has an id: a message is prose and may be reworded, and an id is the thing a caller acts on.</summary>
    private static bool Refused(PlanModel p, string rule) =>
        PlanValidator.Check(p).Any(f => f.Refuses && f.Rule == rule);
    private static bool Lint(PlanModel p, string rule) =>
        PlanValidator.Check(p).Any(f => f.Severity == Severity.Complaint && f.Rule == rule);

    // ── errors ──────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_narrow_seam_is_legal_geometry_no_error_no_lint()
    {
        // a thin (< corridor) shared border is walkable terrain — it connects, and it is not linted (PC-S is
        // retired; narrow seams are legal, corridor quality is judged later on the assembled footprint).
        var p = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,9]}, {"id":"b","role":"lane","rect":[10,0,10,10]} ] }
        """);
        await Assert.That(PlanValidator.Check(p).Any(f => f.Severity == Severity.Refusal)).IsFalse();
        await Assert.That(Lint(p, "PC-S")).IsFalse();
    }

    [Test]
    public async Task Corner_contact_is_lint_not_an_error()
    {
        // a bare corner touch is harmless → PC-C lint, no error.
        var p = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"lane","rect":[10,10,10,10]} ] }
        """);
        await Assert.That(Lint(p, "PC-C")).IsTrue();
        await Assert.That(Err(p, "corner")).IsFalse();
    }

    [Test]
    public async Task A_corner_between_already_connected_pieces_is_suppressed()
    {
        // a and b touch only at the point (10,10), but c lands with both (border on x=10 with a, on z=10 with
        // b), so all three are one land component — the corner is harmless → no PC-C.
        var connected = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]},
                     {"id":"b","role":"lane","rect":[10,10,10,10]},
                     {"id":"c","role":"lane","rect":[10,0,10,10]} ] }
        """);
        // the bare corner alone (no connecting land) stays a finding — the sneaky diagonal between separate areas.
        var alone = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]},
                     {"id":"b","role":"lane","rect":[10,10,10,10]} ] }
        """);
        await Assert.That(Lint(connected, "PC-C")).IsFalse();
        await Assert.That(Lint(alone, "PC-C")).IsTrue();
    }

    [Test]
    public async Task Different_surface_overlap_is_an_error()
    {
        var p = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"mid","rect":[5,5,10,10],"surface":13} ] }
        """);
        await Assert.That(Refused(p, PlanRules.SurfaceClash)).IsTrue();
    }

    [Test]
    public async Task Mixed_mirroring_within_one_landmass_is_an_error()
    {
        // the fan copies whole islands, so a landmass half fanned and half not has no coherent orbit image;
        // the compiler throws on it, and the validator must refuse it first so the gate can name the pieces.
        var mixed = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]},
                     {"id":"b","role":"mid","rect":[0,10,10,10],"mirrors":false} ] }
        """);
        var apart = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]},
                     {"id":"b","role":"mid","rect":[0,15,10,10],"mirrors":false} ] }
        """);
        await Assert.That(Refused(mixed, PlanRules.MixedMirrors)).IsTrue();
        await Assert.That(Refused(apart, PlanRules.MixedMirrors)).IsFalse();
    }

    [Test]
    public async Task A_wall_on_the_wool_rooms_own_interface_is_refused_and_one_an_approach_out_is_not()
    {
        // The wall and the room stamp through each other on the wool's own edge, and the room can barely be
        // entered (the author's ruling): the device belongs an approach out. Same geometry, wall moved one
        // interface back — legal.
        var onTheRoom = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"none"},
          "pieces":[ {"id":"wool","role":"wool-room","rect":[0,0,2,2]},
                     {"id":"approach","role":"piece","rect":[2,0,4,2]},
                     {"id":"hub","role":"piece","rect":[6,0,4,2]} ],
          "walls":[ {"a":"wool","b":"approach"} ] }
        """);
        var anApproachOut = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"none"},
          "pieces":[ {"id":"wool","role":"wool-room","rect":[0,0,2,2]},
                     {"id":"approach","role":"piece","rect":[2,0,4,2]},
                     {"id":"hub","role":"piece","rect":[6,0,4,2]} ],
          "walls":[ {"a":"approach","b":"hub"} ] }
        """);

        await Assert.That(Refused(onTheRoom, PlanRules.WallOnWoolRoom)).IsTrue();
        await Assert.That(Refused(anApproachOut, PlanRules.WallOnWoolRoom)).IsFalse();
    }

    [Test]
    public async Task Placement_outside_its_piece_is_an_error()
    {
        var p = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]} ],
          "placements":{ "spawns":[ {"piece":"a","at":[20,0],"facing":"front"} ] } }
        """);
        await Assert.That(Refused(p, PlanRules.PlacementOutside)).IsTrue();
    }

    [Test]
    public async Task Unknown_piece_reference_is_an_error()
    {
        var p = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]} ],
          "placements":{ "wools":[ {"piece":"ghost","at":[0,0]} ] } }
        """);
        await Assert.That(Refused(p, PlanRules.UnknownPiece)).IsTrue();
    }

    [Test]
    public async Task A_wool_colour_that_is_not_a_dye_is_refused()
    {
        // PGM resolves a wool by its dye name; a word outside the sixteen makes the goal unplaceable rather
        // than mis-coloured, and the auto-assignment the compiler would otherwise reach for is what an
        // ABSENT colour asks for, so substituting it would answer a question the plan did not ask.
        var named = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"w","role":"wool-room","rect":[0,0,10,10]} ],
          "placements":{ "wools":[ {"piece":"w","at":[1,1],"color":"chartreuse"} ] } }
        """);
        await Assert.That(Refused(named, PlanRules.UnknownColor)).IsTrue();

        // Both spellings PGM itself accepts pass: it simplifies case and underscores, and reads LIGHT_GRAY as
        // the SILVER the wire carries.
        foreach (var written in new[] { "light_blue", "Light Blue", "light_gray" })
        {
            var spelled = Plan($$"""
            { "plan":2, "globals":{"cell":1},
              "pieces":[ {"id":"w","role":"wool-room","rect":[0,0,10,10]} ],
              "placements":{ "wools":[ {"piece":"w","at":[1,1],"color":"{{written}}"} ] } }
            """);
            await Assert.That(Refused(spelled, PlanRules.UnknownColor)).IsFalse();
        }

        // Saying nothing is the documented way to have one picked, so it is never the refusal.
        var unstated = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"w","role":"wool-room","rect":[0,0,10,10]} ],
          "placements":{ "wools":[ {"piece":"w","at":[1,1]} ] } }
        """);
        await Assert.That(Refused(unstated, PlanRules.UnknownColor)).IsFalse();
    }

    [Test]
    public async Task An_isolated_wool_is_unreachable()
    {
        // spawn island and wool island, no zone to bridge them → the wool can't be reached
        var p = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"s","role":"lane","rect":[1,4,2,2]}, {"id":"w","role":"wool-room","rect":[10,10,2,2]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ], "wools":[ {"piece":"w","at":[5,5]} ] } }
        """);
        await Assert.That(Refused(p, PlanRules.WoolUnreachable)).IsTrue();
    }

    [Test]
    public async Task A_wool_only_reachable_through_a_spawn_is_an_SP1_error()
    {
        // frontline hub → spawn → wool: the wool sits behind the spawn, so no frontline path avoids it
        var p = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"hub","role":"hub","rect":[-1,2,2,2]},
                     {"id":"s","role":"lane","rect":[-1,4,2,2]},
                     {"id":"w","role":"wool-room","rect":[-1,6,2,2]} ],
          "zones":[ {"id":"mid","rect":[-1,-2,2,4]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ], "wools":[ {"piece":"w","at":[5,5]} ] } }
        """);
        await Assert.That(Refused(p, "SP1")).IsTrue();
    }

    /// <summary>A plan declaring no build zone is told that, once, and not that every wool is unreachable.
    /// <c>SP1</c> walks from the frontline and the frontline is the set of pieces a build zone touches, so a
    /// zone-less plan starts the walk from nowhere and refuses every wool on it — a geometry verdict about a
    /// board whose geometry is fine, which is what sends an author redrawing it (<c>B143</c>).</summary>
    [Test]
    public async Task A_plan_with_no_build_zone_is_told_that_rather_than_that_its_wools_are_unreachable()
    {
        // the SP1 plan above with its one `zones` entry taken out: same pieces, same placements, same shape.
        var p = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"hub","role":"hub","rect":[-1,2,2,2]},
                     {"id":"s","role":"lane","rect":[-1,4,2,2]},
                     {"id":"w","role":"wool-room","rect":[-1,6,2,2]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ], "wools":[ {"piece":"w","at":[5,5]} ] } }
        """);

        await Assert.That(Refused(p, "SP1")).IsFalse().Because("no wool is refused for a zone nobody declared");

        var said = PlanValidator.Check(p).Where(f => f.Rule == "SP1").ToList();
        await Assert.That(said.Count).IsEqualTo(1).Because("the missing zone is stated once, not per wool");
        await Assert.That(said[0].Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(said[0].Message).Contains("no build zone");
    }

    [Test]
    public async Task A_wall_on_a_non_interface_pair_is_an_error()
    {
        // a and b abut over a 10-block border (a real land interface) → wall ok; a and c are disjoint → error.
        var ok = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]}, {"id":"b","role":"piece","rect":[10,0,10,10]} ],
          "walls":[ {"a":"a","b":"b"} ] }
        """);
        var bad = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]}, {"id":"c","role":"piece","rect":[40,0,10,10]} ],
          "walls":[ {"a":"a","b":"c"} ] }
        """);
        await Assert.That(Err(ok, "not a shared land interface")).IsFalse();
        await Assert.That(Err(bad, "not a shared land interface")).IsTrue();
    }

    [Test]
    public async Task The_seed_plans_have_no_errors()
    {
        foreach (var name in new[] { "base-2island", "base-2wool", "base-4team" })
        {
            var plan = Plan(PlanTestSupport.ReadSeed($"{name}.plan.json"));
            var errors = PlanValidator.Check(plan).Where(f => f.Severity == Severity.Refusal).ToList();
            await Assert.That(errors).IsEmpty();
        }
    }

    [Test]
    public async Task The_pinwheel_tower_seed_is_error_free_and_its_thin_contacts_now_connect()
    {
        // Zone-union connectivity clears the pinwheel's cross-team reachability, and the narrow-seam model
        // makes its deliberate thin contacts walkable land interfaces — so its previously-linted thin/corner
        // contacts fold into components: no errors, no PC-S (retired), and no PC-C (the corners now sit inside
        // one land component and are suppressed).
        var plan = Plan(PlanTestSupport.ReadSeed("four-team-towers-big.plan.json"));
        var findings = PlanValidator.Check(plan);
        await Assert.That(findings.Any(f => f.Severity == Severity.Refusal)).IsFalse();
        await Assert.That(findings.Any(f => f.Rule == "PC-S")).IsFalse();
        await Assert.That(findings.Any(f => f.Rule == "PC-C")).IsFalse();
    }

    [Test]
    public async Task Findings_carry_the_ids_of_the_pieces_they_implicate()
    {
        // a different-surface overlap (error) between a/b and a narrow zone (G2 lint) each name their subjects
        var p = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"mid","rect":[5,5,10,10],"surface":13} ],
          "zones":[ {"id":"z","rect":[0,20,8,20]} ] }
        """);
        var all = PlanValidator.Check(p);

        var overlap = all.First(f => f.Severity == Severity.Refusal && f.Message.Contains("different surfaces"));
        await Assert.That(overlap.SubjectIds).Contains("a");
        await Assert.That(overlap.SubjectIds).Contains("b");

        var g2 = all.First(f => f.Rule == "G2");
        await Assert.That(g2.SubjectIds).Contains("z");
    }

    // ── annotation pieces (buffers) ───────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_buffer_overlapping_different_surface_terrain_raises_no_overlap_error()
    {
        // the buffer overlaps both 'a' (surface 9) and 'b' (surface 13); if it were terrain those overlaps would
        // be different-surface errors. As a non-generating annotation it is absent from d.Contacts → no error.
        var p = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]},
                     {"id":"b","role":"piece","rect":[0,20,10,10],"surface":13},
                     {"id":"buffer","role":"buffer","rect":[0,0,10,30]} ] }
        """);
        await Assert.That(Refused(p, PlanRules.SurfaceClash)).IsFalse();
        await Assert.That(PlanValidator.Check(p).Any(f => f.Severity == Severity.Refusal)).IsFalse();
    }

    /// <summary><c>EL1</c> is asked of a seam, not of a piece: two pieces meeting at a step of two or more
    /// leave ground nobody walks up bare, and the finding is the list of seams the relief has to grade. A
    /// buffer produces no terrain, so a seam against one is not a seam.</summary>
    [Test]
    public async Task EL1_names_a_seam_a_player_cannot_walk_up_and_leaves_a_buffer_alone()
    {
        var step = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"low","role":"piece","rect":[0,0,10,10],"surface":9},
                     {"id":"high","role":"piece","rect":[10,0,10,10],"surface":12} ] }
        """);
        var walkable = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"low","role":"piece","rect":[0,0,10,10],"surface":9},
                     {"id":"high","role":"piece","rect":[10,0,10,10],"surface":10} ] }
        """);
        var buffer = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"low","role":"piece","rect":[0,0,10,10],"surface":9},
                     {"id":"buffer","role":"buffer","rect":[10,0,10,10],"surface":12} ] }
        """);

        await Assert.That(Lint(step, "EL1")).IsTrue().Because("a three-block step is not walked up");
        await Assert.That(Lint(walkable, "EL1")).IsFalse().Because("one block is a walk");
        await Assert.That(Lint(buffer, "EL1")).IsFalse().Because("a buffer is not terrain to step onto");
    }

    /// <summary>The seam's finding states the ramp that grades it: a line mark on the group's relief, from a
    /// point inside the higher piece to a point inside the lower, at the two surfaces. The pieces meet on
    /// x = 10; the high one lies east of it, so the mark runs west to east and falls from 12 to 9.</summary>
    [Test]
    public async Task EL1_states_the_ramp_mark_that_grades_its_seam()
    {
        var step = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"low","role":"piece","rect":[0,0,10,10],"surface":9},
                     {"id":"high","role":"piece","rect":[10,0,10,10],"surface":12} ] }
        """);
        var finding = PlanValidator.Check(step).Single(f => f.Rule == "EL1");
        var edit = finding.Edit;
        await Assert.That(edit).IsNotNull();
        await Assert.That(edit!.Document).IsEqualTo("layout");
        await Assert.That(edit.Path).IsEqualTo("relief.team.marks");
        await Assert.That(edit.Op).IsEqualTo("add");
        var value = edit.Value;
        await Assert.That(value.GetProperty("kind").GetString()).IsEqualTo("line");
        var heights = value.GetProperty("h").EnumerateArray().Select(h => h.GetInt32()).ToArray();
        await Assert.That(heights).IsEquivalentTo(new[] { 12, 9 });
        var points = value.GetProperty("points").EnumerateArray()
            .Select(p => p.EnumerateArray().Select(v => v.GetDouble()).ToArray()).ToArray();
        await Assert.That(points[0][0]).IsGreaterThan(10).Because("the high end stands inside the high piece, east of the seam");
        await Assert.That(points[1][0]).IsLessThan(10).Because("the low end stands inside the low piece, west of it");
        await Assert.That(points[0][0] - points[1][0]).IsEqualTo(8).Because("a step of 3 grades over 2 × (3 + 1) blocks");
    }

    /// <summary>A piece off the old odd-surface palette is not itself the fault. What the plan states is a
    /// height per rectangle; what a player meets is the step between two of them, and a lone piece has no
    /// step. The rule used to test the piece's own delta from the global surface, which coincided with the
    /// palette only while that global was odd (<c>G231</c>).</summary>
    [Test]
    public async Task EL1_says_nothing_about_a_lone_piece_however_its_surface_sits()
    {
        var lone = Plan("""{ "plan":2, "globals":{"cell":1,"surface":9}, "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10],"surface":12} ] }""");
        await Assert.That(Lint(lone, "EL1")).IsFalse();
    }

    /// <summary>A flight of one-block treads is authored ground rather than a fault: every seam in it is a
    /// step a player walks. The plan may state a staircase, or state a drop and leave the stair to a sketch
    /// shape with anchor heights — both are the model working.</summary>
    [Test]
    public async Task EL1_is_silent_on_a_flight_of_one_block_treads()
    {
        var flight = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"field","role":"piece","rect":[0,0,20,10]},
                     {"id":"tread-1","role":"piece","rect":[0,10,20,2],"surface":10},
                     {"id":"tread-2","role":"piece","rect":[0,12,20,2],"surface":11},
                     {"id":"tread-3","role":"piece","rect":[0,14,20,2],"surface":12},
                     {"id":"tread-4","role":"piece","rect":[0,16,20,2],"surface":13} ] }
        """);
        await Assert.That(Lint(flight, "EL1")).IsFalse()
            .Because("every seam in the flight steps one block, which is a walk");
    }

    [Test]
    public async Task A_spawn_or_wool_placed_on_a_buffer_is_an_error()
    {
        // nothing may be placed on a buffer — it produces no ground for a marker to sit on.
        var spawn = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"buffer","role":"buffer","rect":[0,0,10,10]} ],
          "placements":{ "spawns":[ {"piece":"buffer","at":[5,5],"facing":"front"} ] } }
        """);
        var wool = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"buffer","role":"buffer","rect":[0,0,10,10]} ],
          "placements":{ "wools":[ {"piece":"buffer","at":[5,5]} ] } }
        """);
        await Assert.That(Err(spawn, "non-generating buffer")).IsTrue();
        await Assert.That(Err(wool, "non-generating buffer")).IsTrue();
    }

    // ── lint ────────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task G2_fires_on_a_narrow_zone_and_not_on_a_wide_one()
    {
        var narrow = Plan("""{ "plan":2, "globals":{"cell":1}, "zones":[ {"id":"z","rect":[0,0,8,20]} ] }""");
        var wide = Plan("""{ "plan":2, "globals":{"cell":1}, "zones":[ {"id":"z","rect":[0,0,10,20]} ] }""");
        await Assert.That(Lint(narrow, "G2")).IsTrue();
        await Assert.That(Lint(wide, "G2")).IsFalse();
    }

    [Test]
    public async Task G5_fires_on_a_long_hop_and_not_on_an_in_range_one()
    {
        var far = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"lane","rect":[40,0,10,10]} ],
          "zones":[ {"id":"z","rect":[0,0,50,10]} ] }
        """);
        var ok = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"lane","rect":[25,0,10,10]} ],
          "zones":[ {"id":"z","rect":[0,0,35,10]} ] }
        """);
        await Assert.That(Lint(far, "G5")).IsTrue();
        await Assert.That(Lint(ok, "G5")).IsFalse();
    }

    [Test]
    public async Task SP2_fires_when_the_spawn_is_in_the_front_half()
    {
        var front = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"lane","role":"lane","rect":[0,50,10,40]} ],
          "placements":{ "spawns":[ {"piece":"lane","at":[5,5],"facing":"front"} ] } }
        """);
        var back = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"lane","role":"lane","rect":[0,50,10,40]} ],
          "placements":{ "spawns":[ {"piece":"lane","at":[5,35],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(front, "SP2")).IsTrue();
        await Assert.That(Lint(back, "SP2")).IsFalse();
    }

    [Test]
    public async Task BZ5_fires_when_a_zone_touches_a_spawn_piece()
    {
        var touching = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"s","role":"lane","rect":[0,10,10,10]} ],
          "zones":[ {"id":"z","rect":[0,0,10,10]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ] } }
        """);
        var clear = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"s","role":"lane","rect":[0,30,10,10]} ],
          "zones":[ {"id":"z","rect":[0,0,10,10]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(touching, "BZ5")).IsTrue();
        await Assert.That(Lint(clear, "BZ5")).IsFalse();
    }


    [Test]
    public async Task ST2_fires_on_a_cube_that_does_not_stand_inside_a_spawn_piece()
    {
        // a spawn-role piece exists; iron on a separate (non-spawn) piece → ST2 fires.
        var outside = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"sp","role":"spawn","rect":[0,0,10,10]}, {"id":"ln","role":"piece","rect":[0,20,10,10]} ],
          "placements":{ "iron":[ {"piece":"ln","at":[5,5]} ] } }
        """);
        // iron sits inside the spawn piece → no ST2.
        var inside = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"sp","role":"spawn","rect":[0,0,10,10]}, {"id":"ln","role":"piece","rect":[0,20,10,10]} ],
          "placements":{ "iron":[ {"piece":"sp","at":[5,5]} ] } }
        """);
        // No spawn-role piece at all: the cube is still one a team mines once, which is what ST2 says.
        var noSpawnRole = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"ln","role":"piece","rect":[0,20,10,10]} ],
          "placements":{ "iron":[ {"piece":"ln","at":[5,5]} ] } }
        """);
        // The whole cube must be inside, not the marker it centres on: a marker one block inside the spawn
        // piece's edge centres a cube reaching past it, so half of it would never renew.
        var halfIn = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"sp","role":"spawn","rect":[0,0,10,10]} ],
          "placements":{ "iron":[ {"piece":"sp","at":[1,5]} ] } }
        """);
        await Assert.That(Lint(outside, "ST2")).IsTrue();
        await Assert.That(Lint(inside, "ST2")).IsFalse();
        await Assert.That(Lint(noSpawnRole, "ST2")).IsTrue();
        await Assert.That(Lint(halfIn, "ST2")).IsTrue();
    }

    // ── completeness (the compile gate, not the continuous validator) ────────────────────────────────────

    private static bool Missing(PlanModel p, string needle) =>
        PlanValidator.Completeness(p).Any(f => f.Severity == Severity.Refusal && f.Message.Contains(needle));

    [Test]
    public async Task An_empty_plan_has_no_land_to_build()
    {
        var p = Plan("""{ "plan":2, "globals":{"cell":1} }""");
        await Assert.That(Missing(p, "no pieces")).IsTrue();
    }

    [Test]
    public async Task An_annotation_only_plan_still_has_no_land_to_build()
    {
        // buffers and other non-generating roles produce no terrain, so a plan of nothing but them is as empty
        // as a blank document.
        var p = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"buffer","role":"buffer","rect":[0,0,10,10]} ] }
        """);
        await Assert.That(Missing(p, "no pieces")).IsTrue();
    }

    [Test]
    public async Task A_plan_with_land_but_no_spawn_cannot_be_loaded()
    {
        var p = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]} ] }
        """);
        await Assert.That(Missing(p, "no spawn")).IsTrue();
    }

    [Test]
    public async Task A_blank_plan_reports_only_the_missing_land()
    {
        // the spawn and objective complaints are consequences of there being no plan at all — saying all three
        // buries the one fact the author needs.
        var p = Plan("""{ "plan":2, "globals":{"cell":1} }""");
        await Assert.That(PlanValidator.Completeness(p).Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_missing_objective_is_a_complaint_not_a_block()
    {
        var p = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]} ],
          "placements":{"spawns":[{"piece":"a","at":[1,1]}]} }
        """);
        var findings = PlanValidator.Completeness(p);
        await Assert.That(findings.Any(f => f.Severity == Severity.Refusal)).IsFalse();
        await Assert.That(findings.Any(f => f.Severity == Severity.Complaint && f.Message.Contains("no objective"))).IsTrue();
    }

    [Test]
    [Arguments("wools")]
    [Arguments("destroyables")]
    [Arguments("cores")]
    public async Task Any_one_objective_kind_silences_the_complaint(string kind)
    {
        var p = Plan($$"""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]} ],
          "placements":{"spawns":[{"piece":"a","at":[1,1]}],"{{kind}}":[{"piece":"a","at":[5,5]}]} }
        """);
        await Assert.That(PlanValidator.Completeness(p).Any(f => f.Message.Contains("no objective"))).IsFalse();
    }

    [Test]
    public async Task Completeness_is_not_part_of_the_continuous_validator()
    {
        // Validate() runs on every candidate the composer scores and on every keystroke in the editor, where a
        // half-built plan is normal — so an incomplete plan must not read as a structural error there.
        var p = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]} ] }
        """);
        await Assert.That(PlanValidator.Check(p).Any(f => f.Severity == Severity.Refusal)).IsFalse();
    }

    // ── the piece-interface lints (SP8/SP9/ST8/ST9/BZ11/FR8/CT12) ───────────────────────────────────────

    private static int LintCount(PlanModel p, string rule) =>
        PlanValidator.Check(p).Count(f => f.Severity == Severity.Complaint && f.Rule == rule);

    [Test]
    public async Task A_wool_room_approach_stepping_two_fires_WL11_on_every_entry()
    {
        // A room has no facing, so every land seam an attacker can arrive across is a door and all of them
        // are measured — which is the one way this differs from SP8's forward-only read.
        var p = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"room","role":"wool-room","rect":[0,10,10,10],"surface":11},
                     {"id":"west","role":"lane","rect":[0,0,10,10],"surface":9},
                     {"id":"east","role":"lane","rect":[0,20,10,10],"surface":9} ],
          "placements":{ "wools":[ {"piece":"room","at":[5,5]} ] } }
        """);
        await Assert.That(LintCount(p, "WL11")).IsEqualTo(2);

        var level = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"room","role":"wool-room","rect":[0,10,10,10],"surface":11},
                     {"id":"west","role":"lane","rect":[0,0,10,10],"surface":11},
                     {"id":"east","role":"lane","rect":[0,20,10,10],"surface":10} ],
          "placements":{ "wools":[ {"piece":"room","at":[5,5]} ] } }
        """);
        await Assert.That(LintCount(level, "WL11")).IsEqualTo(0).Because("a single level is walked");
    }

    [Test]
    public async Task WL11_names_which_way_the_attacker_meets_the_step()
    {
        // A wall to build up and a drop with no way back out are different problems on the same seam, and an
        // attacker meets one of them at the end of the run that decides the map.
        var wall = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"room","role":"wool-room","rect":[0,10,10,10],"surface":9},
                     {"id":"approach","role":"lane","rect":[0,0,10,10],"surface":14} ],
          "placements":{ "wools":[ {"piece":"room","at":[5,5]} ] } }
        """);
        var finding = PlanValidator.Check(wall).Single(f => f.Rule == "WL11");
        await Assert.That(finding.Message).Contains("drops 5 blocks");
        await Assert.That(finding.Severity).IsEqualTo(Severity.Complaint);

        var pit = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"room","role":"wool-room","rect":[0,10,10,10],"surface":14},
                     {"id":"approach","role":"lane","rect":[0,0,10,10],"surface":9} ],
          "placements":{ "wools":[ {"piece":"room","at":[5,5]} ] } }
        """);
        await Assert.That(PlanValidator.Check(pit).Single(f => f.Rule == "WL11").Message)
            .Contains("climbs 5 blocks");
    }

    [Test]
    public async Task A_wool_on_a_plain_piece_is_not_a_room_and_states_no_approach()
    {
        // WL11 is about the seam a cage's door is cut on. A wool marker on ordinary ground has no room and
        // no entries, so there is nothing for the rule to be about.
        var p = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"lane","role":"lane","rect":[0,10,10,10],"surface":11},
                     {"id":"west","role":"lane","rect":[0,0,10,10],"surface":9} ],
          "placements":{ "wools":[ {"piece":"lane","at":[5,5]} ] } }
        """);
        await Assert.That(LintCount(p, "WL11")).IsEqualTo(0);
    }

    [Test]
    public async Task A_spawn_egress_stepping_two_fires_SP8_forward_only()
    {
        // the seam ahead of the door steps 2 (un-walkable bare) → SP8; the identical step behind the spawn
        // is a legitimate back wall and is not the egress
        var p = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"s","role":"spawn","rect":[0,10,10,10],"surface":11},
                     {"id":"ahead","role":"lane","rect":[0,0,10,10],"surface":9},
                     {"id":"behind","role":"lane","rect":[0,20,10,10],"surface":9} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ] } }
        """);
        await Assert.That(LintCount(p, "SP8")).IsEqualTo(1);

        var flat = Plan("""
        { "plan":2, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"s","role":"spawn","rect":[0,10,10,10],"surface":11},
                     {"id":"ahead","role":"lane","rect":[0,0,10,10],"surface":11} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ] } }
        """);
        await Assert.That(LintCount(flat, "SP8")).IsEqualTo(0);
    }

    [Test]
    public async Task A_door_onto_near_void_fires_SP9_and_a_bridgeable_zone_counts_as_ground()
    {
        // five blocks of apron then nothing → SP9; the same doorstep opening onto a build zone is the
        // gap-only spawn's egress bridge and stands
        var shortApron = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"s","role":"spawn","rect":[0,10,10,10]},
                     {"id":"apron","role":"lane","rect":[0,5,10,5]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(shortApron, "SP9")).IsTrue();

        var bridged = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"s","role":"spawn","rect":[0,10,10,10]} ],
          "zones":[ {"id":"egress","rect":[0,-5,10,15]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(bridged, "SP9")).IsFalse();
    }

    [Test]
    public async Task A_wall_too_close_to_the_entrance_or_over_a_wide_interface_fires_ST8()
    {
        // the wall seat 15 out from the room's entry, over a 10-block mouth — the author's geometry, clean
        var seated = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"w","role":"wool-room","rect":[0,0,10,10]},
                     {"id":"a","role":"lane","rect":[0,10,10,15]},
                     {"id":"h","role":"lane","rect":[0,25,10,10]} ],
          "walls":[ {"a":"a","b":"h"} ] }
        """);
        await Assert.That(Lint(seated, "ST8")).IsFalse();

        // the same wall with a four-deep approach stands 4 from the entrance → too close
        var close = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"w","role":"wool-room","rect":[0,0,10,10]},
                     {"id":"a","role":"lane","rect":[0,10,10,4]},
                     {"id":"h","role":"lane","rect":[0,14,10,10]} ],
          "walls":[ {"a":"a","b":"h"} ] }
        """);
        await Assert.That(Lint(close, "ST8")).IsTrue();

        // a 30-block interface is a room face, not a lane mouth
        var wide = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,10,30,15]},
                     {"id":"h","role":"lane","rect":[0,25,30,10]} ],
          "walls":[ {"a":"a","b":"h"} ] }
        """);
        await Assert.That(Lint(wide, "ST8")).IsTrue();
    }

    [Test]
    public async Task A_building_over_the_cap_fires_ST9_and_the_region_it_stands_on_does_not()
    {
        // ST9 measures the footprint, so a region wide enough to hold an over-cap default building fires it,
        // and stating a smaller building on the same region clears it.
        var sprawling = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"s","role":"spawn","rect":[0,0,30,24]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[15,12],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(sprawling, "ST9")).IsTrue();

        var stated = Plan("""
        { "plan":2, "globals":{"cell":1},
          "pieces":[ {"id":"s","role":"spawn","rect":[0,0,30,24]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[15,12],"facing":"front","footprint":[5,2,20,20]} ] } }
        """);
        await Assert.That(Lint(stated, "ST9")).IsFalse();
    }

    [Test]
    public async Task A_region_over_the_cap_fires_ST10_in_either_orientation()
    {
        foreach (var rect in new[] { "[0,0,20,40]", "[0,0,40,20]", "[0,0,26,26]" })
        {
            var oversized = Plan($$"""
            { "plan":2, "globals":{"cell":1},
              "pieces":[ {"id":"w","role":"wool-room","rect":{{rect}}} ] }
            """);
            await Assert.That(Lint(oversized, "ST10")).IsTrue();
        }

        // 20 across and 30 along is the cap itself, either way round.
        foreach (var rect in new[] { "[0,0,20,30]", "[0,0,30,20]" })
        {
            var atCap = Plan($$"""
            { "plan":2, "globals":{"cell":1},
              "pieces":[ {"id":"w","role":"wool-room","rect":{{rect}}} ] }
            """);
            await Assert.That(Lint(atCap, "ST10")).IsFalse();
        }
    }

    [Test]
    public async Task Zones_tiling_a_rectangle_fire_BZ11_and_an_L_decomposition_does_not()
    {
        // two zones whose union is a plain rectangle: one zone would have drawn it → stitched
        var stitched = Plan("""
        { "plan":2, "globals":{"cell":1},
          "zones":[ {"id":"za","rect":[0,0,10,5]}, {"id":"zb","rect":[0,5,10,5]} ] }
        """);
        await Assert.That(Lint(stitched, "BZ11")).IsTrue();

        // an L-shaped region needs two rectangles — that is decomposition, not stitching
        var elbow = Plan("""
        { "plan":2, "globals":{"cell":1},
          "zones":[ {"id":"za","rect":[0,0,10,5]}, {"id":"zb","rect":[0,5,5,5]} ] }
        """);
        await Assert.That(Lint(elbow, "BZ11")).IsFalse();
    }

    [Test]
    public async Task A_zone_funnelling_through_a_slice_of_a_wide_face_fires_FR8()
    {
        // an 80-block front face where the zone and its fanned twin dock 20: share 0.25, the measured fault
        // (sunspit's foreshore read exactly this). The spawn anchors the island to its team — an un-anchored
        // crossing reads as a team's own internal bridge, not a front.
        var funnel = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"front","role":"lane","rect":[-8,-4,16,3]} ],
          "zones":[ {"id":"dock","rect":[-8,-1,2,2]} ],
          "placements":{ "spawns":[ {"piece":"front","at":[40,5],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(funnel, "FR8")).IsTrue();

        // the same face with the zone spanning it: share 1.00 → the fit every authored board shows
        var spanning = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"front","role":"lane","rect":[-8,-4,16,3]} ],
          "zones":[ {"id":"dock","rect":[-8,-1,16,2]} ],
          "placements":{ "spawns":[ {"piece":"front","at":[40,5],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(spanning, "FR8")).IsFalse();
    }

    /// <summary><b>FR9 is the width FR8's share cannot see.</b> A narrow crossing on a narrow face takes the
    /// whole of it — share 1.00, which is the fit every authored board shows — and is still a funnel: how
    /// wide a front is in blocks is a different question from how much of its face it takes, and players
    /// read the blocks. Fifteen is the author's number.</summary>
    [Test]
    public async Task A_crossing_narrower_than_the_floor_fires_FR9_however_much_of_its_face_it_takes()
    {
        // a 10-block face docked across its whole width: FR8 reads 1.00 and says nothing.
        var narrow = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"front","role":"lane","rect":[-1,-4,2,3]} ],
          "zones":[ {"id":"dock","rect":[-1,-1,2,2]} ],
          "placements":{ "spawns":[ {"piece":"front","at":[5,5],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(narrow, "FR8")).IsFalse().Because("the crossing spans its whole face");
        await Assert.That(Lint(narrow, "FR9")).IsTrue().Because("ten blocks is under the fifteen a crossing wants");

        // the same board with a face wide enough to cross: both quiet.
        var wide = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"front","role":"lane","rect":[-2,-4,4,3]} ],
          "zones":[ {"id":"dock","rect":[-2,-1,4,2]} ],
          "placements":{ "spawns":[ {"piece":"front","at":[10,5],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(wide, "FR9")).IsFalse();
    }

    [Test]
    public async Task Team_islands_bridged_across_a_narrow_strait_fire_CT12()
    {
        // rot_180 fans the authored team island opposite itself: a 10-block strait under one zone → too close
        var narrow = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"home","role":"spawn","rect":[-2,-3,4,2]} ],
          "zones":[ {"id":"strait","rect":[-2,-1,4,2]} ],
          "placements":{ "spawns":[ {"piece":"home","at":[10,5],"facing":"front"} ],
                         "wools":[ {"piece":"home","at":[5,5]} ] } }
        """);
        await Assert.That(Lint(narrow, "CT12")).IsTrue();

        // the same board pushed out to a 30-block strait sits inside the band
        var banded = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"home","role":"spawn","rect":[-2,-5,4,2]} ],
          "zones":[ {"id":"strait","rect":[-2,-3,4,6]} ],
          "placements":{ "spawns":[ {"piece":"home","at":[10,5],"facing":"front"} ],
                         "wools":[ {"piece":"home","at":[5,5]} ] } }
        """);
        await Assert.That(Lint(banded, "CT12")).IsFalse();
    }

    [Test]
    public async Task Every_seed_plan_is_a_complete_map_plan()
    {
        // Every seed is a plan that could be finished into a map, including the three that were once pure
        // geometry studies — they carry their spawn and wool markers now, so the corpus no longer holds an
        // example of the thing the gate refuses.
        foreach (var path in Directory.EnumerateFiles(PlanTestSupport.SeedDir(), "*.plan.json"))
        {
            var plan = PlanModel.Parse(File.ReadAllText(path))!;
            var errors = PlanValidator.Completeness(plan).Where(f => f.Severity == Severity.Refusal).ToList();
            await Assert.That(errors).IsEmpty();
        }
    }

    // ── BZ9: a zone reaching past the ground it docks (the author's own five boards) ───────────────────

    /// <summary>The author's shifted <c>rot_180</c> board: one 60×30-block piece and its image, and a zone
    /// across the middle. Every case below is that board with a different zone, which is what makes the five
    /// comparable — the fault is the zone's reach, not the pieces.</summary>
    private static PlanModel Shifted(string pieces, string zone) => Plan($$"""
    { "plan":2, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
      "pieces":[ {"id":"piece","role":"piece","rect":[-9,-10,12,6]},
                 {"id":"spawn","role":"spawn","rect":[-9,-10,2,2]}{{pieces}} ],
      "zones":[ {"id":"zone","rect":{{zone}}} ],
      "placements":{"spawns":[{"team":0,"piece":"spawn","at":[5,5]}]} }
    """);

    [Test]
    public async Task A_zone_docking_part_of_a_shifted_face_is_not_an_overhang()
    {
        // The crossing joins the two team islands over the x they share and no more. Covering only part of
        // each face is what a shifted board's crossing does, and BZ9 does not ask for more.
        await Assert.That(Lint(Shifted("", "[-3,-4,6,8]"), "BZ9")).IsFalse();
    }

    [Test]
    public async Task A_zone_wider_than_either_face_stands_where_it_docks_both_images()
    {
        // The other way to take the same board: span the full width of both faces, which means running wider
        // than either one to reach the mirrored corners. Every column of it has ground on one side.
        await Assert.That(Lint(Shifted("", "[-9,-4,18,8]"), "BZ9")).IsFalse();
        await Assert.That(Lint(Shifted(",{\"id\":\"piece-2\",\"role\":\"piece\",\"rect\":[5,-4,4,4]}",
                                       "[-9,-4,18,8]"), "BZ9")).IsFalse()
            .Because("corner islands framing the crossing dock it further, they do not make it a fault");
    }

    [Test]
    public async Task A_zone_reaching_past_the_last_ground_it_docks_is_an_overhang()
    {
        // The same 90-block zone, but the only thing it docks is a 30-block face and its image in the middle
        // of it: 60 blocks of the zone stand beyond them, connecting nothing.
        var leaking = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[ {"id":"piece","role":"piece","rect":[-9,-12,12,6]},
                     {"id":"spawn","role":"spawn","rect":[-9,-12,2,2]},
                     {"id":"piece-2","role":"piece","rect":[-3,-6,6,2]} ],
          "zones":[ {"id":"zone","rect":[-9,-4,18,8]} ],
          "placements":{"spawns":[{"team":0,"piece":"spawn","at":[5,5]}]} }
        """);

        var finding = PlanValidator.Check(leaking).Single(f => f.Rule == "BZ9");
        await Assert.That(finding.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(finding.Message).Contains("60 blocks");
        await Assert.That(finding.Message).Contains("x -45..-15 and 15..45");
        await Assert.That(finding.SubjectIds).Contains("zone");
    }

    [Test]
    public async Task The_span_between_two_docked_ends_is_the_crossing_and_not_an_overhang()
    {
        // A zone docked left and right with a strip in the middle touching nothing: that strip is the gap the
        // crossing exists to carry. Measured across the wrong axis it reads as a fault, which is why the
        // measure is taken across the axis the zone's own contacts lie on.
        var crossing = Plan("""
        { "plan":2, "globals":{"cell":5,"symmetry":"none","surface":9},
          "pieces":[ {"id":"west","role":"piece","rect":[-12,0,6,8]},
                     {"id":"east","role":"piece","rect":[6,0,6,8]},
                     {"id":"spawn","role":"spawn","rect":[-12,0,2,2]} ],
          "zones":[ {"id":"zone","rect":[-6,0,12,8]} ],
          "placements":{"spawns":[{"team":0,"piece":"spawn","at":[5,5]}]} }
        """);
        await Assert.That(Lint(crossing, "BZ9")).IsFalse();
    }
}
