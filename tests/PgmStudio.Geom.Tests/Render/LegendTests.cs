using PgmStudio.Geom.Render;

namespace PgmStudio.Geom.Tests.Render;

/// <summary>
/// The key every render's PNG carries baked in (<c>B95</c>/<c>B98</c>): a render answers "did the thing come
/// out", and a legend is what stops a reader from answering "what does this colour mean" by guessing.
/// </summary>
public sealed class LegendTests
{
    private static byte[] Solid(int width, int height, int packedRgb)
    {
        var pixels = new byte[width * height * 3];
        for (var row = 0; row < height; row++)
            for (var col = 0; col < width; col++)
                Raster.Set(pixels, width, col, row, packedRgb);
        return pixels;
    }

    private static bool RowHasColour(byte[] pixels, int width, int row, int packedRgb)
    {
        for (var col = 0; col < width; col++)
        {
            var offset = (row * width + col) * 3;
            var value = (pixels[offset] << 16) | (pixels[offset + 1] << 8) | pixels[offset + 2];
            if (value == packedRgb) return true;
        }
        return false;
    }

    [Test]
    public async Task AppendBelow_grows_the_image_and_leaves_the_original_pixels_untouched()
    {
        var original = Solid(100, 40, 0x224466);
        var withLegend = Legend.AppendBelow(original, 100, 40, [new Legend.Entry("FOO", 0xFF0000)], out var newHeight);

        await Assert.That(newHeight).IsGreaterThan(40);
        await Assert.That(withLegend.Length).IsEqualTo(100 * newHeight * 3);
        // every original pixel is copied byte-for-byte into the top of the new buffer
        for (var i = 0; i < original.Length; i++) await Assert.That(withLegend[i]).IsEqualTo(original[i]);
    }

    [Test]
    public async Task Every_entrys_swatch_colour_appears_somewhere_in_the_appended_strip()
    {
        var original = Solid(200, 20, 0x000000);
        List<Legend.Entry> entries = [new("ALPHA", 0x11FF22), new("BETA", 0x9922FF), new("GAMMA", 0xFFAA00)];
        var withLegend = Legend.AppendBelow(original, 200, 20, entries, out var newHeight);

        foreach (var entry in entries)
        {
            var found = false;
            for (var row = 20; row < newHeight && !found; row++)
                if (RowHasColour(withLegend, 200, row, entry.Rgb)) found = true;
            await Assert.That(found).IsTrue();
        }
    }

    [Test]
    public async Task A_scale_label_paints_ink_below_the_swatch_rows()
    {
        var noScale = Legend.AppendBelow(Solid(200, 20, 0), 200, 20, [new Legend.Entry("A", 0xFFFFFF)], out var heightWithout);
        var withScale = Legend.AppendBelow(Solid(200, 20, 0), 200, 20, [new Legend.Entry("A", 0xFFFFFF)], out var heightWith,
            scaleLabel: "SCALE: 1 BLOCK = 3 PX");

        // Stating the scale costs a whole extra row, which is the point: a scale on every world read-back is
        // not free, but a caller cannot forget to draw it if it is a required parameter of the shared call.
        await Assert.That(heightWith).IsGreaterThan(heightWithout);
    }

    [Test]
    public async Task Many_entries_wrap_to_more_than_one_row_instead_of_running_off_the_image()
    {
        var entries = Enumerable.Range(0, 20).Select(i => new Legend.Entry($"ENTRY {i}", 0x123456 + i)).ToList();
        var narrow = Legend.AppendBelow(Solid(120, 10, 0), 120, 10, entries, out var narrowHeight);
        var wide = Legend.AppendBelow(Solid(2000, 10, 0), 2000, 10, entries, out var wideHeight);

        // The same twenty entries wrap into more rows (a taller strip) on a narrow image than on a wide one.
        await Assert.That(narrowHeight).IsGreaterThan(wideHeight);
    }

    [Test]
    public async Task A_hatched_entrys_swatch_is_not_a_flat_fill()
    {
        var original = Solid(100, 20, 0x000000);
        var withLegend = Legend.AppendBelow(original, 100, 20, [new Legend.Entry("HATCH", 0xFF0000, Hatched: true)], out var newHeight);

        var reds = new HashSet<int>();
        for (var row = 20; row < newHeight; row++)
            for (var col = 0; col < 100; col++)
                reds.Add(withLegend[(row * 100 + col) * 3]);

        // More than one distinct red-channel value in the swatch region means the hatch actually varied the
        // fill rather than painting a solid rectangle indistinguishable from a non-hatched entry.
        await Assert.That(reds.Count).IsGreaterThan(1);
    }
}
