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
            {"plan":2,"globals":{"cell":1,"symmetry":"none"},
             "pieces":[{"id":"w","role":"piece","rect":[0,0,2,2]}],
             "placements":{"wools":[{"piece":"w","at":[5,5]}]}}
            """);
        var score = new StructuralIntegrity().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("STRUCT");
        await Assert.That(score.Violation!.Evidence!.OfType<EvidenceRect>().Any()).IsTrue();
    }

    [Test]
    public async Task Structural_integrity_is_clean_on_an_in_bounds_placement()
    {
        var ctx = Ctx("""
            {"plan":2,"globals":{"cell":1,"symmetry":"none"},
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
            {"plan":2,"globals":{"cell":5,"symmetry":"none"},
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
            {"plan":2,"globals":{"cell":5,"symmetry":"none"},
             "pieces":[{"id":"a","role":"piece","rect":[0,0,2,2]},{"id":"b","role":"piece","rect":[2,2,2,2]}]}
            """);
        await Assert.That(new LintRejectTerm("PC-C").Measure(ctx).Violation).IsNotNull();
    }

    [Test]
    public async Task Lint_reject_is_clean_when_the_zone_meets_the_corridor_minimum()
    {
        // a 2×3-cell zone is 10 blocks wide = the corridor minimum → no G2.
        var ctx = Ctx("""
            {"plan":2,"globals":{"cell":5,"symmetry":"none"},
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
            {"plan":2,"globals":{"cell":1,"symmetry":"none"},
             "pieces":[{"id":"a","role":"piece","rect":[0,0,10,10]},{"id":"b","role":"piece","rect":[35,0,10,10]}],
             "zones":[{"id":"z","rect":[0,0,45,10]}]}
            """);
        var score = new GapHopBand().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("G5");
        // the hop draws itself: a dimension line labelled with the offending span
        var measure = score.Violation!.Evidence!.OfType<EvidenceMeasure>().FirstOrDefault();
        await Assert.That(measure).IsNotNull();
        await Assert.That(measure!.Label).Contains("25");
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
            {"plan":2,"globals":{"cell":1,"symmetry":"none"},
             "pieces":[{"id":"w","role":"piece","rect":[0,0,2,2]}],
             "zones":[{"id":"mid-band","rect":[2,0,4,2]}],
             "placements":{"wools":[{"piece":"w","at":[1,1]}]}}
            """);
        var score = new BandWoolClearance().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("BZ6");
        // the wool (offender) and the band (context) are drawable rects
        var rects = score.Violation!.Evidence!.OfType<EvidenceRect>().ToList();
        await Assert.That(rects.Any(r => r.Tag == EvidenceTags.Offender)).IsTrue();
        await Assert.That(rects.Any(r => r.Tag == EvidenceTags.Context)).IsTrue();
    }

    [Test]
    public async Task Band_wool_clearance_is_clean_at_exactly_two_cells()
    {
        // wool piece 0..2, mid-band 4..8 — a full two-cell gap satisfies BZ6.
        var ctx = Ctx("""
            {"plan":2,"globals":{"cell":1,"symmetry":"none"},
             "pieces":[{"id":"w","role":"piece","rect":[0,0,2,2]}],
             "zones":[{"id":"mid-band","rect":[4,0,4,2]}],
             "placements":{"wools":[{"piece":"w","at":[1,1]}]}}
            """);
        await Assert.That(new BandWoolClearance().Measure(ctx).Violation).IsNull();
    }

    // ── SpawnWoolFloor (WL2, surface distance) ──────────────────────────────────────────────────────────

    [Test]
    public async Task Spawn_wool_floor_fires_when_the_wool_hugs_the_spawn()
    {
        // spawn and wool on one lane, ~10 blocks apart by surface path (cell 5) < WL2's 20-block floor.
        var ctx = Ctx("""
            {"plan":2,"globals":{"cell":5,"symmetry":"none"},
             "pieces":[{"id":"lane","role":"piece","rect":[0,0,2,4]}],
             "placements":{"spawns":[{"piece":"lane","at":[5,0],"facing":"front"}],
                           "wools":[{"piece":"lane","at":[5,10]}]}}
            """);
        var score = new SpawnWoolFloor().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("WL2");
    }

    [Test]
    public async Task Spawn_wool_floor_is_clean_when_far_enough()
    {
        // 5 cells = 25 blocks by surface path ≥ the 20-block floor.
        var ctx = Ctx("""
            {"plan":2,"globals":{"cell":5,"symmetry":"none"},
             "pieces":[{"id":"lane","role":"piece","rect":[0,0,2,7]}],
             "placements":{"spawns":[{"piece":"lane","at":[5,0],"facing":"front"}],
                           "wools":[{"piece":"lane","at":[5,25]}]}}
            """);
        await Assert.That(new SpawnWoolFloor().Measure(ctx).Violation).IsNull();
    }


    // ── SpawnFrontFloor (SP10) and WoolFrontFloor (WL10), surface distance to the crossing ───────────────

    // one lane off a mid band, spawn and wool at block offsets along it (cell 4: the lane starts 4 blocks past
    // the band's edge)
    private static EvalContext Lane(int spawnAt, int woolAt) => Ctx($$$"""
        {"plan":2,"globals":{"cell":4,"symmetry":"rot_180"},
         "pieces":[{"id":"lane","role":"piece","rect":[-2,1,4,24]}],
         "zones":[{"id":"mid-band","rect":[-2,-1,4,2]}],
         "placements":{"spawns":[{"piece":"lane","at":[8,{{{spawnAt}}}],"facing":"front"}],
                       "wools":[{"piece":"lane","at":[8,{{{woolAt}}}]}]}}
        """);

    [Test]
    public async Task Spawn_front_floor_fires_on_a_spawn_that_walks_straight_onto_the_crossing()
    {
        var near = new SpawnFrontFloor().Measure(Lane(spawnAt: 30, woolAt: 90));
        await Assert.That(near.Violation).IsNotNull();
        await Assert.That(near.Violation!.RuleId).IsEqualTo("SP10");
        await Assert.That(new SpawnFrontFloor().Measure(Lane(spawnAt: 90, woolAt: 70)).Violation).IsNull();
    }

    [Test]
    public async Task Wool_front_floor_fires_on_a_wool_beside_the_crossing()
    {
        var near = new WoolFrontFloor().Measure(Lane(spawnAt: 90, woolAt: 20));
        await Assert.That(near.Violation).IsNotNull();
        await Assert.That(near.Violation!.RuleId).IsEqualTo("WL10");
        await Assert.That(new WoolFrontFloor().Measure(Lane(spawnAt: 90, woolAt: 70)).Violation).IsNull();
    }

    [Test]
    public async Task The_front_floors_bind_the_composer_and_not_the_default_profile()
    {
        var near = Lane(spawnAt: 30, woolAt: 20);
        await Assert.That(LayoutEvaluator.Gate(near, EvaluationProfile.Composer)).IsNotNull();
        var lint = LayoutEvaluator.Gate(near, EvaluationProfile.Default);
        await Assert.That(lint?.RuleId is "SP10" or "WL10").IsFalse();
    }

    // ── WoolRoomSpawnSeam (WL2, the lane clause) ────────────────────────────────────────────────────────

    // Two wool rooms flanking the spawn in one row, cell 4. `between` is the cell width of a plain run piece
    // standing between the spawn and each room; 0 puts the rooms edge to edge with the spawn.
    private static PlanModel Flanked(int between) => PlanModel.Parse($$$"""
        {"plan":2,"globals":{"cell":4,"symmetry":"none"},
         "pieces":[
           {"id":"dye-w","role":"wool-room","rect":[{{{-13 - between}}},-26,5,4]},
           {{{(between > 0 ? $$"""{"id":"run-w","role":"piece","rect":[{{-8 - between}},-26,{{between}},4]},""" : "")}}}
           {"id":"yard","role":"spawn","rect":[-8,-26,7,4]},
           {{{(between > 0 ? $$"""{"id":"run-e","role":"piece","rect":[-1,-26,{{between}},4]},""" : "")}}}
           {"id":"dye-e","role":"wool-room","rect":[{{{-1 + between}}},-26,5,4]}],
         "placements":{"spawns":[{"piece":"yard","at":[14,8],"facing":"front"}],
                       "wools":[{"piece":"dye-w","at":[10,8]},{"piece":"dye-e","at":[10,8]}]}}
        """)!;

    [Test]
    public async Task A_wool_room_sharing_an_edge_with_its_spawn_is_invalid()
    {
        var evaluation = LayoutEvaluator.Evaluate(Flanked(between: 0), EvaluationProfile.Default);
        await Assert.That(evaluation.IsValid).IsFalse();
        var seam = evaluation.Violations.SingleOrDefault(v => v.TermId == "wool-room-spawn-seam");
        await Assert.That(seam).IsNotNull();
        await Assert.That(seam!.RuleId).IsEqualTo("WL2");
        await Assert.That(seam.Subjects).Contains("dye-w");
        await Assert.That(seam.Subjects).Contains("dye-e");
        await Assert.That(seam.Subjects).Contains("yard");
    }

    [Test]
    public async Task A_run_piece_between_the_wool_room_and_its_spawn_is_valid()
    {
        var evaluation = LayoutEvaluator.Evaluate(Flanked(between: 4), EvaluationProfile.Default);
        var seam = evaluation.Terms.SingleOrDefault(t => t.TermId == "wool-room-spawn-seam");
        await Assert.That(seam).IsNotNull();
        await Assert.That(seam!.Violation).IsNull();
        await Assert.That(evaluation.IsValid).IsTrue();
    }
}
