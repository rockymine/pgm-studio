using PgmStudio.Geom;
using TUnit.Assertions.Enums;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// A pull is a point stated by where it stands on an edge and how far it is moved across it. What has to hold is
/// that the point lands where that statement puts it — on the side the ring itself calls inside, whichever way
/// the ring is wound and wherever its centroid falls — and that a board's coast written that way comes out
/// point for point.
/// </summary>
public sealed class RingPullTests
{
    private static Dictionary<int, IReadOnlyList<RingPull.Pull>> Pulls(params (int Edge, double At, double In)[] pulls) =>
        pulls.GroupBy(pull => pull.Edge)
            .ToDictionary(edge => edge.Key, edge => (IReadOnlyList<RingPull.Pull>)[.. edge.Select(pull => new RingPull.Pull(pull.At, pull.In))]);

    private static List<string> Points(IEnumerable<double[]> ring) =>
        [.. ring.Select(point => FormattableString.Invariant($"({point[0]}, {point[1]})"))];

    /// <summary>Gypsum Reach's field as the plan compiles it, and the twenty points its coast is drawn with: the
    /// frontline, edge 3, pushed out toward the island at its south end and pulled in north of it.</summary>
    [Test]
    public async Task A_coast_stated_as_pulls_is_drawn_point_for_point()
    {
        double[][] field = [[-112, 24], [-104, 24], [-104, -48], [-16, -48], [-16, 48], [-112, 48]];
        var pulls = Pulls(
            (1, 0.3, 2), (1, 0.6, 3), (1, 0.85, 2),
            (2, 0.15, 2), (2, 0.4, 3), (2, 0.65, 1), (2, 0.88, 3),
            (3, 0.083, -4), (3, 0.19, -7), (3, 0.29, 0), (3, 0.42, 4), (3, 0.54, 5), (3, 0.67, 3), (3, 0.79, 6), (3, 0.9, 8), (3, 0.97, 3),
            (4, 0.1, 3), (4, 0.3, 2), (4, 0.5, 4), (4, 0.7, 2));

        var drawn = RingPull.Draw(field, pulls);

        await Assert.That(drawn).IsNotNull();
        await Assert.That(Points(drawn!)).IsEquivalentTo(Points([
            [-112, 24], [-104, 24], [-102, 2.4], [-101, -19.2], [-102, -37.2], [-104, -48], [-90.8, -46], [-68.8, -45],
            [-46.8, -47], [-26.6, -45], [-16, -48], [-12, -40], [-9, -29.8], [-16, -20.2], [-20, -7.7], [-21, 3.8],
            [-19, 16.3], [-22, 27.8], [-24, 38.4], [-19, 45.1], [-16, 48], [-25.6, 45], [-44.8, 46], [-64, 44],
            [-83.2, 46], [-112, 48]]), CollectionOrdering.Matching);
    }

    /// <summary>A U whose centroid stands in the gap between its arms: a pull into the right arm's inner side has
    /// to go into the arm, where a pull toward the centroid would push it out over the gap.</summary>
    [Test]
    public async Task Inside_is_the_side_the_ring_calls_inside_not_the_side_its_centroid_is_on()
    {
        double[][] u = [[0, 0], [30, 0], [30, 30], [25, 30], [25, 5], [5, 5], [5, 30], [0, 30]];

        var drawn = RingPull.Draw(u, Pulls((3, 0.5, 2)));

        await Assert.That(drawn![4]).IsEquivalentTo([27.0, 17.5]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task A_positive_pull_goes_in_and_a_negative_one_out_whichever_way_the_ring_is_wound(bool reversed)
    {
        double[][] square = [[0, 0], [40, 0], [40, 40], [0, 40]];
        var ring = reversed ? [.. square.Reverse()] : square;
        var edge = reversed ? 2 : 0;

        var drawn = RingPull.Draw(ring, Pulls((edge, 0.25, 3), (edge, 0.75, -3)))!;

        var placed = drawn.Where(point => point[1] is not (0 or 40)).Select(point => (point[0], point[1])).ToList();
        await Assert.That(placed).Contains((reversed ? 30.0 : 10.0, 3.0));
        await Assert.That(placed).Contains((reversed ? 10.0 : 30.0, -3.0));
    }

    [Test]
    public async Task Points_land_in_order_along_their_edge_whatever_order_they_are_stated_in()
    {
        double[][] square = [[0, 0], [40, 0], [40, 40], [0, 40]];

        var drawn = RingPull.Draw(square, Pulls((0, 0.75, 1), (0, 0.25, 1), (0, 0.5, 1)))!;

        await Assert.That(Points(drawn[1..4])).IsEquivalentTo(["(10, 1)", "(20, 1)", "(30, 1)"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_pull_that_folds_the_ring_across_itself_draws_nothing()
    {
        double[][] square = [[0, 0], [40, 0], [40, 40], [0, 40]];

        await Assert.That(RingPull.Draw(square, Pulls((0, 0.5, 55)))).IsNull();
    }
}
