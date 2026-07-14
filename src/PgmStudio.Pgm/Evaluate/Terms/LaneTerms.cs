using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>LN2: a lane runs 20–50 blocks before a junction or dead end. Measured as the longest maximal chain
/// of collinear, land-joined pieces (the same read the grower caps at). A chain far past the authored norm is
/// the lane-bloat anti-pattern — length spent where width or an extra route belongs.</summary>
public sealed class MaxChainLength : SoftTerm
{
    public override string Id => "max-chain-length";
    public override string RuleId => "LN2";

    public override double? Value(EvalContext ctx)
    {
        var pieces = ctx.Plan.Pieces.Select(p => p.Rect).ToList();
        if (pieces.Count == 0) return null;
        return TeamUnitGrower.MaxChainBlocks(ctx.Plan.Globals.Cell, pieces);
    }
}
