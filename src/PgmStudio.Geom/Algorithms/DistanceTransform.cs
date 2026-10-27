namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// The exact Euclidean distance from every cell of a dense grid to the nearest <em>source</em> cell, by the
/// separable parabola-envelope sweep (Felzenszwalb–Huttenlocher): one pass down each column, one along each
/// row, linear in the grid. A source cell is at distance 0; a grid with no source answers
/// <see cref="double.PositiveInfinity"/> everywhere.
/// </summary>
public static class DistanceTransform
{
    private const double Far = 1e20;

    /// <summary>The distance, in cells, from each cell of the <paramref name="width"/> by
    /// <paramref name="depth"/> grid to the nearest cell of <paramref name="source"/> (row-major, x along a
    /// row).</summary>
    public static double[] ToNearest(bool[] source, int width, int depth)
    {
        var squared = new double[width * depth];
        for (var cell = 0; cell < squared.Length; cell++) squared[cell] = source[cell] ? 0 : Far;

        var longest = Math.Max(width, depth);
        var line = new double[longest];
        var swept = new double[longest];
        var parabolaAt = new int[longest];
        var boundary = new double[longest + 1];

        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < depth; z++) line[z] = squared[z * width + x];
            Sweep(line, swept, parabolaAt, boundary, depth);
            for (var z = 0; z < depth; z++) squared[z * width + x] = swept[z];
        }
        for (var z = 0; z < depth; z++)
        {
            for (var x = 0; x < width; x++) line[x] = squared[z * width + x];
            Sweep(line, swept, parabolaAt, boundary, width);
            for (var x = 0; x < width; x++) squared[z * width + x] = swept[x];
        }

        for (var cell = 0; cell < squared.Length; cell++)
            squared[cell] = squared[cell] >= Far / 2 ? double.PositiveInfinity : Math.Sqrt(squared[cell]);
        return squared;
    }

    /// <summary>The lower envelope of the parabolas <c>(at - from)² + line[from]</c>, read back at each
    /// <c>at</c>.</summary>
    private static void Sweep(double[] line, double[] swept, int[] parabolaAt, double[] boundary, int length)
    {
        var top = 0;
        parabolaAt[0] = 0;
        boundary[0] = double.NegativeInfinity;
        boundary[1] = double.PositiveInfinity;
        for (var at = 1; at < length; at++)
        {
            double crossing;
            while (true)
            {
                var from = parabolaAt[top];
                crossing = (line[at] + (double)at * at - (line[from] + (double)from * from)) / (2.0 * (at - from));
                if (crossing > boundary[top]) break;
                top--;
            }
            top++;
            parabolaAt[top] = at;
            boundary[top] = crossing;
            boundary[top + 1] = double.PositiveInfinity;
        }
        top = 0;
        for (var at = 0; at < length; at++)
        {
            while (boundary[top + 1] < at) top++;
            var gap = at - parabolaAt[top];
            swept[at] = gap * (double)gap + line[parabolaAt[top]];
        }
    }
}
