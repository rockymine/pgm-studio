namespace PgmStudio.Pgm.Evaluate;

/// <summary>
/// Which terms are enabled and at what weight — the criteria on/off switch. The composer gate, the editor lint,
/// and the ranking harness each run a profile; toggling a validation criterion is a profile edit, not a code
/// change. The default has every term on at flat weight 1.0; weights are tuned only when the labeled set
/// mis-ranks, never by taste. Immutable — <see cref="With"/> returns a modified copy.
/// </summary>
public sealed class EvaluationProfile
{
    public readonly record struct Setting(bool Enabled, double Weight);

    private readonly bool _defaultEnabled;
    private readonly double _defaultWeight;
    private readonly IReadOnlyDictionary<string, Setting> _overrides;

    private EvaluationProfile(bool defaultEnabled, double defaultWeight, IReadOnlyDictionary<string, Setting> overrides)
    {
        _defaultEnabled = defaultEnabled;
        _defaultWeight = defaultWeight;
        _overrides = overrides;
    }

    public bool Enabled(string termId) => _overrides.TryGetValue(termId, out var s) ? s.Enabled : _defaultEnabled;

    public double Weight(string termId) => _overrides.TryGetValue(termId, out var s) ? s.Weight : _defaultWeight;

    /// <summary>Every term on at weight 1.0 — the starting profile for the gate, the lint, and the harness.</summary>
    public static EvaluationProfile Default { get; } =
        new(defaultEnabled: true, defaultWeight: 1.0, new Dictionary<string, Setting>());

    /// <summary>A copy with one term's enabled/weight overridden.</summary>
    public EvaluationProfile With(string termId, bool enabled, double weight = 1.0)
    {
        var next = new Dictionary<string, Setting>(_overrides) { [termId] = new(enabled, weight) };
        return new EvaluationProfile(_defaultEnabled, _defaultWeight, next);
    }
}
