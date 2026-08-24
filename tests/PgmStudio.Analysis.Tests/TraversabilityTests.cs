using PgmStudio.Analysis.Scan;
using PgmStudio.Analysis.Playability;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Synthetic traversability nav-point tests; full-corpus verification of the verdict + nav-points
/// runs in tools/PgmStudio.RoundTrip --traversability over the feature maps.
/// </summary>
public sealed class TraversabilityTests
{
    /// <summary>A board of flat ground, read the way a scan is: every cell a one-block slab at the world
    /// floor, so it is both somewhere to stand and ground that reaches y=0.</summary>
    private static SegmentIndex Flat(IEnumerable<(int, int)> cells)
        => new(cells.Select(cell => (cell.Item1, cell.Item2, 0, 0)));

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

        var res = Traversability.Check(data, Flat(surface), bbox: (-2, -2, 110, 10));

        await Assert.That(res.Points.Count).IsEqualTo(1);
        var p = res.Points[0];
        var inRoomA = p.Point.X is >= 0 and < 4 && p.Point.Z is >= 0 and < 4;
        var inRoomB = p.Point.X is >= 100 and < 104 && p.Point.Z is >= 0 and < 4;
        await Assert.That(inRoomA || inRoomB).IsTrue();   // inside a real room, not the (52,2) gap
        await Assert.That(p.Component).IsGreaterThan(0);    // landed on a navigable component
    }

    [Test]
    public async Task Objective_on_terrain_beyond_the_region_aabb_is_not_clipped_to_isolated()
    {
        // A wool sits 100 blocks out on terrain that reaches it, but the only region (the spawn) is near
        // the origin — so the region AABB + margin stops well short of the wool. With no explicit bbox the
        // grid must still be sized to the terrain, or the far wool falls outside it and reads as isolated
        // however well the surface connects it.
        var data = new Dict
        {
            ["regions"] = new Dict { ["spawn"] = Rect(0, 0, 4, 4) },
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["region"] = "spawn" } },
            ["wools"] = new List<object?> { new Dict { ["color"] = "blue", ["location"] = Xz(100, 2) } },
        };

        // Continuous walkable surface from the spawn out to the wool.
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x <= 100; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));

        var res = Traversability.Check(data, Flat(surface));   // no bbox — the export/gate path

        await Assert.That(res.Connected).IsTrue();
        await Assert.That(res.Isolated.Count).IsEqualTo(0);
        var wool = res.Points.Single(p => p.Point.Kind == "wool");
        await Assert.That(wool.Component).IsGreaterThan(0);   // in-grid, on the connected component
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

        var res = Traversability.Check(data, Flat(surface), bbox: (0, 10, 30, 40));

        await Assert.That(res.Points.Count).IsEqualTo(1);
        await Assert.That(res.Points[0].Point.X).IsEqualTo(15);
        await Assert.That(res.Points[0].Point.Z).IsEqualTo(25);
    }

    [Test]
    public async Task Every_gating_point_off_grid_is_named_isolated_rather_than_reported_as_zero()
    {
        // Both spawns sit outside the analysed bbox entirely, so every gating point comes back
        // Component == 0 and `comps` (Component > 0 points) is empty. Before the fix, `main` stayed at
        // its default 0 and an isolated filter of `Component != main` matched neither point (0 != 0 is
        // false), so a fully-disconnected map reported "0 spawn/wool point(s) are not reachable" and
        // named none of them — the one case an author most needs a name is the one the old check
        // could not give.
        var regions = new Dict
        {
            ["red"] = Rect(-500, -500, -496, -496),
            ["blue"] = Rect(500, 500, 504, 504),
        };
        var data = new Dict
        {
            ["regions"] = regions,
            ["spawns"] = new List<object?>
            {
                new Dict { ["team"] = "red", ["region"] = "red" },
                new Dict { ["team"] = "blue", ["region"] = "blue" },
            },
            ["wools"] = new List<object?>(),
        };

        var res = Traversability.Check(data, null, bbox: (-10, -10, 10, 10));

        await Assert.That(res.Connected).IsFalse();
        await Assert.That(res.Isolated.Count).IsEqualTo(2);
        await Assert.That(res.Isolated.Select(i => i.Name)).Contains("red");
        await Assert.That(res.Isolated.Select(i => i.Name)).Contains("blue");
        await Assert.That(res.Message).Contains("no spawn or objective point");
    }

    [Test]
    public async Task A_declared_build_zone_over_void_is_a_route_and_a_void_denied_lane_is_not()
    {
        // The author's ruling (2026-08-16): before the lane timer fills it, a water lane is a void a player
        // falls into, so a map whose only spawn→wool route crosses one must refuse — the lane's columns have
        // no Y=0 ground and its deny(void) rule means nothing can be bridged across them. A build zone over
        // the same gap is the opposite thing: it is *meant* to be crossed, block by placed block, so it reads
        // navigable and the chain connects.
        var regions = new Dict
        {
            ["spawn"] = Rect(0, 0, 4, 4),
            ["lane"] = Rect(10, -5, 21, 10),
        };
        Dict Data(string? laneRule) => new()
        {
            ["regions"] = regions,
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["region"] = "spawn" } },
            ["wools"] = new List<object?> { new Dict { ["color"] = "blue", ["location"] = Xz(26, 2) } },
            ["apply_rules"] = laneRule is null
                ? new List<object?>()
                : new List<object?> { new Dict { ["region"] = "lane", ["block_place"] = laneRule } },
        };

        // Ground on both banks, nothing across the lane: the surface walk ends at x=9 and resumes at x=21.
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x < 10; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));
        for (var x = 21; x < 30; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));

        var built = Traversability.Check(Data("always"), Flat(surface), bbox: (-5, -5, 35, 10));
        await Assert.That(built.Connected).IsTrue().Because("a granted build zone is bridged across");

        var denied = Traversability.Check(Data("deny(void)"), Flat(surface), bbox: (-5, -5, 35, 10));
        await Assert.That(denied.Connected).IsFalse();
        await Assert.That(denied.Isolated.Select(i => i.Kind)).Contains("wool");
    }

    [Test]
    public async Task A_gap_no_rule_mentions_is_void_and_not_a_crossing()
    {
        // B247. A map grants building by naming a region; ground nobody named is not a grant. Before this,
        // the verdict grid started at "buildable" and a rule only ever wrote a denial over it, so every cell
        // outside every apply rule read buildable and therefore walkable — and a board could pass "all
        // objectives connected" across a void it cannot cross. The two banks below are 11 blocks apart with
        // nothing over the gap and nothing said about it.
        var data = new Dict
        {
            ["regions"] = new Dict { ["spawn"] = Rect(0, 0, 4, 4) },
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["region"] = "spawn" } },
            ["wools"] = new List<object?> { new Dict { ["color"] = "blue", ["location"] = Xz(26, 2) } },
            ["apply_rules"] = new List<object?>(),
        };
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x < 10; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));
        for (var x = 21; x < 30; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));

        var res = Traversability.Check(data, Flat(surface), bbox: (-5, -5, 35, 10));
        await Assert.That(res.Connected).IsFalse();
        await Assert.That(res.Isolated.Select(i => i.Kind)).Contains("wool");
    }

    [Test]
    public async Task A_goal_behind_an_oversized_spawn_protection_refuses_for_the_team_it_bars()
    {
        // The one way a small floating goal genuinely becomes unreachable (the author's ruling): the ground
        // its approach crosses carries an enter rule barring the attacking team. The whole-map navigability
        // cannot see it — the surface connects — so the per-team pass walks red's own map, with the cells
        // blue's protection denies red taken out, and finds the wool cut off.
        var regions = new Dict
        {
            ["red-spawn"] = Rect(0, 0, 4, 4),
            ["blue-prot"] = Rect(24, -5, 34, 15),
        };
        Dict Data(bool withProtection) => new()
        {
            ["regions"] = regions,
            ["filters"] = new Dict { ["only-blue"] = new Dict { ["type"] = "team", ["team"] = "blue" } },
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["region"] = "red-spawn" } },
            ["wools"] = new List<object?>
            {
                new Dict { ["color"] = "blue", ["team"] = "blue", ["location"] = Xz(30, 2) },
            },
            ["apply_rules"] = withProtection
                ? new List<object?> { new Dict { ["region"] = "blue-prot", ["enter"] = "only-blue" } }
                : new List<object?>(),
        };
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x < 34; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));

        var open = Traversability.Check(Data(withProtection: false), Flat(surface), bbox: (-5, -5, 40, 15));
        await Assert.That(open.Connected).IsTrue();

        var barred = Traversability.Check(Data(withProtection: true), Flat(surface), bbox: (-5, -5, 40, 15));
        await Assert.That(barred.Connected).IsFalse();
        var wool = barred.Isolated.Single();
        await Assert.That(wool.Kind).IsEqualTo("wool");
        await Assert.That(wool.For).IsEqualTo("red");
        await Assert.That(barred.Message).Contains("enter rule");
    }

    [Test]
    public async Task A_teams_own_protections_are_never_its_own_fault()
    {
        // The two denials every properly wired map carries: a wool room barring its defender (enter=not-owner)
        // and a spawn protection admitting only its own team. Neither is a fault — the defender is never
        // required to reach its own wool, and a team is not barred by its own protection — so the verdict
        // holds connected with both rules live.
        var regions = new Dict
        {
            ["red-spawn"] = Rect(0, 0, 4, 4),
            ["blue-spawn"] = Rect(8, 0, 12, 4),
            ["wool-room"] = Rect(28, 0, 33, 4),
        };
        var data = new Dict
        {
            ["regions"] = regions,
            ["filters"] = new Dict
            {
                ["only-red"] = new Dict { ["type"] = "team", ["team"] = "red" },
                ["only-blue"] = new Dict { ["type"] = "team", ["team"] = "blue" },
                ["not-blue"] = new Dict { ["type"] = "not", ["child"] = "only-blue" },
            },
            ["spawns"] = new List<object?>
            {
                new Dict { ["team"] = "red", ["region"] = "red-spawn" },
                new Dict { ["team"] = "blue", ["region"] = "blue-spawn" },
            },
            ["wools"] = new List<object?>
            {
                new Dict { ["color"] = "blue", ["team"] = "blue", ["location"] = Xz(30, 2) },
            },
            ["apply_rules"] = new List<object?>
            {
                new Dict { ["region"] = "wool-room", ["enter"] = "not-blue" },
                new Dict { ["region"] = "red-spawn", ["enter"] = "only-red" },
            },
        };
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x < 34; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));

        var res = Traversability.Check(data, Flat(surface), bbox: (-5, -5, 40, 15));

        await Assert.That(res.Connected).IsTrue();
        await Assert.That(res.Isolated).IsEmpty();
    }

    [Test]
    public async Task An_unreachable_destroyable_gates_the_verdict_like_a_wool()
    {
        // Spawn and wool sit on a connected surface; a destroyable's region sits off in the void with no
        // navigable ground anywhere near it. The author's ruling (2026-08-16): every goal gates — a match
        // whose goal nobody can approach cannot be finished, so the map refuses rather than shipping.
        var regions = new Dict
        {
            ["spawn"] = Rect(0, 0, 4, 4),
            ["destroyable"] = Rect(900, 900, 904, 904),
        };
        var data = new Dict
        {
            ["regions"] = regions,
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["region"] = "spawn" } },
            ["wools"] = new List<object?> { new Dict { ["color"] = "red", ["location"] = Xz(2, 2) } },
            ["destroyables"] = new List<object?>
            {
                new Dict { ["name"] = "core", ["owner"] = "blue", ["region"] = "destroyable" },
            },
        };
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x < 4; x++) for (var z = 0; z < 4; z++) surface.Add((x, z));

        var res = Traversability.Check(data, Flat(surface), bbox: (-10, -10, 20, 20));

        await Assert.That(res.Connected).IsFalse();
        await Assert.That(res.Isolated.Select(i => i.Kind)).Contains("destroyable");
        var destroyable = res.Points.Single(p => p.Point.Kind == "destroyable");
        await Assert.That(destroyable.Component).IsEqualTo(0);   // off the navigable grid — and gating
    }
}
