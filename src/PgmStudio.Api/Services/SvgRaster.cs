using System.Text;

namespace PgmStudio.Api.Services;

/// <summary>
/// Emits a grid of coloured cells as an SVG, one <c>&lt;rect&gt;</c> per <b>rectangle</b> of one colour rather
/// than one per cell: cells merge along a row into runs, and a row whose runs repeat the row above extends that
/// row's rects downward instead of restating them. Every terrain preview is a raster of block colours and the
/// ones that read best are exactly the ones with the most structure — a solid is one rect, a layer stack one per
/// layer, a wall run one per stripe — so the per-cell form spent kilobytes repeating a colour the picture says
/// once. The merges carry the same pixels, so only the payload changes.
/// </summary>
public static class SvgRaster
{
    /// <summary>A <paramref name="columns"/>×<paramref name="rows"/> grid at <paramref name="cell"/> pixels per
    /// cell. <paramref name="colorAt"/> answers the "#rrggbb" of a cell, or null where nothing is drawn — an
    /// undrawn cell emits no rect, so the surface behind the image (a card, the plan canvas) shows through.</summary>
    public static string Raster(int columns, int rows, int cell, Func<int, int, string?> colorAt)
    {
        var sb = new StringBuilder();
        int width = columns * cell, height = rows * cell;
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' ")
          .Append($"viewBox='0 0 {width} {height}' preserveAspectRatio='xMidYMid meet' shape-rendering='crispEdges'>");

        List<Run>? band = null;      // the runs the open band is drawn from
        var bandTop = 0;             // the row it starts at
        for (var row = 0; row <= rows; row++)
        {
            var runs = row < rows ? RunsOf(columns, column => colorAt(column, row)) : null;
            if (band is not null && runs is not null && Same(band, runs)) continue;
            if (band is not null) Flush(sb, band, bandTop, row, cell);
            band = runs;
            bandTop = row;
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>One horizontal run of a row: the columns [<see cref="From"/>, <see cref="To"/>) sharing a colour,
    /// or no colour at all where nothing is drawn.</summary>
    private readonly record struct Run(int From, int To, string? Color);

    private static List<Run> RunsOf(int columns, Func<int, string?> colorAt)
    {
        var runs = new List<Run>();
        var from = 0;
        string? color = null;
        for (var column = 0; column <= columns; column++)
        {
            var next = column < columns ? colorAt(column) : null;
            if (column < columns && next == color) continue;
            if (color is not null) runs.Add(new Run(from, column, color));
            from = column;
            color = next;
        }
        return runs;
    }

    private static bool Same(List<Run> a, List<Run> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static void Flush(StringBuilder sb, List<Run> runs, int top, int bottom, int cell)
    {
        foreach (var (from, to, color) in runs)
            sb.Append($"<rect x='{from * cell}' y='{top * cell}' width='{(to - from) * cell}' ")
              .Append($"height='{(bottom - top) * cell}' fill='{color}'/>");
    }
}
