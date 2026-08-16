using PgmStudio.Geom.Render;

namespace PgmStudio.Minecraft.Views;

/// <summary>
/// One block-preview picture as the derivation it is — a grid of coloured cells — with its two encodings
/// hanging off it. Every preview is computed once as a colour function and then asked for either form, so
/// the SVG a card shows and the PNG an agent saves are one picture by construction rather than two renders
/// agreeing today. <see cref="ColorAt"/> answers a cell's <c>#rrggbb</c>, or null where nothing is drawn.
/// </summary>
public sealed record CellRaster(int Columns, int Rows, int Cell, Func<int, int, string?> ColorAt)
{
    /// <summary>What an undrawn cell becomes in a PNG. An SVG leaves it unpainted and the card behind shows
    /// through; a PNG has no behind, so the studio's dark canvas tone stands in for it.</summary>
    public const string Background = "#14161a";

    public string Svg() => SvgRaster.Raster(Columns, Rows, Cell, ColorAt);

    /// <summary>The same picture rasterized: each cell becomes a <see cref="Cell"/>-pixel square, encoded by
    /// the studio's own <see cref="PngWriter"/> — no imaging library, exactly as the world renders write.</summary>
    public byte[] Png(string background = Background)
    {
        int width = Columns * Cell, height = Rows * Cell;
        var rgb = new byte[width * height * 3];
        var back = Rgb(background);

        for (var row = 0; row < Rows; row++)
        for (var column = 0; column < Columns; column++)
        {
            var color = ColorAt(column, row) is { } hex ? Rgb(hex) : back;
            for (var py = row * Cell; py < (row + 1) * Cell; py++)
            {
                var at = (py * width + column * Cell) * 3;
                for (var px = 0; px < Cell; px++)
                {
                    rgb[at++] = color.R; rgb[at++] = color.G; rgb[at++] = color.B;
                }
            }
        }
        return PngWriter.Encode(width, height, rgb);
    }

    private static (byte R, byte G, byte B) Rgb(string hex)
    {
        // "#rrggbb", the one form BlockPalette.Hex emits; anything shorter or malformed falls back to the
        // background's own black rather than throwing mid-picture.
        if (hex.Length != 7 || hex[0] != '#') return (0, 0, 0);
        return (Byte(hex, 1), Byte(hex, 3), Byte(hex, 5));

        static byte Byte(string text, int at) =>
            (byte)(Digit(text[at]) * 16 + Digit(text[at + 1]));
        static int Digit(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => 0,
        };
    }
}
