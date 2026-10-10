namespace PgmStudio.Vocabulary;

/// <summary>
/// Which rules stop the work. A rule's level comes from the first of three places that states one: an admin's
/// setting (<see cref="RuleLevelSources.Studio"/>), the configuration (<see cref="RuleLevelSources.Config"/>), or
/// the minimal mode, which relaxes every rule outside the set it is given (<see cref="RuleLevelSources.Mode"/>).
/// A rule none of them names does what its code says.
///
/// <para>A <see cref="RuleLevels.Hint"/> answers a refusal under that rule as a complaint, so the work goes ahead
/// and the finding still rides along, and a dressing site that would have turned a prop away seats it instead. A
/// <see cref="RuleLevels.Refuse"/> answers a complaint under that rule as a refusal, so every gate that asks
/// whether anything stops the work stops on it.</para>
///
/// <para>The settings are replaced whole and read without locking: every reader sees one complete set.</para>
/// </summary>
public static class RulePolicy
{
    private sealed record Settings(
        HashSet<string>? Minimal,
        IReadOnlyDictionary<string, string> Config,
        IReadOnlyDictionary<string, string> Studio);

    private static volatile Settings current = new(null, new Dictionary<string, string>(), new Dictionary<string, string>());

    /// <summary>Whether the studio runs the minimal mode.</summary>
    public static bool Minimal => current.Minimal is not null;

    /// <summary>Only <paramref name="enforced"/> keep their code's refusals; every other rule is a hint unless a
    /// setting says otherwise.</summary>
    public static void UseMinimal(IEnumerable<string> enforced) => current = current with { Minimal = [.. enforced] };

    /// <summary>Every rule keeps its code's refusals unless a setting says otherwise.</summary>
    public static void UseFull() => current = current with { Minimal = null };

    /// <summary>The levels the configuration states, rule id to <see cref="RuleLevels"/> word.</summary>
    public static void Configure(IReadOnlyDictionary<string, string> levels) =>
        current = current with { Config = new Dictionary<string, string>(levels) };

    /// <summary>The levels an admin has set, rule id to <see cref="RuleLevels"/> word.</summary>
    public static void Store(IReadOnlyDictionary<string, string> levels) =>
        current = current with { Studio = new Dictionary<string, string>(levels) };

    /// <summary>The level stated for <paramref name="rule"/> and where it was stated, or null where the rule
    /// does what its code says.</summary>
    public static (string Level, string Source)? LevelOf(string rule)
    {
        var settings = current;
        if (settings.Studio.TryGetValue(rule, out var studio)) return (studio, RuleLevelSources.Studio);
        if (settings.Config.TryGetValue(rule, out var config)) return (config, RuleLevelSources.Config);
        if (settings.Minimal is { } kept && !kept.Contains(rule)) return (RuleLevels.Hint, RuleLevelSources.Mode);
        return null;
    }

    /// <summary>Whether a refusal or a decline citing <paramref name="rule"/> stops the work or turns something
    /// away.</summary>
    public static bool Enforces(string rule) => LevelOf(rule)?.Level != RuleLevels.Hint;

    /// <summary>The severity a finding is answered with. A decline is left as it is: it says something is not in
    /// the world, and only the site that dropped it can decide to keep it instead.</summary>
    public static Severity Apply(string rule, Severity severity) => (severity, LevelOf(rule)?.Level) switch
    {
        (Severity.Refusal, RuleLevels.Hint) => Severity.Complaint,
        (Severity.Complaint, RuleLevels.Refuse) => Severity.Refusal,
        _ => severity,
    };
}
