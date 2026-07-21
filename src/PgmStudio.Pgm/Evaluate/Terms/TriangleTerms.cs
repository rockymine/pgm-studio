using PgmStudio.Geom;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>WL9: a team's wools sit <b>comparably far from the spawn</b>. The metric is the spread (max −
/// min) of the per-wool spawn→wool surface-traversal distances, in blocks — the same rectilinear measure as
/// <see cref="SpawnWoolDistance"/>. A large spread means one wool is trivially defended (spawn on its
/// doorstep) while another is left to fend for itself. Applies only with two or more reachable wools.</summary>
public sealed class SpawnWoolSpread : SoftTerm
{
    public override string Id => "spawn-wool-spread";
    public override string RuleId => "WL9";

    public override double? Value(EvalContext ctx)
    {
        var d = Triangle.SpawnDistances(ctx).Where(v => v is not null).Select(v => v!.Value).ToList();
        return d.Count < 2 ? null : d.Max() - d.Min();
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Spawns.Select(s => s.Piece)
            .Concat(ctx.Plan.Placements.Wools.Select(w => w.Piece)).Distinct().ToList();
}

/// <summary>WL10 (distance half): how far the <b>most exposed wool</b> sits from the frontline edge — the
/// seam where the mid build band meets the land, the line an attacker crosses onto the team side. Measured
/// as the smallest per-wool surface traversal to a band cell, in blocks. Dormant when the plan carries no
/// mid band.</summary>
public sealed class WoolFrontDistance : SoftTerm
{
    public override string Id => "wool-front-distance";
    public override string RuleId => "WL10";

    public override double? Value(EvalContext ctx)
    {
        var d = Triangle.FrontDistances(ctx).Where(v => v is not null).Select(v => v!.Value).ToList();
        return d.Count == 0 ? null : d.Min();
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Wools.Select(w => w.Piece).ToList();
}

/// <summary>WL10 (triangle half): the spawn–wool–frontline <b>triangle stays balanced across a team's
/// wools</b>. Per wool, the <i>defence deficit</i> is its spawn distance minus its frontline distance —
/// roughly how much farther the defender travels than the attacker. The metric is the spread (max − min) of
/// the deficits, in blocks: a front-near wool with a far spawn (free to capture) beside a back wool with the
/// spawn on its doorstep (trivially defended) reads as a large spread. Applies only with two or more wools
/// carrying both distances.</summary>
public sealed class WoolFrontBalance : SoftTerm
{
    public override string Id => "wool-front-balance";
    public override string RuleId => "WL10";

    public override double? Value(EvalContext ctx)
    {
        var spawn = Triangle.SpawnDistances(ctx);
        var front = Triangle.FrontDistances(ctx);
        var deficits = spawn.Zip(front, (s, f) => s is not null && f is not null ? s.Value - f.Value : (double?)null)
            .Where(v => v is not null).Select(v => v!.Value).ToList();
        return deficits.Count < 2 ? null : deficits.Max() - deficits.Min();
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Spawns.Select(s => s.Piece)
            .Concat(ctx.Plan.Placements.Wools.Select(w => w.Piece)).Distinct().ToList();
}

/// <summary>WL9 (factor half): the <b>size-independent</b> spawn↔wool imbalance — the ratio (max ÷ min) of the
/// per-wool spawn distances. A 40-vs-105 pair on a big board and a 20-vs-52 pair on a small one read the same
/// 2.6× factor. An authored cap (<see cref="SoftTerm.LearnsFromTraced"/> false): the intent seeds teach the
/// tolerable factor; the traced real maps do not get to widen it. Applies with two or more reachable
/// wools.</summary>
public sealed class SpawnWoolRatio : SoftTerm
{
    public override string Id => "spawn-wool-ratio";
    public override string RuleId => "WL9";
    public override bool LearnsFromTraced => false;

    public override double? Value(EvalContext ctx)
    {
        var d = Triangle.SpawnDistances(ctx).Where(v => v is not null).Select(v => v!.Value).ToList();
        return d.Count < 2 || d.Min() <= 0 ? null : d.Max() / d.Min();
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Spawns.Select(s => s.Piece)
            .Concat(ctx.Plan.Placements.Wools.Select(w => w.Piece)).Distinct().ToList();
}

/// <summary>WL10 (factor half): the <b>size-independent</b> wool↔frontline imbalance — the ratio (max ÷ min)
/// of the per-wool frontline distances. Catches the equal-spawn-but-unequal-front boards (one wool hugging the
/// front while its sibling hides at the back) at any board size. An authored cap
/// (<see cref="SoftTerm.LearnsFromTraced"/> false). Applies with two or more wools carrying a front
/// distance.</summary>
public sealed class WoolFrontRatio : SoftTerm
{
    public override string Id => "wool-front-ratio";
    public override string RuleId => "WL10";
    public override bool LearnsFromTraced => false;

    public override double? Value(EvalContext ctx)
    {
        var d = Triangle.FrontDistances(ctx).Where(v => v is not null).Select(v => v!.Value).ToList();
        return d.Count < 2 || d.Min() <= 0 ? null : d.Max() / d.Min();
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Wools.Select(w => w.Piece).ToList();
}

/// <summary>WL10 (remoteness half): how far the <b>most remote wool</b> sits from the frontline edge — the
/// stalemate signature the balance terms are blind to: a wool far from the front <i>and</i> far from
/// everything (its deficits can read perfectly balanced) forces the attacker to run the whole board into a
/// defended chokepoint. Measured as the largest per-wool front distance, in blocks; an authored cap
/// (<see cref="SoftTerm.LearnsFromTraced"/> false). Applies to any wool count.</summary>
public sealed class WoolFrontRemoteness : SoftTerm
{
    public override string Id => "wool-front-remoteness";
    public override string RuleId => "WL10";
    public override bool LearnsFromTraced => false;

    public override double? Value(EvalContext ctx)
    {
        var d = Triangle.FrontDistances(ctx).Where(v => v is not null).Select(v => v!.Value).ToList();
        return d.Count == 0 ? null : d.Max();
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Wools.Select(w => w.Piece).ToList();
}

/// <summary>The shared per-wool distance reads of the spawn–wool–frontline triangle, all by rectilinear
/// surface traversal (the walkable set: pieces + zones) in blocks, index-aligned with
/// <c>Plan.Placements.Wools</c> (<c>null</c> where a wool is unreachable or the target is absent).</summary>
internal static class Triangle
{
    /// <summary>Per wool: the traversal distance from the nearest spawn, in blocks.</summary>
    public static List<double?> SpawnDistances(EvalContext ctx)
    {
        var walkable = SurfaceNav.Walkable(ctx);
        var cell = ctx.Plan.Globals.Cell;
        var spawns = ctx.Plan.Placements.Spawns
            .Select(s => SurfaceNav.MarkerCell(ctx, s.Piece, s.At, walkable))
            .Where(c => c is not null).Select(c => c!.Value).ToList();
        return ctx.Plan.Placements.Wools.Select(w =>
        {
            if (spawns.Count == 0 || SurfaceNav.MarkerCell(ctx, w.Piece, w.At, walkable) is not { } wc) return (double?)null;
            double? best = null;
            foreach (var s in spawns)
                if (Cells.PathLength(s, wc, walkable) is { } steps && steps * (double)cell < (best ?? double.MaxValue))
                    best = steps * cell;
            return best;
        }).ToList();
    }

    /// <summary>Per wool: the traversal distance to the nearest <b>front-front build cell</b> — the mid band
    /// as the derived board reads it (the build region linking the two team fronts, whatever its plan zone is
    /// named), the seam an attacker crosses — in blocks. Null where the board carries no such band or the wool
    /// cannot reach it.</summary>
    public static List<double?> FrontDistances(EvalContext ctx)
    {
        var walkable = SurfaceNav.Walkable(ctx);
        var cell = ctx.Plan.Globals.Cell;
        var band = ctx.Board.BuildKindOf.Where(kv => kv.Value == "front-front").Select(kv => kv.Key).ToHashSet();
        return ctx.Plan.Placements.Wools.Select(w =>
        {
            if (band.Count == 0 || SurfaceNav.MarkerCell(ctx, w.Piece, w.At, walkable) is not { } wc) return (double?)null;
            return ToSet(wc, band, walkable) is { } steps ? steps * (double)cell : null;
        }).ToList();
    }

    // breadth-first over the walkable surface from `start` to the nearest cell of `targets`, in steps
    private static int? ToSet((int, int) start, HashSet<(int, int)> targets, HashSet<(int, int)> walkable)
    {
        if (!walkable.Contains(start)) return null;
        if (targets.Contains(start)) return 0;
        var seen = new HashSet<(int, int)> { start };
        var q = new Queue<((int X, int Z) C, int D)>();
        q.Enqueue((start, 0));
        while (q.Count > 0)
        {
            var ((x, z), d) = q.Dequeue();
            foreach (var n in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            {
                if (!walkable.Contains(n) || !seen.Add(n)) continue;
                if (targets.Contains(n)) return d + 1;
                q.Enqueue((n, d + 1));
            }
        }
        return null;
    }
}
