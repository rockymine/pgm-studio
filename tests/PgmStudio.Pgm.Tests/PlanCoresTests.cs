using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The core placement's compile: the destroyable's fan with the casing's own knobs, the corpus defaults, and
/// the float/leak pair that only means something together (DC2).
/// </summary>
public sealed class PlanCoresTests
{
    private const string Json = """
        {
          "plan": 2,
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "bar-w", "role": "piece", "rect": [0, 0, 2, 2], "surface": 12 }
          ],
          "placements": {
            "cores": [ { "piece": "bar-w", "at": [5, 5] } ]
          }
        }
        """;

    private const string Marker = """{ "piece": "bar-w", "at": [5, 5] }""";

    private static List<CoreIntent> Compile(string json) =>
        PlanCompiler.Compile(PlanModel.Parse(json)!).Intent.Cores!;

    private static IReadOnlyList<Finding> Validate(string json) =>
        PlanValidator.Check(PlanModel.Parse(json)!);

    private static bool Errors(IReadOnlyList<Finding> f, string contains) =>
        f.Any(x => x.Severity == Severity.Refusal && x.Message.Contains(contains));

    [Test]
    public async Task One_authored_marker_fans_to_one_core_per_team()
    {
        var c = Compile(Json);
        await Assert.That(c.Count).IsEqualTo(2);
        await Assert.That(c.Select(x => x.Owner)).IsEquivalentTo(new[] { "red", "blue" });
        // An anchor names a block, so a pair of orbit images straddles the boundary the board turns about.
        await Assert.That(c[0].Anchor.X + c[1].Anchor.X).IsEqualTo(-1);
        await Assert.That(c[0].Anchor.Z + c[1].Anchor.Z).IsEqualTo(-1);
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
        var named = Json.Replace(Marker, """{ "piece": "bar-w", "at": [5, 5], "name": "The Heart" }""");
        await Assert.That(Compile(named)[0].Name).IsEqualTo("The Heart");
    }

    /// <summary>A core has three knobs and the casing follows from them: the lava's footprint, its courses,
    /// and whether the top is capped. An open casing gave up its cap course, so the same lava stands under
    /// one less block of obsidian.</summary>
    [Test]
    public async Task The_stated_interior_decides_the_casing()
    {
        var json = Json.Replace(Marker,
            """{ "piece": "bar-w", "at": [5, 5], "lava": 5, "lavaHeight": 4, "openTop": true, "float": 3, "leak": 4 }""");
        var c = Compile(json)[0];
        await Assert.That((c.Lava, c.LavaHeight)).IsEqualTo((5, 4));
        await Assert.That((c.Size, c.Height, c.Shell)).IsEqualTo((7, 5, 1))
            .Because("5 lava walled on both sides is 7 across; open, 4 courses need one block of floor");
        await Assert.That(c.OpenTop).IsTrue();
        await Assert.That((c.Float, c.Leak)).IsEqualTo((3, 4));

        var capped = Json.Replace(Marker,
            """{ "piece": "bar-w", "at": [5, 5], "lava": 5, "lavaHeight": 4 }""");
        await Assert.That(Compile(capped)[0].Height).IsEqualTo(6).Because("a cap is the sixth course");
    }

    // ── OB22 — how far a goal may float ─────────────────────────────────────────────────
    [Test]
    public async Task A_float_over_the_cap_is_refused_and_the_cap_itself_stands()
    {
        var over = Json.Replace(Marker, Marked(ObjectiveDefaults.MaxFloat + 1));
        var findings = Validate(over);
        await Assert.That(findings.Any(f => f.Rule == ObjectiveRules.FloatCap)).IsTrue();
        await Assert.That(Errors(findings, ObjectiveDefaults.MaxFloat.ToString())).IsTrue();

        // At the cap exactly, a goal stands: the number is what a goal may float, not what it may not reach.
        var atCap = Json.Replace(Marker, Marked(ObjectiveDefaults.MaxFloat));
        await Assert.That(Validate(atCap).Any(f => f.Rule == ObjectiveRules.FloatCap)).IsFalse();

        static string Marked(int floatBlocks) =>
            $$"""{ "piece": "bar-w", "at": [5, 5], "float": {{floatBlocks}}, "leak": 5 }""";
    }

    [Test]
    public async Task A_destroyables_float_is_capped_by_the_same_number()
    {
        // One rule over both goal kinds: the cap is about how far a player will climb, which does not depend
        // on what is at the top of the climb.
        var high = ObjectiveDefaults.MaxFloat + 4;
        var json = Json
            .Replace("\"cores\":", "\"destroyables\":")
            .Replace(Marker, $$"""{ "piece": "bar-w", "at": [5, 5], "float": {{high}} }""");
        await Assert.That(Validate(json).Any(f => f.Rule == ObjectiveRules.FloatCap)).IsTrue();
    }

    // DC2 — the pair is one knob: together they say how far players dig to make the lava leak.
    [Test]
    [Arguments(6, 5, 0)]    // the defaults: leak < float, so a breached casing leaks on its own
    [Arguments(2, 5, 4)]    // leak > float: digging is part of the capture
    [Arguments(0, 5, 6)]    // resting on the floor (27% of the corpus) — the full leak depth to dig
    [Arguments(5, 5, 1)]    // leak == float still costs the one course the block centre does
    public async Task Float_and_leak_together_state_the_dig_depth(int floatBlocks, int leak, int expected)
    {
        var json = Json.Replace(Marker,
            $$"""{ "piece": "bar-w", "at": [5, 5], "float": {{floatBlocks}}, "leak": {{leak}} }""");
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
        var json = Json.Replace(Marker, $$"""{ "piece": "bar-w", "at": [5, 5], {{half}} }""");
        await Assert.That(Errors(Validate(json), "without its pair")).IsTrue();
    }

    [Test]
    public async Task Authoring_both_halves_is_fine()
    {
        var json = Json.Replace(Marker, """{ "piece": "bar-w", "at": [5, 5], "float": 3, "leak": 5 }""");
        await Assert.That(Errors(Validate(json), "without its pair")).IsFalse();
        await Assert.That(Errors(Validate(Json), "without its pair")).IsFalse().Because("neither half is authored");
    }

    /// <summary><b>A casing with no lava in it is no longer a thing that can be written down.</b> An author
    /// used to state a size and a wall thickness — two numbers that can contradict each other, and a 5
    /// against a 3 left a solid block of obsidian nothing could leak. Stating the interior instead makes the
    /// contradiction unrepresentable; what is left to check is the range, and a number outside it is named
    /// rather than quietly clamped.</summary>
    [Test]
    [Arguments("lava", 1)]
    [Arguments("lava", 6)]
    [Arguments("lavaHeight", 1)]
    [Arguments("lavaHeight", 9)]
    public async Task A_core_outside_the_offered_range_is_an_error(string knob, int value)
    {
        var json = Json.Replace(Marker, $$"""{ "piece": "bar-w", "at": [5, 5], "{{knob}}": {{value}} }""");
        await Assert.That(Errors(Validate(json), "outside")).IsTrue();
    }

    [Test]
    public async Task Every_offered_core_leaves_lava_inside_its_casing()
    {
        for (var lava = ObjectiveDefaults.MinCoreLava; lava <= ObjectiveDefaults.MaxCoreLava; lava++)
        for (var height = ObjectiveDefaults.MinCoreLavaHeight; height <= ObjectiveDefaults.MaxCoreLavaHeight; height++)
        foreach (var open in new[] { false, true })
        {
            var json = Json.Replace(Marker,
                $$"""{ "piece": "bar-w", "at": [5, 5], "lava": {{lava}}, "lavaHeight": {{height}}, "openTop": {{(open ? "true" : "false")}} }""");
            await Assert.That(Errors(Validate(json), "outside")).IsFalse();
            var c = Compile(json)[0];
            await Assert.That(c.Size - 2 * c.Shell).IsEqualTo(lava);
            await Assert.That(c.Height - (open ? 1 : 2) * c.Shell).IsEqualTo(height)
                .Because($"lava {lava}×{height}{(open ? " open" : "")} has to survive the casing it implies");
        }
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
        var json = Json.Replace(Marker, """{ "piece": "bar-w", "at": [45, 45] }""");
        await Assert.That(Errors(Validate(json), "core")).IsTrue();
    }

    // A core is the destroyable's absolute-addressing exception, proved separately here because the two
    // markers resolve through independent loops in `PlanCompiler` and a fix to one does not imply the other.
    // Under the old code `d.Piece("")` returned null and the whole core was dropped from the compiled intent.
    [Test]
    public async Task A_core_with_no_piece_compiles_by_absolute_board_position()
    {
        var json = Json.Replace(Marker, """{ "piece": "", "at": [-10, 30] }""");
        var c = Compile(json);

        await Assert.That(c.Count).IsEqualTo(2);
        await Assert.That(c[0].Anchor.X).IsEqualTo(-2.0 * 5);
        await Assert.That(c[0].Anchor.Z).IsEqualTo(6.0 * 5);
        // An anchor names a block, so a pair of orbit images straddles the boundary the board turns about.
        await Assert.That(c[0].Anchor.X + c[1].Anchor.X).IsEqualTo(-1);
        await Assert.That(c[0].Anchor.Z + c[1].Anchor.Z).IsEqualTo(-1);
    }

    [Test]
    public async Task A_core_with_no_piece_is_not_a_dangling_reference()
    {
        var json = Json.Replace(Marker, """{ "piece": "", "at": [-10, 30] }""");
        await Assert.That(Errors(Validate(json), "unknown piece")).IsFalse();
    }
}
