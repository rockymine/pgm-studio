namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>SP10: a spawn stands at least <see cref="MinBlocks"/> blocks <b>by the walk</b> from the front-front
/// build band (<see cref="Triangle.SpawnFrontDistances"/>). A spawn nearer than that walks straight out onto
/// the crossing, so its team defends from the frontline rather than from its own ground. A composer floor:
/// <see cref="EvaluationProfile.Default"/> leaves it off, because an author may seat a spawn anywhere.</summary>
public sealed class SpawnFrontFloor : ILayoutTerm
{
    /// <summary>The author's floor, set off judged boards: every spawn called too close walked 43 blocks or
    /// less to the band on this read, and every one accepted 58 or more.</summary>
    public const int MinBlocks = 55;

    public string Id => "spawn-front-floor";
    public string RuleId => "SP10";
    public TermKind Kind => TermKind.Hard;

    public TermScore Measure(EvalContext ctx)
    {
        var distances = Triangle.SpawnFrontDistances(ctx).Where(d => d is not null).Select(d => d!.Value).ToList();
        if (distances.Count == 0 || distances.Min() >= MinBlocks) return TermScores.Clean(this);
        return TermScores.Violated(this, $"spawn↔band traversal {distances.Min():0} < {MinBlocks} blocks",
            ctx.Plan.Placements.Spawns.Select(s => s.Piece).Distinct().ToList(), []);
    }
}

/// <summary>WL10 as a hard floor: every wool stands at least <see cref="MinBlocks"/> blocks <b>by the walk</b>
/// from the front-front build band — the same read as <see cref="WoolFrontDistance"/>, taken at the most
/// exposed wool. A composer floor, off in <see cref="EvaluationProfile.Default"/>.</summary>
public sealed class WoolFrontFloor : ILayoutTerm
{
    /// <summary>The author's floor, set off judged boards: every wool called too close to the frontline walked
    /// 58 blocks or less to the band on this read, and the nearest one called fine 59.</summary>
    public const int MinBlocks = 59;

    public string Id => "wool-front-floor";
    public string RuleId => "WL10";
    public TermKind Kind => TermKind.Hard;

    public TermScore Measure(EvalContext ctx)
    {
        var distances = Triangle.FrontDistances(ctx).Where(d => d is not null).Select(d => d!.Value).ToList();
        if (distances.Count == 0 || distances.Min() >= MinBlocks) return TermScores.Clean(this);
        return TermScores.Violated(this, $"wool↔band traversal {distances.Min():0} < {MinBlocks} blocks",
            ctx.Plan.Placements.Wools.Select(w => w.Piece).Distinct().ToList(), []);
    }
}
