using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>WL8: no closure hole may be ringed by a wool plateau. A hole bordered by a wool piece is the
/// two-approaches-around-the-wool motif the grammar does not author (the default is a single chokepoint route).
/// Reads the fanned closure via <see cref="ClosureAnalysis"/> — the dense-grid hole twin kept for the hunt
/// loop's speed.</summary>
public sealed class WoolRingedHole : ILayoutTerm
{
    public string Id => "wool-ringed-hole";
    public string RuleId => "WL8";
    public TermKind Kind => TermKind.Hard;

    public TermScore Measure(EvalContext ctx)
    {
        var woolPieces = ctx.Plan.Placements.Wools.Select(w => w.Piece).ToHashSet();
        // Evidence is the wool pieces only; the ringed hole's own rect is not in hand (ClosureAnalysis returns a
        // verdict, not the enclosed cells) — richer evidence waits on it surfacing the hole geometry.
        return ClosureAnalysis.AnyHoleRingedBy(ctx.Plan, woolPieces)
            ? TermScores.Violated(this, "a closure hole is ringed by a wool plateau (two approaches, WL8)",
                woolPieces.ToList(), TermEvidence.OffenderRects(ctx.Plan, woolPieces))
            : TermScores.Clean(this);
    }
}
