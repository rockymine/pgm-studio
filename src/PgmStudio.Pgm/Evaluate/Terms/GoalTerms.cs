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
