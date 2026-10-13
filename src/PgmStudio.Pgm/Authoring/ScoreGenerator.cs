using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The <c>&lt;score&gt;</c> a board ends at. Without one PGM builds no score module and nothing a point or a
/// flag pays is counted, so a board whose objectives pay states a limit, and one whose objectives do not states
/// none. One slice writes it because two objectives pay into it.
/// </summary>
public static class ScoreGenerator
{
    public static void Apply(Dict doc, MapIntent intent)
    {
        doc.Remove("score");
        if (Limit(intent) is { } limit) doc["score"] = new Dict { ["limit"] = (long)limit };
    }

    /// <summary>
    /// The limit the match ends at: the author's where they state one, the corpus's default on a board that
    /// carries a point or a flag that pays, and none at all otherwise. Zero is an author asking for no limit — a
    /// board played to a time limit instead — and is honoured rather than replaced.
    /// </summary>
    private static int? Limit(MapIntent intent)
    {
        if (intent.ScoreLimit is { } stated) return stated > 0 ? stated : null;
        if (intent.ControlPoints?.Any(p => p.CaptureBox is not null && p.Points > 0) ?? false)
            return ObjectiveDefaults.ControlPointScoreLimit;
        if (intent.Flags?.Any(flag => flag.Posts.Count > 0 && flag.PointsRate > 0) ?? false)
            return ObjectiveDefaults.FlagScoreLimit;
        return null;
    }
}
