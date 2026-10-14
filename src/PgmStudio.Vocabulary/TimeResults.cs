namespace PgmStudio.Vocabulary;

/// <summary>
/// What decides a match when its time runs out, in PGM's words. The intent states it and the Configure screen
/// offers it, so they are one set.
/// </summary>
public static class TimeResults
{
    /// <summary>The match's own order: the team furthest along its objectives, then the higher score.</summary>
    public const string Default = "default";

    /// <summary>The team furthest along its objectives.</summary>
    public const string Objectives = "objectives";

    /// <summary>The team with the higher score.</summary>
    public const string Score = "score";

    /// <summary>Nobody: the match ends tied.</summary>
    public const string Tie = "tie";

    /// <summary>The four, in the order a picker offers them.</summary>
    public static readonly string[] All = [Default, Objectives, Score, Tie];
}
