namespace PgmStudio.Geom.Render;

/// <summary>
/// A key, drawn onto the picture rather than left to a caption an image reader never sees: a strip appended
/// below a render's own pixels, naming every colour the render used. <c>docs/tools/capabilities.md</c> states
/// the rule this exists for — an image is a check, not a source of meaning, so a colour a reader cannot name
/// from the picture alone is a colour that will be misread as whatever convention the reader already carries
/// (blue as water, green as grass). One legend implementation is shared by the plan render and every world
/// read-back so a swatch is drawn the same way everywhere it appears.
/// </summary>
public static class Legend
{
    /// <param name="Label">What the swatch stands for.</param>
    /// <param name="Rgb">The swatch's own colour.</param>
    /// <param name="Hatched">Whether the swatch is drawn as diagonal stripes rather than a solid fill — the
    /// same distinction the legend explains applies to the picture itself, so a hatched category reads as
    /// hatched in its own key rather than as a plain colour with a hatched picture nobody was told to expect.</param>
    public readonly record struct Entry(string Label, int Rgb, bool Hatched = false);

    private const int Margin = 6;
    private const int SwatchSize = 10;
    private const int LabelGap = 4;
    private const int EntryGap = 14;
    private const int Background = 0x14161C;
    private const int TextRgb = 0xEDEFF2;
    private const int HatchRgb = 0x0B0C10;
    private static int RowHeight(int textScale) => Math.Max(SwatchSize, PixelFont.GlyphSize * textScale) + 6;

    /// <summary>Appends a legend strip below <paramref name="pixels"/> — every entry wrapped to fit
    /// <paramref name="width"/>, plus one more line stating <paramref name="scaleLabel"/> when given (the
    /// "how many blocks is one pixel" reading every world read-back needs beside its key). The original image
    /// is untouched pixel-for-pixel; only rows are added beneath it, so a caller comparing renders before and
    /// after adding a legend can still diff the original region directly.
    ///
    /// <para>Text draws at the largest of two sizes that still fits <paramref name="width"/> — a small map
    /// (a low player count, a synthetic test fixture) gets a narrow image, and a fixed large text size would
    /// run the scale label off the edge exactly the way an unreadable render does, which is the one failure
    /// a legend must never itself commit.</para></summary>
    public static byte[] AppendBelow(byte[] pixels, int width, int height, IReadOnlyList<Entry> entries,
        out int newHeight, string? scaleLabel = null)
    {
        var textScale = PickTextScale(entries, scaleLabel, width);
        var rows = LayoutRows(entries, width, textScale);
        var scaleRows = scaleLabel is null ? 0 : 1;
        var rowHeight = RowHeight(textScale);
        var stripHeight = Margin * 2 + (rows.Count + scaleRows) * rowHeight;
        newHeight = height + stripHeight;

        var canvas = new byte[width * newHeight * 3];
        Array.Copy(pixels, canvas, pixels.Length);
        Raster.FillRect(canvas, width, newHeight, 0, height, width, stripHeight, Background);

        var textBaseline = (SwatchSize - PixelFont.GlyphSize * textScale) / 2;
        var y = height + Margin;
        foreach (var row in rows)
        {
            var x = Margin;
            foreach (var entry in row)
            {
                if (entry.Hatched) DrawHatchedSwatch(canvas, width, newHeight, x, y, entry.Rgb, textScale);
                else Raster.FillRect(canvas, width, newHeight, x, y, SwatchSize, SwatchSize, entry.Rgb);
                x += SwatchSize + LabelGap;
                x += Raster.DrawText(canvas, width, newHeight, x, y + textBaseline, entry.Label, TextRgb, textScale);
                x += EntryGap;
            }
            y += rowHeight;
        }
        if (scaleLabel is not null) Raster.DrawText(canvas, width, newHeight, Margin, y + textBaseline, scaleLabel, TextRgb, textScale);

        return canvas;
    }

    /// <summary>Text draws at 2px per font-pixel when everything — every entry's own row, and the scale label
    /// on its own — fits the image at that size; a narrower image (or a caller with an unusually long label)
    /// falls back to 1px per font-pixel rather than clipping.</summary>
    private static int PickTextScale(IReadOnlyList<Entry> entries, string? scaleLabel, int width)
    {
        for (var candidate = 2; candidate > 1; candidate--)
        {
            var fits = LayoutRows(entries, width, candidate).All(row =>
                row.Sum(entry => EntryWidth(entry, candidate)) + Margin <= width);
            var scaleFits = scaleLabel is null || Raster.TextWidth(scaleLabel, candidate) + Margin <= width;
            if (fits && scaleFits) return candidate;
        }
        return 1;
    }

    /// <summary>Greedy left-to-right wrap: an entry starts a new row only when it would not fit the one it is
    /// on, so a short legend stays one line and a long one wraps rather than running off the image.</summary>
    private static List<List<Entry>> LayoutRows(IReadOnlyList<Entry> entries, int width, int textScale)
    {
        if (entries.Count == 0) return [[]];

        var rows = new List<List<Entry>>();
        var current = new List<Entry>();
        var used = Margin;
        var available = Math.Max(width, EntryWidth(entries[0], textScale) + 2 * Margin);

        foreach (var entry in entries)
        {
            var entryWidth = EntryWidth(entry, textScale);
            if (current.Count > 0 && used + entryWidth > available - Margin)
            {
                rows.Add(current);
                current = [];
                used = Margin;
            }
            current.Add(entry);
            used += entryWidth;
        }
        if (current.Count > 0) rows.Add(current);
        return rows.Count > 0 ? rows : [[]];
    }

    private static int EntryWidth(Entry entry, int textScale) =>
        SwatchSize + LabelGap + Raster.TextWidth(entry.Label, textScale) + EntryGap;

    /// <summary>A diagonal-stripe swatch — the same hatch a render uses to mark a category whose colour alone
    /// would not survive being looked at (a water lane over a build zone's own hue).</summary>
    private static void DrawHatchedSwatch(byte[] pixels, int width, int height, int x, int y, int rgb, int textScale)
    {
        var size = Math.Max(SwatchSize, PixelFont.GlyphSize * textScale);
        Raster.FillRect(pixels, width, height, x, y, size, size, rgb);
        Raster.FillHatchedRect(pixels, width, height, x, y, size, size, HatchRgb, 0.55, period: 3);
    }
}
