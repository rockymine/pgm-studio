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

    // A spawn region + its wool, with a build area but NO terrain under either point. The cells are
    // "buildable" (so connectivity sees them) yet have no ground — a player would spawn into the void.
    private static Dict VoidPlacementDoc() => new()
    {
        ["regions"] = new Dict { ["red-spawn"] = Rect(0, 0, 4, 4), ["red-wool"] = Rect(0, 0, 4, 4) },
        ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["region"] = "red-spawn" } },
        ["wools"] = new List<object?> { new Dict { ["color"] = "red", ["location"] = Xz(2, 2) } },
    };

    [Test]
    public async Task Ungrounded_points_over_a_build_area_fail_when_terrain_is_known()
    {
        // Terrain exists somewhere (so grounding is judged) but NOT under the placements.
        var surface = new HashSet<(int, int)> { (200, 200) };
        var res = Traversability.Check(VoidPlacementDoc(), surface, null, bbox: (-4, -4, 210, 210));

        await Assert.That(res.Grounded).IsFalse();
        await Assert.That(res.Ungrounded.Count).IsEqualTo(2);   // spawn + wool both float over void
        await Assert.That(res.Ungrounded.Any(u => u.Kind == "spawn")).IsTrue();
        await Assert.That(res.Ungrounded.Any(u => u.Kind == "wool")).IsTrue();
    }

    [Test]
    public async Task Grounded_points_pass()
    {
        // Same placements, but now terrain covers them — a valid, playable arrangement.
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x < 4; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));
        var res = Traversability.Check(VoidPlacementDoc(), surface, null, bbox: (-4, -4, 10, 10));

        await Assert.That(res.Grounded).IsTrue();
        await Assert.That(res.Ungrounded.Count).IsEqualTo(0);
        await Assert.That(res.Points.All(p => p.Grounded)).IsTrue();
    }

    [Test]
    public async Task Grounding_is_skipped_when_no_terrain_layer_is_known()
    {
        // xml-only / un-scanned map: surface and y0 both absent → grounding can't be judged, so it must
        // not block (preserves corpus / layerless behaviour).
        var res = Traversability.Check(VoidPlacementDoc(), null, null, bbox: (-4, -4, 10, 10));

        await Assert.That(res.Grounded).IsTrue();
        await Assert.That(res.Ungrounded.Count).IsEqualTo(0);
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
