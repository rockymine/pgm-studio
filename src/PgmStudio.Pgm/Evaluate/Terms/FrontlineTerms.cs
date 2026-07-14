using PgmStudio.Geom;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>FR4: a team's angles of attack — the number of distinct void-facing frontline faces it presents, per
/// team (fanned runs ÷ orbit order). A team side fragmented into many exposed faces reads over-exposed; the
/// corpus keeps it low. (FR4's "a single face is fine when it is wide" is the count↔width coupling the composite
/// scores later — this term is the count alone.) Reads the derived frontline runs — no shape names.</summary>
public sealed class FrontlineCount : SoftTerm
{
    public override string Id => "frontline-count";
    public override string RuleId => "FR4";

    public override double? Value(EvalContext ctx)
    {
        var runs = ctx.Board.FrontlineRuns;
        if (runs.Count == 0) return null;
        return runs.Count / (double)Symmetry.Order(ctx.Plan.Globals.Symmetry);
    }
}

/// <summary>FR6: the width of a team's broadest frontline face, in cells — the wide-vs-split axis. A wide front
/// is one 6–8-cell face; a split front is narrower tips hung off a hub. Bands how broad the widest authored face
/// runs; a frontline far wider than the corpus over-commits the edge. Reads the widest derived frontline run.</summary>
public sealed class FrontlineWidth : SoftTerm
{
    public override string Id => "frontline-width";
    public override string RuleId => "FR6";

    public override double? Value(EvalContext ctx)
    {
        var runs = ctx.Board.FrontlineRuns;
        if (runs.Count == 0) return null;
        return runs.Max(r => r.Width);
    }
}
