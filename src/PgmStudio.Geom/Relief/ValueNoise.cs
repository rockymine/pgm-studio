namespace PgmStudio.Geom.Relief;

/// <summary>
/// Lattice value noise over the plan, hashed with wrap-around 32-bit integer arithmetic so one coordinate and
/// seed give one value on every machine. <see cref="Fractal"/> is the sum a Roughness size is made of.
/// </summary>
public static class ValueNoise
{
    private const double Range = 4294967296.0;

    /// <summary>A hashed value in [0, 1) for a lattice point.</summary>
    public static double Hash(int ix, int iz, int seed)
    {
        unchecked
        {
            var mixed = ix * 374761393 + iz * 668265263 + seed * 2147483647;
            mixed = (mixed ^ (int)((uint)mixed >> 13)) * 1274126177;
            mixed ^= (int)((uint)mixed >> 16);
            return (uint)mixed / Range;
        }
    }

    /// <summary>Smooth noise in [0, 1): the lattice hash blended across a cell by a quintic fade.</summary>
    public static double Value(double x, double z, int seed)
    {
        var ix = (int)Math.Floor(x);
        var iz = (int)Math.Floor(z);
        var fx = Fade(x - ix);
        var fz = Fade(z - iz);
        return Lerp(Lerp(Hash(ix, iz, seed), Hash(ix + 1, iz, seed), fx),
                    Lerp(Hash(ix, iz + 1, seed), Hash(ix + 1, iz + 1, seed), fx), fz);
    }

    /// <summary>Noise in [-1, 1] of features <paramref name="cell"/> blocks wide. Each of the
    /// <paramref name="octaves"/> halves the width of the one before, down to one block, at half its weight.</summary>
    public static double Fractal(double x, double z, double cell, int octaves, int seed)
    {
        double sum = 0, amplitude = 1, total = 0;
        var width = cell > 0 ? cell : 1;
        for (var octave = 0; octave < Math.Max(1, octaves); octave++)
        {
            sum += amplitude * Value(x / width, z / width, unchecked(seed + octave * 101));
            total += amplitude;
            amplitude *= 0.5;
            width = Math.Max(1, width / 2);
        }
        return sum / total * 2 - 1;
    }

    private static double Fade(double fraction) =>
        fraction * fraction * fraction * (fraction * (fraction * 6 - 15) + 10);

    private static double Lerp(double from, double to, double blend) => from + (to - from) * blend;
}
