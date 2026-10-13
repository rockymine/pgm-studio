namespace PgmStudio.Vocabulary;

/// <summary>
/// What carrying a flag does to the carrier: the kit given on picking it up, undone on dropping it. The
/// intent states it and the Configure screen offers it, so they are one set.
/// </summary>
public static class FlagCarriers
{
    /// <summary>The carrier's health is lowered to a stated number of hearts.</summary>
    public const string Lowered = "lowered";

    /// <summary>The carrier is shielded for a moment on pick-up: absorption III for three seconds.</summary>
    public const string Absorption = "absorption";

    /// <summary>The carrier's health is raised to a stated number of hearts by health boost.</summary>
    public const string Boost = "boost";

    /// <summary>The three, in the order a picker offers them.</summary>
    public static readonly string[] All = [Lowered, Absorption, Boost];
}
