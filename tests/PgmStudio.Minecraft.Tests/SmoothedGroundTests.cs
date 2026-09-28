using PgmStudio.Minecraft.Painting;
namespace PgmStudio.Minecraft.Tests;

/// <summary>The ground a following height stack measures from (TP26): each column's surface averaged over the
/// footprint cells round it.</summary>
public sealed class SmoothedGroundTests
{
    private static Dictionary<(int X, int Z), int> Board(int width, int depth, Func<int, int, int> top)
    {
        var tops = new Dictionary<(int X, int Z), int>();
        for (var x = 0; x < width; x++)
            for (var z = 0; z < depth; z++)
                tops[(x, z)] = top(x, z);
        return tops;
    }

    /// <summary>A plane is its own mean: away from the edges a window centred on a column averages to that
    /// column's own top, whatever the reach, so a following stack lays its bands parallel to an even grade.</summary>
    [Test]
    public async Task An_even_grade_averages_to_the_columns_own_top_at_any_reach()
    {
        var ground = new SmoothedGround(Board(40, 40, (x, z) => 10 + x + 2 * z));
        foreach (var reach in new[] { 1, 4, 9 })
            foreach (var (x, z) in new[] { (10, 10), (20, 15), (29, 29) })
                await Assert.That((reach, x, z, ground.Height(x, z, reach))).IsEqualTo((reach, x, z, (int?)(10 + x + 2 * z)));
    }

    /// <summary>Only ground counts toward the mean. A coast averages the land it has, so a level shelf ending at
    /// the void stays level to its edge instead of sagging toward y 0.</summary>
    [Test]
    public async Task The_void_beside_a_coast_is_not_counted_as_ground()
    {
        var ground = new SmoothedGround(Board(10, 10, (_, _) => 20));
        await Assert.That(ground.Height(9, 9, 5)).IsEqualTo(20);
        await Assert.That(ground.Height(0, 5, 8)).IsEqualTo(20);
    }

    /// <summary>The reach is what decides whether a hill lifts the bands. Read one cell either side, a 3×3 knoll
    /// six blocks over a level field lifts the datum by all six; read sixteen either side, it moves it by nothing,
    /// so the knoll cuts through the bands the field around it carries.</summary>
    [Test]
    public async Task A_narrow_reach_follows_a_knoll_and_a_wide_one_does_not()
    {
        var ground = new SmoothedGround(Board(41, 41, (x, z) => Math.Abs(x - 20) <= 1 && Math.Abs(z - 20) <= 1 ? 26 : 20));
        await Assert.That(ground.Height(20, 20, 1)).IsEqualTo(26);
        await Assert.That(ground.Height(20, 20, 16)).IsEqualTo(20);
    }

    [Test]
    public async Task A_window_holding_no_ground_answers_nothing()
    {
        var ground = new SmoothedGround(Board(5, 5, (_, _) => 12));
        await Assert.That(ground.Height(100, 100, 3)).IsNull();
        await Assert.That(new SmoothedGround(new Dictionary<(int X, int Z), int>()).Height(0, 0, 3)).IsNull();
    }
}
