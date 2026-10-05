using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>WL7: a team's wools sit well apart. Distance is <b>the walk over the walkable surface</b>
/// (terrain + build cells), not straight-line: the route goes round a void rather than over it, so two wools a
/// short hop apart but separated by a gap read as far apart — the real "how far a player travels between them"
/// (and correspondingly higher than a straight-line reading of WL7's ~45). Measured
/// as the smallest such traversal distance over the team's wool pairs; only applies with two or more wools. Draws
/// the offending pair and the route between them.</summary>
public sealed class WoolWoolDistance : SoftTerm
{
    public override string Id => "wool-wool-distance";
    public override string RuleId => LayoutRules.WoolWoolDistance;

    public override double? Value(EvalContext ctx) => Closest(ctx).Blocks;

    public override MeasureUnit Unit => MeasureUnit.Blocks;

    protected override string Reads(EvalContext ctx, string value)
    {
        var closest = Closest(ctx);
        return $"wools '{closest.First}' and '{closest.Second}' have a walking distance of {value} between them";
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Wools.Select(w => w.Piece).ToList();

    protected override IReadOnlyList<Evidence> Evidence(EvalContext ctx, double value, Band band)
    {
        var (_, a, b, _, _) = Closest(ctx);
        return a is null || b is null
            ? []
            : SurfaceNav.RouteEvidence(SurfaceNav.Ground(ctx), a.Value, b.Value, $"{value:0} < {band.Lo:0}");
    }

    // The closest wool pair by surface traversal, in blocks, its endpoint cells and the two wools' ids.
    private static (double? Blocks, (int, int)? A, (int, int)? B, string First, string Second) Closest(EvalContext ctx)
    {
        var ground = SurfaceNav.Ground(ctx);
        var wools = ctx.Plan.Placements.Wools
            .Select(w => (w.Id, Cell: SurfaceNav.MarkerCell(ctx, w.Piece, w.At, ground.Footprint)))
            .Where(w => w.Cell is not null).Select(w => (w.Id, Cell: w.Cell!.Value)).ToList();
        if (wools.Count < 2) return (null, null, null, "", "");

        double? best = null;
        (int, int)? ba = null, bb = null;
        string first = "", second = "";
        for (var i = 0; i < wools.Count; i++)
            for (var j = i + 1; j < wools.Count; j++)
                if (ground.Stand(wools[i].Cell) is { } from && ground.Stand(wools[j].Cell) is { } to
                    && Walk.Between(from, to, ground) is { } walked
                    && walked.Cost.Distance < (best ?? double.MaxValue))
                    { best = walked.Cost.Distance; ba = wools[i].Cell; bb = wools[j].Cell; first = wools[i].Id; second = wools[j].Id; }
        return (best, ba, bb, first, second);
    }
}
