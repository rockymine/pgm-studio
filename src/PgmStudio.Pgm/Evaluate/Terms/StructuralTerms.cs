using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>Structural well-formedness: any <see cref="PlanSeverity.Error"/> the validator reports
/// (different-surface overlap, a placement outside its piece, a wall off a real seam, a wool unreachable or
/// only reachable through a spawn — SP1). These are the parse/topology errors <see cref="PlanValidator"/> owns;
/// this term surfaces them as one hard violation so the same gate that scores layout rules also blocks a plan
/// that does not compile. Cites the sentinel <c>STRUCT</c> (not a single layout-rules id — it aggregates the
/// structural error set).</summary>
public sealed class StructuralIntegrity : ILayoutTerm
{
    public const string Rule = "STRUCT";

    public string Id => "structural-integrity";
    public string RuleId => Rule;
    public TermKind Kind => TermKind.Hard;

    public TermScore Measure(EvalContext ctx)
    {
        var errors = ctx.Findings.Where(f => f.Severity == PlanSeverity.Error).ToList();
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
