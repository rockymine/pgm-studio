using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// GO1 and GO4: the destroy-goal spawn walks — the enemy÷own ratio (GO1) and the own leg on its own (GO4) —
/// held to the author's stated bands [3, 4] and [40, 90]. Both bands are authored on the term itself rather
/// than learned from seeds, so each term scores with no envelope set loaded — a ruling does not wait for a
/// generator run.
/// </summary>
public sealed class GoalTermsTests
{
    private static EvalContext Context(string json) => EvalContext.Build(PlanModel.Parse(json)!);

    // One lane down +z with the spawn at the back and a monument on it — the destroy topology from the
    // GoalDistances tests, with the monument's position the knob the two cases turn.
    private static string LanePlan(double monumentZ) => $$"""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,8,2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,0,2,8] },
            { "id":"mid","role":"piece","rect":[-1,-2,2,2] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front" } ],
            "destroyables":[ { "id":"monument-1","piece":"lane","at":[1,{{monumentZ}}],
                               "style":"pillar-3","materials":"obsidian" } ] } }
        """;

    [Test]
    public async Task A_goal_too_near_the_lanes_front_fires_GO1_under_the_band()
    {
        // The monument toward the lane's front sits nearly as far from its own spawn as from the enemy's —
        // the flat ratio every square board produces (this lane reads ≈1.9), and the rush the band's floor
        // exists to refuse.
        var score = new GoalSpawnRatio().Measure(Context(LanePlan(monumentZ: 2)));

        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.Finding.Rule).IsEqualTo("GO1");
        await Assert.That(score.Distance).IsGreaterThan(0);
    }

    [Test]
    public async Task A_goal_on_the_spawns_doorstep_fires_GO1_over_the_band()
    {
        // Tucked right in front of its own spawn the ratio reads ≈8.5 — the enemy's attack is a march down
        // the whole lane into a set defence, the band's ceiling.
        var score = new GoalSpawnRatio().Measure(Context(LanePlan(monumentZ: 7)));

        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.Finding.Rule).IsEqualTo("GO1");
    }

    [Test]
    public async Task A_goal_at_the_banded_ratio_scores_clean_with_no_envelopes_loaded()
    {
        // Monument in the lane's back third: the own walk is short, the enemy's is most of the lane, and
        // the ratio lands inside [3, 4] (≈3.75 here). EvalContext.Build with no envelopes proves the band
        // is the term's own — an authored ruling, live before any envelope file exists.
        var score = new GoalSpawnRatio().Measure(Context(LanePlan(monumentZ: 5)));

        await Assert.That(score.Violation).IsNull();
        await Assert.That(score.Distance).IsEqualTo(0);
    }

    // A lane long enough (30 cells = 150 blocks) that a monument's own-spawn walk can land clear of GO4's
    // band on either side, not just the shorter LanePlan's own-spawn range.
    private static string LongLanePlan(double monumentZ) => $$"""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,30,2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,0,2,30] },
            { "id":"mid","role":"piece","rect":[-1,-2,2,2] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front" } ],
            "destroyables":[ { "id":"monument-1","piece":"lane","at":[1,{{monumentZ}}],
                               "style":"pillar-3","materials":"obsidian" } ] } }
        """;

    [Test]
    public async Task A_goal_on_the_spawns_doorstep_fires_GO4_under_the_band()
    {
        // Own-spawn walk reads 15 blocks — well under the 40 floor: the spawn is already standing on the goal.
        var score = new GoalSpawnDistance().Measure(Context(LongLanePlan(monumentZ: 28)));

        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.Finding.Rule).IsEqualTo("GO4");
        await Assert.That(score.Distance).IsGreaterThan(0);
    }

    [Test]
    public async Task A_goal_far_down_the_lane_fires_GO4_over_the_band()
    {
        // Own-spawn walk reads 145 blocks — well past the 90 ceiling: the spawn cannot reinforce in time.
        var score = new GoalSpawnDistance().Measure(Context(LongLanePlan(monumentZ: 2)));

        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.Finding.Rule).IsEqualTo("GO4");
    }

    [Test]
    public async Task A_goal_at_a_banded_own_spawn_distance_scores_clean_with_no_envelopes_loaded()
    {
        // Own-spawn walk reads 80 blocks, inside [40, 90]. EvalContext.Build with no envelopes proves the
        // band is the term's own — an authored ruling, live before any envelope file exists.
        var score = new GoalSpawnDistance().Measure(Context(LongLanePlan(monumentZ: 15)));

        await Assert.That(score.Violation).IsNull();
        await Assert.That(score.Distance).IsEqualTo(0);
    }

    [Test]
    public async Task A_plan_with_no_destroy_goal_is_not_GO1s_business()
    {
        var score = new GoalSpawnRatio().Measure(Context(NoGoalPlan));

        await Assert.That(score.Violation).IsNull();
        await Assert.That(score.Distance).IsEqualTo(0);
    }

    [Test]
    public async Task A_plan_with_no_destroy_goal_is_not_GO4s_business()
    {
        var score = new GoalSpawnDistance().Measure(Context(NoGoalPlan));

        await Assert.That(score.Violation).IsNull();
        await Assert.That(score.Distance).IsEqualTo(0);
    }

    private const string NoGoalPlan = """
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[ { "id":"spawn","role":"spawn","rect":[-1,8,2,2],"surface":13 },
                     { "id":"lane","role":"piece","rect":[-1,0,2,8] } ],
          "placements":{ "spawns":[ { "id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front" } ] } }
        """;
}
