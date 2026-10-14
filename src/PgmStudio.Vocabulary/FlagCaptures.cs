namespace PgmStudio.Vocabulary;

/// <summary>
/// When a team may capture another team's flag in its net. The intent states it and the Configure screen offers
/// it, so they are one set.
/// </summary>
public static class FlagCaptures
{
    /// <summary>Whenever its carrier reaches the net.</summary>
    public const string Anytime = "anytime";

    /// <summary>Only while the capturing team's own flag stands at its post; a carrier waiting in the net
    /// captures the moment it is back.</summary>
    public const string Home = "home";

    /// <summary>The two, in the order a picker offers them.</summary>
    public static readonly string[] All = [Anytime, Home];
}
