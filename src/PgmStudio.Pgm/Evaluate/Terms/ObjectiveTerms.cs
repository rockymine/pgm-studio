using PgmStudio.Geom;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>WL7: a team's wools sit well apart. Distance is <b>rectilinear traversal over the walkable surface</b>
/// (terrain + build cells), not straight-line: a 4-connected shortest path routes around voids and hugs the
/// shapes' borders, so two wools a short hop apart but separated by a gap read as far apart — the real "how far a
/// player travels between them" (and correspondingly higher than a straight-line reading of WL7's ~45). Measured
/// as the smallest such traversal distance over the team's wool pairs; only applies with two or more wools. Draws
/// the offending pair and the route between them.</summary>
public sealed class WoolWoolDistance : SoftTerm
{
    public override string Id => "wool-wool-distance";
    public override string RuleId => "WL7";

    public override double? Value(EvalContext ctx) => Closest(ctx).Blocks;

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Wools.Select(w => w.Piece).ToList();

    protected override IReadOnlyList<Evidence> Evidence(EvalContext ctx, double value, Band band)
    {
        var (_, a, b) = Closest(ctx);
        return a is null || b is null
            ? []
            : SurfaceNav.RouteEvidence(SurfaceNav.Walkable(ctx), a.Value, b.Value, $"{value:0} < {band.Lo:0}");
    }

    // The closest wool pair by surface traversal, in blocks, and its endpoint cells.
    private static (double? Blocks, (int, int)? A, (int, int)? B) Closest(EvalContext ctx)
    {
        var walkable = SurfaceNav.Walkable(ctx);
        var cell = ctx.Plan.Globals.Cell;
        var wools = ctx.Plan.Placements.Wools
            .Select(w => SurfaceNav.MarkerCell(ctx, w.Piece, w.At, walkable))
            .Where(c => c is not null).Select(c => c!.Value).ToList();
        if (wools.Count < 2) return (null, null, null);

        double? best = null;
        (int, int)? ba = null, bb = null;
        for (var i = 0; i < wools.Count; i++)
            for (var j = i + 1; j < wools.Count; j++)
                if (Cells.PathLength(wools[i], wools[j], walkable) is { } steps && steps * (double)cell < (best ?? double.MaxValue))
                    { best = steps * cell; ba = wools[i]; bb = wools[j]; }
        return (best, ba, bb);
    }
}
