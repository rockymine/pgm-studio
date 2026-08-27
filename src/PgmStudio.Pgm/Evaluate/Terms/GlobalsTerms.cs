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

    public override double? Value(EvalContext ctx)
    {
        if (ctx.Plan.Placements.Wools.Count == 0) return null;
        var board = ctx.Board;
        var cells = board.Filled.Keys.Concat(board.Build).ToList();
        if (cells.Count == 0) return null;
        double w = cells.Max(c => c.Item1) - cells.Min(c => c.Item1) + 1;
        double h = cells.Max(c => c.Item2) - cells.Min(c => c.Item2) + 1;
        return board.Filled.Count / (w * h);
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
