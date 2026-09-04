using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>
/// A quantity stated at each drawn point of an open line, read anywhere along it. The whole of it is the
/// mapping from a drawn point to its arc length on the dense centreline, which is <c>k · SmoothSamples</c>
/// in both branches of <see cref="Centerline.Of"/> — so a straight line and a curved one are read the same
/// way, and a caller never re-derives where a drawn point went.
/// </summary>
public sealed class ArcProfileTests
{
    /// <summary>A straight two-point line's drawn points sit at nought and its own length.</summary>
    [Test]
    public async Task A_straight_line_anchors_at_its_two_ends()
    {
        var line = Centerline.Of([[0, 0], [100, 0]]);
        var anchors = Centerline.Anchors(line, 2);

        await Assert.That(anchors.Length).IsEqualTo(2);
        await Assert.That(anchors[0]).IsEqualTo(0);
        await Assert.That(anchors[1]).IsEqualTo(100).Within(0.001);
    }

    /// <summary>A curve's drawn points sit at their own arc lengths, ascending, and the last is the whole
    /// length — which is longer than the chords, a spline bowing between its points.</summary>
    [Test]
    public async Task A_curve_anchors_in_order_and_ends_at_its_whole_length()
    {
        double[][] drawn = [[0, 0], [50, 30], [100, 0]];
        var line = Centerline.Of(drawn);
        var anchors = Centerline.Anchors(line, 3);

        await Assert.That(anchors[0]).IsEqualTo(0);
        await Assert.That(anchors[1]).IsGreaterThan(anchors[0]);
        await Assert.That(anchors[2]).IsGreaterThan(anchors[1]);
        await Assert.That(anchors[2]).IsGreaterThan(116.0)
            .Because("the chords are 2 × √(50² + 30²) ≈ 116.6 and the curve bows past them");
    }

    /// <summary>The reading between two drawn points is the mix of what they state, by how far along the arc
    /// the place asked about sits.</summary>
    [Test]
    public async Task A_place_between_two_drawn_points_mixes_what_they_state()
    {
        var line = Centerline.Of([[0, 0], [100, 0]]);
        var profile = ArcProfile.Of(line, [4.0, 20.0])!.Value;

        await Assert.That(profile.At(0)).IsEqualTo(4).Within(0.001);
        await Assert.That(profile.At(50)).IsEqualTo(12).Within(0.001);
        await Assert.That(profile.At(100)).IsEqualTo(20).Within(0.001);
        await Assert.That(profile.At(25)).IsEqualTo(8).Within(0.001);
    }

    /// <summary>Before the first drawn point and past the last, it is that end's own value — a band is cut
    /// square at its ends, and the cells there carry the height they were drawn at rather than nought.</summary>
    [Test]
    public async Task Past_either_end_it_is_that_ends_own_value()
    {
        var profile = ArcProfile.Of(Centerline.Of([[0, 0], [100, 0]]), [4.0, 20.0])!.Value;

        await Assert.That(profile.At(-30)).IsEqualTo(4);
        await Assert.That(profile.At(400)).IsEqualTo(20);
    }

    /// <summary>Three drawn points read as two runs, so a ramp up and down is one profile.</summary>
    [Test]
    public async Task Three_drawn_points_read_as_two_runs()
    {
        var line = Centerline.Of([[-60, 0], [0, 0], [60, 0]]);
        var profile = ArcProfile.Of(line, [4.0, 20.0, 4.0])!.Value;
        var whole = profile.Anchors[^1];

        await Assert.That(profile.At(whole * 0.5)).IsEqualTo(20).Within(0.5);
        await Assert.That(profile.At(whole * 0.25)).IsEqualTo(12).Within(0.5);
        await Assert.That(profile.At(whole * 0.75)).IsEqualTo(12).Within(0.5);
    }

    /// <summary>A statement that does not line up with the line is no profile: the reading is one value to
    /// one drawn point, and fewer than two points is no line to read along.</summary>
    [Test]
    public async Task A_statement_that_does_not_line_up_is_no_profile()
    {
        await Assert.That(ArcProfile.Of(Centerline.Of([[0, 0], [100, 0]]), [4.0])).IsNull();
        await Assert.That(ArcProfile.Of([], [4.0, 20.0])).IsNull();
    }
}
