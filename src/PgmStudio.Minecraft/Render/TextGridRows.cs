namespace PgmStudio.Minecraft.Render;

/// <summary>
/// The ruler and the coordinate-prefixed rows a text grid shares, so a heightmap and a slope grid draw the
/// same shape a reader learns once: two lines above the grid mark every tenth column with its tens digit and
/// every column with its units digit, then each row opens with its own coordinate right-aligned in four
/// characters and a space, so a column is read off the picture rather than counted from an edge.
/// </summary>
internal static class TextGridRows
{
    public static void Append(System.Text.StringBuilder text, int width, int height, int minX, int minZ,
        int every, Func<int, int, char> glyphAt)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            text.Append(new string(' ', 5));
            for (var col = 0; col < width; col++)
            {
                var x = minX + col * every;
                text.Append(pass == 0
                    ? (x % 10 == 0 ? Digit(Math.Abs(x / 10) % 10) : (x < 0 && x % 10 == 0 ? '-' : ' '))
                    : Digit(Math.Abs(x) % 10));
            }
            text.Append('\n');
        }

        for (var row = 0; row < height; row++)
        {
            text.Append($"{minZ + row * every,4} ");
            for (var col = 0; col < width; col++) text.Append(glyphAt(col, row));
            text.Append('\n');
        }
    }

    private static char Digit(int value) => (char)('0' + value);
}
