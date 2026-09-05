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
    private static LineMark Switchback(double pitch, double drop, double radius, double tread = double.NaN)
    {
        var left = 30 - pitch / 2;
        var right = 30 + pitch / 2;
        double[][] points = [[left, 6], [left, 50], [right, 54], [right, 6]];
        return new LineMark(points, [20, 20, 20 - drop, 20 - drop], radius, tread);
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

    [Test]
    public async Task A_lone_line_is_unchanged_by_a_tread()
    {
        // No second pass to ramp toward, so the shoulder past the tread is left to the relaxation exactly as
        // it always was — the loft is a statement about a line that comes back, not about every line.
        var footprint = Board();
        double[][] straight = [[30, 6], [30, 54]];
        var whole = Pinned(footprint, new LineMark(straight, [20], 5));
        var trod = Pinned(footprint, new LineMark(straight, [20], 5, 2));

        // The tread pins its flat band and nothing else; the full-band mark pins the whole five.
        await Assert.That(trod.ContainsKey((31, 30))).IsTrue();     // inside the tread
        await Assert.That(trod.ContainsKey((34, 30))).IsFalse();    // past it, and free
        await Assert.That(whole.ContainsKey((34, 30))).IsTrue();
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
