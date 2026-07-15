using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The core placement's compile: the destroyable's fan with the casing's own knobs, the corpus defaults, and
/// the float/leak pair that only means something together (DC2).
/// </summary>
public sealed class PlanCoresTests
{
    private const string Json = """
        {
          "plan": 1,
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "bar-w", "role": "piece", "rect": [0, 0, 2, 2], "surface": 12 }
          ],
          "placements": {
            "cores": [ { "piece": "bar-w", "at": [1, 1] } ]
          }
        }
        """;

    private const string Marker = """{ "piece": "bar-w", "at": [1, 1] }""";

    private static List<CoreIntent> Compile(string json) =>
        PlanCompiler.Compile(PlanModel.Parse(json)!).Intent.Cores!;

    private static IReadOnlyList<PlanFinding> Validate(string json) =>
        PlanValidator.Validate(PlanModel.Parse(json)!);

    private static bool Errors(IReadOnlyList<PlanFinding> f, string contains) =>
        f.Any(x => x.Severity == PlanSeverity.Error && x.Message.Contains(contains));

    [Test]
    public async Task One_authored_marker_fans_to_one_core_per_team()
    {
        var c = Compile(Json);
        await Assert.That(c.Count).IsEqualTo(2);
        await Assert.That(c.Select(x => x.Owner)).IsEquivalentTo(new[] { "red", "blue" });
        await Assert.That(c[0].Anchor.X).IsEqualTo(-c[1].Anchor.X);
        await Assert.That(c[0].Anchor.Z).IsEqualTo(-c[1].Anchor.Z);
    }

    [Test]
    public async Task A_bare_marker_takes_the_corpus_defaults()
    {
        var c = Compile(Json)[0];
        await Assert.That((c.Size, c.Height, c.Shell)).IsEqualTo((5, 5, 1));
        await Assert.That(c.OpenTop).IsFalse();          // 65% of corpus cores are capped
        await Assert.That((c.Float, c.Leak)).IsEqualTo((6, 5));
        await Assert.That(c.Box).IsNull();               // the terrain's to decide
    }

    [Test]
    public async Task A_core_is_nameless_by_default_because_PGM_names_it()
    {
        // Unlike a destroyable, which PGM rejects nameless — so the compiler must NOT invent one here.
        await Assert.That(Compile(Json)[0].Name).IsEqualTo(string.Empty);
        var named = Json.Replace(Marker, """{ "piece": "bar-w", "at": [1, 1], "name": "The Heart" }""");
        await Assert.That(Compile(named)[0].Name).IsEqualTo("The Heart");
    }

    [Test]
    public async Task Authored_knobs_win_over_the_defaults()
    {
        var json = Json.Replace(Marker,
            """{ "piece": "bar-w", "at": [1, 1], "size": 7, "height": 7, "shell": 2, "openTop": true, "float": 3, "leak": 4 }""");
        var c = Compile(json)[0];
        await Assert.That((c.Size, c.Height, c.Shell)).IsEqualTo((7, 7, 2));
        await Assert.That(c.OpenTop).IsTrue();
        await Assert.That((c.Float, c.Leak)).IsEqualTo((3, 4));
    }

    // DC2 — the pair is one knob: together they say how far players dig to make the lava leak.
    [Test]
    [Arguments(6, 5, 0)]    // the defaults: leak ≤ float, so a breached casing leaks on its own
    [Arguments(2, 5, 3)]    // leak > float: digging is part of the capture
    [Arguments(0, 5, 5)]    // resting on the floor (27% of the corpus) — the full leak depth to dig
    public async Task Float_and_leak_together_state_the_dig_depth(int floatBlocks, int leak, int expected)
    {
        var json = Json.Replace(Marker,
            $$"""{ "piece": "bar-w", "at": [1, 1], "float": {{floatBlocks}}, "leak": {{leak}} }""");
        await Assert.That(Compile(json)[0].DigDepth).IsEqualTo(expected);
        // The intent and the stamper must agree on the rule, or the world and the XML tell different stories.
        await Assert.That(ObjectiveDefaults.DigDepth(leak, floatBlocks)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("\"float\": 3")]
    [Arguments("\"leak\": 3")]
    public async Task Authoring_one_of_the_pair_without_the_other_is_an_error(string half)
    {
        // Silently pairing it with the other's default is a dig depth nobody chose.
        var json = Json.Replace(Marker, $$"""{ "piece": "bar-w", "at": [1, 1], {{half}} }""");
        await Assert.That(Errors(Validate(json), "without its pair")).IsTrue();
    }

    [Test]
    public async Task Authoring_both_halves_is_fine()
    {
        var json = Json.Replace(Marker, """{ "piece": "bar-w", "at": [1, 1], "float": 3, "leak": 5 }""");
        await Assert.That(Errors(Validate(json), "without its pair")).IsFalse();
        await Assert.That(Errors(Validate(Json), "without its pair")).IsFalse().Because("neither half is authored");
    }

    [Test]
    [Arguments(3, 2)]   // 3 − 2·2 = −1 across
    [Arguments(2, 1)]   // 2 − 2·1 = 0 across
    public async Task A_casing_with_no_room_for_lava_is_an_error(int size, int shell)
    {
        // The stamper would fill a solid block of obsidian: a goal that can never leak, so never captured.
        var json = Json.Replace(Marker,
            $$"""{ "piece": "bar-w", "at": [1, 1], "size": {{size}}, "height": {{size}}, "shell": {{shell}} }""");
        await Assert.That(Errors(Validate(json), "no lava inside")).IsTrue();
    }

    [Test]
    public async Task A_shell_thinner_than_one_block_is_not_a_casing()
    {
        var json = Json.Replace(Marker, """{ "piece": "bar-w", "at": [1, 1], "shell": 0 }""");
        await Assert.That(Errors(Validate(json), "not a casing")).IsTrue();
    }

    [Test]
    public async Task Cores_need_a_two_team_symmetry_and_compile_to_nothing_otherwise()
    {
        // OB14, same as destroyables — and the preview compiles unvalidated, so it must not draw four.
        var json = Json.Replace("\"symmetry\": \"rot_180\"", "\"symmetry\": \"rot_90\"");
        await Assert.That(Errors(Validate(json), "two-team")).IsTrue();
        await Assert.That(PlanCompiler.Compile(PlanModel.Parse(json)!).Intent.Cores).IsNull();
    }

    [Test]
    public async Task A_plan_with_no_core_carries_none_rather_than_an_empty_list()
    {
        var json = Json.Replace(Marker, "");
        await Assert.That(PlanCompiler.Compile(PlanModel.Parse(json)!).Intent.Cores).IsNull();
    }

    [Test]
    public async Task A_marker_outside_its_piece_is_an_error()
    {
        var json = Json.Replace(Marker, """{ "piece": "bar-w", "at": [9, 9] }""");
        await Assert.That(Errors(Validate(json), "core")).IsTrue();
    }
}
