namespace PgmStudio.Vocabulary;

/// <summary>
/// How a control point is taken. The intent states it and a screen offers it, so they are one set.
/// </summary>
public static class CaptureModes
{
    /// <summary>A team holds the point alone for its capture time, through a neutral state, to take it.</summary>
    public const string Hold = "hold";

    /// <summary>A player of any team but the holder's takes the point the moment they step on it, and it stays
    /// theirs until another team touches it.</summary>
    public const string Touch = "touch";

    /// <summary>The two, in the order a picker offers them.</summary>
    public static readonly string[] All = [Hold, Touch];
}
