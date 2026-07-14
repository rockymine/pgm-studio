using PgmStudio.Geom;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>CT4: contested mid islands — the neutral stepping stones a second team can also reach. Counted per
/// team (the fanned total over the orbit order) so a 2-team and a 4-team board are comparable. The specific
/// half of the retired island-count: a board scattered with more contested stones than the authored norm reads
/// over-fragmented.</summary>
public sealed class NeutralSteppingCount : SoftTerm
{
    public override string Id => "neutral-stepping-count";
    public override string RuleId => "CT4";

    public override double? Value(EvalContext ctx) =>
        ctx.Board.SteppingKind.Count(k => k == "neutral") / (double)Symmetry.Order(ctx.Plan.Globals.Symmetry);
}

/// <summary>CT4: a team's own captive movement stones — the transient-link pads on its internal spawn↔wool route
/// that no enemy can flank (CT4's team transient-link, the WL4/SP6 bridge pads). Per team (fanned ÷ order).
/// Rare in the corpus, so the band is tight: a generator staking captive stones stands out.</summary>
public sealed class TeamSteppingCount : SoftTerm
{
    public override string Id => "team-stepping-count";
    public override string RuleId => "CT4";

    public override double? Value(EvalContext ctx) =>
        ctx.Board.SteppingKind.Count(k => k == "team") / (double)Symmetry.Order(ctx.Plan.Globals.Symmetry);
}

/// <summary>CT1: how many team↔team crossings the mid presents — the front-front build bands. One is the
/// channelled mid, two or more are parallel approaches, none is a hash (all flow directed through the mid
/// islands). A shared mid property, so the fanned count, not per-team — it tracks how many routes cross the
/// interface.</summary>
public sealed class BandCount : SoftTerm
{
    public override string Id => "band-count";
    public override string RuleId => "CT1";

    public override double? Value(EvalContext ctx) => ctx.Board.Zones.Count(z => z.Kind == "front-front");
}

/// <summary>CT5: how much a team cuts its own side — the intra/self isolation cuts (a piece severed from its
/// parent and bridged back across a slow-down gap: the isolated wool WL4, the isolated spawn SP6). Counted per
/// team (fanned ÷ order). A team side over- or under-cut relative to the authored norm reads wrong.</summary>
public sealed class IsolationCutCount : SoftTerm
{
    public override string Id => "isolation-cut-count";
    public override string RuleId => "CT5";

    public override double? Value(EvalContext ctx) =>
        ctx.Board.Zones.Count(z => z.Kind is "intra" or "self") / (double)Symmetry.Order(ctx.Plan.Globals.Symmetry);
}
