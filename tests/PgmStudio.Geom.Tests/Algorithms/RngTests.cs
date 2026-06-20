using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>
/// Deterministic seeded RNG (SplitMix64): the same seed replays the same sequence, different seeds diverge,
/// and the range helpers stay within bounds.
/// </summary>
public sealed class RngTests
{
    [Test]
    public async Task The_same_seed_replays_the_same_sequence()
    {
        var a = new Rng(42);
        var b = new Rng(42);
        for (var i = 0; i < 100; i++) await Assert.That(a.NextULong()).IsEqualTo(b.NextULong());
    }

    [Test]
    public async Task Different_seeds_diverge()
    {
        var a = new Rng(1);
        var b = new Rng(2);
        await Assert.That(a.NextULong()).IsNotEqualTo(b.NextULong());
    }

    [Test]
    public async Task NextDouble_stays_in_unit_interval()
    {
        var r = new Rng(7);
        for (var i = 0; i < 1000; i++)
        {
            var d = r.NextDouble();
            await Assert.That(d).IsGreaterThanOrEqualTo(0d);
            await Assert.That(d).IsLessThan(1d);
        }
    }

    [Test]
    public async Task Int_and_Range_respect_bounds()
    {
        var r = new Rng(9);
        for (var i = 0; i < 1000; i++)
        {
            var n = r.Int(3, 8);
            await Assert.That(n).IsGreaterThanOrEqualTo(3);
            await Assert.That(n).IsLessThan(8);
            var x = r.Range(-2.5, 4.5);
            await Assert.That(x).IsGreaterThanOrEqualTo(-2.5);
            await Assert.That(x).IsLessThan(4.5);
        }
        await Assert.That(r.Int(5, 5)).IsEqualTo(5);   // empty range → low bound
    }
}
