using PgmStudio.Domain;
using PgmStudio.Geom;
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
/// a wool room (<c>LN1</c>), the walks between its markers (<c>WL</c>, <c>GO</c>) and the band between the fronts
/// (<c>MD7</c>, <c>MD8</c>). Each is the evaluator's own term, held to the band the corpus taught it, and a
/// value outside its band is said as a complaint carrying the number. Nothing is summed: a measure describes
/// how the board will play, and the author weighs it.
/// </summary>
public static class SketchMeasures
{
    /// <summary>The cell the corpus's bands were measured at, in blocks, which the sketch's ground is read at for
    /// a count or a width to mean what the band means.</summary>
    public const int CorpusCell = 5;

    /// <summary>The terms asked: those reading the board's structure, and those walking a team's own ground or the
    /// fanned closure between its markers, and nothing of a plan.</summary>
    public static readonly IReadOnlyList<ILayoutTerm> Terms =
    [
        new FrontlineCount(), new FrontlineWidth(), new BandCount(), new NeutralSteppingCount(), new TeamSteppingCount(),
        new IsolationCutCount(), new EnclosedVoidCount(), new UncrossedMiddleVoid(), new LaneWidth(),
        new WoolWoolDistance(), new SpawnWoolDistance(), new SpawnWoolSpread(), new WoolFrontDistance(),
        new WoolFrontBalance(), new SpawnWoolRatio(), new WoolFrontRatio(), new WoolFrontRemoteness(),
        new GoalSpawnRatio(), new OwnGoalDistance(), new OpposingGoalDistance(), new GoalSpawnDistance(),
        new ThinMiddle(), new LongMiddle(),
    ];

    /// <summary>What the board the layout and the intent build measures outside its bands; nothing where it builds
    /// no ground.</summary>
    public static Findings Check(string layoutJson, MapIntent intent, SymmetryIntent? board = null)
    {
        if (Context(layoutJson, intent, board) is not { } context) return Findings.None;
        return new Findings([
            .. Terms.Select(term => term.Measure(context).Violation).OfType<Violation>().Select(violation => violation.Finding),
        ]);
    }

    /// <summary>The context the terms read the sketch's board in: its structure at the corpus's cell, the team's
    /// own ground and the build cells with the authored spawns and wools seated on them, and on a two-team board
    /// the fanned closure with each goal and the other team's copy of it. Null where the layout builds nothing.</summary>
    public static EvalContext? Context(string layoutJson, MapIntent intent, SymmetryIntent? board = null)
    {
        if (SketchGround.Of(layoutJson, intent, CorpusCell) is not { } ground) return null;
        BoardStructure structure;
        try { structure = BoardDeriver.Derive(ground); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        { return null; }

        var spawns = intent.Spawns.Where(spawn => Authored(spawn.Stamp)).ToList();
        var wools = (intent.Wools ?? []).Where(wool => Authored(wool.Stamp)).ToList();
        var goals = (intent.Destroyables ?? []).Where(goal => Authored(goal.Stamp)).Select(goal => (goal.Id, Kind: "destroyable", goal.Stamp, goal.Anchor))
            .Concat((intent.Cores ?? []).Where(core => Authored(core.Stamp)).Select(core => (core.Id, Kind: "core", core.Stamp, core.Anchor)))
            .ToList();
        var teams = Math.Max(1, IntentCheck.Teams(intent, board));

        var own = ground.UnitFilled.Concat(AuthoredBuild(intent, board, ground)).ToHashSet();
        var ownGround = WalkGround.Over(own, ground.Cell);
        (int, int)? Seat(Pt point, IReadOnlySet<(int, int)> walkable) =>
            Cells.SnapToWalkable(Coarse(point, ground.Cell), walkable, radius: 2);
        var walks = new BoardWalks(ownGround,
            [.. spawns.Select(spawn => new BoardMarker(spawn.Id, spawn.Id, Seat(spawn.Point, own)))],
            [.. wools.Select(wool => new BoardMarker(wool.Id, wool.Id, Seat(wool.Spawn, own)))]);

        GoalDistances.GoalGround? goalGround = null;
        if (teams == 2 && goals.Count > 0 && spawns.Count > 0)
        {
            var closure = ground.Filled.Keys.Concat(ground.Build).ToHashSet();
            var images = (intent.Destroyables ?? []).Select(goal => (goal.Stamp, goal.Anchor))
                .Concat((intent.Cores ?? []).Select(core => (core.Stamp, core.Anchor)))
                .Where(goal => goal.Stamp.Image == 1)
                .ToDictionary(goal => goal.Stamp.Identity, goal => goal.Anchor);
            var seats = goals.Select(goal => new GoalDistances.GoalSeat(goal.Id, goal.Kind, goal.Id, Seat(goal.Anchor, closure),
                images.TryGetValue(goal.Stamp.Identity, out var image) ? Seat(image, closure) : null)).ToList();
            var enemy = intent.Spawns.Where(spawn => spawn.Stamp.Image == 1).Select(spawn => Seat(spawn.Point, closure));
            goalGround = new GoalDistances.GoalGround(WalkGround.Over(closure, ground.Cell), seats,
                [.. spawns.Select(spawn => Seat(spawn.Point, closure)).OfType<(int, int)>()],
                [.. enemy.OfType<(int, int)>()]);
        }

        return EvalContext.OfBoard(structure, walks, goalGround, teams, (intent.Symmetry ?? board)?.Mode,
            new BoardGoals(wools.Count, goals.Count(goal => goal.Kind == "destroyable"), goals.Count(goal => goal.Kind == "core")),
            intent.MaxPlayers, SeedEnvelopes.Default);
    }

    /// <summary>The build cells of the areas the author placed rather than the symmetry carried.</summary>
    private static IEnumerable<(int, int)> AuthoredBuild(MapIntent intent, SymmetryIntent? board, BoardGround ground)
    {
        var authored = SketchGround.AuthoredAreas(intent, board).Select(area => area.Rect).ToList();
        return ground.Build.Where(cell => authored.Any(rect =>
            (cell.Item1 + 0.5) * ground.Cell >= rect.MinX && (cell.Item1 + 0.5) * ground.Cell < rect.MaxX
            && (cell.Item2 + 0.5) * ground.Cell >= rect.MinZ && (cell.Item2 + 0.5) * ground.Cell < rect.MaxZ));
    }

    private static bool Authored(StampId stamp) => string.IsNullOrEmpty(stamp.Kind) || stamp.Image == 0;

    private static (int, int) Coarse(Pt point, int cell) =>
        ((int)Math.Floor(point.X / cell), (int)Math.Floor(point.Z / cell));
}
