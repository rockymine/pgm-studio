namespace PgmStudio.Vocabulary;

/// <summary>The kinds an authored <c>PlanBox</c> may carry — the partition's typed box vocabulary
/// (docs/generator/model.md §4) as authoring strings. A box names <b>what its pieces realize</b>: the
/// <see cref="Spawn"/> and <see cref="Wool"/> approaches (each a terminal capping a corridor), the
/// <see cref="Hub"/> body they seat on, the <see cref="Frontline"/> that fronts it, and the <see cref="Mid"/>
/// between the fanned images.</summary>
public static class PlanBoxKinds
{
    public const string Spawn = "spawn";
    public const string Hub = "hub";
    public const string Wool = "wool";
    public const string Frontline = "frontline";
    public const string Mid = "mid";

    /// <summary>The five, in the order a picker offers them.</summary>
    public static readonly string[] All = [Hub, Wool, Spawn, Frontline, Mid];

    /// <summary>The canonical kind for a raw (possibly unknown) value. An unrecognised kind folds to
    /// <see cref="Mid"/> — the same fallback a box read off an unexpected piece already takes — so a box
    /// authored under a vocabulary this build does not know still loads as a group, just an unclassified
    /// one.</summary>
    public static string Canonical(string? kind) =>
        kind is not null && Array.IndexOf(All, kind) >= 0 ? kind : Mid;
}
