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

    // A ring: spawn on one arm, wool on the other, a hole between them and both ways round it the same length.
    private static Dict RingBoard => new()
    {
        ["regions"] = new Dict { ["spawn"] = Rect(0, 10, 2, 12) },
        ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["region"] = "spawn" } },
        ["wools"] = new List<object?> { new Dict { ["color"] = "blue", ["team"] = "blue", ["location"] = Xz(40, 11) } },
    };

    private static HashSet<(int, int)> RingGround()
    {
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x <= 40; x++) for (var z = 0; z <= 4; z++) surface.Add((x, z));       // north arm
        for (var x = 0; x <= 40; x++) for (var z = 18; z <= 22; z++) surface.Add((x, z));     // south arm
        for (var x = 0; x <= 4; x++) for (var z = 0; z <= 22; z++) surface.Add((x, z));       // west end
        for (var x = 36; x <= 40; x++) for (var z = 0; z <= 22; z++) surface.Add((x, z));     // east end
        return surface;                                                                       // hole: 5..35 x 5..17
    }

    [Test]
    public async Task Both_ways_round_a_hole_are_walked_where_one_path_would_pick_a_side()
    {
        var res = GroundCoverage.Read(RingBoard, RingGround(), null, [], bbox: (-8, -8, 48, 30));
        int At(int x, int z) => (z - res.MinZ) * res.Width + (x - res.MinX);

        // The two arms are the two ways round. A dilated single geodesic has to commit to one of them, and
        // the other then reads unused however many players walk it; the ribbon carries both.
        for (var x = 8; x <= 32; x += 4)
        {
            await Assert.That(res.Traffic[At(x, 2)]).IsGreaterThan(0).Because($"the north arm at x {x}");
            await Assert.That(res.Traffic[At(x, 20)]).IsGreaterThan(0).Because($"the south arm at x {x}");
            await Assert.That(res.Traffic[At(x, 2)]).IsEqualTo(res.Traffic[At(x, 20)])
                .Because("the arms are the same length, so neither is picked by a tie-break");
        }

        // and the hole between them is not a shortcut: no rule grants building over it, so it is not ground
        await Assert.That(res.Cells[At(20, 11)]).IsEqualTo(GroundCoverage.Void);
        await Assert.That(res.DeadCells).IsEqualTo(0).Because("a ring is all lane");
    }

    // Two team blocks joined by two ways across. Every waypoint sits in the north, so a goal-to-goal demand
    // set walks the north crossing only and the south one carries nothing.
    private static Dict TwoSidedBoard => new()
    {
        ["regions"] = new Dict { ["red-spawn"] = Rect(0, 1, 4, 5), ["blue-spawn"] = Rect(56, 1, 60, 5) },
        ["spawns"] = new List<object?>
        {
            new Dict { ["team"] = "red", ["region"] = "red-spawn" },
            new Dict { ["team"] = "blue", ["region"] = "blue-spawn" },
        },
    };

    private static HashSet<(int, int)> TwoSidedGround()
    {
        var surface = new HashSet<(int, int)>();
        for (var x = 0; x <= 20; x++) for (var z = 0; z <= 40; z++) surface.Add((x, z));      // west block
        for (var x = 40; x <= 60; x++) for (var z = 0; z <= 40; z++) surface.Add((x, z));     // east block
        for (var x = 21; x < 40; x++) for (var z = 0; z <= 6; z++) surface.Add((x, z));       // north crossing
        for (var x = 21; x < 40; x++) for (var z = 34; z <= 40; z++) surface.Add((x, z));     // south crossing
        return surface;
    }

    [Test]
    public async Task A_way_across_the_middle_is_walked_even_where_no_objective_stands_near_it()
    {
        var res = GroundCoverage.Read(TwoSidedBoard, TwoSidedGround(), null, [], bbox: (-8, -8, 68, 48));
        int At(int x, int z) => (z - res.MinZ) * res.Width + (x - res.MinX);

        // Both spawns are in the north, so every spawn-to-spawn journey takes the north crossing. The south
        // one is a way across all the same, and the read finds it because the middle is an origin.
        await Assert.That(res.Traffic[At(30, 3)]).IsGreaterThan(0).Because("the north crossing");
        await Assert.That(res.Traffic[At(30, 37)]).IsGreaterThan(0).Because("the south crossing");
        await Assert.That(res.Cells[At(30, 37)]).IsNotEqualTo(GroundCoverage.Dead);
    }

    [Test]
    public async Task Every_waypoint_is_named_with_the_kind_of_place_it_is()
    {
        var res = GroundCoverage.Read(TwoSidedBoard, TwoSidedGround(), null, [], bbox: (-8, -8, 68, 48));

        await Assert.That(res.Markers.Count(marker => marker.Kind == "spawn")).IsEqualTo(2);
        await Assert.That(res.Markers.Any(marker => marker.Kind == "crossing")).IsTrue();
        // Every marker sits where a journey could start, which is what makes the picture checkable in-game.
        foreach (var marker in res.Markers)
        {
            var code = res.Cells[(marker.Z - res.MinZ) * res.Width + (marker.X - res.MinX)];
            await Assert.That(code).IsNotEqualTo(GroundCoverage.Void).Because($"{marker.Kind} at {marker.X},{marker.Z}");
        }
    }

    [Test]
    public async Task The_route_never_claims_a_cell_the_read_called_void()
    {
        var res = GroundCoverage.Read(Board, Ground(), null, [], bbox: (-10, -10, 70, 50));

        var ground = Ground();
        for (var i = 0; i < res.Cells.Length; i++)
            if (res.Cells[i] == GroundCoverage.Route)
                await Assert.That(ground.Contains((res.MinX + i % res.Width, res.MinZ + i / res.Width)))
                    .IsTrue().Because("an annotation may not add a cell to the picture");
    }

    [Test]
    public async Task Traffic_counts_the_journeys_rather_than_answering_whether_any()
    {
        var res = GroundCoverage.Read(Board, Ground(), null, [], bbox: (-10, -10, 70, 50));
        int At(int x, int z) => (z - res.MinZ) * res.Width + (x - res.MinX);

        await Assert.That(res.Journeys).IsGreaterThan(0);
        await Assert.That(res.Traffic.Length).IsEqualTo(res.Cells.Length);
        await Assert.That(res.Traffic[At(30, 3)]).IsGreaterThan(0).Because("the strip carries the journey");
        await Assert.That(res.Traffic[At(30, 30)]).IsEqualTo(0).Because("the plateau carries none");
        await Assert.That(res.Traffic.Max()).IsLessThanOrEqualTo(res.Journeys)
            .Because("a cell cannot be covered by more journeys than there are");
    }

    [Test]
    public async Task A_long_way_round_is_out_of_the_corridor_however_reachable_it_is()
    {
        // The plateau is walkable and hangs off the middle of the strip: reaching it and coming back costs
        // far more than the twenty blocks a player will spend, so no journey claims it.
        var res = GroundCoverage.Read(Board, Ground(), null, [], bbox: (-10, -10, 70, 50));
        int At(int x, int z) => (z - res.MinZ) * res.Width + (x - res.MinX);

        await Assert.That(res.Cells[At(30, 30)]).IsEqualTo(GroundCoverage.Dead);
        await Assert.That(res.DeadCells).IsGreaterThan(0);
        await Assert.That(res.DeadPatches).IsNotEmpty();
        await Assert.That(res.DeadPatches[0].Area).IsGreaterThan(100).Because("the plateau is one named place");
    }
}
