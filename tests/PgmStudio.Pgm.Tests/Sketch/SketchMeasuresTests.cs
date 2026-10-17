using System.Text.Json;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The evaluator's board measures read off the ground a sketch builds. A seed compiled to a layout and an intent
/// and read back at the corpus's cell measures what the plan measures, term by term, and a term the sketch asks
/// never reaches for a plan it does not have.
/// </summary>
public sealed class SketchMeasuresTests
{
    private static (PlanModel Plan, string Layout, PgmStudio.Pgm.Authoring.MapIntent Intent) Compiled(string seed)
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed(seed))!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        return (plan, JsonSerializer.Serialize(layout, SketchLayout.Json), intent);
    }

    [Test]
    [Arguments("base-2island.plan.json")]
    [Arguments("base-4team.plan.json")]
    [Arguments("big-board-wool-two-sided-plaza-parallel-mid.plan.json")]
    [Arguments("four-team-wool-two-sided.plan.json")]
    public async Task A_compiled_seed_measures_on_its_sketch_what_its_plan_measures(string seed)
    {
        var (plan, layout, intent) = Compiled(seed);
        var planned = EvalContext.Build(plan, SeedEnvelopes.Default);
        var ground = SketchGround.Of(layout, intent, SketchMeasures.CorpusCell)!;
        var sketched = EvalContext.OfBoard(BoardDeriver.Derive(ground), planned.Order, planned.Goals, SeedEnvelopes.Default);

        foreach (var term in SketchMeasures.Terms)
            await Assert.That(term.Value(sketched)).IsEqualTo(term.Value(planned)).Because($"{term.RuleId} on {seed}");
    }

    [Test]
    public async Task A_board_read_off_no_plan_refuses_a_term_that_asks_for_one()
    {
        var (_, layout, intent) = Compiled("base-2island.plan.json");
        var board = BoardDeriver.Derive(SketchGround.Of(layout, intent, SketchMeasures.CorpusCell)!);
        var context = EvalContext.OfBoard(board, 2, new BoardGoals(1, 0, 0), SeedEnvelopes.Default);

        await Assert.That(() => context.Plan).Throws<InvalidOperationException>();
        await Assert.That(() => new PgmStudio.Pgm.Evaluate.Terms.MaxChainLength().Value(context)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task A_measure_outside_its_band_is_a_complaint_carrying_its_number()
    {
        var (_, layout, intent) = Compiled("four-team-wool-two-sided.plan.json");

        foreach (var finding in SketchMeasures.Check(layout, intent))
        {
            await Assert.That(finding.Severity).IsEqualTo(Vocabulary.Severity.Complaint);
            await Assert.That(finding.Measured).IsNotNull();
        }
    }
}
