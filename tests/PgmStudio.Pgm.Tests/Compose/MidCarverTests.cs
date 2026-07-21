using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The band-only crossing arithmetic and the symmetry-frame flip classification. The full carved-band
/// behaviour on real plans (flush dock, hull-exact span, connectivity) is asserted by the
/// <see cref="ComposerTests"/> sweep; these tests pin the pure pieces the carver is built from.
/// </summary>
public sealed class MidCarverTests
{
    [Test]
    public async Task The_band_only_crossing_is_a_uniform_20_block_gap()
    {
        var request = new ComposeRequest(12, seed: 1);
        var env = Envelope.Derive(request, new ComposeRng(1));
        var crossing = MidCarver.BandOnly(env);
        await Assert.That(crossing.HalfGapCells * env.Cell).IsEqualTo(10);   // 10 blocks per side
    }

    [Test]
    public async Task Mirror_symmetries_do_not_flip_and_rotations_do()
    {
        await Assert.That(MidCarver.LateralFlip("rot_180")).IsTrue();
        await Assert.That(MidCarver.LateralFlip("rot_90")).IsTrue();
        await Assert.That(MidCarver.LateralFlip("mirror_x")).IsFalse();
        await Assert.That(MidCarver.LateralFlip("mirror_z")).IsFalse();
    }
}
