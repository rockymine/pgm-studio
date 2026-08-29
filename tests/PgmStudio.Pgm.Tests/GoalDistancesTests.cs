using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The destroy-goal walk read: own-spawn and enemy-spawn distances in blocks over the fanned closure, the
/// enemy÷own ratio, and the walk between one goal and another. A measurement surface — the judging is the
/// goal terms' (GO1–GO4) — so what is asserted here is the geometry: the walk is the rectilinear traversal,
/// the enemy leg crosses the axis onto the fanned image, a goal the closure cannot reach answers null rather
/// than a straight-line invention, and both readings are laid over one closure.
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

    // ── the goal-to-goal walk (GO2, GO3) ──────────────────────────────────────────────────────────────

    /// <summary>A lane with two goals on it, the second forward of the first. Their own pair and both
    /// against the images are what `Pairs` answers.</summary>
    private static PlanModel TwoGoalLane() => Plan("""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,8,2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,0,2,8] },
            { "id":"mid","role":"piece","rect":[-1,-2,2,2] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front" } ],
            "destroyables":[
              { "id":"near","piece":"lane","at":[1,6],"style":"pillar-3","materials":"obsidian" },
              { "id":"far","piece":"lane","at":[1,1],"style":"pillar-3","materials":"obsidian" } ] } }
        """);

    [Test]
    public async Task Each_pair_of_goals_is_stated_once_and_the_images_are_the_opposing_ones()
    {
        // Two authored goals: one own pair, and three opposing ones — each against the other's image and
        // each against its own. Stating a pair twice would double-count it in the term that reads the list.
        var pairs = GoalDistances.Pairs(TwoGoalLane());

        await Assert.That(pairs.Count(pair => !pair.Opposing)).IsEqualTo(1);
        await Assert.That(pairs.Count(pair => pair.Opposing)).IsEqualTo(3);
        var own = pairs.Single(pair => !pair.Opposing);
        await Assert.That(new[] { own.From, own.To }.Order().ToList()).IsEquivalentTo(["far", "near"]);
    }

    [Test]
    public async Task A_goal_stands_further_from_its_own_image_than_from_the_goal_beside_it()
    {
        // The geometry the two bands rest on: a team's own goals share a lane and the opposing pair spans
        // the board. Reading them off one closure is what stops the two rules disagreeing about a board.
        var pairs = GoalDistances.Pairs(TwoGoalLane());

        var beside = pairs.Single(pair => !pair.Opposing).Blocks!.Value;
        var across = pairs.Single(pair => pair.Opposing && pair.From == "near" && pair.To == "near")
                          .Blocks!.Value;
        await Assert.That(beside).IsGreaterThan(0);
        await Assert.That(across).IsGreaterThan(beside);
    }

    [Test]
    public async Task A_goal_the_closure_cannot_reach_keeps_its_pairs_and_loses_only_their_distance()
    {
        // A pair is a fact about the board and its walk is the thing that is unknown, so an unreachable goal
        // answers the same pairs with no number rather than dropping them and shrinking the set a term reads.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,8,2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,0,2,8] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front" } ],
            "destroyables":[
              { "id":"on-lane","piece":"lane","at":[1,4],"style":"pillar-3","materials":"obsidian" },
              { "id":"nowhere","piece":"absent","at":[400,400],"style":"pillar-3","materials":"obsidian" } ] } }
        """);

        var pairs = GoalDistances.Pairs(plan);

        await Assert.That(pairs.Count(pair => !pair.Opposing)).IsEqualTo(1);
        await Assert.That(pairs.Count(pair => pair.Opposing)).IsEqualTo(3);
        foreach (var pair in pairs.Where(pair => pair.From == "nowhere" || pair.To == "nowhere"))
            await Assert.That(pair.Blocks).IsNull();
    }

    [Test]
    public async Task A_board_with_one_goal_a_team_still_states_the_pair_across_the_axis()
    {
        // One goal per team is the ordinary destroy board: it has no own pair for GO2 to read, and exactly
        // one opposing pair — the monument against its own mirror — for GO3.
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

        var pairs = GoalDistances.Pairs(plan);

        await Assert.That(pairs.Any(pair => !pair.Opposing)).IsFalse();
        var across = pairs.Single();
        await Assert.That(across.Opposing).IsTrue();
        await Assert.That(across.From).IsEqualTo("monument-1");
        await Assert.That(across.To).IsEqualTo("monument-1");
        await Assert.That(across.Blocks!.Value).IsGreaterThan(0);
    }
}
