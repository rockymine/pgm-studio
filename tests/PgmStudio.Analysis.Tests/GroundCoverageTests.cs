using PgmStudio.Analysis.Playability;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The coverage read: the traffic corridors between waypoints, each waypoint's ring, the decorated fringe,
/// and the dead ground left over — asserted on a board built to have exactly one dead place, so the classes
/// and the patch report are checkable by construction.
/// </summary>
public sealed class GroundCoverageTests
{
    private static Dict Xz(double x, double z) => new() { ["x"] = x, ["z"] = z };

    private static Dict Rect(double minx, double minz, double maxx, double maxz) => new()
    {
        ["type"] = "rectangle",
        ["min"] = Xz(minx, minz),
        ["max"] = Xz(maxx, maxz),
        ["bounds_2d"] = new Dict { ["min"] = Xz(minx, minz), ["max"] = Xz(maxx, maxz) },
    };

    // A strip the match happens on — spawn at one end, wool at the other — with a plateau hanging off its
    // middle by a neck: ground a player *could* walk to and no journey ever crosses.
    private static Dict Board => new()
    {
        ["regions"] = new Dict { ["spawn"] = Rect(0, 0, 4, 4) },
        ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["region"] = "spawn" } },
        ["wools"] = new List<object?> { new Dict { ["color"] = "blue", ["team"] = "blue", ["location"] = Xz(58, 3) } },
    };

    private static HashSet<(int, int)> Ground()
    {
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x <= 60; x++) for (var z = 0; z <= 6; z++) surface.Add((x, z));       // the strip
        for (var x = 28; x <= 32; x++) for (var z = 7; z <= 19; z++) surface.Add((x, z));     // the neck
        for (var x = 20; x <= 40; x++) for (var z = 20; z <= 40; z++) surface.Add((x, z));    // the plateau
        return surface;
    }

    [Test]
    public async Task The_strip_the_match_walks_is_reached_and_the_plateau_off_it_is_dead()
    {
        var res = GroundCoverage.Read(Board, Ground(), null, [], bbox: (-10, -10, 70, 50));

        await Assert.That(res.HaveRoutes).IsTrue();
        await Assert.That(res.ReachedCells + res.DecoratedCells + res.DeadCells).IsEqualTo(res.GroundCells);
        // the strip between the waypoints is inside the corridor, so nothing on it is dead
        await Assert.That(res.DeadCells).IsGreaterThan(300);       // the plateau
        await Assert.That(res.DecoratedCells).IsEqualTo(0);        // nothing was placed anywhere

        var patch = res.DeadPatches[0];
        await Assert.That(patch.Area).IsGreaterThan(300);
        await Assert.That(patch.CentroidZ).IsGreaterThan(18);      // it names the plateau, not the strip
        await Assert.That(patch.NearestReachedBlocks).IsGreaterThan(0);
    }

    [Test]
    public async Task A_prop_on_the_plateau_turns_its_surroundings_decorated_but_not_reached()
    {
        var bare = GroundCoverage.Read(Board, Ground(), null, [], bbox: (-10, -10, 70, 50));
        var dressed = GroundCoverage.Read(Board, Ground(), null, [(30, 30)], bbox: (-10, -10, 70, 50));

        await Assert.That(dressed.DecoratedCells).IsGreaterThan(0);
        await Assert.That(dressed.DeadCells).IsLessThan(bare.DeadCells);
        // decoration is scenery, not traffic — the reached ground is unchanged by it
        await Assert.That(dressed.ReachedCells).IsEqualTo(bare.ReachedCells);
    }

    [Test]
    public async Task A_board_that_is_all_corridor_reports_no_dead_ground()
    {
        // The strip alone: every cell within the corridor margin or a waypoint ring.
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x <= 60; x++) for (var z = 0; z <= 6; z++) surface.Add((x, z));

        var res = GroundCoverage.Read(Board, surface, null, [], bbox: (-10, -10, 70, 20));

        await Assert.That(res.DeadCells).IsEqualTo(0);
        await Assert.That(res.DeadPatches).IsEmpty();
    }
}
