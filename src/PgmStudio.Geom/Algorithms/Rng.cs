namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Deterministic seeded RNG (SplitMix64). Same seed → same sequence, so a generated layout is reproducible
/// and a seed can be iterated on. Pure value type of state; not thread-safe. Sits with the other named
/// algorithms — the generative layout code (and future grid / noise generators) draw from it.
/// </summary>
public sealed class Rng(long seed)
{
    private ulong _s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x123456789UL;

    public ulong NextULong()
    {
        _s += 0x9E3779B97F4A7C15UL;
        var z = _s;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A double in [0, 1).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>A double in [lo, hi).</summary>
    public double Range(double lo, double hi) => lo + (hi - lo) * NextDouble();

    /// <summary>An int in [loInclusive, hiExclusive).</summary>
    public int Int(int loInclusive, int hiExclusive) =>
        hiExclusive <= loInclusive ? loInclusive : loInclusive + (int)(NextULong() % (ulong)(hiExclusive - loInclusive));

    /// <summary>True with probability <paramref name="p"/>.</summary>
    public bool Chance(double p) => NextDouble() < p;
}
