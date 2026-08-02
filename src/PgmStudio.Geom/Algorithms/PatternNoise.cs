namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Deterministic value/fractal noise and lattice hashing — pure geometry math, so it lives in the
/// dependency-free <c>Geom</c> leaf beside <see cref="CatmullRom"/> and <see cref="Ribbon"/> where every
/// generator (the terrain painter's pattern materials, the dressing stage, any preview) can reach it. The
/// same coordinates and seed always give the same result, on any machine — never RNG, so a map exports the
/// same pattern every time.
/// </summary>
public static class PatternNoise
{
    public static uint Hash(int x, int z, uint seed)
    {
        unchecked
        {
            uint h = seed + 0x9E3779B9u;
            h ^= (uint)x * 0x85EBCA77u; h = (h << 13) | (h >> 19); h *= 0xC2B2AE3Du;
            h ^= (uint)z * 0x27D4EB2Fu; h = (h << 15) | (h >> 17); h *= 0x165667B1u;
            h ^= h >> 16; return h;
        }
    }

    /// <summary>A hashed unit value in [0,1) for a lattice point.</summary>
    public static double Unit(int x, int z, uint seed) => (Hash(x, z, seed) & 0xFFFFFF) / (double)0x1000000;

    /// <summary>Smooth (smoothstep-interpolated) value noise at a scale, in [0,1).</summary>
    public static double Value(int x, int z, uint seed, int scale)
    {
        double fx = (double)x / scale, fz = (double)z / scale;
        int x0 = (int)Math.Floor(fx), z0 = (int)Math.Floor(fz);
        double tx = fx - x0, tz = fz - z0;
        double v00 = Unit(x0, z0, seed), v10 = Unit(x0 + 1, z0, seed), v01 = Unit(x0, z0 + 1, seed), v11 = Unit(x0 + 1, z0 + 1, seed);
        double sx = tx * tx * (3 - 2 * tx), sz = tz * tz * (3 - 2 * tz);
        double a = v00 + (v10 - v00) * sx, b = v01 + (v11 - v01) * sx;
        return a + (b - a) * sz;
    }

    /// <summary>Fractional Brownian motion: <paramref name="octaves"/> octaves of value noise summed at halving
    /// scale and amplitude, normalised to [0,1). One octave is plain value noise; more octaves add finer detail.</summary>
    public static double Fbm(int x, int z, uint seed, int scale, int octaves)
    {
        double sum = 0, amp = 1, norm = 0; int sc = Math.Max(1, scale);
        for (var o = 0; o < Math.Max(1, octaves); o++)
        {
            sum += amp * Value(x, z, seed + (uint)o * 7919u, sc);
            norm += amp; amp *= 0.5; sc = Math.Max(1, sc / 2);
        }
        return norm > 0 ? sum / norm : 0;
    }
}
