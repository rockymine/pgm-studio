using PgmStudio.Pgm.Evaluate.Terms;

namespace PgmStudio.Pgm.Evaluate;

/// <summary>
/// Which terms are enabled and at what weight — the criteria on/off switch. The composer gate, the editor lint,
/// and the ranking harness each run a profile; toggling a validation criterion is a profile edit, not a code
/// change. <see cref="Composer"/> has every term on at flat weight 1.0; <see cref="Default"/> is the same less
/// the floors only a composed board is held to. Weights are tuned only when the labeled set mis-ranks, never by
/// taste. Immutable — <see cref="With"/> returns a modified copy.
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

    /// <summary>The floors the composer holds its own boards to and an authored plan is not held to: where a
    /// spawn or a wool stands against the crossing is the author's to choose, and a composed board takes the
    /// author's judgement of where it should be.</summary>
    public static IReadOnlyList<string> ComposerFloors { get; } = [new SpawnFrontFloor().Id, new WoolFrontFloor().Id];

    /// <summary>Every term on at weight 1.0 — the composer's gate and the browse feed that scores its boards.</summary>
    public static EvaluationProfile Composer { get; } =
        new(defaultEnabled: true, defaultWeight: 1.0, new Dictionary<string, Setting>());

    /// <summary><see cref="Composer"/> less <see cref="ComposerFloors"/> — the editor lint and the harness.</summary>
    public static EvaluationProfile Default { get; } =
        new(defaultEnabled: true, defaultWeight: 1.0,
            ComposerFloors.ToDictionary(id => id, _ => new Setting(false, 1.0)));

    /// <summary>A copy with one term's enabled/weight overridden.</summary>
    public EvaluationProfile With(string termId, bool enabled, double weight = 1.0)
    {
        var next = new Dictionary<string, Setting>(_overrides) { [termId] = new(enabled, weight) };
        return new EvaluationProfile(_defaultEnabled, _defaultWeight, next);
    }
}
