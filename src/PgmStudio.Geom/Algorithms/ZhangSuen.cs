namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Zhang–Suen thinning: erode a cell mask to a 1-cell-wide skeleton, preserving connectivity and endpoints.
/// Pure raster math (z increases downward, "north" = z−1). The tracing that reads the resulting skeleton
/// into a lane graph (endpoints / junctions / edges) lives in <see cref="Skeleton"/>.
/// </summary>
public static class ZhangSuen
{
    /// <summary>Thin the set of solid cells to a 1-cell-wide skeleton.</summary>
    public static HashSet<(int X, int Z)> Thin(IEnumerable<(int X, int Z)> cells)
    {
        var set = new HashSet<(int, int)>(cells);
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (var step = 0; step < 2; step++)
            {
                var remove = new List<(int, int)>();
                foreach (var (x, z) in set)
                {
                    int p2 = In(set, x, z - 1), p3 = In(set, x + 1, z - 1), p4 = In(set, x + 1, z),
                        p5 = In(set, x + 1, z + 1), p6 = In(set, x, z + 1), p7 = In(set, x - 1, z + 1),
                        p8 = In(set, x - 1, z), p9 = In(set, x - 1, z - 1);
                    var b = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9;
                    if (b is < 2 or > 6) continue;
                    int[] seq = [p2, p3, p4, p5, p6, p7, p8, p9, p2];
                    var a = 0;
                    for (var i = 0; i < 8; i++) if (seq[i] == 0 && seq[i + 1] == 1) a++;
                    if (a != 1) continue;
                    if (step == 0) { if (p2 * p4 * p6 != 0 || p4 * p6 * p8 != 0) continue; }
                    else { if (p2 * p4 * p8 != 0 || p2 * p6 * p8 != 0) continue; }
                    remove.Add((x, z));
                }
                if (remove.Count > 0) { changed = true; foreach (var p in remove) set.Remove(p); }
            }
        }
        return set;
    }

    private static int In(HashSet<(int, int)> s, int x, int z) => s.Contains((x, z)) ? 1 : 0;
}
