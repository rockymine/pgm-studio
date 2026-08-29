using PgmStudio.Pgm.Derive;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>GO1: a destroy goal's spawn asymmetry — the walk from the enemy's spawn over the walk from its
/// own, per goal, with the worst-offending goal's ratio scored against the authored band <b>[3, 4]</b>. The
/// band is the author's ruling calibrated on the two shipped boards (their ratios read 3.0 and 3.9 by walk):
/// under it the goal sits too near the attacker and falls to a rush; over it the attack is a march across
/// the whole board into a set defence. Distances are <see cref="GoalDistances"/>'s — the fanned-closure
/// walk, never the straight line — and a goal with no route at all is the traversability gate's refusal
/// rather than this term's, so it contributes nothing here.</summary>
public sealed class GoalSpawnRatio : SoftTerm
{
    public override string Id => "goal-spawn-ratio";
    public override string RuleId => "GO1";
    public override bool LearnsFromTraced => false;
    public override Band? AuthoredBand => new Band(3.0, 4.0);

    public override double? Value(EvalContext ctx)
    {
        var ratios = GoalDistances.Read(ctx.Plan)
            .Where(goal => goal.Ratio is not null)
            .Select(goal => goal.Ratio!.Value)
            .ToList();
        if (ratios.Count == 0) return null;

        // Every in-band ratio scores zero, so the term's one value is the goal the band judges hardest.
        var band = AuthoredBand!.Value;
        return ratios.OrderByDescending(band.Distance).First();
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Destroyables.Select(goal => goal.Piece)
            .Concat(ctx.Plan.Placements.Cores.Select(goal => goal.Piece))
            .Concat(ctx.Plan.Placements.Spawns.Select(spawn => spawn.Piece))
            .Where(piece => piece.Length > 0)
            .Distinct()
            .ToList();
}

/// <summary>GO4: a destroy goal's distance from its own spawn, by walk, held to the authored band
/// <b>[40, 90]</b>. The same own-spawn leg <see cref="GoalSpawnRatio"/> divides by is judged here on its own:
/// under the band the spawn already stands on the goal, over it the spawn cannot reinforce before the goal
/// falls. A goal with no route contributes nothing — that is the traversability gate's refusal.</summary>
public sealed class GoalSpawnDistance : SoftTerm
{
    public override string Id => "goal-spawn-distance";
    public override string RuleId => "GO4";
    public override bool LearnsFromTraced => false;
    public override Band? AuthoredBand => new Band(40, 90);

    public override double? Value(EvalContext ctx)
    {
        var distances = GoalDistances.Read(ctx.Plan)
            .Where(goal => goal.OwnSpawnBlocks is not null)
            .Select(goal => goal.OwnSpawnBlocks!.Value)
            .ToList();
        if (distances.Count == 0) return null;

        // Every in-band distance scores zero, so the term's one value is the goal the band judges hardest.
        var band = AuthoredBand!.Value;
        return distances.OrderByDescending(band.Distance).First();
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Destroyables.Select(goal => goal.Piece)
            .Concat(ctx.Plan.Placements.Cores.Select(goal => goal.Piece))
            .Concat(ctx.Plan.Placements.Spawns.Select(spawn => spawn.Piece))
            .Where(piece => piece.Length > 0)
            .Distinct()
            .ToList();
}
/// <summary>GO2: how far a team's own destroy goals stand apart, by walk, held to the authored band
/// <b>[35, 65]</b>. Two goals a team defends from one position are one goal with two names; two so far apart
/// that no position covers both make the defence a shuttle rather than a stand. It is the destroy-side
/// counterpart of <c>WL7</c>, which separates a team's wools. A board carrying one goal per team has no such
/// pair and the term is silent.</summary>
public sealed class OwnGoalDistance : GoalPairTerm
{
    public override string Id => "own-goal-distance";
    public override string RuleId => "GO2";
    public override Band? AuthoredBand => new Band(35, 65);
    protected override bool Opposing => false;
}

/// <summary>GO3: how far opposing destroy goals stand apart, by walk, held to the authored band
/// <b>[85, 150]</b> — what the contest spans. Under the band a rush reaches one objective before the other
/// team has formed; over it the attacker crosses a board's width into a set defence and the match settles
/// into a stalemate. The pair every symmetric board carries is a monument against its own mirror.</summary>
public sealed class OpposingGoalDistance : GoalPairTerm
{
    public override string Id => "opposing-goal-distance";
    public override string RuleId => "GO3";
    public override Band? AuthoredBand => new Band(85, 150);
    protected override bool Opposing => true;
}

/// <summary>What <c>GO2</c> and <c>GO3</c> share: both read <see cref="GoalDistances.Pairs"/> and score the
/// pair their band judges hardest, and they differ only in which half of that list they take. The half is
/// the whole difference, so it is what the subclass states and nothing else is.</summary>
public abstract class GoalPairTerm : SoftTerm
{
    public override bool LearnsFromTraced => false;

    /// <summary>Which pairs this term is about: the goals one team defends, or a goal against one the other
    /// team defends.</summary>
    protected abstract bool Opposing { get; }

    public override double? Value(EvalContext ctx)
    {
        var walks = GoalDistances.Pairs(ctx.Plan)
            .Where(pair => pair.Opposing == Opposing && pair.Blocks is not null)
            .Select(pair => pair.Blocks!.Value)
            .ToList();
        if (walks.Count == 0) return null;

        // Every in-band walk scores zero, so the term's one value is the pair the band judges hardest.
        var band = AuthoredBand!.Value;
        return walks.OrderByDescending(band.Distance).First();
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Destroyables.Select(goal => goal.Piece)
            .Concat(ctx.Plan.Placements.Cores.Select(goal => goal.Piece))
            .Where(piece => piece.Length > 0)
            .Distinct()
            .ToList();
}
