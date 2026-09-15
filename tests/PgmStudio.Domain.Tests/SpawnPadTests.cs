using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// The pad on its own (<c>WX3</c>/<c>WX4</c>), including the ground it is <b>not</b> given. A room passes its
/// interior and the pad is clamped into it; open ground passes none and the pad lands on its marker.
///
/// <para><see cref="RoomFramesTests"/> holds the same rules read through a room, which is what proves the two
/// agree — this file is the shape a caller with no room gets.</para>
/// </summary>
public sealed class SpawnPadTests
{
    // Ground wide enough that nothing is ever clamped, so a difference here is the parity's and not the fit's.
    private static readonly BlockRect Open = new(0, 0, 40, 40);

    /// <summary>A marker on a grid line straddles it: an even square whose centre is the line itself.</summary>
    [Test]
    public async Task A_marker_on_a_grid_line_straddles_it()
    {
        var pad = SpawnPad.Fit(10, 10, Open)!.Value;

        await Assert.That(pad.Size).IsEqualTo(SpawnPad.Straddling);
        await Assert.That((pad.CenterX, pad.CenterZ)).IsEqualTo((10d, 10d));
        await Assert.That(pad.Shifted).IsFalse();
    }

    /// <summary>A marker at a block centre sits on it: an odd square whose centre is the block's own.</summary>
    [Test]
    public async Task A_marker_on_a_block_centre_sits_on_it()
    {
        var pad = SpawnPad.Fit(10.5, 10.5, Open)!.Value;

        await Assert.That(pad.Size).IsEqualTo(SpawnPad.Centred);
        await Assert.That((pad.CenterX, pad.CenterZ)).IsEqualTo((10.5, 10.5));
    }

    /// <summary>Ground too narrow for the centred square narrows the pad to the marker's own block rather
    /// than refusing — the two axes decide jointly, so one tight axis narrows both. The ground here is five
    /// across and two deep, and it is the two that decides.</summary>
    [Test]
    public async Task Ground_that_cannot_hold_the_centred_square_narrows_the_pad()
    {
        var pad = SpawnPad.Fit(10.5, 10.5, new BlockRect(8, 9, 13, 11))!.Value;

        await Assert.That(pad.Size).IsEqualTo(SpawnPad.Narrow);
        await Assert.That((pad.CenterX, pad.CenterZ)).IsEqualTo((10.5, 10.5));
    }

    /// <summary>A pad the ground pushes off its marker says so, because the exported point follows the pad
    /// and an author whose spawn moved has to be able to see that it did (<c>WX5</c>).</summary>
    [Test]
    public async Task A_pad_pushed_off_its_marker_is_flagged()
    {
        var pad = SpawnPad.Fit(1, 10, new BlockRect(4, 4, 20, 20))!.Value;

        await Assert.That(pad.Shifted).IsTrue();
        await Assert.That(pad.MinX).IsEqualTo(4);
    }

    /// <summary>Ground too small for any pad has none, which is the caller's to report.</summary>
    [Test]
    public async Task Ground_too_small_for_any_pad_has_none()
    {
        await Assert.That(SpawnPad.Fit(10, 10, new BlockRect(10, 10, 11, 11))).IsNull();
    }

    // ── open ground ─────────────────────────────────────────────────────────────────────────────────────
    /// <summary><b>A pad given no ground lands on its marker.</b> There is nothing to clamp to, so it is never
    /// shifted — and nothing to narrow against, so a block-centre marker keeps the centred square.</summary>
    [Test]
    [Arguments(10.0, SpawnPad.Straddling, 10.0)]
    [Arguments(10.5, SpawnPad.Centred, 10.5)]
    public async Task Open_ground_lands_the_pad_on_its_marker(double marker, int size, double centre)
    {
        var pad = SpawnPad.Fit(marker, marker, allowed: null)!.Value;

        await Assert.That(pad.Size).IsEqualTo(size);
        await Assert.That((pad.CenterX, pad.CenterZ)).IsEqualTo((centre, centre));
        await Assert.That(pad.Shifted).IsFalse();
    }

    /// <summary>And open ground never refuses: a pad with nothing to fit inside always has somewhere to
    /// go.</summary>
    [Test]
    public async Task Open_ground_never_refuses_a_pad()
    {
        await Assert.That(SpawnPad.Fit(-200.5, 4000.5, allowed: null)).IsNotNull();
    }

    // ── the parity the size is read off ─────────────────────────────────────────────────────────────────
    /// <summary>A pad is square, so a marker whose axes disagree is one no pad answers — the caller refuses
    /// it (<c>WX3</c>) rather than picking an axis.</summary>
    [Test]
    [Arguments(10.0, 10.5, true)]
    [Arguments(10.5, 10.0, true)]
    [Arguments(10.0, 10.0, false)]
    [Arguments(10.5, 10.5, false)]
    public async Task Mixed_parity_is_named_rather_than_resolved(double markerX, double markerZ, bool mixed)
    {
        await Assert.That(SpawnPad.MixedParity(markerX, markerZ)).IsEqualTo(mixed);
    }

    /// <summary>And the nearest offset that agrees moves the block-centre axis down onto the grid line,
    /// never below nought — offsets are piece-relative and a negative one is off the piece.</summary>
    [Test]
    [Arguments(10.0, 10.5, 10.0, 10.0)]
    [Arguments(10.5, 10.0, 10.0, 10.0)]
    [Arguments(0.5, 4.0, 0.0, 4.0)]
    public async Task The_nearest_agreeing_offset_moves_one_axis_down(
        double markerX, double markerZ, double expectedX, double expectedZ)
    {
        await Assert.That(SpawnPad.SameParity(markerX, markerZ)).IsEqualTo((expectedX, expectedZ));
    }
}
