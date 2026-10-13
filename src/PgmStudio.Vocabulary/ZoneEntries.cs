namespace PgmStudio.Vocabulary;

/// <summary>
/// Who may walk into a zone. The intent states it and a screen offers it, so they are one set.
/// </summary>
public static class ZoneEntries
{
    /// <summary>Every player.</summary>
    public const string Anyone = "anyone";

    /// <summary>The zone's own team, and nobody else.</summary>
    public const string Team = "team";

    /// <summary>No player at all.</summary>
    public const string Nobody = "nobody";

    /// <summary>The three, in the order a picker offers them.</summary>
    public static readonly string[] All = [Anyone, Team, Nobody];
}
