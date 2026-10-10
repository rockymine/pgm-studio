namespace PgmStudio.Vocabulary;

/// <summary>
/// What a rule does when a finding cites it, where a setting states it: <see cref="Refuse"/> stops the work,
/// <see cref="Hint"/> lets it go ahead with the finding beside it. A rule with no setting does what its code says.
/// </summary>
public static class RuleLevels
{
    public const string Refuse = "refuse";
    public const string Hint = "hint";

    public static readonly string[] All = [Refuse, Hint];

    public static bool IsValid(string? level) => level is not null && All.Contains(level);
}

/// <summary>Where a rule's level was set: the studio's mode (<c>Rules:Mode</c>), its configuration
/// (<c>Rules:Levels</c>), or an admin on the rules page, which wins over both.</summary>
public static class RuleLevelSources
{
    public const string Mode = "mode";
    public const string Config = "config";
    public const string Studio = "studio";

    public static readonly string[] All = [Mode, Config, Studio];
}
