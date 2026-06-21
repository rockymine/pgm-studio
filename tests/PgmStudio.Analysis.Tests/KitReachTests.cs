using PgmStudio.Analysis.Playability;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Synthetic budget-aware reachability tests: the bridge cost is the count of non-walkable cells
/// crossed (one placed block each), compared to the kit's placeable-block budget. Full-map runs go
/// through the kit-reach endpoint over the feature maps.
/// </summary>
public sealed class KitReachTests
{
    private static Dict Xz(double x, double z) => new() { ["x"] = x, ["z"] = z };

    private static Dict Rect(double minx, double minz, double maxx, double maxz) => new()
    {
        ["type"] = "rectangle",
        ["min"] = Xz(minx, minz),
        ["max"] = Xz(maxx, maxz),
        ["bounds_2d"] = new Dict { ["min"] = Xz(minx, minz), ["max"] = Xz(maxx, maxz) },
    };

    // Two 3×3 walkable pads at z∈{0,1,2}: spawn x∈{0,1,2}, wool x∈{8,9,10}. The x∈{3..7} gap (5 cells)
    // has no walkable ground and no deny rule → bridgeable (cost 1 each). woodAmount = kit block budget.
    private static (Dict data, HashSet<(int, int)> walkable) Scenario(int woodAmount)
    {
        var data = new Dict
        {
            ["regions"] = new Dict { ["spawn"] = Rect(0, 0, 3, 3) },
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["kit"] = "k", ["region"] = "spawn" } },
            ["wools"] = new List<object?> { new Dict { ["color"] = "green", ["location"] = new Dict { ["x"] = 9.0, ["y"] = 0.0, ["z"] = 1.0 } } },
            ["kits"] = new List<object?>
            {
                new Dict { ["id"] = "k", ["items"] = new List<object?> { new Dict { ["material"] = "wood", ["amount"] = woodAmount } } },
            },
        };
        var walkable = new HashSet<(int, int)>();
        for (var z = 0; z < 3; z++) { for (var x = 0; x < 3; x++) walkable.Add((x, z)); for (var x = 8; x < 11; x++) walkable.Add((x, z)); }
        return (data, walkable);
    }

    [Test]
    public async Task Bridge_cost_is_the_gap_width_and_fits_a_sufficient_kit()
    {
        var (data, walkable) = Scenario(woodAmount: 10);
        var res = KitReach.Check(data, walkable, y0Columns: null, bbox: (-2, -2, 13, 5));

        var wool = res.Teams.Single().Wools.Single();
        await Assert.That(res.Teams.Single().Budget).IsEqualTo(10);
        await Assert.That(wool.BlocksNeeded).IsEqualTo(5);     // the 5-wide bridgeable gap
        await Assert.That(wool.Reachable).IsTrue();
        await Assert.That(wool.WithinBudget).IsTrue();
        await Assert.That(res.Severity).IsEqualTo("ok");
    }

    [Test]
    public async Task A_kit_short_of_the_gap_warns_but_stays_reachable()
    {
        var (data, walkable) = Scenario(woodAmount: 3);
        var res = KitReach.Check(data, walkable, y0Columns: null, bbox: (-2, -2, 13, 5));

        var wool = res.Teams.Single().Wools.Single();
        await Assert.That(res.Teams.Single().Budget).IsEqualTo(3);
        await Assert.That(wool.BlocksNeeded).IsEqualTo(5);     // gap unchanged
        await Assert.That(wool.Reachable).IsTrue();
        await Assert.That(wool.WithinBudget).IsFalse();        // 3 < 5
        await Assert.That(res.Severity).IsEqualTo("warning");
    }
}
