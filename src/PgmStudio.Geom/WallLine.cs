namespace PgmStudio.Geom;

/// <summary>
/// The columns a wall laid along a line of corners stands in: every block whose centre lies within half the
/// wall's <see cref="Thickness"/> of one of its legs, square at both ends, and the four blocks round each bend so
/// the band turns without a notch. The corners are block corners, so a leg along a grid line fills the two columns
/// either side of it, exactly as a box two blocks thick does, and a leg at any other angle fills the band the same
/// distance either side of it.
/// </summary>
public static class WallLine
{
    /// <summary>How thick a wall is, across its line, in blocks.</summary>
    public const int Thickness = 2;

    // A column whose centre lies on an end or an edge of the band to within rounding is decided as if exactly.
    private const double Tolerance = 1e-9;

    /// <summary>The columns of the wall through <paramref name="corners"/>, each once; none for a wall whose
    /// corners are all one point.</summary>
    public static IEnumerable<(int X, int Z)> Cells(IReadOnlyList<(int X, int Z)> corners)
    {
        var seen = new HashSet<(int X, int Z)>();
        for (var i = 1; i < corners.Count; i++)
            foreach (var cell in Leg(corners[i - 1], corners[i]))
                if (seen.Add(cell)) yield return cell;
        for (var i = 1; i < corners.Count - 1; i++)
        {
            var (x, z) = corners[i];
            foreach (var cell in new[] { (x - 1, z - 1), (x, z - 1), (x - 1, z), (x, z) })
                if (seen.Add(cell)) yield return cell;
        }
    }

    /// <summary>The block rectangle the wall's columns span, max-exclusive; empty at its first corner where it has
    /// none.</summary>
    public static BlockRect Bounds(IReadOnlyList<(int X, int Z)> corners)
    {
        var cells = Cells(corners).ToList();
        return cells.Count == 0 ? new BlockRect(corners[0].X, corners[0].Z, corners[0].X, corners[0].Z) : BlockRect.Around(cells);
    }

    private static IEnumerable<(int X, int Z)> Leg((int X, int Z) from, (int X, int Z) to)
    {
        double dx = to.X - from.X, dz = to.Z - from.Z;
        var length = Math.Sqrt(dx * dx + dz * dz);
        if (length == 0) yield break;
        var half = Thickness / 2.0;
        for (var x = Math.Min(from.X, to.X) - Thickness; x < Math.Max(from.X, to.X) + Thickness; x++)
        for (var z = Math.Min(from.Z, to.Z) - Thickness; z < Math.Max(from.Z, to.Z) + Thickness; z++)
        {
            double cx = x + 0.5 - from.X, cz = z + 0.5 - from.Z;
            var along = (cx * dx + cz * dz) / length;
            var across = Math.Abs(cx * dz - cz * dx) / length;
            if (along > Tolerance && along < length - Tolerance && across <= half + Tolerance) yield return (x, z);
        }
    }
}
