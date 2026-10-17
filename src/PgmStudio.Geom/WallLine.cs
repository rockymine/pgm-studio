namespace PgmStudio.Geom;

/// <summary>
/// The columns a wall laid along a line stands in: every block whose centre lies within half the wall's
/// <see cref="Thickness"/> of the line between its two ends, square at both ends. The ends are block corners,
/// so a line along a grid line fills the two columns either side of it, exactly as a box two blocks thick
/// does, and a line at any other angle fills the band the same distance either side of it.
/// </summary>
public static class WallLine
{
    /// <summary>How thick a wall is, across its line, in blocks.</summary>
    public const int Thickness = 2;

    // A column whose centre lies on an end or an edge of the band to within rounding is decided as if exactly.
    private const double Tolerance = 1e-9;

    /// <summary>The columns of the wall from (<paramref name="x1"/>, <paramref name="z1"/>) to
    /// (<paramref name="x2"/>, <paramref name="z2"/>); none for a wall whose two ends are one point.</summary>
    public static IEnumerable<(int X, int Z)> Cells(int x1, int z1, int x2, int z2)
    {
        double dx = x2 - x1, dz = z2 - z1;
        var length = Math.Sqrt(dx * dx + dz * dz);
        if (length == 0) yield break;
        var half = Thickness / 2.0;
        for (var x = Math.Min(x1, x2) - Thickness; x < Math.Max(x1, x2) + Thickness; x++)
        for (var z = Math.Min(z1, z2) - Thickness; z < Math.Max(z1, z2) + Thickness; z++)
        {
            double cx = x + 0.5 - x1, cz = z + 0.5 - z1;
            var along = (cx * dx + cz * dz) / length;
            var across = Math.Abs(cx * dz - cz * dx) / length;
            if (along > Tolerance && along < length - Tolerance && across <= half + Tolerance) yield return (x, z);
        }
    }

    /// <summary>The block rectangle the wall's columns span, max-exclusive; empty where it has none.</summary>
    public static BlockRect Bounds(int x1, int z1, int x2, int z2)
    {
        var cells = Cells(x1, z1, x2, z2).ToList();
        return cells.Count == 0 ? new BlockRect(x1, z1, x1, z1) : BlockRect.Around(cells);
    }
}
