using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>What the levels water holds along a course guarantee: they never climb, they fall only by two or more
/// after a reach of ten unless the ground forces them sooner, they never stand the water above the ground beside
/// them, and a course that runs both ways holds one level.</summary>
public sealed class ReachesTests
{
    private static double[] Arc(int stations) => [.. Enumerable.Range(0, stations).Select(station => (double)station)];

    [Test]
    public async Task Floors_never_climb_and_fall_only_after_a_reach_or_where_the_water_would_top_its_ground()
    {
        double[] low = [.. Enumerable.Range(0, 60).Select(station => 70 - 0.15 * station + (station == 30 ? 6 : 0))];
        var floors = Reaches.Floors(Arc(60), low, below: 2, water: 1, bothWays: false);

        var since = 0;
        for (var j = 1; j < floors.Length; j++)
        {
            await Assert.That(floors[j]).IsLessThanOrEqualTo(floors[j - 1]);
            await Assert.That(floors[j] + 1).IsLessThanOrEqualTo(low[j]).Because($"the water at station {j} tops its ground");
            since++;
            if (floors[j] == floors[j - 1]) continue;
            await Assert.That(floors[j - 1] - floors[j] >= 2 || since < 10).IsTrue();
            since = 0;
        }
    }

    [Test]
    public async Task A_course_that_runs_both_ways_holds_one_floor_under_its_lowest_ground()
    {
        double[] low = [.. Enumerable.Range(0, 40).Select(station => 50 + 0.4 * Math.Abs(station - 20))];
        var floors = Reaches.Floors(Arc(40), low, below: 2, water: 1, bothWays: true);

        await Assert.That(floors.Distinct().Single()).IsEqualTo(48);
    }

    [Test]
    public async Task A_station_with_no_ground_constrains_nothing()
    {
        double[] low = [double.PositiveInfinity, 60, double.PositiveInfinity, 60];
        var floors = Reaches.Floors(Arc(4), low, below: 0, water: 0, bothWays: false);

        await Assert.That(floors.All(floor => floor == 60)).IsTrue();
        await Assert.That(Reaches.Floors(Arc(2), [double.PositiveInfinity, double.PositiveInfinity], 0, 0, false)
                                 .All(double.IsNaN)).IsTrue();
    }

    [Test]
    public async Task The_low_beside_a_station_is_the_least_of_it_and_its_neighbours()
    {
        double[] lowest = [55, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, 52];

        await Assert.That(Reaches.Low(lowest, null)).IsEquivalentTo(new[] { 55, 55, double.PositiveInfinity, 52, 52 });
        await Assert.That(Reaches.Low(lowest, [1.0, 2, 3, 4, 5])[2]).IsEqualTo(3);
    }
}
