namespace PgmStudio.Vocabulary;

/// <summary>
/// What a board lets a player do to its blocks. A board played to complete an objective — a wool, a
/// destroyable, a core — is built on; a board played for control is not, and where it hands out blocks at
/// all, players place and break those and nothing else. The intent states it and the Configure screen offers
/// it, so they are one set.
/// </summary>
public static class BuildModes
{
    /// <summary>Building anywhere but over the void outside the build areas — the rule a completion map is
    /// played under.</summary>
    public const string Void = "void";

    /// <summary>No block placed or broken anywhere: the conquest default.</summary>
    public const string Never = "never";

    /// <summary>The named materials placed and broken, and nothing else.</summary>
    public const string Only = "only";

    /// <summary>The three, in the order a picker offers them.</summary>
    public static readonly string[] All = [Void, Never, Only];
}
