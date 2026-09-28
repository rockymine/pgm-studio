namespace PgmStudio.Minecraft.Painting;

/// <summary>A layer's ground averaged round each column — the datum a following height stack measures its bands
/// from (TP26, docs/world-export/terrain-painting.md §3.1).
///
/// <para>The mean is taken over the footprint cells of a square window <c>reach</c> cells either side, so a
/// coast averages only the land it has: the void beside it is not ground at y 0. One summed-area table over the
/// footprint answers every reach at the same cost, which is what lets each stack state its own. It is built on
/// the first question, because most boards never follow the ground and a table nobody reads is a table nobody
/// should pay for.</para></summary>
public sealed class SmoothedGround
{
    private readonly Lazy<Table> table;

    public SmoothedGround(IReadOnlyDictionary<(int X, int Z), int> surfaceTop)
        => table = new Lazy<Table>(() => Table.Of(surfaceTop));

    /// <summary>The mean surface top of the footprint cells within <paramref name="reach"/> cells of
    /// <c>(x, z)</c> in both directions, rounded to a whole course — or null where the window holds no ground
    /// at all.</summary>
    public int? Height(int x, int z, int reach) => table.Value.Mean(x, z, Math.Max(0, reach));

    /// <summary>Running totals of the surface tops and of the footprint cells, one row and one column wider than
    /// the footprint's bounding box so a window's sum is four lookups however large it is.</summary>
    private sealed class Table(int minX, int minZ, int width, int depth, long[] tops, int[] cells)
    {
        public static Table Of(IReadOnlyDictionary<(int X, int Z), int> surfaceTop)
        {
            if (surfaceTop.Count == 0) return new Table(0, 0, 0, 0, [0], [0]);
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var (cell, _) in surfaceTop)
            {
                minX = Math.Min(minX, cell.X); maxX = Math.Max(maxX, cell.X);
                minZ = Math.Min(minZ, cell.Z); maxZ = Math.Max(maxZ, cell.Z);
            }
            int width = maxX - minX + 1, depth = maxZ - minZ + 1, stride = depth + 1;
            var tops = new long[(width + 1) * stride];
            var cells = new int[(width + 1) * stride];
            for (var i = 1; i <= width; i++)
                for (var j = 1; j <= depth; j++)
                {
                    var on = surfaceTop.TryGetValue((minX + i - 1, minZ + j - 1), out var top);
                    var at = i * stride + j;
                    tops[at] = (on ? top : 0) + tops[at - stride] + tops[at - 1] - tops[at - stride - 1];
                    cells[at] = (on ? 1 : 0) + cells[at - stride] + cells[at - 1] - cells[at - stride - 1];
                }
            return new Table(minX, minZ, width, depth, tops, cells);
        }

        public int? Mean(int x, int z, int reach)
        {
            int stride = depth + 1;
            int i0 = Math.Clamp(x - minX - reach, 0, width), i1 = Math.Clamp(x - minX + reach + 1, 0, width);
            int j0 = Math.Clamp(z - minZ - reach, 0, depth), j1 = Math.Clamp(z - minZ + reach + 1, 0, depth);
            if (i1 <= i0 || j1 <= j0) return null;
            var count = cells[i1 * stride + j1] - cells[i0 * stride + j1] - cells[i1 * stride + j0] + cells[i0 * stride + j0];
            if (count == 0) return null;
            var total = tops[i1 * stride + j1] - tops[i0 * stride + j1] - tops[i1 * stride + j0] + tops[i0 * stride + j0];
            return (int)Math.Round((double)total / count, MidpointRounding.AwayFromZero);
        }
    }
}
