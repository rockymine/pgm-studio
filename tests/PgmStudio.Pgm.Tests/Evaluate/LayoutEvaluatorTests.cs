using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Evaluate;

/// <summary>
/// The evaluator engine: hard penalties dominate the score, the gate short-circuits on the first hard
/// violation, and the profile is the on/off switch. The load-bearing regression: every plan the composer emits
/// passes the gate and scores 0 (the dissolved <c>Composer.Acceptable</c> and the evaluator agree exactly).
/// </summary>
public sealed class LayoutEvaluatorTests
{
    private static readonly string ErrorPlanJson = """
        {"plan":1,"globals":{"cell":1,"symmetry":"none"},
         "pieces":[{"id":"w","role":"piece","rect":[0,0,2,2]}],
         "placements":{"wools":[{"piece":"w","at":[5,5]}]}}
        """;

    [Test]
    public async Task A_clean_composed_plan_is_valid_with_no_hard_violation()
    {
        // Composed plans pass the gate (no hard violation); their soft score is how authored-like they are —
        // nonzero is expected once soft terms score against the seed envelopes.
        var plan = Composer.Compose(new ComposeRequest(12, seed: 1));
        var eval = LayoutEvaluator.Evaluate(plan, EvaluationProfile.Default);
        await Assert.That(eval.IsValid).IsTrue();
        await Assert.That(eval.Violations.Any(v => v.RuleId == "STRUCT")).IsFalse();
    }

    [Test]
    public async Task A_hard_violation_dominates_the_score_and_invalidates()
    {
        var plan = PlanModel.Parse(ErrorPlanJson)!;
        var eval = LayoutEvaluator.Evaluate(plan, EvaluationProfile.Default);
        await Assert.That(eval.IsValid).IsFalse();
        await Assert.That(eval.Score).IsGreaterThanOrEqualTo(LayoutEvaluator.HardPenalty);
        await Assert.That(eval.Violations.Any(v => v.RuleId == "STRUCT")).IsTrue();
    }

    [Test]
    public async Task The_gate_returns_the_first_hard_violation()
    {
        var violation = LayoutEvaluator.Gate(EvalContext.Build(PlanModel.Parse(ErrorPlanJson)!), EvaluationProfile.Default);
        await Assert.That(violation).IsNotNull();
        await Assert.That(violation!.RuleId).IsEqualTo("STRUCT");
    }

    [Test]
    public async Task Disabling_a_term_in_the_profile_skips_it()
    {
        var ctx = EvalContext.Build(PlanModel.Parse(ErrorPlanJson)!);
        var profile = EvaluationProfile.Default.With("structural-integrity", enabled: false);
        await Assert.That(LayoutEvaluator.Gate(ctx, profile)).IsNull();
    }

    // Every plan the composer emits must pass the gate (no hard violation) — the permanent form of the
    // byte-identity check made when Composer.Acceptable was dissolved into the evaluator. (Soft score is not
    // asserted 0: soft terms measure authored-likeness, which the current constructive grower does not optimize.)
    [Test]
    public async Task Every_composed_plan_passes_the_hard_gate()
    {
        foreach (var players in new[] { 12, 20 })
            foreach (var teams in new[] { 2, 4 })
                for (ulong seed = 1; seed <= 10; seed++)
                {
                    var plan = Composer.Compose(new ComposeRequest(players, teams, seed: seed));
                    var ctx = EvalContext.Build(plan);
                    await Assert.That(LayoutEvaluator.Gate(ctx, EvaluationProfile.Default)).IsNull();
                    await Assert.That(LayoutEvaluator.Evaluate(plan, EvaluationProfile.Default).IsValid).IsTrue();
                }
    }

    // The reject sink is opt-in: passing one never changes the composed plan, and every captured record cites a
    // real gate rule with a reproducible seed/attempt.
    [Test]
    public async Task The_reject_sink_captures_well_formed_records_without_changing_output()
    {
        var knownRules = LayoutEvaluator.AllTerms.Select(t => t.RuleId).ToHashSet();
        var sink = new CollectingSink();
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var withoutSink = Composer.Compose(new ComposeRequest(20, seed: seed));
            var withSink = Composer.Compose(new ComposeRequest(20, seed: seed), sink);
            await Assert.That(withSink.ToJson()).IsEqualTo(withoutSink.ToJson());
        }
        foreach (var r in sink.Records)
        {
            await Assert.That(knownRules).Contains(r.RuleId);
            await Assert.That(r.Stage).IsEqualTo("acceptance");
        }
    }

    private sealed class CollectingSink : IComposeRejectSink
    {
        public List<RejectRecord> Records { get; } = [];
        public void Reject(RejectRecord record) => Records.Add(record);
    }
}
