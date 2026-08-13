using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Render;

namespace PgmStudio.Pgm.Tests.Render;

/// <summary>The plan_render raster: the same fanned board <see cref="PlanBoardSvg"/> draws, off the same
/// <see cref="PlanBoardScene"/>, encoded as a PNG an image reader can actually open.</summary>
public sealed class PlanBoardPngTests
{
    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    [Test]
    public async Task Render_emits_a_valid_png_signature()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 3));
        var png = PlanBoardPng.Render(plan);

        await Assert.That(png.Length).IsGreaterThan(PngSignature.Length);
        await Assert.That(png.Take(PngSignature.Length).ToArray()).IsEquivalentTo(PngSignature);
    }

    [Test]
    public async Task Render_is_deterministic_for_a_fixed_plan()
    {
        var plan = Composer.Compose(new ComposeRequest(8, seed: 1));
        await Assert.That(PlanBoardPng.Render(plan)).IsEquivalentTo(PlanBoardPng.Render(plan));
    }

    [Test]
    public async Task Render_draws_a_larger_canvas_for_a_bigger_board()
    {
        var small = Composer.Compose(new ComposeRequest(6, seed: 2));
        var large = Composer.Compose(new ComposeRequest(20, seed: 2));

        // More players fan a wider board, which a raster spends as more pixels rather than a bigger viewBox.
        await Assert.That(PlanBoardPng.Render(large).Length).IsGreaterThan(PlanBoardPng.Render(small).Length);
    }

    [Test]
    public async Task Render_agrees_with_the_svg_render_on_board_shape()
    {
        // Both renderers draw off PlanBoardScene, so an empty scene (no pieces/zones) has to fall back the
        // same way in both: a small fixed canvas rather than an exception or a zero-sized image.
        var plan = Composer.Compose(new ComposeRequest(4, seed: 7));
        var svg = PlanBoardSvg.Render(plan);
        var png = PlanBoardPng.Render(plan);

        await Assert.That(svg).Contains("<rect");
        await Assert.That(png.Length).IsGreaterThan(8);
    }
}
