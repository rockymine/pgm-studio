using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The measures a board's structure answers, read off the ground the sketch builds at the corpus's cell: front
/// faces per team and the widest of them (<c>FR4</c>, <c>FR6</c>), the stepping stones, crossings, cuts and holes
/// of the middle (<c>CT1</c>, <c>CT4</c>, <c>CT5</c>, <c>CT8</c>, <c>CT9</c>, <c>CT13</c>) and the narrowest lane to
/// a wool room (<c>LN1</c>). Each is the evaluator's own term, held to the band the corpus taught it, and a
/// value outside its band is said as a complaint carrying the number. Nothing is summed: a measure describes
/// how the board will play, and the author weighs it.
/// </summary>
public static class SketchMeasures
{
    /// <summary>The cell the corpus's bands were measured at, in blocks, which the sketch's ground is read at for
    /// a count or a width to mean what the band means.</summary>
    public const int CorpusCell = 5;

    /// <summary>The terms asked, each reading the board's structure and nothing of a plan.</summary>
    public static readonly IReadOnlyList<SoftTerm> Terms =
    [
        new FrontlineCount(), new FrontlineWidth(), new BandCount(), new NeutralSteppingCount(), new TeamSteppingCount(),
        new IsolationCutCount(), new EnclosedVoidCount(), new UncrossedMiddleVoid(), new LaneWidth(),
    ];

    /// <summary>What the board the layout and the intent build measures outside its bands; nothing where it builds
    /// no ground.</summary>
    public static Findings Check(string layoutJson, MapIntent intent, SymmetryIntent? board = null)
    {
        if (SketchGround.Of(layoutJson, intent, CorpusCell) is not { } ground) return Findings.None;
        BoardStructure structure;
        try { structure = BoardDeriver.Derive(ground); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        { return Findings.None; }
        var context = EvalContext.OfBoard(structure, Math.Max(1, IntentCheck.Teams(intent, board)),
            new BoardGoals((intent.Wools ?? []).Count, (intent.Destroyables ?? []).Count, (intent.Cores ?? []).Count),
            SeedEnvelopes.Default);
        return new Findings([
            .. Terms.Select(term => term.Measure(context).Violation).OfType<Violation>().Select(violation => violation.Finding),
        ]);
    }
}
