using PgmStudio.Domain;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>Structural well-formedness: any refusal the validator reports
/// (different-surface overlap, a placement outside its piece, a wall off a real seam, a wool unreachable or
/// only reachable through a spawn — SP1). These are the parse/topology errors <see cref="PlanValidator"/> owns;
/// this term surfaces them so the same gate that scores layout rules also blocks a plan that does not compile.
///
/// <para><b>It has two readings, and they answer different questions.</b> <see cref="Measure"/> is the score:
/// one hard violation however many refusals fired, because a plan that does not compile costs the flat hard
/// penalty once. <see cref="Each"/> is the report: one violation per refusal, keeping the <c>PL</c> id it was
/// refused under and pointing at its own subjects, which is what a caller answers a reader with. Scoring the
/// itemised list would charge a plan twice for one fault; reporting the aggregate would tell an author a count
/// where the validator gave them sentences.</para></summary>
public sealed class StructuralIntegrity : ILayoutTerm
{
    /// <remarks>Not a rule of its own: the id the evaluator gives the structural set when it scores as one hard
    /// term, and it is deliberately not a catalogued rule — <c>GET /api/rules</c> answers the <c>PL</c> ids the
    /// refusals themselves carry, which is what <see cref="Each"/> reports and what a reader looks up.</remarks>
    public const string Rule = "STRUCT";

    /// <summary>The term id as a constant, so a caller naming this measurement spells it once.</summary>
    public const string Term = "structural-integrity";

    public string Id => Term;
    public string RuleId => Rule;
    public TermKind Kind => TermKind.Hard;

    /// <summary>Every refusal the validator made, one violation each: the finding is the validator's own, so
    /// the rule id and the sentence are the ones <c>/plan/compile</c> refuses under, and the evidence points at
    /// that refusal's own subjects rather than at the union. Empty for a plan the validator accepts.</summary>
    public static IEnumerable<Violation> Each(EvalContext ctx) =>
        ctx.Findings.Refusals.Select(refusal =>
            new Violation(Term, refusal, TermEvidence.OffenderRects(ctx.Plan, refusal.SubjectIds)));

    public TermScore Measure(EvalContext ctx)
    {
        var errors = ctx.Findings.Refusals.ToList();
        if (errors.Count == 0) return TermScores.Clean(this);

        var subjects = errors.SelectMany(e => e.SubjectIds).Distinct().ToList();
        var message = errors.Count == 1
            ? errors[0].Message
            : $"{errors.Count} structural errors ({errors[0].Message})";
        return TermScores.Violated(this, message, subjects, TermEvidence.OffenderRects(ctx.Plan, subjects));
    }
}

/// <summary>A hard reject on the presence of a specific <see cref="PlanValidator"/> lint finding: the composer
/// resamples rather than emit a plan carrying it. One instance per rejected rule id (the composer gate rejects
/// on <c>WL2</c>, <c>PC-C</c>, <c>G2</c> — the geometric constructions almost always satisfy them, but a
/// resample beats emitting a lint). The lint computation stays in <see cref="PlanValidator"/>; this term only
/// reads the finding it produced.</summary>
public sealed class LintRejectTerm(string ruleId) : ILayoutTerm
{
    public string Id => $"lint-{ruleId.ToLowerInvariant()}";
    public string RuleId => ruleId;
    public TermKind Kind => TermKind.Hard;

    public TermScore Measure(EvalContext ctx)
    {
        var finding = ctx.Findings.FirstOrDefault(f => f.Rule == ruleId);
        return finding is null
            ? TermScores.Clean(this)
            : TermScores.Violated(this, finding.Message, finding.SubjectIds,
                TermEvidence.OffenderRects(ctx.Plan, finding.SubjectIds));
    }
}
