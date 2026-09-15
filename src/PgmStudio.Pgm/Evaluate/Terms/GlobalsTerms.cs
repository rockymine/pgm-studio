namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>The share of the board's footprint that is land — filled land cells over the bounding box of all
/// land + build. The authored corpus sits in a middle band (roughly a third to three-fifths land); a board far
/// denser or sparser than the seeds reads wrong. A global scalar, so no single geometry to point at.
///
/// <para><b>It measures a wool board, and answers for no other kind</b> (the author's ruling). The band is
/// what capture-the-wool geometry is: a closure with lanes through it, technical voids between them, and a
/// strait a raider crosses — the ratio is that shape stated as one number. A destroy board is not built that
/// way. It is squarer, it has no technical voids, and the land it does not fill is the map's edge rather than
/// a device, so the same number carries no judgement about it — it just reads dense and always will. A plan
/// with no wool in it therefore measures nothing here, which is the same silence a wool-spacing term keeps on
/// a single-wool plan, and the same silence keeps the band honest: the envelope generator learns it from the
/// boards it applies to and from no others.</para></summary>
public sealed class FillRatio : SoftTerm
{
    public override string Id => "fill-ratio";
    public override string RuleId => "G8";

    /// <summary>Ground over the frame the ground itself occupies. Both halves are the terrain and only the
    /// terrain: a build zone is buildable void rather than land, so it neither fills a cell nor widens the
    /// frame, and what the board does not fill — its own margin, and every void a buffer or a cut declares —
    /// is what the ratio is measuring. Nothing stamped on the terrain counts either; a house is not board.</summary>
    public override double? Value(EvalContext ctx)
    {
        if (ctx.Plan.Placements.Wools.Count == 0) return null;
        var ground = ctx.Board.Filled.Keys.ToList();
        if (ground.Count == 0) return null;
        double w = ground.Max(cell => cell.Item1) - ground.Min(cell => cell.Item1) + 1;
        double h = ground.Max(cell => cell.Item2) - ground.Min(cell => cell.Item2) + 1;
        return ground.Count / (w * h);
    }
}

/// <summary>CT8: the closure encloses internal void pockets — holes — as the player-rotation device, ~1 per team
/// side by default (2–13 across the fanned seeds). A board with far more or fewer enclosed voids than the
/// authored distribution reads wrong: none is the flat-arena exception, a great many is Swiss cheese. Counts the
/// enclosed voids the deriver classifies; a global scalar, so no single hole to point at.</summary>
public sealed class EnclosedVoidCount : SoftTerm
{
    public override string Id => "enclosed-void-count";
    public override string RuleId => "CT8";

    public override double? Value(EvalContext ctx) => ctx.Board.Voids.Count;
}

/// <summary>G5: every void gap a build region spans between two individual landmasses is a 10..20-block hop. The
/// geometric crossing/bridge constructions guarantee the designed hops; this catches any incidental link the
/// fanned zones create (and a zero-length weld, which is not a legal hop either). Reads the gap links off the
/// contact graph — no shape names.</summary>
public sealed class GapHopBand : ILayoutTerm
{
    public const int MinHop = 10;
    public const int MaxHop = 20;

    public string Id => "gap-hop-band";
    public string RuleId => "G5";
    public TermKind Kind => TermKind.Hard;

    public TermScore Measure(EvalContext ctx)
    {
        foreach (var g in ctx.Contacts.GapLinks)
            if (g.Hop < MinHop || g.Hop > MaxHop)
            {
                var evidence = TermEvidence.OffenderRects(ctx.Plan, [g.A, g.B]);
                if (TermEvidence.Locate(ctx.Plan, g.A) is { } ra && TermEvidence.Locate(ctx.Plan, g.B) is { } rb)
                {
                    var (ax, az) = TermEvidence.Center(ra);
                    var (bx, bz) = TermEvidence.Center(rb);
                    evidence.Add(Ev.Measure(ax, az, bx, bz, $"hop {g.Hop} (band {MinHop}..{MaxHop})"));
                }
                return TermScores.Violated(this,
                    $"gap hop {g.Hop} outside {MinHop}..{MaxHop} between '{g.A}' and '{g.B}'", [g.A, g.B], evidence);
            }
        return TermScores.Clean(this);
    }
}

/// <summary>The share of a board's ground that <b>no journey reaches</b> — reachable, and on the way to
/// nothing. Every pair of places the board has claims a corridor and every place claims a ring around itself;
/// what neither covers is ground a player walks past at most and stands on never.
///
/// <para>It reads the same concern <c>G8</c> states from the other side. Land per player says how much board
/// there is for the people on it; this says how much of that board the match actually spends, and a high
/// share is a board bigger than the thing it plays rather than a board with a big number in its globals.</para>
///
/// <para>A global scalar, so there is no one rectangle to point at — <c>PlanFlow</c>'s own read names the
/// patches with their coordinates, which is what an author acts on. Null where the plan states no objective,
/// since a board with nowhere to go has no journey to be off.</para></summary>
public sealed class DeadShare : SoftTerm
{
    public override string Id => "dead-share";
    public override string RuleId => "G8";

    public override double? Value(EvalContext ctx) =>
        ctx.Flow.Gamemode == "none" || ctx.Flow.GroundBlocks == 0 ? null : ctx.Flow.DeadShare;
}

/// <summary>How much of the ground a defence crosses to reach an objective is ground the attack is already
/// on — the share of the defender's corridor the attacker's corridor also covers, averaged over the board's
/// objectives. Both ribbons are walked over the ground their own side has, at the detour tolerance the
/// coverage read uses, from each side's own spawn.
///
/// <para><c>CT8</c> claims a hole gives alternative routes between lanes; counting those routes does not say
/// whether taking one buys anything, and this does. A board whose ways in all collide with the reinforcement
/// lane offers no approach that misses it, whatever its route count reads; a board where the share falls is
/// one on which going round is worth its distance. A global scalar, so there is no single rectangle to point
/// at — <c>PlanFlow</c>'s own read gives it per objective, which is what an author acts on.</para>
///
/// <para>Null where the plan states no leg to read: both spawns and an enemy wool are needed before two
/// routes can be laid over each other, and a board with one route has no collision rather than a collision
/// of zero.</para></summary>
public sealed class RouteInterference : SoftTerm
{
    public override string Id => "route-interference";
    public override string RuleId => "CT8";

    public override double? Value(EvalContext ctx) =>
        ctx.Flow.Legs.Count == 0 ? null : ctx.Flow.Interference;
}
