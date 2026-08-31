using PgmStudio.Geom;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>WL2: spawn and wool sit apart. Distance is <b>the walk over the walkable surface</b>
/// (terrain + build cells), not straight-line — the same measure as <see cref="WoolWoolDistance"/> — so it is
/// how far a player actually travels from spawn to the objective, routing around voids. Measured as the smallest
/// spawn→wool traversal (the nearest wool to spawn); only applies when both a spawn and a wool exist. Draws the
/// spawn, the nearest wool, and the route between them.</summary>
public sealed class SpawnWoolDistance : SoftTerm
{
    public override string Id => "spawn-wool-distance";
    public override string RuleId => "WL2";

    public override double? Value(EvalContext ctx) => Closest(ctx).Blocks;

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Spawns.Select(s => s.Piece)
            .Concat(ctx.Plan.Placements.Wools.Select(w => w.Piece)).Distinct().ToList();

    protected override IReadOnlyList<Evidence> Evidence(EvalContext ctx, double value, Band band)
    {
        var (_, a, b) = Closest(ctx);
        return a is null || b is null
            ? []
            : SurfaceNav.RouteEvidence(SurfaceNav.Ground(ctx), a.Value, b.Value, $"{value:0} < {band.Lo:0}");
    }

    // The nearest spawn→wool pair by surface traversal, in blocks, and its endpoint cells.
    internal static (double? Blocks, (int, int)? A, (int, int)? B) Closest(EvalContext ctx)
    {
        var ground = SurfaceNav.Ground(ctx);
        var spawns = ctx.Plan.Placements.Spawns
            .Select(s => SurfaceNav.MarkerCell(ctx, s.Piece, s.At, ground.Footprint))
            .Where(c => c is not null).Select(c => c!.Value).ToList();
        var wools = ctx.Plan.Placements.Wools
            .Select(w => SurfaceNav.MarkerCell(ctx, w.Piece, w.At, ground.Footprint))
            .Where(c => c is not null).Select(c => c!.Value).ToList();
        if (spawns.Count == 0 || wools.Count == 0) return (null, null, null);

        double? best = null;
        (int, int)? ba = null, bb = null;
        foreach (var s in spawns)
            foreach (var w in wools)
                if (ground.Stand(s) is { } from && ground.Stand(w) is { } to
                    && Walk.Between(from, to, ground) is { } walked
                    && walked.Cost.Distance < (best ?? double.MaxValue))
                    { best = walked.Cost.Distance; ba = s; bb = w; }
        return (best, ba, bb);
    }
}

/// <summary>WL2 as a hard floor: the spawn and its nearest wool must be at least <see cref="MinBlocks"/> blocks
/// apart <b>by the walk</b> (the same measure as <see cref="SpawnWoolDistance"/>, not
/// straight-line). The generator clears it comfortably — the authored floor is ~30 by traversal — so it costs
/// nothing today; the surface measure is the correct one to guard against a generator cramming a wool onto
/// spawn, which a Euclidean gate would let through wherever the walk round is long.</summary>
public sealed class SpawnWoolFloor : ILayoutTerm
{
    /// <summary>WL2's 20 blocks, now read as real travel rather than straight-line.</summary>
    public const int MinBlocks = 20;

    public string Id => "spawn-wool-floor";
    public string RuleId => "WL2";
    public TermKind Kind => TermKind.Hard;

    public TermScore Measure(EvalContext ctx)
    {
        var (blocks, a, b) = SpawnWoolDistance.Closest(ctx);
        if (blocks is null || blocks.Value >= MinBlocks) return TermScores.Clean(this);

        var evidence = a is null || b is null
            ? (IReadOnlyList<Evidence>)[]
            : SurfaceNav.RouteEvidence(SurfaceNav.Ground(ctx), a.Value, b.Value, $"{blocks:0} < {MinBlocks}");
        var subjects = ctx.Plan.Placements.Spawns.Select(s => s.Piece)
            .Concat(ctx.Plan.Placements.Wools.Select(w => w.Piece)).Distinct().ToList();
        return TermScores.Violated(this, $"spawn↔wool traversal {blocks:0} < {MinBlocks} blocks", subjects, evidence);
    }
}
