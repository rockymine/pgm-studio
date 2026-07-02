using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The two-tier validator. Structural errors (sliver/corner contacts, different-surface overlaps, placements
/// outside a piece, unreachable wool, a wool reachable only through a spawn) block a compile; rule lint cites
/// a provisional layout-rule id and never blocks. Each rule is exercised firing and not firing on synthetic
/// fixtures; the three seed plans must be error-free.
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
    public async Task Sliver_contact_is_an_error()
    {
        var p = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,9]}, {"id":"b","role":"lane","rect":[10,0,10,10]} ] }
        """);
        await Assert.That(Err(p, "sliver")).IsTrue();
    }

    [Test]
    public async Task Corner_contact_is_an_error()
    {
        var p = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"lane","rect":[10,10,10,10]} ] }
        """);
        await Assert.That(Err(p, "corner")).IsTrue();
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
    public async Task Findings_carry_the_ids_of_the_pieces_they_implicate()
    {
        // a sliver contact (error) between a/b and a narrow zone (G2 lint) each name their subjects
        var p = Plan("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,9]}, {"id":"b","role":"lane","rect":[10,0,10,10]} ],
          "zones":[ {"id":"z","rect":[0,20,8,20]} ] }
        """);
        var all = PlanValidator.Validate(p);

        var sliver = all.First(f => f.Severity == PlanSeverity.Error && f.Message.Contains("sliver"));
        await Assert.That(sliver.SubjectIds).Contains("a");
        await Assert.That(sliver.SubjectIds).Contains("b");

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
    public async Task EL3_fires_on_a_tall_land_interface_unless_a_cliff_is_declared()
    {
        var step = Plan("""
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"lane","rect":[10,0,10,10],"surface":13} ] }
        """);
        var cliff = Plan("""
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"lane","rect":[10,0,10,10],"surface":13} ],
          "cliffs":[ {"a":"a","b":"b"} ] }
        """);
        await Assert.That(Lint(step, "EL3")).IsTrue();
        await Assert.That(Lint(cliff, "EL3")).IsFalse();
    }
}
