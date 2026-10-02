using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Geom;
using PgmStudio.Pgm.Render;

namespace PgmStudio.Pgm.Tests.Render;

/// <summary>The browse feed's board renderer: a composed plan renders to a self-contained SVG carrying the
/// fanned pieces (rects) and the spawn markers (circles), and the render is deterministic for a fixed plan.</summary>
public sealed class PlanBoardSvgTests
{
    [Test]
    public async Task Render_draws_the_fanned_board_with_pieces_and_a_spawn_marker()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 3));
        var svg = PlanBoardSvg.Render(plan);

        await Assert.That(svg).StartsWith("<svg");
        await Assert.That(svg).Contains("</svg>");
        await Assert.That(svg).Contains("<rect");     // fanned pieces
        await Assert.That(svg).Contains("<circle");   // spawn markers
    }

    [Test]
    public async Task Render_is_deterministic_for_a_fixed_plan()
    {
        var plan = Composer.Compose(new ComposeRequest(8, seed: 1));
        await Assert.That(PlanBoardSvg.Render(plan)).IsEqualTo(PlanBoardSvg.Render(plan));
    }

    [Test]
    public async Task The_key_names_every_role_and_both_zone_kinds_once()
    {
        // B95: whoever shows a board shows its key, so the key has to cover every colour a board can paint,
        // whether or not one particular board uses them all.
        var labels = PlanBoardPalette.Key.Select(entry => entry.Label).ToList();

        await Assert.That(labels).IsEquivalentTo(new[] { "Hub", "Spawn", "Wool", "Front line", "Other", "Build zone", "Water lane" });
        await Assert.That(PlanBoardPalette.Key.Single(entry => entry.Hatched).Label).IsEqualTo("Water lane");
    }

    [Test]
    public async Task A_board_picture_carries_no_text()
    {
        // The page draws the key once beside its pictures; a key inside each one is unreadable at card size.
        var svg = PlanBoardSvg.Render(Composer.Compose(new ComposeRequest(12, seed: 3)));

        await Assert.That(svg).DoesNotContain("<text");
    }

    [Test]
    public async Task A_water_lane_draws_with_the_hatch_pattern_and_a_build_zone_does_not()
    {
        var plan = new PlanModel();
        plan.Globals.Symmetry = "none";
        plan.Pieces.Add(new PlanPiece { Id = "hub-1", Role = PlanRoles.Piece, Rect = new CellRect(0, 0, 4, 4) });
        plan.Zones.Add(new PlanZone { Id = "bz-1", Rect = new CellRect(4, 0, 4, 4) });
        plan.Zones.Add(new PlanZone { Id = "wl-1", Rect = new CellRect(8, 0, 4, 4), Kind = PlanZoneKinds.WaterLane });

        var svg = PlanBoardSvg.Render(plan);

        await Assert.That(svg).Contains("waterLaneHatch");
        // The build zone's own rect fills with its flat colour rather than the hatch pattern.
        await Assert.That(svg).Contains($"fill='{PlanBoardPalette.BuildZoneColor}' fill-opacity='0.38'");
    }
}
