using PgmStudio.Geom.Render;

namespace PgmStudio.Geom.Tests.Render;

/// <summary>The studio's own PNG encoding reads back as the pixels it was given, with or without alpha.</summary>
public sealed class PngWriterTests
{
    [Test]
    public async Task An_rgba_picture_reads_back_with_its_alpha()
    {
        byte[] rgba = [10, 20, 30, 255, 40, 50, 60, 0, 70, 80, 90, 128, 1, 2, 3, 255];
        var image = PngReader.Decode(PngWriter.EncodeRgba(2, 2, rgba));

        await Assert.That(image.Width).IsEqualTo(2);
        await Assert.That(image.Rgba).IsEquivalentTo(rgba);
    }

    [Test]
    public async Task An_rgb_picture_reads_back_opaque()
    {
        byte[] rgb = [10, 20, 30, 40, 50, 60];
        var image = PngReader.Decode(PngWriter.Encode(2, 1, rgb));

        await Assert.That(image.Rgba).IsEquivalentTo(new byte[] { 10, 20, 30, 255, 40, 50, 60, 255 });
    }
}
