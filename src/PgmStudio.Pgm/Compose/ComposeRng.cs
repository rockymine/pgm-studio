namespace PgmStudio.Pgm.Compose;

/// <summary>
/// A small, deterministic PRNG used for every random draw the composer makes: PCG32 (PCG-XSH-RR, the
/// standard 64-bit-state / 32-bit-output variant). <see cref="System.Random"/> is deliberately not used —
/// its algorithm is not part of the .NET stability contract, so golden/regression outputs pinned against it
/// could silently drift across runtimes. No wall-clock, no <see cref="Guid"/>: the same seed always produces
/// the same sequence, forever. The algorithm choice, the multiplier/stream constants, and the reduction
/// formulas below are a <b>stability requirement</b> — changing any of them breaks every pinned golden output
/// and every seed corpus generated against it, so treat this file as frozen once shipped.
/// </summary>
public sealed class ComposeRng
{
    private const ulong Multiplier = 6364136223846793005UL;

    // PCG's own default stream constant — fixed and arbitrary, only its oddness matters. The increment is
    // derived from it once at construction and never varies, so a given seed always walks the same stream.
    private const ulong StreamConstant = 0xda3e39cb94b95bdbUL;

    private ulong _state;
    private readonly ulong _inc;

    public ComposeRng(ulong seed)
    {
        _inc = (StreamConstant << 1) | 1UL;
        _state = 0UL;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    /// <summary>The next raw 32-bit output: a 64-bit LCG step, xorshifted down to 32 bits, then rotated by the
    /// top bits of the pre-step state (PCG-XSH-RR).</summary>
    public uint NextUInt()
    {
        var old = _state;
        _state = old * Multiplier + _inc;
        var xorshifted = (uint)(((old >> 18) ^ old) >> 27);
        var rot = (int)(old >> 59);
        return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
    }

    /// <summary>A uniform integer in <c>[minInclusive, maxExclusive)</c>. Uses a plain modulo reduction — a
    /// deliberate simplicity choice over Lemire's rejection method. The ranges the composer draws from are
    /// small, so the resulting modulo bias is negligible; the formula is pinned forever alongside the
    /// algorithm itself.</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentException($"empty range [{minInclusive},{maxExclusive})");
        var range = (uint)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt() % range);
    }

    /// <summary>A uniform double in <c>[0,1)</c>, built from one 32-bit draw.</summary>
    public double NextDouble() => NextUInt() / 4294967296.0;

    /// <summary>A uniform double in <c>[minInclusive, maxExclusive)</c>.</summary>
    public double NextDouble(double minInclusive, double maxExclusive) =>
        minInclusive + NextDouble() * (maxExclusive - minInclusive);

    /// <summary>True with probability <paramref name="p"/>.</summary>
    public bool NextBool(double p) => NextDouble() < p;

    /// <summary>Pick a uniformly random element. Indexes the list directly (never enumerates it), so the draw
    /// never depends on a collection's iteration order.</summary>
    public T Pick<T>(IReadOnlyList<T> items) => items[NextInt(0, items.Count)];
}
