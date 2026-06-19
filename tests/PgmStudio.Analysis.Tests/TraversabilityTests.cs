using PgmStudio.Analysis.Playability;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Synthetic traversability nav-point tests; full-corpus verification of the verdict + nav-points
/// runs in tools/PgmStudio.RoundTrip --traversability over the feature maps.
/// </summary>
public sealed class TraversabilityTests
{
    private static Dict Xz(double x, double z) => new() { ["x"] = x, ["z"] = z };

    private static Dict Rect(double minx, double minz, double maxx, double maxz) => new()
    {
        ["type"] = "rectangle",
        ["min"] = Xz(minx, minz),
        ["max"] = Xz(maxx, maxz),
        ["bounds_2d"] = new Dict { ["min"] = Xz(minx, minz), ["max"] = Xz(maxx, maxz) },
    };

    [Test]
    public async Task Spawn_navpoint_lands_inside_a_disjoint_union_not_the_bounds_gap()
    {
        // Two 4×4 rooms 100 blocks apart. The union's bounding-box midpoint is (52,2) — squarely
        // in the empty gap between them, so the AABB-midpoint nav-point would land in void. The
        // interior point must instead sit inside one of the real rooms.
        var regions = new Dict
        {
            ["roomA"] = Rect(0, 0, 4, 4),
            ["roomB"] = Rect(100, 0, 104, 4),
            ["room"] = new Dict
            {
                ["type"] = "union",
                ["children"] = new List<object?> { "roomA", "roomB" },
                ["bounds_2d"] = new Dict { ["min"] = Xz(0, 0), ["max"] = Xz(104, 4) },
            },
        };
        var data = new Dict
        {
            ["regions"] = regions,
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["region"] = "room" } },
            ["wools"] = new List<object?>(),
        };

        // Navigable surface covers both rooms but NOT the gap between them.
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x < 4; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));
        for (var x = 100; x < 104; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));

        var res = Traversability.Check(data, surface, null, bbox: (-2, -2, 110, 10));

        await Assert.That(res.Points.Count).IsEqualTo(1);
        var p = res.Points[0];
        var inRoomA = p.X is >= 0 and < 4 && p.Z is >= 0 and < 4;
        var inRoomB = p.X is >= 100 and < 104 && p.Z is >= 0 and < 4;
        await Assert.That(inRoomA || inRoomB).IsTrue();   // inside a real room, not the (52,2) gap
        await Assert.That(p.Component).IsGreaterThan(0);    // landed on a navigable component
    }

    [Test]
    public async Task Rectangle_navpoint_matches_the_centre()
    {
        // Convex case: interior point of a rectangle coincides with its midpoint (parity-preserving
        // for every simple spawn/wool footprint in the corpus).
        var regions = new Dict { ["spawn"] = Rect(10, 20, 20, 30) };
        var data = new Dict
        {
            ["regions"] = regions,
            ["spawns"] = new List<object?> { new Dict { ["team"] = "blue", ["region"] = "spawn" } },
            ["wools"] = new List<object?>(),
        };
        var surface = new HashSet<(int, int)>();
        for (var x = 10; x < 20; x++) for (var z = 20; z < 30; z++) surface.Add((x, z));

        var res = Traversability.Check(data, surface, null, bbox: (0, 10, 30, 40));

        await Assert.That(res.Points.Count).IsEqualTo(1);
        await Assert.That(res.Points[0].X).IsEqualTo(15);
        await Assert.That(res.Points[0].Z).IsEqualTo(25);
    }
}
