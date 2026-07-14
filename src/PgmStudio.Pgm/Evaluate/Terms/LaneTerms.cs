using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>LN2: a lane runs 20–50 blocks before a junction or dead end. Measured as the longest maximal chain
/// of collinear, land-joined pieces (the same read the grower caps at). A chain far past the authored norm is
/// the lane-bloat anti-pattern — length spent where width or an extra route belongs.</summary>
public sealed class MaxChainLength : SoftTerm
{
    public override string Id => "max-chain-length";
    public override string RuleId => "LN2";

    // LN2 is an authored *cap* on lane length, not a distribution we widen to fit: traced real maps run much
    // longer chains, so the band learns from the authored intent seeds only, not the traced corpus.
    public override bool LearnsFromTraced => false;

    public override double? Value(EvalContext ctx)
    {
        var pieces = ctx.Plan.Pieces.Select(p => p.Rect).ToList();
        if (pieces.Count == 0) return null;
        return TeamUnitGrower.MaxChainBlocks(ctx.Plan.Globals.Cell, pieces);
    }
}

/// <summary>LN1: a wool lane runs about 10 blocks wide (15 on big maps). Measured as the narrowest lane on the
/// board, in blocks — the width of the tightest wool approach. A lane thinner than the authored norm is the
/// goat-path anti-pattern (a one-wide walkway a lone defender plugs); the band's floor is the narrowest lane any
/// authored map dares. Reads the per-wool lane widths the deriver stacks — no shape names.</summary>
public sealed class LaneWidth : SoftTerm
{
    public override string Id => "lane-width";
    public override string RuleId => "LN1";

    public override double? Value(EvalContext ctx)
    {
        var shapes = ctx.Board.WoolShapes;
        if (shapes.Count == 0) return null;
        return shapes.Min(s => s.Width) * (double)ctx.Board.Cell;
    }
}
