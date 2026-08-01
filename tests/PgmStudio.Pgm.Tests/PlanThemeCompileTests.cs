using System.Text.Json;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The plan compiler's terrain-theme bake (docs/world-export/terrain-painting.md TP10): authored themes,
/// the map default, scope assignments and piece footprints are carried into the intent for scoped painting.
/// Themes are opaque JSON here (the plan/intent projects don't reference the world package that owns the type),
/// so the bake is asserted on the raw text and the resolved pieceId → themeId map.
/// </summary>
public sealed class PlanThemeCompileTests
{
    private static PlanModel Seed() => PlanModel.Parse(PlanTestSupport.ReadSeed("mirror-tiny-map-cliff.plan.json"))!;
    private static JsonElement Solid(int id) => JsonSerializer.SerializeToElement(new { kind = "solid", id });

    [Test]
    public async Task Bakes_the_registry_map_default_a_piece_scope_and_footprints()
    {
        var plan = Seed();
        var pieceId = plan.Pieces.First(p => PlanRoles.IsGenerating(p.Role)).Id;
        plan.Themes = new() { ["base"] = Solid(1), ["accent"] = Solid(42) };
        plan.MapTheme = "base";
        plan.ThemeScopes = [new PlanThemeScope { Theme = "accent", Pieces = [pieceId] }];

        var (_, intent) = PlanCompiler.Compile(plan);

        await Assert.That(intent.MapTheme).IsEqualTo("base");
        await Assert.That(intent.Themes["accent"]).Contains("\"id\":42");
        await Assert.That(intent.PieceThemes[pieceId]).IsEqualTo("accent");
        await Assert.That(intent.PieceFootprints.Select(f => f.PieceId)).Contains(pieceId);
    }

    [Test]
    public async Task A_box_scope_expands_to_its_member_pieces()
    {
        var plan = Seed();
        var members = plan.Pieces.Where(p => PlanRoles.IsGenerating(p.Role)).Take(2).Select(p => p.Id).ToList();
        plan.Themes = new() { ["boxed"] = Solid(9) };
        plan.Boxes.Add(new PlanBox { Id = "b1", Kind = "mid", Members = members });
        plan.ThemeScopes = [new PlanThemeScope { Theme = "boxed", Box = "b1" }];

        var (_, intent) = PlanCompiler.Compile(plan);

        foreach (var id in members)
            await Assert.That(intent.PieceThemes[id]).IsEqualTo("boxed");
    }

    [Test]
    public async Task A_later_scope_wins_a_shared_piece()
    {
        var plan = Seed();
        var pieceId = plan.Pieces.First(p => PlanRoles.IsGenerating(p.Role)).Id;
        plan.Themes = new() { ["first"] = Solid(1), ["second"] = Solid(2) };
        plan.ThemeScopes =
        [
            new PlanThemeScope { Theme = "first", Pieces = [pieceId] },
            new PlanThemeScope { Theme = "second", Pieces = [pieceId] },   // overrides
        ];

        var (_, intent) = PlanCompiler.Compile(plan);

        await Assert.That(intent.PieceThemes[pieceId]).IsEqualTo("second");
    }

    [Test]
    public async Task No_authored_themes_leaves_the_theme_fields_empty()
    {
        var (_, intent) = PlanCompiler.Compile(Seed());
        await Assert.That(intent.MapTheme).IsNull();
        await Assert.That(intent.Themes.Count).IsEqualTo(0);
        await Assert.That(intent.PieceThemes.Count).IsEqualTo(0);
        await Assert.That(intent.PieceFootprints.Count).IsEqualTo(0);
    }
}
