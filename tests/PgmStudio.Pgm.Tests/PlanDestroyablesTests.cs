using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The destroyable placement's compile: the authored team-0 marker fans to one objective per team, the
/// optional structure parameters take their defaults, and PGM's required name is derived rather than asked
/// for. The box itself is not compiled here — it needs the terrain, so the world-export path resolves it.
/// </summary>
public sealed class PlanDestroyablesTests
{
    // rot_180 → order 2, so one authored marker is two destroyables: red's and blue's.
    private const string Json = """
        {
          "plan": 1,
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "bar-w", "role": "piece", "rect": [0, 0, 2, 2], "surface": 12 }
          ],
          "placements": {
            "destroyables": [ { "piece": "bar-w", "at": [1, 1] } ]
          }
        }
        """;

    private static List<DestroyableIntent> Compile(string json) =>
        PlanCompiler.Compile(PlanModel.Parse(json)!).Intent.Destroyables!;

    [Test]
    public async Task One_authored_marker_fans_to_one_destroyable_per_team()
    {
        var d = Compile(Json);
        await Assert.That(d.Count).IsEqualTo(2);
        await Assert.That(d.Select(x => x.Owner)).IsEquivalentTo(new[] { "red", "blue" });

        // The orbit image is the team-0 anchor rotated a half turn about the centre, not a copy of it.
        await Assert.That(d[0].Anchor.X).IsEqualTo(-d[1].Anchor.X);
        await Assert.That(d[0].Anchor.Z).IsEqualTo(-d[1].Anchor.Z);
    }

    [Test]
    public async Task A_bare_marker_takes_the_corpus_defaults()
    {
        var b = Compile(Json)[0];
        await Assert.That(b.Style).IsEqualTo("pillar-3");
        await Assert.That(b.Materials).IsEqualTo("obsidian");
        await Assert.That(b.Float).IsEqualTo(4);
        // The box is the terrain's to decide; nothing before the world build may invent one.
        await Assert.That(b.Box).IsNull();
    }

    [Test]
    public async Task Authored_parameters_win_over_the_defaults()
    {
        var json = Json.Replace("""{ "piece": "bar-w", "at": [1, 1] }""",
            """{ "piece": "bar-w", "at": [1, 1], "style": "cube-4", "materials": "gold block", "float": 7, "name": "The Vault" }""");
        var b = Compile(json)[0];
        await Assert.That(b.Style).IsEqualTo("cube-4");
        await Assert.That(b.Materials).IsEqualTo("gold block");
        await Assert.That(b.Float).IsEqualTo(7);
        await Assert.That(b.Name).IsEqualTo("The Vault");
    }

    [Test]
    public async Task PGM_requires_a_name_so_the_compiler_derives_one_per_owner_and_index()
    {
        // Two markers for one team: the first is the team's monument, the second is numbered.
        var json = Json.Replace("""[ { "piece": "bar-w", "at": [1, 1] } ]""",
            """[ { "piece": "bar-w", "at": [1, 1] }, { "piece": "bar-w", "at": [0, 0] } ]""");
        var d = Compile(json);
        await Assert.That(d.Where(x => x.Owner == "red").Select(x => x.Name))
            .IsEquivalentTo(new[] { "Red Monument", "Red Monument 2" });
        await Assert.That(d.Where(x => x.Owner == "blue").Select(x => x.Name))
            .IsEquivalentTo(new[] { "Blue Monument", "Blue Monument 2" });
    }

    [Test]
    public async Task A_plan_with_no_destroyable_carries_none_rather_than_an_empty_list()
    {
        // Every CTW map is this case: the intent must look exactly as it did before destroyables existed.
        var json = Json.Replace("""{ "piece": "bar-w", "at": [1, 1] }""", "");
        await Assert.That(PlanCompiler.Compile(PlanModel.Parse(json)!).Intent.Destroyables).IsNull();
    }

    [Test]
    public async Task An_unknown_style_is_an_error_not_a_silent_default()
    {
        var json = Json.Replace("""{ "piece": "bar-w", "at": [1, 1] }""",
            """{ "piece": "bar-w", "at": [1, 1], "style": "pyramid" }""");
        var findings = PlanValidator.Validate(PlanModel.Parse(json)!);
        await Assert.That(findings.Any(f => f.Severity == PlanSeverity.Error && f.Message.Contains("pyramid"))).IsTrue();
    }

    [Test]
    public async Task A_marker_outside_its_piece_is_an_error()
    {
        var json = Json.Replace("""{ "piece": "bar-w", "at": [1, 1] }""", """{ "piece": "bar-w", "at": [9, 9] }""");
        var findings = PlanValidator.Validate(PlanModel.Parse(json)!);
        await Assert.That(findings.Any(f => f.Severity == PlanSeverity.Error && f.Message.Contains("destroyable"))).IsTrue();
    }

    [Test]
    [Arguments("rot_90", 4)]
    [Arguments("none", 1)]
    public async Task A_destroyable_needs_a_two_team_symmetry(string mode, int order)
    {
        // OB14 — at four teams PGM treats every goal as shared, and what that plays like is undecided; at
        // one team there is nobody to break it. The editor hides the tool, but a hand-written plan can ask.
        var json = Json.Replace("\"symmetry\": \"rot_180\"", $"\"symmetry\": \"{mode}\"");
        var findings = PlanValidator.Validate(PlanModel.Parse(json)!);
        await Assert.That(findings.Any(f => f.Severity == PlanSeverity.Error && f.Message.Contains("two-team"))).IsTrue();
        await Assert.That(Geom.Symmetry.Order(mode)).IsEqualTo(order);

        // And it compiles to nothing rather than to `order` shared goals. The structure preview compiles
        // plans mid-edit without validating, so a fanned result here would draw the very design the error
        // forbids — switching an authored plan to rot_90 would silently show four destroyables.
        await Assert.That(PlanCompiler.Compile(PlanModel.Parse(json)!).Intent.Destroyables).IsNull();
    }

    [Test]
    public async Task The_two_team_symmetries_are_accepted()
    {
        foreach (var mode in new[] { "rot_180", "mirror_x", "mirror_z" })
        {
            var json = Json.Replace("\"symmetry\": \"rot_180\"", $"\"symmetry\": \"{mode}\"");
            var findings = PlanValidator.Validate(PlanModel.Parse(json)!);
            await Assert.That(findings.Any(f => f.Severity == PlanSeverity.Error && f.Message.Contains("two-team")))
                .IsFalse().Because($"{mode} is a two-team symmetry");
        }
    }

    [Test]
    public async Task Every_style_slug_resolves_and_round_trips()
    {
        foreach (var slug in DestroyableStyles.All)
        {
            await Assert.That(DestroyableStyles.TryParse(slug, out var style)).IsTrue();
            await Assert.That(DestroyableStyles.Slug(style)).IsEqualTo(slug);
        }
        await Assert.That(DestroyableStyles.IsKnown("pyramid")).IsFalse();
        await Assert.That(DestroyableStyles.IsKnown("")).IsFalse();
    }
}
