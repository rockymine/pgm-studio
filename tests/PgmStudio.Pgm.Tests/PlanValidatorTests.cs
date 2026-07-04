using PgmStudio.Pgm.Plan;

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
        PlanValidator.Validate(p).Any(f => f.Severity == PlanSeverity.Error && f.Message.Contains(needle));
    private static bool Lint(PlanModel p, string rule) =>
        PlanValidator.Validate(p).Any(f => f.Severity == PlanSeverity.Lint && f.Rule == rule);

    // ── errors ──────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_narrow_seam_is_legal_geometry_no_error_no_lint()
    {
        // a thin (< corridor) shared border is walkable terrain — it connects, and it is not linted (PC-S is
        // retired; narrow seams are legal, corridor quality is judged later on the assembled footprint).
        var p = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,9]}, {"id":"b","role":"lane","rect":[10,0,10,10]} ] }
        """);
        await Assert.That(PlanValidator.Validate(p).Any(f => f.Severity == PlanSeverity.Error)).IsFalse();
        await Assert.That(Lint(p, "PC-S")).IsFalse();
    }

    [Test]
    public async Task Corner_contact_is_lint_not_an_error()
    {
        // a bare corner touch is harmless → PC-C lint, no error.
        var p = Plan("""
        { "plan":1, "globals":{"cell":1},
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
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]},
                     {"id":"b","role":"lane","rect":[10,10,10,10]},
                     {"id":"c","role":"lane","rect":[10,0,10,10]} ] }
        """);
        // the bare corner alone (no connecting land) stays a finding — the sneaky diagonal between separate areas.
        var alone = Plan("""
        { "plan":1, "globals":{"cell":1},
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
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"mid","rect":[5,5,10,10],"surface":13} ] }
        """);
        await Assert.That(Err(p, "different surfaces")).IsTrue();
    }

    [Test]
    public async Task Placement_outside_its_piece_is_an_error()
    {
        var p = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]} ],
          "placements":{ "spawns":[ {"piece":"a","at":[20,0],"facing":"front"} ] } }
        """);
        await Assert.That(Err(p, "outside piece")).IsTrue();
    }

    [Test]
    public async Task Unknown_piece_reference_is_an_error()
    {
        var p = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]} ],
          "placements":{ "wools":[ {"piece":"ghost","at":[0,0]} ] } }
        """);
        await Assert.That(Err(p, "unknown piece")).IsTrue();
    }

    [Test]
    public async Task An_isolated_wool_is_unreachable()
    {
        // spawn island and wool island, no zone to bridge them → the wool can't be reached
        var p = Plan("""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"s","role":"lane","rect":[1,4,2,2]}, {"id":"w","role":"wool-room","rect":[10,10,2,2]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[1,1],"facing":"front"} ], "wools":[ {"piece":"w","at":[1,1]} ] } }
        """);
        await Assert.That(Err(p, "unreachable")).IsTrue();
    }

    [Test]
    public async Task A_wool_only_reachable_through_a_spawn_is_an_SP1_error()
    {
        // frontline hub → spawn → wool: the wool sits behind the spawn, so no frontline path avoids it
        var p = Plan("""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180"},
          "pieces":[ {"id":"hub","role":"hub","rect":[-1,2,2,2]},
                     {"id":"s","role":"lane","rect":[-1,4,2,2]},
                     {"id":"w","role":"wool-room","rect":[-1,6,2,2]} ],
          "zones":[ {"id":"mid","rect":[-1,-2,2,4]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[1,1],"facing":"front"} ], "wools":[ {"piece":"w","at":[1,1]} ] } }
        """);
        await Assert.That(Err(p, "SP1")).IsTrue();
    }

    [Test]
    public async Task A_wall_on_a_non_interface_pair_is_an_error()
    {
        // a and b abut over a 10-block border (a real land interface) → wall ok; a and c are disjoint → error.
        var ok = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]}, {"id":"b","role":"piece","rect":[10,0,10,10]} ],
          "walls":[ {"a":"a","b":"b"} ] }
        """);
        var bad = Plan("""
        { "plan":1, "globals":{"cell":1},
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
            var errors = PlanValidator.Validate(plan).Where(f => f.Severity == PlanSeverity.Error).ToList();
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
        var findings = PlanValidator.Validate(plan);
        await Assert.That(findings.Any(f => f.Severity == PlanSeverity.Error)).IsFalse();
        await Assert.That(findings.Any(f => f.Rule == "PC-S")).IsFalse();
        await Assert.That(findings.Any(f => f.Rule == "PC-C")).IsFalse();
    }

    [Test]
    public async Task Findings_carry_the_ids_of_the_pieces_they_implicate()
    {
        // a different-surface overlap (error) between a/b and a narrow zone (G2 lint) each name their subjects
        var p = Plan("""
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"mid","rect":[5,5,10,10],"surface":13} ],
          "zones":[ {"id":"z","rect":[0,20,8,20]} ] }
        """);
        var all = PlanValidator.Validate(p);

        var overlap = all.First(f => f.Severity == PlanSeverity.Error && f.Message.Contains("different surfaces"));
        await Assert.That(overlap.SubjectIds).Contains("a");
        await Assert.That(overlap.SubjectIds).Contains("b");

        var g2 = all.First(f => f.Rule == "G2");
        await Assert.That(g2.SubjectIds).Contains("z");
    }

    // ── lint ────────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task G2_fires_on_a_narrow_zone_and_not_on_a_wide_one()
    {
        var narrow = Plan("""{ "plan":1, "globals":{"cell":1}, "zones":[ {"id":"z","rect":[0,0,8,20]} ] }""");
        var wide = Plan("""{ "plan":1, "globals":{"cell":1}, "zones":[ {"id":"z","rect":[0,0,10,20]} ] }""");
        await Assert.That(Lint(narrow, "G2")).IsTrue();
        await Assert.That(Lint(wide, "G2")).IsFalse();
    }

    [Test]
    public async Task G5_fires_on_a_long_hop_and_not_on_an_in_range_one()
    {
        var far = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"lane","rect":[40,0,10,10]} ],
          "zones":[ {"id":"z","rect":[0,0,50,10]} ] }
        """);
        var ok = Plan("""
        { "plan":1, "globals":{"cell":1},
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
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"lane","role":"lane","rect":[0,50,10,40]} ],
          "placements":{ "spawns":[ {"piece":"lane","at":[5,5],"facing":"front"} ] } }
        """);
        var back = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"lane","role":"lane","rect":[0,50,10,40]} ],
          "placements":{ "spawns":[ {"piece":"lane","at":[5,35],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(front, "SP2")).IsTrue();
        await Assert.That(Lint(back, "SP2")).IsFalse();
    }

    [Test]
    public async Task WL2_fires_when_the_wool_is_too_close_to_the_spawn()
    {
        var near = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"s","role":"lane","rect":[0,0,10,10]}, {"id":"w","role":"wool-room","rect":[0,10,10,10]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ], "wools":[ {"piece":"w","at":[5,5]} ] } }
        """);
        var far = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"s","role":"lane","rect":[0,0,10,10]}, {"id":"w","role":"wool-room","rect":[0,40,10,10]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ], "wools":[ {"piece":"w","at":[5,5]} ] } }
        """);
        await Assert.That(Lint(near, "WL2")).IsTrue();
        await Assert.That(Lint(far, "WL2")).IsFalse();
    }

    [Test]
    public async Task BZ5_fires_when_a_zone_touches_a_spawn_piece()
    {
        var touching = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"s","role":"lane","rect":[0,10,10,10]} ],
          "zones":[ {"id":"z","rect":[0,0,10,10]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ] } }
        """);
        var clear = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"s","role":"lane","rect":[0,30,10,10]} ],
          "zones":[ {"id":"z","rect":[0,0,10,10]} ],
          "placements":{ "spawns":[ {"piece":"s","at":[5,5],"facing":"front"} ] } }
        """);
        await Assert.That(Lint(touching, "BZ5")).IsTrue();
        await Assert.That(Lint(clear, "BZ5")).IsFalse();
    }

    [Test]
    public async Task EL1_fires_on_an_odd_surface_delta()
    {
        var odd = Plan("""{ "plan":1, "globals":{"cell":1,"surface":9}, "pieces":[ {"id":"a","role":"mid","rect":[0,0,10,10],"surface":12} ] }""");
        var even = Plan("""{ "plan":1, "globals":{"cell":1,"surface":9}, "pieces":[ {"id":"a","role":"mid","rect":[0,0,10,10],"surface":13} ] }""");
        await Assert.That(Lint(odd, "EL1")).IsTrue();
        await Assert.That(Lint(even, "EL1")).IsFalse();
    }

    [Test]
    public async Task ST2_fires_on_iron_outside_the_spawn_piece_only_when_a_spawn_role_exists()
    {
        // a spawn-role piece exists; iron on a separate (non-spawn) piece → ST2 fires.
        var outside = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"sp","role":"spawn","rect":[0,0,10,10]}, {"id":"ln","role":"piece","rect":[0,20,10,10]} ],
          "placements":{ "iron":[ {"piece":"ln","at":[5,5]} ] } }
        """);
        // iron sits inside the spawn piece → no ST2.
        var inside = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"sp","role":"spawn","rect":[0,0,10,10]}, {"id":"ln","role":"piece","rect":[0,20,10,10]} ],
          "placements":{ "iron":[ {"piece":"sp","at":[5,5]} ] } }
        """);
        // no spawn-role piece at all → ST2 dormant even with stray iron.
        var noSpawnRole = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"ln","role":"piece","rect":[0,20,10,10]} ],
          "placements":{ "iron":[ {"piece":"ln","at":[5,5]} ] } }
        """);
        await Assert.That(Lint(outside, "ST2")).IsTrue();
        await Assert.That(Lint(inside, "ST2")).IsFalse();
        await Assert.That(Lint(noSpawnRole, "ST2")).IsFalse();
    }

    [Test]
    public async Task EL6_fires_on_a_full_width_big_drop_unless_a_cliff_is_declared()
    {
        // a full-lane-width (border ≥ 10) seam with Δ ≥ 6 is always a cliff (EL6) and needs a `cliffs` mark.
        var drop = Plan("""
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]}, {"id":"b","role":"piece","rect":[10,0,10,10],"surface":15} ] }
        """);
        var marked = Plan("""
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]}, {"id":"b","role":"piece","rect":[10,0,10,10],"surface":15} ],
          "cliffs":[ {"a":"a","b":"b"} ] }
        """);
        await Assert.That(Lint(drop, "EL6")).IsTrue();
        await Assert.That(Lint(marked, "EL6")).IsFalse();
    }

    [Test]
    public async Task EL6_ignores_a_narrow_big_drop()
    {
        // a narrow (5 < 10) seam does not cut a full lane width, so even a big drop across it is a stepped path
        // edge, not a cliff — no EL6.
        var narrow = Plan("""
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,5]}, {"id":"b","role":"piece","rect":[10,0,10,5],"surface":15} ] }
        """);
        await Assert.That(Lint(narrow, "EL6")).IsFalse();
    }

    [Test]
    public async Task EL6_ignores_a_lone_shallow_step_up()
    {
        // a full-width Δ4 seam that is a lone step up onto a plateau (the higher piece is not a pit floor) is a
        // stepped path edge, not a cliff — no EL6.
        var step = Plan("""
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]}, {"id":"b","role":"piece","rect":[10,0,10,10],"surface":13} ] }
        """);
        await Assert.That(Lint(step, "EL6")).IsFalse();
    }

    [Test]
    public async Task EL6_fires_on_a_shallow_pit_wall()
    {
        // a low floor pinched between opposing Δ4 walls is a pit (EL7): each shallow wall is a cliff and needs a
        // mark. floor 'f' (surface 9) drops from 'w' on its west and 'e' on its east.
        var pit = Plan("""
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"f","role":"piece","rect":[10,0,10,10]},
                     {"id":"w","role":"piece","rect":[0,0,10,10],"surface":13},
                     {"id":"e","role":"piece","rect":[20,0,10,10],"surface":13} ] }
        """);
        // both pit walls lint until marked
        var findings = PlanValidator.Validate(pit).Where(f => f.Rule == "EL6").ToList();
        await Assert.That(findings.Count).IsEqualTo(2);

        var marked = Plan("""
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"f","role":"piece","rect":[10,0,10,10]},
                     {"id":"w","role":"piece","rect":[0,0,10,10],"surface":13},
                     {"id":"e","role":"piece","rect":[20,0,10,10],"surface":13} ],
          "cliffs":[ {"a":"f","b":"w"}, {"a":"f","b":"e"} ] }
        """);
        await Assert.That(Lint(marked, "EL6")).IsFalse();
    }

    [Test]
    public async Task EL6_demotes_a_pit_wall_that_has_a_gentle_bypass()
    {
        // a pit whose floor also has a gentle (≤2 step) way out around one wall is not sealed there: that wall
        // is a stepped edge (walk around gently), while the still-sealed opposite wall stays a cliff. Floor 'f'
        // drops Δ4 from 'w' (west) and 'e' (east); a surface-11 apron 'g' bridges 'w'→'f' at ≤2 steps.
        var pit = Plan("""
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"f","role":"piece","rect":[10,10,10,10]},
                     {"id":"w","role":"piece","rect":[0,10,10,10],"surface":13},
                     {"id":"e","role":"piece","rect":[20,10,10,10],"surface":13},
                     {"id":"g","role":"piece","rect":[0,20,20,10],"surface":11} ] }
        """);
        var findings = PlanValidator.Validate(pit).Where(f => f.Rule == "EL6").Select(f => f.SubjectIds).ToList();
        // only the east wall stays a cliff (the west wall is bypassed through 'g')
        await Assert.That(findings.Count).IsEqualTo(1);
        await Assert.That(findings[0].Contains("e")).IsTrue();
    }

    [Test]
    public async Task Every_seed_plan_lints_clean_on_EL6()
    {
        // after the author's cliff marks, no seed carries an unmarked genuine cliff.
        var seeds = Directory.EnumerateFiles(PlanTestSupport.SeedDir(), "*.plan.json");
        foreach (var path in seeds)
        {
            var plan = PlanModel.Parse(File.ReadAllText(path))!;
            var el6 = PlanValidator.Validate(plan).Where(f => f.Rule == "EL6").ToList();
            await Assert.That(el6).IsEmpty();
        }
    }
}
