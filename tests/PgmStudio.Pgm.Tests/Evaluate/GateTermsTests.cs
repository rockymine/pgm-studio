using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Evaluate;

/// <summary>
/// Each hard gate term in isolation, at its boundary — synthetic plans that carry exactly the one property the
/// term reads. Their collective fidelity to the old inline acceptance gate is proven separately (byte-identical
/// composed output across the sweep); these pin each term one at a time so a future change is caught locally.
/// </summary>
public sealed class GateTermsTests
{
    private static EvalContext Ctx(string json) => EvalContext.Build(PlanModel.Parse(json)!);

    // ── StructuralIntegrity (STRUCT) ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Structural_integrity_fires_on_a_placement_outside_its_piece()
    {
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":1,"symmetry":"none"},
             "pieces":[{"id":"w","role":"piece","rect":[0,0,2,2]}],
             "placements":{"wools":[{"piece":"w","at":[5,5]}]}}
            """);
        var score = new StructuralIntegrity().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("STRUCT");
    }

    [Test]
    public async Task Structural_integrity_is_clean_on_an_in_bounds_placement()
    {
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":1,"symmetry":"none"},
             "pieces":[{"id":"w","role":"piece","rect":[0,0,2,2]}],
             "placements":{"wools":[{"piece":"w","at":[1,1]}]}}
            """);
        await Assert.That(new StructuralIntegrity().Measure(ctx).Violation).IsNull();
    }

    // ── LintRejectTerm (WL2 / PC-C / G2) ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Lint_reject_fires_on_a_narrow_zone_g2()
    {
        // cell 5, a 1×3-cell zone is 5 blocks wide < the 10-block corridor minimum → G2 lint present.
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":5,"symmetry":"none"},
             "pieces":[{"id":"a","role":"piece","rect":[0,0,4,4]}],
             "zones":[{"id":"z","rect":[0,0,1,3]}]}
            """);
        var score = new LintRejectTerm("G2").Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("G2");
        await Assert.That(score.Violation!.Subjects).Contains("z");
    }

    [Test]
    public async Task Lint_reject_fires_on_a_corner_only_contact_pcc()
    {
        // two pieces meeting at a single corner in separate land components → PC-C lint.
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":5,"symmetry":"none"},
             "pieces":[{"id":"a","role":"piece","rect":[0,0,2,2]},{"id":"b","role":"piece","rect":[2,2,2,2]}]}
            """);
        await Assert.That(new LintRejectTerm("PC-C").Measure(ctx).Violation).IsNotNull();
    }

    [Test]
    public async Task Lint_reject_is_clean_when_the_zone_meets_the_corridor_minimum()
    {
        // a 2×3-cell zone is 10 blocks wide = the corridor minimum → no G2.
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":5,"symmetry":"none"},
             "pieces":[{"id":"a","role":"piece","rect":[0,0,4,4]}],
             "zones":[{"id":"z","rect":[0,0,2,3]}]}
            """);
        await Assert.That(new LintRejectTerm("G2").Measure(ctx).Violation).IsNull();
    }

    // ── GapHopBand (G5) ─────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Gap_hop_band_fires_on_a_gap_wider_than_twenty()
    {
        // two pieces 25 blocks apart, bridged by one zone spanning both → an out-of-band 25-block hop.
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":1,"symmetry":"none"},
             "pieces":[{"id":"a","role":"piece","rect":[0,0,10,10]},{"id":"b","role":"piece","rect":[35,0,10,10]}],
             "zones":[{"id":"z","rect":[0,0,45,10]}]}
            """);
        var score = new GapHopBand().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("G5");
    }

    [Test]
    public async Task Gap_hop_band_is_clean_on_a_seed_with_in_band_hops()
    {
        var ctx = EvalContext.Build(PlanModel.Parse(PlanTestSupport.ReadSeed("base-2wool.plan.json"))!);
        await Assert.That(new GapHopBand().Measure(ctx).Violation).IsNull();
    }

    // ── BandWoolClearance (BZ6) ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Band_wool_clearance_fires_when_the_band_abuts_a_wool()
    {
        // wool piece 0..2, mid-band 2..6 — the band touches the wool (0-cell clearance) → BZ6.
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":1,"symmetry":"none"},
             "pieces":[{"id":"w","role":"piece","rect":[0,0,2,2]}],
             "zones":[{"id":"mid-band","rect":[2,0,4,2]}],
             "placements":{"wools":[{"piece":"w","at":[1,1]}]}}
            """);
        var score = new BandWoolClearance().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("BZ6");
    }

    [Test]
    public async Task Band_wool_clearance_is_clean_at_exactly_two_cells()
    {
        // wool piece 0..2, mid-band 4..8 — a full two-cell gap satisfies BZ6.
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":1,"symmetry":"none"},
             "pieces":[{"id":"w","role":"piece","rect":[0,0,2,2]}],
             "zones":[{"id":"mid-band","rect":[4,0,4,2]}],
             "placements":{"wools":[{"piece":"w","at":[1,1]}]}}
            """);
        await Assert.That(new BandWoolClearance().Measure(ctx).Violation).IsNull();
    }

    // ── WoolRingedHole (WL8) ────────────────────────────────────────────────────────────────────────────
    // A rot_180 annulus (top + left bars fan to bottom + right) enclosing a central hole.

    [Test]
    public async Task Wool_ringed_hole_fires_when_a_wool_borders_the_hole()
    {
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":1,"symmetry":"rot_180"},
             "pieces":[{"id":"top","role":"piece","rect":[-3,-3,6,2]},{"id":"left","role":"piece","rect":[-3,-1,2,2]}],
             "placements":{"wools":[{"piece":"top","at":[0,0]}]}}
            """);
        var score = new WoolRingedHole().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("WL8");
    }

    [Test]
    public async Task Wool_ringed_hole_is_clean_with_no_wool_on_the_ring()
    {
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":1,"symmetry":"rot_180"},
             "pieces":[{"id":"top","role":"piece","rect":[-3,-3,6,2]},{"id":"left","role":"piece","rect":[-3,-1,2,2]}]}
            """);
        await Assert.That(new WoolRingedHole().Measure(ctx).Violation).IsNull();
    }
}
