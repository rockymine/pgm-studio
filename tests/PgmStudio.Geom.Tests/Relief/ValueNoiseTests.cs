using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// The lattice hash is 32-bit integer arithmetic with wrap-around, so its values are fixed numbers rather than
/// whatever a platform's random generator gave. The expected values below are the same hash, vnoise and fractal
/// evaluated with JavaScript's <c>Math.imul</c> and <c>&gt;&gt;&gt;</c>.
/// </summary>
public sealed class ValueNoiseTests
{
    [Test]
    [Arguments(0, 0, 0, 0.0)]
    [Arguments(1, 2, 3, 0.57672061352059245)]
    [Arguments(-5, 7, 1, 0.24609528481960297)]
    [Arguments(123, -456, 789, 0.23843768681399524)]
    [Arguments(1000, 1000, 1, 0.83704255009070039)]
    [Arguments(3, 3, 38, 0.89389655063860118)]
    public async Task The_hash_reproduces_the_32_bit_integer_values(int ix, int iz, int seed, double expected)
    {
        await Assert.That(ValueNoise.Hash(ix, iz, seed)).IsEqualTo(expected).Within(1e-15);
    }

    [Test]
    [Arguments(0.5, 0.5, 1, 0.45755650318460539)]
    [Arguments(10.25, -3.75, 7, 0.74791747731299107)]
    [Arguments(100.5, 200.5, 38, 0.58072003972483799)]
    public async Task Value_noise_blends_the_lattice_the_same_way(double x, double z, int seed, double expected)
    {
        await Assert.That(ValueNoise.Value(x, z, seed)).IsEqualTo(expected).Within(1e-13);
    }

    [Test]
    [Arguments(20.5, 20.5, 12.0, 2, 1, 0.43030921721499538)]
    [Arguments(7.5, 31.5, 6.0, 3, 38, -0.035856428556193976)]
    [Arguments(100.5, 40.5, 24.0, 1, 75, 0.51979503535227045)]
    public async Task Fractal_noise_sums_octaves_of_halving_width(
        double x, double z, double cell, int octaves, int seed, double expected)
    {
        await Assert.That(ValueNoise.Fractal(x, z, cell, octaves, seed)).IsEqualTo(expected).Within(1e-13);
    }

    [Test]
    public async Task Fractal_noise_stays_inside_its_range_over_a_field()
    {
        double lowest = 1, highest = -1;
        for (var x = 0; x < 80; x++)
            for (var z = 0; z < 80; z++)
            {
                var value = ValueNoise.Fractal(x + 0.5, z + 0.5, 12, 3, 5);
                lowest = Math.Min(lowest, value);
                highest = Math.Max(highest, value);
            }

        await Assert.That(lowest).IsGreaterThanOrEqualTo(-1);
        await Assert.That(highest).IsLessThanOrEqualTo(1);
        await Assert.That(highest - lowest).IsGreaterThan(0.3);
    }
}
