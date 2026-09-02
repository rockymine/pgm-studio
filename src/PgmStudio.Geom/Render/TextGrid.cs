using System.Text;

namespace PgmStudio.Geom.Render;

/// <summary>
/// The parts every text grid draws the same way, so a reader learns one shape and reads every grid the
/// studio answers in it: a ruler of two lines over the columns, each row opened with its own coordinate,
/// and one alphabet for a value a cell prints as a single character. A coordinate is read off the picture
/// rather than counted from an edge.
/// </summary>
public static class TextGrid
{
    /// <summary>The margin the ruler and every row label take, so a column lines up under its ruler digit.</summary>
    private const string Margin = "     ";

    /// <summary>A value as one character: <c>0</c>–<c>9</c>, then <c>a</c>–<c>z</c> for 10 to 35. Below zero
    /// prints <c>0</c> and above 35 prints <c>z</c>, so a grid never fails over an outlier.</summary>
    public static char Base36(int value) =>
        value <= 0 ? '0' : value < 10 ? (char)('0' + value) : value < 36 ? (char)('a' + value - 10) : 'z';

    /// <summary>Two ruler lines over <paramref name="columns"/> columns whose x runs from
    /// <paramref name="minX"/> in steps of <paramref name="every"/>. The upper line carries the tens digit of
    /// every multiple of ten, at the column whose step reaches it — its own column when the step is one — with
    /// a <c>-</c> in the column before where that ten is negative; the lower line carries the units digit of
    /// every column. Each line opens with <paramref name="prefix"/> and closes with <paramref name="suffix"/>.</summary>
    public static void Ruler(StringBuilder text, string prefix, int minX, int columns, int every, string suffix = "")
    {
        text.Append(prefix);
        for (var column = 0; column < columns; column++)
        {
            var x = minX + column * every;
            text.Append(TenReached(x, every) is { } ten ? (char)('0' + Math.Abs(ten / 10) % 10)
                : TenReached(x + every, every) is { } coming && coming < 0 ? '-' : ' ');
        }
        text.Append(suffix).Append('\n');

        text.Append(prefix);
        for (var column = 0; column < columns; column++) text.Append((char)('0' + Math.Abs(minX + column * every) % 10));
        text.Append(suffix).Append('\n');
    }

    /// <summary>The multiple of ten a column's step reaches — the largest one at or below <paramref name="x"/>
    /// and above the column before it — or null where the step crosses none.</summary>
    private static int? TenReached(int x, int every)
    {
        var ten = x - ((x % 10) + 10) % 10;
        return ten > x - every ? ten : null;
    }

    /// <summary>The ruler and the rows as one frame: a five-character margin over the ruler, then every row
    /// opened with its own z right-aligned in four characters and a space, and one character per column from
    /// <paramref name="glyphAt"/>, called with the column then the row.</summary>
    public static void Frame(StringBuilder text, int minX, int minZ, int width, int height, int every,
        Func<int, int, char> glyphAt)
    {
        Ruler(text, Margin, minX, width, every);
        for (var row = 0; row < height; row++)
        {
            text.Append($"{minZ + row * every,4} ");
            for (var column = 0; column < width; column++) text.Append(glyphAt(column, row));
            text.Append('\n');
        }
    }
}
