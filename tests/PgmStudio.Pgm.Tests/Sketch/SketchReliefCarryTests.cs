using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// What happens to authored terrain when a plan is recompiled over it. A relief is geometry, so the recompile
/// replaces the shapes it was authored against — and unlike a theme, it cannot be re-derived from anything.
/// The rule is therefore to refuse rather than to carry what happens to still match: island identity comes
/// from the geometry, so a re-fused board does not move an island, it produces a different one.
/// </summary>
public sealed class SketchReliefCarryTests
{
    private static string Layout(string islandId, string? relief = null) => $$"""
    {
      "layout": {
        "shapes": [
          { "id": "s1", "type": "rectangle", "operation": "add",
            "min_x": 0, "min_z": 0, "max_x": 10, "max_z": 10, "base_height": 2 }
        ],
        "islands": [ { "id": "{{islandId}}", "mirrors": false, "shapeIds": ["s1"] } ]
      }
      {{(relief is null ? "" : $", \"relief\": {relief}")}}
    }
    """;

    private const string OneRelief = """
    { "i1": { "base": 6, "marks": [ { "kind": "point", "at": [5, 5], "h": 11, "r": 2 } ] } }
    """;

    [Test]
    public async Task A_relief_survives_a_recompile_that_keeps_its_island()
    {
        var merged = SketchLayout.CarryRelief(Layout("i1"), Layout("i1", OneRelief));
        var state = SketchLayout.Parse(merged);

        await Assert.That(state!.Relief).IsNotNull();
        await Assert.That(state.Relief!.ContainsKey("i1")).IsTrue();
        await Assert.That(state.Relief["i1"].Base).IsEqualTo(6);
    }

    [Test]
    public async Task A_recompile_that_loses_the_island_reports_the_orphan()
    {
        var orphans = SketchLayout.OrphanedRelief(Layout("i2"), Layout("i1", OneRelief));
        await Assert.That(orphans.Count).IsEqualTo(1);
        await Assert.That(orphans[0]).IsEqualTo("i1");
    }

    [Test]
    public async Task A_recompile_that_keeps_the_island_reports_no_orphan()
    {
        await Assert.That(SketchLayout.OrphanedRelief(Layout("i1"), Layout("i1", OneRelief)).Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_layout_with_no_relief_has_nothing_to_orphan()
    {
        await Assert.That(SketchLayout.OrphanedRelief(Layout("i2"), Layout("i1")).Count).IsEqualTo(0);
        await Assert.That(SketchLayout.OrphanedRelief(Layout("i2"), null).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Carrying_drops_an_orphan_rather_than_rebinding_it()
    {
        // Reached only once the author has accepted the loss — a relief authored against the old fusion has
        // nowhere correct to land on the new one, so it is not guessed at.
        var merged = SketchLayout.CarryRelief(Layout("i2"), Layout("i1", OneRelief));
        var state = SketchLayout.Parse(merged);

        await Assert.That(state!.Relief is null || !state.Relief.ContainsKey("i1")).IsTrue();
    }

    [Test]
    public async Task Carrying_a_relief_leaves_the_finish_carry_alone()
    {
        // The two carries are independent, and both run on a recompile: a relief is geometry, a theme is not.
        const string stored = """
        {
          "layout": { "shapes": [], "islands": [ { "id": "i1", "mirrors": false, "shapeIds": [] } ] },
          "mapTheme": "grass",
          "relief": { "i1": { "base": 6 } }
        }
        """;
        var compiled = Layout("i1");
        var merged = SketchLayout.CarryRelief(SketchLayout.CarryFinish(compiled, stored), stored);
        var state = SketchLayout.Parse(merged);

        await Assert.That(state!.MapTheme).IsEqualTo("grass");
        await Assert.That(state.Relief!.ContainsKey("i1")).IsTrue();
    }
}
