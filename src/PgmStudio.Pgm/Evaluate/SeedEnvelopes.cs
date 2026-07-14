namespace PgmStudio.Pgm.Evaluate;

/// <summary>An authored metric band <c>[Lo, Hi]</c> — the range a soft metric lands in across the teaching
/// seeds. A degenerate band (<c>Lo == Hi</c>, e.g. a <c>[0,0]</c> counting band) still yields a finite
/// distance via a floored half-width.</summary>
public readonly record struct Band(double Lo, double Hi)
{
    /// <summary>Half the band's width — the distance normalizer. A degenerate band (<c>Lo == Hi</c>) has no
    /// width, so it falls back to a floor (<c>|Hi|·0.1</c>, itself floored to ε) rather than dividing by zero.
    /// The floor applies <i>only</i> when degenerate; a wide band keeps its true half-width.</summary>
    public double HalfWidth
    {
        get
        {
            var hw = (Hi - Lo) / 2.0;
            return hw > 1e-9 ? hw : Math.Max(Math.Abs(Hi) * 0.1, 1e-9);
        }
    }

    /// <summary>Distance of a metric outside the band, normalized by the half-width so <c>1.0</c> means "as far
    /// outside as the band is wide" regardless of unit (blocks, counts, ratios). Zero inside the band.</summary>
    public double Distance(double m)
    {
        if (m >= Lo && m <= Hi) return 0.0;
        return (m < Lo ? Lo - m : m - Hi) / HalfWidth;
    }
}

/// <summary>The soft terms' authored metric bands, keyed by metric id. Source of truth is generated and checked
/// in (<c>Evaluate/seed-envelopes.json</c>, from <c>tools/deriver/envelope-stats.cs</c> over the teaching
/// seeds) — never hand-edited. A term looks its band up by id and measures distance against it.</summary>
public sealed record SeedEnvelopes(IReadOnlyDictionary<string, Band> Bands)
{
    public static readonly SeedEnvelopes Empty = new(new Dictionary<string, Band>());

    /// <summary>The band for <paramref name="metricId"/>, or null when the envelope set does not carry it
    /// (a soft term with no band contributes nothing — it stays dormant until the stats are generated).</summary>
    public Band? this[string metricId] => Bands.TryGetValue(metricId, out var b) ? b : null;
}
