using PgmStudio.Pgm.Evaluate;

namespace PgmStudio.Pgm.Tests.Evaluate;

/// <summary>
/// The distance convention: a metric inside its band scores 0; outside, the overshoot normalized by the band's
/// half-width, so distance 1.0 means "as far outside as the band is wide" regardless of unit. The degenerate
/// floor must not leak into wide bands.
/// </summary>
public sealed class BandTests
{
    [Test]
    [Arguments(10.0, 20.0, 15.0, 0.0)]   // inside → 0
    [Arguments(10.0, 20.0, 10.0, 0.0)]   // on the low edge → 0
    [Arguments(10.0, 20.0, 20.0, 0.0)]   // on the high edge → 0
    [Arguments(10.0, 20.0, 5.0, 1.0)]    // half-width below (hw=5) → 1.0
    [Arguments(10.0, 20.0, 25.0, 1.0)]   // half-width above → 1.0
    [Arguments(10.0, 20.0, 30.0, 2.0)]   // two half-widths above → 2.0
    public async Task Distance_normalizes_by_half_width(double lo, double hi, double m, double expected)
    {
        await Assert.That(new Band(lo, hi).Distance(m)).IsEqualTo(expected).Within(1e-9);
    }

    [Test]
    public async Task A_wide_band_keeps_its_true_half_width_not_the_degenerate_floor()
    {
        // [100,120] has half-width 10; the floor (|hi|·0.1 = 12) must NOT apply — m=130 is one half-width out.
        await Assert.That(new Band(100.0, 120.0).Distance(130.0)).IsEqualTo(1.0).Within(1e-9);
    }

    [Test]
    public async Task A_degenerate_band_falls_back_to_the_floor_instead_of_dividing_by_zero()
    {
        // [5,5] has zero width → floor = |hi|·0.1 = 0.5; m=6 is one block out = distance 2.
        await Assert.That(new Band(5.0, 5.0).Distance(6.0)).IsEqualTo(2.0).Within(1e-9);
        await Assert.That(new Band(5.0, 5.0).Distance(5.0)).IsEqualTo(0.0).Within(1e-9);
    }
}
