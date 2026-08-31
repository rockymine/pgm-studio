using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The four destroy-goal distance terms: the enemy÷own spawn ratio (GO1), a team's own goals against each
/// other (GO2), opposing goals against each other (GO3) and the own-spawn leg on its own (GO4), held to the
/// author's stated bands [3, 4], [35, 65], [85, 150] and [40, 90]. Every band is authored on the term itself
/// rather than learned from seeds, so each scores with no envelope set loaded — a ruling does not wait for a
/// generator run.
/// </summary>
public sealed class GoalTermsTests
{
    private static EvalContext Context(string json) => EvalContext.Build(PlanModel.Parse(json)!);

    // One lane down +z with the spawn at the back and a monument on it — the destroy topology from the
    // GoalDistances tests, with the monument's position the knob the two cases turn.
    private static string LanePlan(double monumentZ) => $$"""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,8,2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,0,2,8] },
            { "id":"mid","role":"piece","rect":[-1,-2,2,2] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[5,5],"facing":"front" } ],
            "destroyables":[ { "id":"monument-1","piece":"lane","at":[5,{{monumentZ * 5}}],
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
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,30,2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,0,2,30] },
            { "id":"mid","role":"piece","rect":[-1,-2,2,2] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[5,5],"facing":"front" } ],
            "destroyables":[ { "id":"monument-1","piece":"lane","at":[5,{{monumentZ * 5}}],
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
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[ { "id":"spawn","role":"spawn","rect":[-1,8,2,2],"surface":13 },
                     { "id":"lane","role":"piece","rect":[-1,0,2,8] } ],
          "placements":{ "spawns":[ { "id":"spawn-1","piece":"spawn","at":[5,5],"facing":"front" } ] } }
        """;
    // ── GO2 and GO3: goal against goal ────────────────────────────────────────────────────────────────

    // A lane carrying two of a team's goals at the stated cells. On a rot_180 lane a goal's own image sits
    // as far the other side of the axis, so where the pair stands sets both bands at once: the own walk is
    // the gap between them and the opposing walks run through `mid`.
    private static string PairPlan(double near, double far, int laneCells) => $$"""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,{{laneCells}},2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,0,2,{{laneCells}}] },
            { "id":"mid","role":"piece","rect":[-1,-2,2,2] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[5,5],"facing":"front" } ],
            "destroyables":[
              { "id":"near","piece":"lane","at":[5,{{near * 5}}],"style":"pillar-3","materials":"obsidian" },
              { "id":"far","piece":"lane","at":[5,{{far * 5}}],"style":"pillar-3","materials":"obsidian" } ] } }
        """;

    // One goal a team, the ordinary destroy board: no own pair for GO2, and exactly one opposing pair —
    // the monument against its own mirror — for GO3 to read.
    private static string SoloPlan(double monumentZ, int laneCells) => $$"""
        { "plan":2, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[
            { "id":"spawn","role":"spawn","rect":[-1,{{laneCells}},2,2],"surface":13 },
            { "id":"lane","role":"piece","rect":[-1,0,2,{{laneCells}}] },
            { "id":"mid","role":"piece","rect":[-1,-2,2,2] } ],
          "placements":{
            "spawns":[ { "id":"spawn-1","piece":"spawn","at":[5,5],"facing":"front" } ],
            "destroyables":[ { "id":"monument-1","piece":"lane","at":[5,{{monumentZ * 5}}],
                               "style":"pillar-3","materials":"obsidian" } ] } }
        """;

    [Test]
    public async Task Two_goals_a_team_defends_from_one_spot_fire_GO2_under_the_band()
    {
        // Ten blocks apart on one lane: two goals one position covers, which is one goal with two names.
        var score = new OwnGoalDistance().Measure(Context(PairPlan(near: 4, far: 6, laneCells: 20)));

        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.Finding.Rule).IsEqualTo("GO2");
        await Assert.That(score.Distance).IsGreaterThan(0);
    }

    [Test]
    public async Task Two_goals_at_opposite_ends_of_the_lane_fire_GO2_over_the_band()
    {
        // Seventy blocks apart: no one position covers both, so the defence is a shuttle rather than a stand.
        var score = new OwnGoalDistance().Measure(Context(PairPlan(near: 4, far: 18, laneCells: 20)));

        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.Finding.Rule).IsEqualTo("GO2");
    }

    [Test]
    public async Task Goals_a_stand_apart_satisfy_GO2()
    {
        // Fifty blocks — inside [35, 65]: a pair a team holds from one position without the two being one
        // objective with two names.
        var score = new OwnGoalDistance().Measure(Context(PairPlan(near: 4, far: 14, laneCells: 20)));

        await Assert.That(score.Violation).IsNull();
        await Assert.That(score.Distance).IsEqualTo(0);
    }

    [Test]
    public async Task One_goal_a_team_leaves_GO2_silent()
    {
        // GO2 is about a pair, and a board with one goal per team has none. A term with nothing to read
        // scores nothing rather than inventing a pass.
        var score = new OwnGoalDistance().Measure(Context(SoloPlan(monumentZ: 4, laneCells: 8)));

        await Assert.That(score.Violation).IsNull();
        await Assert.That(score.Distance).IsEqualTo(0);
    }

    [Test]
    public async Task Objectives_facing_each_other_across_a_short_board_fire_GO3_under_the_band()
    {
        // Twenty-seven blocks between the two teams' monuments: a rush reaches one before the other side
        // has formed, which is what the band's floor refuses.
        var score = new OpposingGoalDistance().Measure(Context(SoloPlan(monumentZ: 2, laneCells: 8)));

        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.Finding.Rule).IsEqualTo("GO3");
        await Assert.That(score.Distance).IsGreaterThan(0);
    }

    [Test]
    public async Task Objectives_a_board_apart_fire_GO3_over_the_band()
    {
        // A hundred and sixty-seven blocks: the attacker crosses a board's width into a set defence, and
        // the match settles into the stalemate the band's ceiling refuses.
        var score = new OpposingGoalDistance().Measure(Context(SoloPlan(monumentZ: 16, laneCells: 20)));

        await Assert.That(score.Violation).IsNotNull();
        await Assert.That(score.Violation!.Finding.Rule).IsEqualTo("GO3");
    }

    [Test]
    public async Task A_contest_a_hundred_blocks_wide_satisfies_GO3()
    {
        // A hundred and seven blocks — inside [85, 150], which is the span the rule exists to describe.
        var score = new OpposingGoalDistance().Measure(Context(SoloPlan(monumentZ: 10, laneCells: 14)));

        await Assert.That(score.Violation).IsNull();
        await Assert.That(score.Distance).IsEqualTo(0);
    }

    [Test]
    public async Task GO2_and_GO3_read_one_board_at_two_distances()
    {
        // Goals at 50 and 100 blocks out: the team's own pair is 50 apart and passes GO2, while the far
        // goal stands 207 from its own mirror and fails GO3. The two terms take different halves of one
        // pair list, which is the whole reason they are two rules over one walk rather than one rule.
        var context = Context(PairPlan(near: 10, far: 20, laneCells: 24));

        await Assert.That(new OwnGoalDistance().Measure(context).Violation).IsNull();
        var across = new OpposingGoalDistance().Measure(context);
        await Assert.That(across.Violation).IsNotNull();
        await Assert.That(across.Violation!.Finding.Rule).IsEqualTo("GO3");
    }
}
