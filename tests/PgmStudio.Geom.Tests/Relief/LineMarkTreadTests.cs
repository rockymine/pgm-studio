using PgmStudio.Geom;
using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// A line that passes close to itself, and what its tread decides. The claim: pinning the whole band flat
/// makes the ground between two passes a wall whatever the drawing says, because every cell snaps to whichever
/// pass is nearer and two cells either side of the midline take heights a whole winding apart. Stating a tread
/// narrower than the reach lofts the rest — a straight ramp between the two treads' edges — so the same
/// drawing comes out as flat road and graded batter.
/// </summary>
public sealed class LineMarkTreadTests
{
    private static Footprint Board(int span = 60) =>
        Footprint.Of([([[0, 0], [span, 0], [span, span], [0, span]], false)]);

    /// <summary>Two straight passes of one line running down the board, <paramref name="pitch"/> apart in x
    /// and <paramref name="drop"/> blocks apart in height, joined round the far end so they are one polyline.
    /// A switchback, reduced to the two things about it that matter.</summary>
    private static LineMark Switchback(double pitch, double drop, double radius, double tread = double.NaN,
                                       double batter = 0)
    {
        var left = 30 - pitch / 2;
        var right = 30 + pitch / 2;
        double[][] points = [[left, 6], [left, 50], [right, 54], [right, 6]];
        return new LineMark(points, [20, 20, 20 - drop, 20 - drop], radius, tread, batter);
    }

    private static Dictionary<(int X, int Z), double> Pinned(Footprint footprint, Mark mark)
    {
        var pins = new Dictionary<(int X, int Z), double>();
        foreach (var (cell, height) in mark.Pins(footprint)) pins[cell] = height;
        return pins;
    }

    /// <summary>The worst height difference between two neighbouring pinned cells along the row that crosses
    /// both passes — the wall, measured.</summary>
    private static double WorstStep(Dictionary<(int X, int Z), double> pins, int z, int fromX, int toX)
    {
        double worst = 0;
        for (var x = fromX; x < toX; x++)
            if (pins.TryGetValue((x, z), out var here) && pins.TryGetValue((x + 1, z), out var next))
                worst = Math.Max(worst, Math.Abs(next - here));
        return worst;
    }

    [Test]
    public async Task A_line_that_laps_itself_pins_a_wall_between_its_passes()
    {
        // Bands 5 either side, passes 8 apart: they overlap, so no cell between them is left free and the
        // relaxation has nothing to grade. Every cell takes its nearer pass, and the midline is the whole drop.
        var footprint = Board();
        var pins = Pinned(footprint, Switchback(pitch: 8, drop: 6, radius: 5));

        await Assert.That(WorstStep(pins, 28, 20, 40)).IsEqualTo(6).Within(0.01);
    }

    [Test]
    public async Task A_tread_narrower_than_the_reach_lofts_the_ground_between_the_passes()
    {
        // The same drawing with two of the five cells flat: the drop now runs over the four cells between the
        // two treads, so no single step is more than a block and a half.
        var footprint = Board();
        var pins = Pinned(footprint, Switchback(pitch: 8, drop: 6, radius: 5, tread: 2));

        await Assert.That(WorstStep(pins, 28, 20, 40)).IsLessThanOrEqualTo(1.6);
    }

    [Test]
    public async Task The_tread_itself_stays_flat()
    {
        // The point of stating a tread rather than just narrowing the radius: what it keeps is a road. Every
        // cell within two of a pass sits at that pass's own height, to the block.
        var footprint = Board();
        var mark = Switchback(pitch: 8, drop: 6, radius: 5, tread: 2);
        var pins = Pinned(footprint, mark);

        // The left pass runs at x = 26 and is stated at 20 for its whole length.
        foreach (var x in new[] { 25, 26, 27 })
        {
            await Assert.That(pins.TryGetValue((x, 28), out var height)).IsTrue();
            await Assert.That((x, height)).IsEqualTo((x, 20d));
        }
    }

    /// <summary><b>A tread never shrinks the band a mark claims.</b> The tread says what happens where the
    /// line comes back past itself and nothing else, so a line with no second pass pins its whole reach
    /// exactly as it did before there was a tread at all. A mark that stopped claiming its band would hand
    /// those cells back to whichever earlier mark had pinned them, and what shows through is that mark's
    /// height standing beside this one's — a wall from somewhere else.</summary>
    [Test]
    public async Task A_tread_never_shrinks_the_band_a_line_claims()
    {
        var footprint = Board();
        double[][] straight = [[30, 6], [30, 54]];
        var whole = Pinned(footprint, new LineMark(straight, [20], 5));
        var trod = Pinned(footprint, new LineMark(straight, [20], 5, 2));

        await Assert.That(trod.Keys.Order()).IsEquivalentTo(whole.Keys.Order());
        foreach (var cell in whole.Keys)
            await Assert.That((cell, trod[cell])).IsEqualTo((cell, whole[cell]));
    }

    /// <summary>The regression this cost a build to find: an earlier mark's pin showing through a later
    /// mark's shoulder. A high line drawn first, a low one drawn over it with a tread — every cell the low
    /// line's band covers must carry the low line's ground, or the high one stands beside the low one as a
    /// wall nobody drew.</summary>
    [Test]
    public async Task A_lines_shoulder_still_covers_what_an_earlier_mark_pinned()
    {
        var footprint = Board();
        var high = new LineMark([[30, 0], [30, 60]], [34], 18);
        var low = new LineMark([[30, 20], [30, 40]], [12], 5, 2);

        var pins = new Dictionary<(int X, int Z), double>();
        foreach (var (cell, height) in high.Pins(footprint)) pins[cell] = height;
        foreach (var (cell, height) in low.Pins(footprint)) pins[cell] = height;

        // Four cells out from the low line is past its tread and inside its reach: the low line's, not the
        // high line's, whatever order they were written in.
        await Assert.That(pins[(34, 30)]).IsEqualTo(12);
    }

    /// <summary>A stated batter is a bench under a bank: the fall runs at the angle from the upper tread's
    /// edge and then holds at the lower pass's height, rather than spreading over the whole run.</summary>
    [Test]
    public async Task A_stated_batter_falls_at_its_angle_and_then_holds()
    {
        // Passes 14 apart with treads of 2 leave 10 blocks of run for a 6-block fall — 31 degrees left to
        // itself. Asked for 60, the fall takes about 3.5 blocks and the rest is flat at the lower level.
        var footprint = Board();
        var pins = Pinned(footprint, Switchback(pitch: 14, drop: 6, radius: 7, tread: 2, batter: 60));

        // The high pass runs at x = 23 at height 20; the low at x = 37 at height 14.
        await Assert.That(pins[(24, 28)]).IsEqualTo(20).Within(0.01);        // on the tread
        await Assert.That(pins[(30, 28)]).IsEqualTo(14).Within(0.01);        // past the toe, flat and low
        // The batter is doing the work rather than a gentle ramp: three and a half blocks out from the upper
        // tread the ground has already fallen the whole six, where the free ramp would be a fifth of the way.
        await Assert.That(pins[(28, 28)]).IsEqualTo(14).Within(0.01);
        await Assert.That(pins[(27, 28)]).IsLessThan(16.5);
    }

    /// <summary>A batter gentler than the run requires is raised to what the run needs, because the ramp has
    /// to have arrived by the time it meets the next tread — anything left over is a step.</summary>
    [Test]
    public async Task A_batter_gentler_than_the_run_requires_is_raised_to_it()
    {
        var footprint = Board();
        var asked = Pinned(footprint, Switchback(pitch: 8, drop: 6, radius: 5, tread: 2, batter: 10));
        var free = Pinned(footprint, Switchback(pitch: 8, drop: 6, radius: 5, tread: 2));

        foreach (var cell in free.Keys)
            await Assert.That((cell, asked[cell])).IsEqualTo((cell, free[cell]));
    }

    [Test]
    public async Task The_far_side_of_a_bend_is_not_a_second_pass()
    {
        // What separates a winding from a corner: distance travelled along the line, not distance across it.
        // A hairpin's two limbs are one pass where they meet, so the cells at the turn stay flat rather than
        // ramping toward a height the line reaches two blocks away.
        var footprint = Board();
        double[][] hairpin = [[28, 10], [28, 30], [32, 30], [32, 10]];
        var pins = Pinned(footprint, new LineMark(hairpin, [20, 20, 14, 14], 5, 2));

        // At the turn itself both limbs are the same height, so nothing there can ramp.
        await Assert.That(pins.TryGetValue((30, 29), out var atTurn)).IsTrue();
        await Assert.That(atTurn).IsBetween(13.9, 20.1);
    }
}
