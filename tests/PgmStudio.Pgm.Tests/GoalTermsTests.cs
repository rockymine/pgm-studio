using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// GO1: the destroy-goal spawn ratio held to the author's stated band [3, 4]. The band is authored on the
/// term itself rather than learned from seeds, so the term scores with no envelope set loaded — a ruling
/// does not wait for a generator run.
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

    [Test]
    public async Task A_plan_with_no_destroy_goal_is_not_this_terms_business()
    {
        var score = new GoalSpawnRatio().Measure(Context("""
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[ { "id":"spawn","role":"spawn","rect":[-1,8,2,2],"surface":13 },
                     { "id":"lane","role":"piece","rect":[-1,0,2,8] } ],
          "placements":{ "spawns":[ { "id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front" } ] } }
        """));

        await Assert.That(score.Violation).IsNull();
        await Assert.That(score.Distance).IsEqualTo(0);
    }
}
