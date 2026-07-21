using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>WL8: no closure hole may be ringed by a wool plateau <b>and foreign terrain</b> — the
/// two-approaches-around-the-wool motif where outside terrain wraps the wool. A staple-class or donut wool's
/// <b>own sealed courtyard</b> (its bay/hole enclosed by its own legs + room, sealed by the one host edge it
/// docks) is the shape's sanctioned design and exempt — see <see cref="ClosureAnalysis.AnyHoleRingedBy"/>.
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
