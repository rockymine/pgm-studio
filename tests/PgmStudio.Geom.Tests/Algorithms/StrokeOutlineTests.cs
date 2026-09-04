using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>
/// The band a drawn line stands for. What is asserted here is what an author can see: that the band is the
/// width they asked for, that it stays that width through a turn, that the three edges differ in the way
/// their names promise, and that the same line always gives the same band.
/// </summary>
public sealed class StrokeOutlineTests
{
    private static readonly double[][] StraightLine = [[0, 0], [40, 0]];
    private static readonly double[][] BentLine = [[0, 0], [20, 0], [30, 20], [50, 24]];

    // The band's width measured across the line at a given x — the distance between its two sides there.
    private static double WidthAcross(IReadOnlyList<double[]> ring, double x)
    {
        var atX = ring.Where(point => Math.Abs(point[0] - x) < 0.5).Select(point => point[1]).ToList();
        return atX.Count < 2 ? 0 : atX.Max() - atX.Min();
    }

    [Test]
    public async Task A_straight_line_becomes_a_band_of_the_width_that_was_asked_for()
    {
        var ring = StrokeOutline.Ring(StraightLine, radius: 4, StrokeEdge.Solid, seed: 0);

        await Assert.That(ring).IsNotEmpty();
        await Assert.That(ring.Min(point => point[1])).IsEqualTo(-4).Within(1e-9);
        await Assert.That(ring.Max(point => point[1])).IsEqualTo(4).Within(1e-9);
        // And no further than the line goes: the ends are cut square rather than capped round.
        await Assert.That(ring.Min(point => point[0])).IsEqualTo(0).Within(1e-9);
        await Assert.That(ring.Max(point => point[0])).IsEqualTo(40).Within(1e-9);
    }

    [Test]
    public async Task A_bend_does_not_pinch_the_band()
    {
        // Offsetting each side by the same amount along its own segment normal narrows a corner to
        // width·cos(θ/2); averaging the two normals at the joint is what keeps a turn walkable.
        var ring = StrokeOutline.Ring(BentLine, radius: 5, StrokeEdge.Solid, seed: 0);
        var centerline = Centerline.Of(BentLine);

        foreach (var point in centerline)
        {
            var nearest = ring.Min(edge => Math.Sqrt(
                (edge[0] - point[0]) * (edge[0] - point[0]) + (edge[1] - point[1]) * (edge[1] - point[1])));
            await Assert.That(nearest).IsGreaterThan(4.0);   // never closer than four fifths of the half-width
        }
    }

    [Test]
    public async Task A_tapered_path_is_widest_in_the_middle_and_thinnest_at_its_ends()
    {
        var ring = StrokeOutline.Ring(StraightLine, radius: 6, StrokeEdge.Tapered, seed: 0);

        await Assert.That(WidthAcross(ring, 20)).IsGreaterThan(WidthAcross(ring, 2));
        await Assert.That(WidthAcross(ring, 20)).IsGreaterThan(WidthAcross(ring, 38));
        await Assert.That(WidthAcross(ring, 20)).IsEqualTo(12).Within(0.2);   // full width at the middle
    }

    [Test]
    public async Task A_rough_edge_wanders_around_the_width_rather_than_away_from_it()
    {
        var solid = StrokeOutline.Ring(StraightLine, radius: 5, StrokeEdge.Solid, seed: 0);
        var rough = StrokeOutline.Ring(StraightLine, radius: 5, StrokeEdge.Rough, seed: 11);

        var widths = Enumerable.Range(2, 36).Select(x => WidthAcross(rough, x)).Where(w => w > 0).ToList();
        await Assert.That(widths.Distinct().Count()).IsGreaterThan(5);        // it actually varies
        await Assert.That(widths.Average()).IsEqualTo(10).Within(2.0);        // around the width asked for
        await Assert.That(rough.Count).IsEqualTo(solid.Count);                // same line, same point count
    }

    [Test]
    public async Task Each_side_of_a_rough_path_wanders_on_its_own()
    {
        // Both sides reading one noise row would make the band breathe in and out symmetrically, which reads
        // as a pulsing tube rather than an eroded verge.
        var ring = StrokeOutline.Ring(StraightLine, radius: 5, StrokeEdge.Rough, seed: 3);
        var half = ring.Count / 2;
        var left = ring.Take(half).Select(point => Math.Abs(point[1])).ToList();
        var right = ring.Skip(half).Select(point => Math.Abs(point[1])).Reverse().ToList();

        var same = left.Zip(right).Count(pair => Math.Abs(pair.First - pair.Second) < 1e-9);
        await Assert.That(same).IsLessThan(left.Count / 2);
    }

    [Test]
    public async Task The_same_line_and_seed_always_give_the_same_band()
    {
        var first = StrokeOutline.Ring(BentLine, radius: 4, StrokeEdge.Rough, seed: 42);
        var again = StrokeOutline.Ring(BentLine, radius: 4, StrokeEdge.Rough, seed: 42);
        var other = StrokeOutline.Ring(BentLine, radius: 4, StrokeEdge.Rough, seed: 43);

        await Assert.That(first.SelectMany(point => point)).IsEquivalentTo(again.SelectMany(point => point));
        await Assert.That(first.SelectMany(point => point)).IsNotEquivalentTo(other.SelectMany(point => point));
    }

    [Test]
    public async Task A_line_with_nowhere_to_go_is_no_band()
    {
        await Assert.That(StrokeOutline.Ring([[5, 5]], radius: 3, StrokeEdge.Solid, seed: 0)).IsEmpty();
        await Assert.That(StrokeOutline.Ring(StraightLine, radius: 0, StrokeEdge.Solid, seed: 0)).IsEmpty();
    }

    [Test]
    public async Task Two_drawn_points_are_densified_along_the_line_they_drew()
    {
        // There is no curve to sample through two points, but a width that varies along the stroke needs
        // somewhere to vary: left at two, a taper reads only its two ends and comes out uniformly thin.
        var centerline = Centerline.Of(StraightLine);

        await Assert.That(centerline.Count).IsEqualTo(Centerline.SmoothSamples + 1);
        await Assert.That(centerline[0]).IsEquivalentTo(StraightLine[0]);      // and the ends do not move
        await Assert.That(centerline[^1]).IsEquivalentTo(StraightLine[1]);
        await Assert.That(centerline.All(point => point[1] == 0)).IsTrue();
    }
}
