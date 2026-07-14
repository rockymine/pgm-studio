namespace PgmStudio.Pgm.Evaluate.Terms;

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
