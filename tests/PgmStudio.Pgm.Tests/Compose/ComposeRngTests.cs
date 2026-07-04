using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The composer's PRNG: known-answer pins against the frozen PCG32 implementation (a regression against the
/// algorithm itself — if these ever need updating, something changed the sequence), plus range/bounds checks.
/// </summary>
public sealed class ComposeRngTests
{
    // Pinned once from the shipped implementation (seed 12345) — a change here means the sequence drifted.
    [Test]
    public async Task NextUInt_is_a_known_sequence_for_a_fixed_seed()
    {
        var rng = new ComposeRng(12345);
        var values = new uint[5];
        for (var i = 0; i < values.Length; i++) values[i] = rng.NextUInt();
        await Assert.That(values).IsEquivalentTo(new uint[]
        {
            2251339066, 380381740, 3135122815, 3539316216, 3092115165,
        });
    }

    [Test]
    public async Task Same_seed_reproduces_the_same_sequence()
    {
        var a = new ComposeRng(999);
        var b = new ComposeRng(999);
        for (var i = 0; i < 20; i++)
            await Assert.That(a.NextUInt()).IsEqualTo(b.NextUInt());
    }

    [Test]
    public async Task Different_seeds_diverge()
    {
        var a = new ComposeRng(1);
        var b = new ComposeRng(2);
        var same = true;
        for (var i = 0; i < 10; i++) same &= a.NextUInt() == b.NextUInt();
        await Assert.That(same).IsFalse();
    }

    [Test]
    public async Task NextInt_stays_within_its_range()
    {
        var rng = new ComposeRng(42);
        for (var i = 0; i < 2000; i++)
        {
            var v = rng.NextInt(5, 15);
            await Assert.That(v >= 5 && v < 15).IsTrue();
        }
    }

    [Test]
    public async Task NextInt_covers_a_small_range_over_many_draws()
    {
        var rng = new ComposeRng(7);
        var seen = new HashSet<int>();
        for (var i = 0; i < 500; i++) seen.Add(rng.NextInt(0, 3));
        await Assert.That(seen).IsEquivalentTo(new[] { 0, 1, 2 });
    }

    [Test]
    public async Task NextDouble_stays_within_zero_one()
    {
        var rng = new ComposeRng(3);
        for (var i = 0; i < 2000; i++)
        {
            var v = rng.NextDouble();
            await Assert.That(v >= 0.0 && v < 1.0).IsTrue();
        }
    }

    [Test]
    public async Task NextDouble_range_stays_within_bounds()
    {
        var rng = new ComposeRng(11);
        for (var i = 0; i < 2000; i++)
        {
            var v = rng.NextDouble(0.28, 0.42);
            await Assert.That(v >= 0.28 && v < 0.42).IsTrue();
        }
    }

    [Test]
    public async Task NextBool_respects_its_probability_at_the_extremes()
    {
        var rng = new ComposeRng(21);
        for (var i = 0; i < 100; i++) await Assert.That(rng.NextBool(0.0)).IsFalse();
        var always = new ComposeRng(22);
        for (var i = 0; i < 100; i++) await Assert.That(always.NextBool(1.0)).IsTrue();
    }

    [Test]
    public async Task Pick_only_returns_items_from_the_list()
    {
        var rng = new ComposeRng(5);
        var items = new[] { "a", "b", "c" };
        for (var i = 0; i < 200; i++)
            await Assert.That(items).Contains(rng.Pick(items));
    }
}
