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

    /// <summary>The same lattice hash over three axes — what a volume needs. Folding y in through its own
    /// mixing round rather than into one of the plan axes keeps a vertical stack of cells as unrelated as a
    /// horizontal run of them, which is the whole point of a hash field: a boulder eroded by a field that
    /// repeated up its height would read as a fluted column.</summary>
    public static uint Hash(int x, int y, int z, uint seed)
    {
        unchecked
        {
            uint h = Hash(x, z, seed);
            h ^= (uint)y * 0x9E3779B1u; h = (h << 11) | (h >> 21); h *= 0x85EBCA77u;
            h ^= h >> 16; return h;
        }
    }

    /// <summary>A hashed unit value in [0,1) for a lattice point.</summary>
    public static double Unit(int x, int z, uint seed) => (Hash(x, z, seed) & 0xFFFFFF) / (double)0x1000000;

    /// <summary>A hashed unit value in [0,1) for a lattice point in a volume.</summary>
    public static double Unit(int x, int y, int z, uint seed) => (Hash(x, y, z, seed) & 0xFFFFFF) / (double)0x1000000;

    /// <summary>Smooth value noise over a volume — the three-axis <see cref="Value(int,int,uint,int)"/>,
    /// trilinearly interpolated. The vertical period is its own parameter because terrain is a slab: a map is
    /// hundreds of blocks across and a dozen tall, so a field at one isotropic scale either repeats far too
    /// fast across the ground or never turns over at all within a wall's height.</summary>
    public static double Value(int x, int y, int z, uint seed, int scale, int rise)
    {
        int sc = Math.Max(1, scale), sr = Math.Max(1, rise);
        double fx = (double)x / sc, fy = (double)y / sr, fz = (double)z / sc;
        int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy), z0 = (int)Math.Floor(fz);
        double sx = Smooth(fx - x0), sy = Smooth(fy - y0), sz = Smooth(fz - z0);

        double Corner(int dy) =>
            Lerp(Lerp(Unit(x0, y0 + dy, z0, seed), Unit(x0 + 1, y0 + dy, z0, seed), sx),
                 Lerp(Unit(x0, y0 + dy, z0 + 1, seed), Unit(x0 + 1, y0 + dy, z0 + 1, seed), sx), sz);

        return Lerp(Corner(0), Corner(1), sy);
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

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
    /// scale and amplitude, normalised to [0,1). One octave is plain value noise; more octaves add finer detail.
    /// <para>Kept for callers that want the raw sum — the path roughness reads it. A field that is going to be
    /// <em>cut into bands</em> wants <see cref="Field"/> instead, which holds its spread constant.</para></summary>
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

    /// <summary>What one octave is bent into before the sum — the whole difference between the three field
    /// patterns. <see cref="Plain"/> is the noise itself, so the sum is ordinary cloudy fBm.
    /// <see cref="Billow"/> takes its distance from the midpoint, folding the field at every zero crossing into
    /// a crease, which reads as billowed or marbled. <see cref="Ridge"/> inverts that fold and squares it, so
    /// the crossings become thin bright filaments with everything else falling away from them.</summary>
    public enum NoiseShape { Plain, Billow, Ridge }

    // Mean and standard deviation of a single octave of each shape, measured over the smoothed value field.
    // They are what Field cancels to leave a distribution that does not depend on the shape or the octave count.
    // A volume octave has its own pair: trilinear interpolation averages eight lattice corners where bilinear
    // averages four, so the same noise comes out visibly narrower and a plane's numbers would push a volume's
    // outer bands towards nothing — the very collapse this normalisation exists to prevent.
    private static (double Mean, double Deviation) OctaveStats(NoiseShape shape, bool volume) => (shape, volume) switch
    {
        (NoiseShape.Billow, false) => (0.3639, 0.2373),
        (NoiseShape.Ridge, false) => (0.4609, 0.2836),
        (NoiseShape.Billow, true) => (0.3061, 0.2136),
        (NoiseShape.Ridge, true) => (0.5271, 0.2735),
        (_, true) => (0.4996, 0.1866),
        _ => (0.5114, 0.2169),
    };

    /// <summary>How much of [0,1) one standard deviation of a <see cref="Field"/> spans. Chosen so a field cut
    /// into four or five bands puts a few percent in each outer band rather than nothing: it is what makes the
    /// last material an author names actually appear.</summary>
    private const double FieldSpread = 0.21;

    /// <summary>
    /// A fractal field in [0,1) built to be <b>cut into bands</b>: <paramref name="octaves"/> octaves of the
    /// given <paramref name="shape"/> summed at halving scale and amplitude, then normalised by the sum's own
    /// deviation rather than by its amplitude total.
    ///
    /// <para>That normalisation is the point. Summing octaves and dividing by the amplitude sum averages
    /// independent samples, so the spread narrows as octaves are added — the field crowds towards its middle
    /// and the outermost bands starve. Dividing by <c>sqrt(Σ amplitude²)</c> instead holds the spread constant,
    /// so raising the octave count adds detail without quietly deleting the first and last material.</para>
    /// </summary>
    public static double Field(int x, int z, uint seed, int scale, int octaves, NoiseShape shape)
        => Field(x, 0, z, seed, scale, 0, octaves, shape);

    /// <summary>The same field over a volume when <paramref name="rise"/> is positive: the octaves are sampled
    /// from the three-axis noise at a vertical period of <paramref name="rise"/> blocks, so a column no longer
    /// resolves to one value all the way up. A rise of 0 is the plane — the y coordinate is not read at all,
    /// and the field is the cheaper two-axis one.</summary>
    public static double Field(int x, int y, int z, uint seed, int scale, int rise, int octaves, NoiseShape shape)
    {
        var (mean, deviation) = OctaveStats(shape, rise > 0);
        double sum = 0, amp = 1, square = 0;
        int sc = Math.Max(1, scale), sr = Math.Max(1, rise);
        for (var o = 0; o < Math.Max(1, octaves); o++)
        {
            var octave = rise > 0
                ? Value(x, y, z, seed + (uint)o * 7919u, sc, sr)
                : Value(x, z, seed + (uint)o * 7919u, sc);
            sum += amp * (Shape(octave, shape) - mean);
            square += amp * amp; amp *= 0.5; sc = Math.Max(1, sc / 2); sr = Math.Max(1, sr / 2);
        }
        if (square <= 0) return 0.5;
        double normalised = 0.5 + sum / (Math.Sqrt(square) * deviation) * FieldSpread;
        return Math.Clamp(normalised, 0, 0.9999999);
    }

    private static double Shape(double value, NoiseShape shape) => shape switch
    {
        NoiseShape.Billow => Math.Abs(2 * value - 1),
        NoiseShape.Ridge => (1 - Math.Abs(2 * value - 1)) * (1 - Math.Abs(2 * value - 1)),
        _ => value,
    };
}
