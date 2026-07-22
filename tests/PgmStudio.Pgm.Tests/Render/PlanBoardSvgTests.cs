using PgmStudio.Pgm.Compose;
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
}
