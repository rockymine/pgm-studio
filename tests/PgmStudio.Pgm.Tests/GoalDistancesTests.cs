using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The destroy-goal walk read: own-spawn and enemy-spawn distances in blocks over the fanned closure, and
/// the enemy÷own ratio. A measurement surface with no band of its own — what is asserted here is the
/// geometry: the walk is the rectilinear traversal, the enemy leg crosses the axis onto the fanned image,
/// and a goal the closure cannot reach answers null rather than a straight-line invention.
/// </summary>
public sealed class GoalDistancesTests
{
    private static PlanModel Plan(string json) => PlanModel.Parse(json)!;

    [Test]
    public async Task A_lane_board_reads_a_ratio_above_one_over_the_walk()
    {
        // One lane down +z: spawn at the back, monument forward of it — the destroy topology. The enemy
        // spawn is the rot_180 image at the far end, so its walk to this goal is the lane's whole length.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,8,2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,0,2,8] },
            { "id":"mid","role":"piece","rect":[-1,-2,2,2] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front" } ],
            "destroyables":[ { "id":"monument-1","piece":"lane","at":[1,2],
                               "style":"pillar-3","materials":"obsidian" } ] } }
        """);

        var walks = GoalDistances.Read(plan);

        await Assert.That(walks.Count).IsEqualTo(1);
        var walk = walks[0];
        await Assert.That(walk.Kind).IsEqualTo("destroyable");
        await Assert.That(walk.OwnSpawnBlocks!.Value).IsGreaterThan(0);
        await Assert.That(walk.EnemySpawnBlocks!.Value).IsGreaterThan(walk.OwnSpawnBlocks.Value);
        await Assert.That(walk.Ratio!.Value).IsGreaterThan(1);
    }

    [Test]
    public async Task An_absolutely_placed_goal_reads_its_at_as_cells_from_the_centre()
    {
        // The B128 shape: a goal naming no piece, its `at` an absolute cell position. It sits on the mid
        // ground the lane fans across, so both walks exist and the enemy leg is the longer one.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,8,2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,0,2,8] },
            { "id":"mid","role":"piece","rect":[-1,-2,2,2] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front" } ],
            "destroyables":[ { "id":"monument-1","piece":"","at":[0,1],
                               "style":"pillar-3","materials":"obsidian" } ] } }
        """);

        var walks = GoalDistances.Read(plan);

        await Assert.That(walks.Count).IsEqualTo(1);
        await Assert.That(walks[0].OwnSpawnBlocks).IsNotNull();
        await Assert.That(walks[0].EnemySpawnBlocks!.Value).IsGreaterThan(walks[0].OwnSpawnBlocks!.Value);
    }

    [Test]
    public async Task A_goal_the_closure_cannot_reach_answers_null_not_a_straight_line()
    {
        // The goal stands on its own island, no zone bridges it: the walk does not exist, and the read says
        // so instead of measuring through the air.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,8,2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,4,2,4] },
            { "id":"islet","role":"piece","rect":[6,4,2,2] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front" } ],
            "destroyables":[ { "id":"monument-1","piece":"islet","at":[1,1],
                               "style":"pillar-3","materials":"obsidian" } ] } }
        """);

        var walks = GoalDistances.Read(plan);

        await Assert.That(walks.Count).IsEqualTo(1);
        await Assert.That(walks[0].OwnSpawnBlocks).IsNull();
        await Assert.That(walks[0].Ratio).IsNull();
    }

    [Test]
    public async Task A_wool_only_plan_reads_empty()
    {
        var plan = Plan("""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[ { "id":"spawn","role":"spawn","rect":[-1,8,2,2] } ],
          "placements":{ "spawns":[ { "id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front" } ] } }
        """);

        await Assert.That(GoalDistances.Read(plan)).IsEmpty();
    }
}
