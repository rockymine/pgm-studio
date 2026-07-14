using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Evaluate;

/// <summary>
/// The one place layout rules are scored. <c>Score = Σ_hard-violated P_HARD + Σ_soft w·distance</c> — lower is
/// better, 0 is perfect, and any hard violation dominates every soft sum so a malformed layout always ranks
/// below a merely-ugly one. The same engine serves three consumers on three profiles: the composer's
/// short-circuit <see cref="Gate"/>, the editor lint, and the ranking harness. Terms read the derived
/// <see cref="EvalContext"/> only — never a shape/family name.
/// </summary>
public static class LayoutEvaluator
{
    /// <summary>The flat penalty per violated hard term — large enough to dominate any realistic soft sum.</summary>
    public const double HardPenalty = 1000.0;

    /// <summary>Every registered term. The composer gate is exactly these hard terms; soft terms join as the
    /// envelope catalogue is built. A profile selects/weights them by id.</summary>
    public static readonly IReadOnlyList<ILayoutTerm> AllTerms =
    [
        // hard terms — the acceptance gate (port of Composer.Acceptable)
        new StructuralIntegrity(),
        new LintRejectTerm("PC-C"),
        new LintRejectTerm("G2"),
        new GapHopBand(),
        new BandWoolClearance(),
        new WoolRingedHole(),
        new SpawnWoolFloor(),      // WL2 as a surface-distance floor (was the Euclidean WL2 lint)
        // soft terms — feel metrics scored against the authored seed envelopes
        new FillRatio(),
        new EnclosedVoidCount(),
        new NeutralSteppingCount(),
        new TeamSteppingCount(),
        new BandCount(),
        new IsolationCutCount(),
        new MaxChainLength(),
        new LaneWidth(),
        new WoolWoolDistance(),
        new SpawnWoolDistance(),
    ];

    /// <summary>Full scored evaluation: run every enabled term, sum hard penalties + weighted soft distances.</summary>
    public static Evaluation Evaluate(EvalContext ctx, EvaluationProfile profile)
    {
        var scores = new List<TermScore>();
        var sum = 0.0;
        foreach (var term in AllTerms)
        {
            if (!profile.Enabled(term.Id)) continue;
            var score = term.Measure(ctx);
            scores.Add(score);
            if (term.Kind == TermKind.Hard)
            {
                if (score.Violation is not null) sum += HardPenalty;
            }
            else
            {
                sum += profile.Weight(term.Id) * score.Distance;
            }
        }
        return new Evaluation(sum, scores);
    }

    /// <summary>Build the context and evaluate a plan. Soft terms score against <paramref name="envelopes"/>,
    /// defaulting to the checked-in <see cref="SeedEnvelopes.Default"/>.</summary>
    public static Evaluation Evaluate(PlanModel plan, EvaluationProfile profile, SeedEnvelopes? envelopes = null) =>
        Evaluate(EvalContext.Build(plan, envelopes ?? SeedEnvelopes.Default), profile);

    /// <summary>The composer's acceptance gate: run the enabled hard terms in order and return the first
    /// violation (or null to accept). Short-circuit — a rejected attempt costs only the terms up to its first
    /// failure, and the board is never derived (no ported hard term needs it).</summary>
    public static Violation? Gate(EvalContext ctx, EvaluationProfile profile)
    {
        foreach (var term in AllTerms)
        {
            if (term.Kind != TermKind.Hard || !profile.Enabled(term.Id)) continue;
            var score = term.Measure(ctx);
            if (score.Violation is not null) return score.Violation;
        }
        return null;
    }
}
