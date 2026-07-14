using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Evaluate;

/// <summary>
/// The soft "feel" terms and the envelopes they score against. Each term is dormant without a band (no false
/// positives before the stats are generated), scores distance outside its authored band, and draws its metric.
/// </summary>
public sealed class SoftTermsTests
{
    private static EvalContext Ctx(string json, SeedEnvelopes envelopes) =>
        EvalContext.Build(PlanModel.Parse(json)!, envelopes);

    // two wools on one connected lane piece, ~15 blocks apart by surface path (cell 5) — below WL7's band low
    private const string CloseWoolsJson = """
        {"plan":1,"globals":{"cell":5,"symmetry":"none"},
         "pieces":[{"id":"lane","role":"piece","rect":[0,0,2,6]}],
         "placements":{"wools":[{"piece":"lane","at":[1,1]},{"piece":"lane","at":[1,4]}]}}
        """;

    [Test]
    public async Task The_embedded_default_envelopes_load_the_generated_bands()
    {
        await Assert.That(SeedEnvelopes.Default.Bands).IsNotEmpty();
        await Assert.That(SeedEnvelopes.Default["fill-ratio"]).IsNotNull();
        await Assert.That(SeedEnvelopes.Default["wool-wool-distance"]).IsNotNull();
        await Assert.That(SeedEnvelopes.Default["spawn-wool-distance"]).IsNotNull();
        await Assert.That(SeedEnvelopes.Default["lane-width"]).IsNotNull();
        await Assert.That(SeedEnvelopes.Default["enclosed-void-count"]).IsNotNull();
        await Assert.That(SeedEnvelopes.Default["neutral-stepping-count"]).IsNotNull();
        await Assert.That(SeedEnvelopes.Default["band-count"]).IsNotNull();
        await Assert.That(SeedEnvelopes.Default["isolation-cut-count"]).IsNotNull();
    }

    [Test]
    public async Task Island_count_is_retired_from_the_catalogue()
    {
        await Assert.That(SeedEnvelopes.Default["island-count"]).IsNull();
        await Assert.That(LayoutEvaluator.AllTerms.Any(t => t.Id == "island-count")).IsFalse();
    }

    [Test]
    public async Task Neutral_stepping_count_fires_above_the_band_and_cites_ct4()
    {
        // base-2island carries contested mid stones; scored against a no-stones band [0,0] → out of band.
        var env = SeedEnvelopes.Load("""{"neutral-stepping-count":[0,0]}""");
        var ctx = EvalContext.Build(PlanModel.Parse(PlanTestSupport.ReadSeed("base-2island.plan.json"))!, env);
        var score = new NeutralSteppingCount().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("CT4");
    }

    [Test]
    public async Task Isolation_cut_count_fires_above_the_band_and_cites_ct5()
    {
        // base-2wool cuts its team side (intra zones); scored against a no-cuts band [0,0] → out of band.
        var env = SeedEnvelopes.Load("""{"isolation-cut-count":[0,0]}""");
        var ctx = EvalContext.Build(PlanModel.Parse(PlanTestSupport.ReadSeed("base-2wool.plan.json"))!, env);
        var score = new IsolationCutCount().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("CT5");
    }

    [Test]
    public async Task Band_count_reads_the_front_front_crossings()
    {
        // isolated-spawn is one channelled crossing → a single team↔team band (a shared count, not per-team).
        var ctx = EvalContext.Build(PlanModel.Parse(PlanTestSupport.ReadSeed("isolated-spawn.plan.json"))!, SeedEnvelopes.Default);
        await Assert.That(new BandCount().Value(ctx)).IsEqualTo(1.0);
    }

    [Test]
    public async Task Lane_width_fires_when_the_narrowest_lane_is_below_the_authored_band()
    {
        // a real seed's lane (10–20 blocks) scored against an absurd band [100,200] → far below → violation.
        var env = SeedEnvelopes.Load("""{"lane-width":[100,200]}""");
        var ctx = EvalContext.Build(PlanModel.Parse(PlanTestSupport.ReadSeed("base-2wool.plan.json"))!, env);
        var score = new LaneWidth().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("LN1");
        await Assert.That(score.Distance).IsGreaterThan(0.0);
    }

    [Test]
    public async Task Lane_width_does_not_apply_without_a_wool_lane()
    {
        // no wools → no lane shapes → the metric does not apply → clean, not a violation.
        var ctx = Ctx("""
            {"plan":1,"globals":{"cell":5,"symmetry":"none"},
             "pieces":[{"id":"p","role":"piece","rect":[0,0,4,4]}]}
            """, SeedEnvelopes.Default);
        await Assert.That(new LaneWidth().Value(ctx)).IsNull();
        await Assert.That(new LaneWidth().Measure(ctx).Violation).IsNull();
    }

    [Test]
    public async Task Max_chain_length_learns_from_the_authored_seeds_only()
    {
        // LN2 is an authored cap: the band is anchored to intent, not widened by the traced real maps.
        await Assert.That(new MaxChainLength().LearnsFromTraced).IsFalse();
    }

    [Test]
    public async Task Enclosed_void_count_fires_when_the_board_holes_leave_the_band()
    {
        // base-2wool derives two enclosed voids; scored against a no-holes band [0,0] → out of band → violation.
        var env = SeedEnvelopes.Load("""{"enclosed-void-count":[0,0]}""");
        var ctx = EvalContext.Build(PlanModel.Parse(PlanTestSupport.ReadSeed("base-2wool.plan.json"))!, env);
        var score = new EnclosedVoidCount().Measure(ctx);
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("CT8");
    }

    [Test]
    public async Task Spawn_wool_distance_fires_when_a_wool_hugs_the_spawn()
    {
        // spawn and wool on one lane piece, ~10 blocks apart by surface path — below WL2's band low.
        var score = new SpawnWoolDistance().Measure(Ctx("""
            {"plan":1,"globals":{"cell":5,"symmetry":"none"},
             "pieces":[{"id":"lane","role":"piece","rect":[0,0,2,4]}],
             "placements":{"spawns":[{"piece":"lane","at":[1,0],"facing":"front"}],
                           "wools":[{"piece":"lane","at":[1,2]}]}}
            """, SeedEnvelopes.Default));
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.RuleId).IsEqualTo("WL2");
        await Assert.That(score.Violation!.Evidence!.OfType<EvidenceMeasure>().Any()).IsTrue();
    }

    [Test]
    public async Task Wool_wool_distance_fires_on_crammed_wools_and_draws_the_pair()
    {
        var score = new WoolWoolDistance().Measure(Ctx(CloseWoolsJson, SeedEnvelopes.Default));
        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Distance).IsGreaterThan(0.0);
        await Assert.That(score.Violation!.Evidence!.OfType<EvidenceMarker>().Count()).IsEqualTo(2);
        await Assert.That(score.Violation!.Evidence!.OfType<EvidenceMeasure>().Any()).IsTrue();
    }

    [Test]
    public async Task A_soft_term_is_dormant_without_a_band()
    {
        // same crammed wools, but no envelopes → the term has no band to judge against → clean, not a violation.
        var score = new WoolWoolDistance().Measure(Ctx(CloseWoolsJson, SeedEnvelopes.Empty));
        await Assert.That(score.Violation).IsNull();
    }

    [Test]
    public async Task Wool_wool_distance_does_not_apply_to_a_single_wool()
    {
        var score = new WoolWoolDistance().Measure(Ctx("""
            {"plan":1,"globals":{"cell":5,"symmetry":"none"},
             "pieces":[{"id":"w1","role":"piece","rect":[0,0,2,2]}],
             "placements":{"wools":[{"piece":"w1","at":[1,1]}]}}
            """, SeedEnvelopes.Default));
        await Assert.That(new WoolWoolDistance().Value(Ctx("""
            {"plan":1,"globals":{"cell":5,"symmetry":"none"},
             "pieces":[{"id":"w1","role":"piece","rect":[0,0,2,2]}],
             "placements":{"wools":[{"piece":"w1","at":[1,1]}]}}
            """, SeedEnvelopes.Default))).IsNull();
        await Assert.That(score.Violation).IsNull();
    }

    // A seed defines the bands, so it lands in-band on every soft term by construction (the check guards term
    // bugs, not the seeds).
    [Test]
    [Arguments("base-2wool")]
    [Arguments("isolated-spawn")]
    [Arguments("rotate-wide-frontline")]
    public async Task A_seed_lands_in_band_on_every_soft_term(string seed)
    {
        var ctx = EvalContext.Build(PlanModel.Parse(PlanTestSupport.ReadSeed($"{seed}.plan.json"))!, SeedEnvelopes.Default);
        foreach (var term in LayoutEvaluator.AllTerms.OfType<SoftTerm>())
            await Assert.That(term.Measure(ctx).Violation).IsNull();
    }
}
