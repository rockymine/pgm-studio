using PgmStudio.Analysis.Scan;
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

    // Two 3×3 walkable pads at z∈{0,1,2}: spawn x∈{0,1,2}, wool x∈{8,9,10}. The x∈{3..7} gap (5 cells) has
    // no walkable ground, and a build zone over the whole board grants placing there → bridgeable (cost 1
    // each). The grant is what makes it a crossing: ground no rule names is void nobody may bridge.
    // woodAmount = kit block budget.
    private static (Dict data, SegmentIndex ground) Scenario(int woodAmount, int woolPadTop = 0)
    {
        var data = new Dict
        {
            ["regions"] = new Dict { ["spawn"] = Rect(0, 0, 3, 3), ["field"] = Rect(-2, -2, 13, 6) },
            ["apply_rules"] = new List<object?>
            {
                new Dict { ["region"] = "field", ["block_place"] = "always" },
            },
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["kit"] = "k", ["region"] = "spawn" } },
            ["wools"] = new List<object?> { new Dict { ["color"] = "green", ["location"] = new Dict { ["x"] = 9.0, ["y"] = 0.0, ["z"] = 1.0 } } },
            ["kits"] = new List<object?>
            {
                new Dict { ["id"] = "k", ["items"] = new List<object?> { new Dict { ["material"] = "wood", ["amount"] = woodAmount } } },
            },
        };
        // Each pad is one solid course, so a column's standing top is one above it. The wool pad may be
        // raised, which is what gives the walk a climb to charge for.
        var rows = new List<(int, int, int, int)>();
        for (var z = 0; z < 3; z++)
        {
            for (var x = 0; x < 3; x++) rows.Add((x, z, 0, 0));
            for (var x = 8; x < 11; x++) rows.Add((x, z, 0, woolPadTop));
        }
        return (data, new SegmentIndex(rows));
    }

    [Test]
    public async Task Bridge_cost_is_the_gap_width_and_fits_a_sufficient_kit()
    {
        var (data, ground) = Scenario(woodAmount: 10);
        var res = KitReach.Check(data, ground);

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
        var (data, ground) = Scenario(woodAmount: 3);
        var res = KitReach.Check(data, ground);

        var wool = res.Teams.Single().Wools.Single();
        await Assert.That(res.Teams.Single().Budget).IsEqualTo(3);
        await Assert.That(wool.BlocksNeeded).IsEqualTo(5);     // gap unchanged
        await Assert.That(wool.Reachable).IsTrue();
        await Assert.That(wool.WithinBudget).IsFalse();        // 3 < 5
        await Assert.That(res.Severity).IsEqualTo("warning");
    }

    [Test]
    public async Task A_wool_up_a_scarp_costs_the_climb_as_well_as_the_gap()
    {
        // The same five-cell gap, with the wool's pad standing four blocks over the spawn's: five blocks to
        // bridge, and three more to climb the four (a rise of Δ costs Δ−1).
        var (data, ground) = Scenario(woodAmount: 10, woolPadTop: 4);
        var wool = KitReach.Check(data, ground).Teams.Single().Wools.Single();

        await Assert.That(wool.BlocksNeeded).IsEqualTo(8);
        await Assert.That(wool.Reachable).IsTrue();
        await Assert.That(wool.Blocks).IsGreaterThan(0);       // and it says how far round it went
    }

    [Test]
    public async Task A_wool_behind_an_enter_denial_is_unreachable_for_the_team_it_bars()
    {
        // The same board, with a rule barring red from the ground its own bridge would land on. The gap is
        // still bridgeable and the wool still stands there; red is simply not allowed to arrive.
        var (data, ground) = Scenario(woodAmount: 10);
        data["regions"] = new Dict
        {
            ["spawn"] = Rect(0, 0, 3, 3),
            ["field"] = Rect(-2, -2, 13, 6),
            ["keep"] = Rect(7, 0, 11, 3),
        };
        data["filters"] = new Dict { ["only-blue"] = new Dict { ["type"] = "team", ["team"] = "blue" } };
        data["apply_rules"] = new List<object?>
        {
            new Dict { ["region"] = "field", ["block_place"] = "always" },
            new Dict { ["region"] = "keep", ["enter"] = "only-blue" },
        };

        var wool = KitReach.Check(data, ground).Teams.Single().Wools.Single();

        await Assert.That(wool.Reachable).IsFalse();
        await Assert.That(wool.Severity).IsEqualTo("error");
    }

    [Test]
    public async Task A_rule_that_bars_nobody_leaves_the_crossing_as_it_was()
    {
        // The same wiring with a filter that admits red: an entry denial has to be provable to subtract, so
        // this one takes no ground away and the gap costs what it always did.
        var (data, ground) = Scenario(woodAmount: 10);
        data["regions"] = new Dict
        {
            ["spawn"] = Rect(0, 0, 3, 3),
            ["field"] = Rect(-2, -2, 13, 6),
            ["keep"] = Rect(7, 0, 11, 3),
        };
        data["filters"] = new Dict { ["only-red"] = new Dict { ["type"] = "team", ["team"] = "red" } };
        data["apply_rules"] = new List<object?>
        {
            new Dict { ["region"] = "field", ["block_place"] = "always" },
            new Dict { ["region"] = "keep", ["enter"] = "only-red" },
        };

        var wool = KitReach.Check(data, ground).Teams.Single().Wools.Single();

        await Assert.That(wool.Reachable).IsTrue();
        await Assert.That(wool.BlocksNeeded).IsEqualTo(5);
    }

    [Test]
    public async Task A_team_barred_from_its_own_wool_is_reported_and_not_blamed()
    {
        // A wool room's own rule bars its defender by design, which is the ruling the traversability verdict
        // is written under. The row still says what it found; the verdict is over the wools a team must
        // capture, and its own is not one of them.
        var (data, ground) = Scenario(woodAmount: 10);
        data["wools"] = new List<object?>
        {
            new Dict
            {
                ["color"] = "green", ["team"] = "red",
                ["location"] = new Dict { ["x"] = 9.0, ["y"] = 0.0, ["z"] = 1.0 },
            },
        };
        data["regions"] = new Dict
        {
            ["spawn"] = Rect(0, 0, 3, 3),
            ["field"] = Rect(-2, -2, 13, 6),
            ["keep"] = Rect(7, 0, 11, 3),
        };
        data["filters"] = new Dict { ["only-blue"] = new Dict { ["type"] = "team", ["team"] = "blue" } };
        data["apply_rules"] = new List<object?>
        {
            new Dict { ["region"] = "field", ["block_place"] = "always" },
            new Dict { ["region"] = "keep", ["enter"] = "only-blue" },
        };

        var res = KitReach.Check(data, ground);
        var wool = res.Teams.Single().Wools.Single();

        await Assert.That(wool.Owner).IsEqualTo("red");
        await Assert.That(wool.Reachable).IsFalse();
        await Assert.That(res.Severity).IsEqualTo("ok");
    }
}
