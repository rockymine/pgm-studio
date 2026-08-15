using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// The two shapes that used to be re-declared per consumer, and the conventions that make them two types
/// rather than one.
///
/// <para>Neither is interesting arithmetic. What is worth pinning is the convention each carries, because
/// that is what a re-declaration loses: a copy written from the field names alone gets the numbers right and
/// the edges wrong, and an off-by-one on a footprint edge is a wool room an author can stand outside of.</para>
/// </summary>
public sealed class RectTests
{
    /// <summary>A footprint covers both its edges. This is the whole difference from
    /// <see cref="CellRect.Contains"/>, which is half-open, and it is why the two do not share a verb: a block
    /// on the far edge of a protection region is inside it, where a cell at <c>MaxX</c> is the first one past.
    /// </summary>
    [Test]
    public async Task A_footprint_covers_both_of_its_edges()
    {
        var footprint = new Rect(10, 20, 14, 26);

        await Assert.That(footprint.Covers(10, 20)).IsTrue();
        await Assert.That(footprint.Covers(14, 26)).IsTrue();
        await Assert.That(footprint.Covers(14.5, 26)).IsFalse();
        await Assert.That(footprint.Covers(9.5, 20)).IsFalse();

        await Assert.That(CellRect.FromBounds(10, 20, 14, 26).Contains(14, 26)).IsFalse();
    }

    /// <summary>The half-block lattice is the point of the doubles: a marker is placed on a half-step, so a
    /// footprint that could only hold integers would round every one of them onto a block corner.</summary>
    [Test]
    public async Task A_footprint_holds_the_half_steps_a_marker_sits_on()
    {
        var room = new Rect(0.5, 0.5, 8.5, 8.5);

        await Assert.That(room.Covers(0.5, 8.5)).IsTrue();
        await Assert.That(room.Covers(0.4, 8.5)).IsFalse();
    }

    /// <summary>A volume's centre falls on a half-step whenever its span is odd, because both corners are
    /// occupied blocks: a 5-wide casing from x=0 covers 0..4 and is centred on 2.5, not on 2. Getting this
    /// wrong puts an objective's marker one half-block off its own casing.</summary>
    [Test]
    public async Task A_volumes_centre_counts_both_of_its_corner_blocks()
    {
        var casing = new BlockBox(0, 64, 0, 4, 68, 4);

        await Assert.That(casing.Width).IsEqualTo(5);
        await Assert.That(casing.CentreX).IsEqualTo(2.5);
        await Assert.That(casing.CentreZ).IsEqualTo(2.5);
    }
}
