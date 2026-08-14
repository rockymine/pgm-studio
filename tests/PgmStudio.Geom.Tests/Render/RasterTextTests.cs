using PgmStudio.Geom.Render;

namespace PgmStudio.Geom.Tests.Render;

/// <summary>The pixel-buffer text/rect helpers a legend and a scale bar are built from.</summary>
public sealed class RasterTextTests
{
    private const int Background = 0x000000;
    private const int Ink = 0xFFFFFF;

    private static byte[] Blank(int width, int height)
    {
        var pixels = new byte[width * height * 3];
        for (var i = 0; i < width * height; i++) Raster.Set(pixels, width, i % width, i / width, Background);
        return pixels;
    }

    private static bool AnyPixelIs(byte[] pixels, int width, int height, int packedRgb)
    {
        for (var row = 0; row < height; row++)
            for (var col = 0; col < width; col++)
            {
                var offset = (row * width + col) * 3;
                var value = (pixels[offset] << 16) | (pixels[offset + 1] << 8) | pixels[offset + 2];
                if (value == packedRgb) return true;
            }
        return false;
    }

    [Test]
    public async Task DrawText_paints_at_least_one_ink_pixel_for_non_blank_text()
    {
        var pixels = Blank(60, 10);
        Raster.DrawText(pixels, 60, 10, 0, 0, "HI", Ink, pixelSize: 1);
        await Assert.That(AnyPixelIs(pixels, 60, 10, Ink)).IsTrue();
    }

    [Test]
    public async Task DrawText_of_only_spaces_paints_nothing()
    {
        var pixels = Blank(60, 10);
        Raster.DrawText(pixels, 60, 10, 0, 0, "   ", Ink, pixelSize: 1);
        await Assert.That(AnyPixelIs(pixels, 60, 10, Ink)).IsFalse();
    }

    [Test]
    public async Task DrawText_returns_the_pixel_width_it_consumed_and_TextWidth_agrees()
    {
        var pixels = Blank(200, 10);
        var consumed = Raster.DrawText(pixels, 200, 10, 0, 0, "SCALE", Ink, pixelSize: 2);
        await Assert.That(consumed).IsEqualTo(Raster.TextWidth("SCALE", pixelSize: 2));
        await Assert.That(consumed).IsGreaterThan(0);
    }

    [Test]
    public async Task FillHatchedRect_leaves_some_cells_at_the_base_opacity_and_others_stronger()
    {
        // A hatch is a stripe pattern, not a flat tint — some cells in the rect must read differently from
        // others, or the "hatch" is indistinguishable from FillRect at one opacity.
        var pixels = Blank(20, 20);
        Raster.FillHatchedRect(pixels, 20, 20, 0, 0, 20, 20, 0xFF0000, opacity: 0.4, period: 5);

        var reds = new HashSet<int>();
        for (var row = 0; row < 20; row++)
            for (var col = 0; col < 20; col++)
                reds.Add(pixels[(row * 20 + col) * 3]);   // the red channel: the only one this colour touches

        await Assert.That(reds.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task FillRect_is_clipped_to_the_buffer_rather_than_throwing()
    {
        var pixels = Blank(10, 10);
        Raster.FillRect(pixels, 10, 10, -5, -5, 20, 20, Ink);
        await Assert.That(AnyPixelIs(pixels, 10, 10, Ink)).IsTrue();
    }
}
