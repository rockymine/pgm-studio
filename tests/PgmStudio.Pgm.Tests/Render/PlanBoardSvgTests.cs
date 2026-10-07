using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Geom;
using PgmStudio.Pgm.Render;
using PgmStudio.Vocabulary;

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
    public async Task The_key_names_the_four_inks_and_dashes_only_the_build_zone()
    {
        var labels = PlanBoardPalette.Key.Select(entry => entry.Label).ToList();

        await Assert.That(labels).IsEquivalentTo(new[] { "Spawn", "Wool room", "Ground (rest)", "Build zone" });
        await Assert.That(PlanBoardPalette.Key.Single(entry => entry.Dashed).Label).IsEqualTo("Build zone");
    }

    [Test]
    public async Task A_board_picture_carries_no_text()
    {
        // The page draws the key once beside its pictures; a key inside each one is unreadable at card size.
        var svg = PlanBoardSvg.Render(Composer.Compose(new ComposeRequest(12, seed: 3)));

        await Assert.That(svg).DoesNotContain("<text");
    }

    [Test]
    public async Task Every_piece_carries_its_role_class_and_ground_roles_share_the_ground_ink()
    {
        var plan = new PlanModel();
        plan.Globals.Symmetry = "none";
        plan.Pieces.Add(new PlanPiece { Id = "hub", Role = PlanRoles.Piece, Rect = new CellRect(0, 0, 4, 4) });
        plan.Pieces.Add(new PlanPiece { Id = "frontline", Role = PlanRoles.Piece, Rect = new CellRect(4, 0, 4, 4) });
        plan.Pieces.Add(new PlanPiece { Id = "wool-a", Role = PlanRoles.Piece, Rect = new CellRect(8, 0, 4, 4) });
        plan.Pieces.Add(new PlanPiece { Id = "wool-a-room", Role = PlanRoles.WoolRoom, Rect = new CellRect(12, 0, 4, 4) });
        plan.Pieces.Add(new PlanPiece { Id = "spawn-room", Role = PlanRoles.Spawn, Rect = new CellRect(16, 0, 4, 4) });

        var svg = PlanBoardSvg.Render(plan);

        foreach (var role in new[] { BoardRoles.Hub, BoardRoles.Frontline, BoardRoles.Approach, BoardRoles.Wool, BoardRoles.Spawn })
            await Assert.That(svg).Contains($"class='role-{role}'");
        var ground = PlanBoardPalette.Ground.FillCss;
        await Assert.That(svg.Split(ground).Length - 1).IsEqualTo(3);
        await Assert.That(svg).Contains(PlanBoardPalette.WoolRoom.FillCss);
        await Assert.That(svg).Contains(PlanBoardPalette.Spawn.FillCss);
    }

    [Test]
    public async Task A_zone_draws_as_a_dashed_outline_and_a_water_lane_draws_the_same()
    {
        var plan = new PlanModel();
        plan.Globals.Symmetry = "none";
        plan.Pieces.Add(new PlanPiece { Id = "hub", Role = PlanRoles.Piece, Rect = new CellRect(0, 0, 4, 4) });
        plan.Zones.Add(new PlanZone { Id = "bz-1", Rect = new CellRect(4, 0, 4, 4) });
        plan.Zones.Add(new PlanZone { Id = "wl-1", Rect = new CellRect(8, 0, 4, 4), Kind = PlanZoneKinds.WaterLane });

        var svg = PlanBoardSvg.Render(plan);

        await Assert.That(svg.Split($"class='role-{BoardRoles.Zone}'").Length - 1).IsEqualTo(2);
        await Assert.That(svg.Split("stroke-dasharray='5 3'").Length - 1).IsEqualTo(2);
        await Assert.That(svg).DoesNotContain("Hatch");
    }

    [Test]
    public async Task Every_fanned_image_is_drawn_at_full_strength()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 3));

        var svg = PlanBoardSvg.Render(plan);

        await Assert.That(svg).DoesNotContain("opacity='0.5'");
        await Assert.That(svg).Contains("stroke-width='1'/>");
    }

    [Test]
    public async Task The_svg_paints_no_ground_and_themes_every_ink()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 3));

        var svg = PlanBoardSvg.Render(plan);

        await Assert.That(svg).DoesNotContain("0b1222");
        await Assert.That(svg).Contains("var(--board-ground,");
        await Assert.That(svg).Contains("var(--board-spawn-edge,");
    }
}
