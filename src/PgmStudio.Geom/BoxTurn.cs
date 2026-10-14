namespace PgmStudio.Geom;

/// <summary>
/// A way of laying a box-local shape back onto its own box: left as it is, flipped side to side or top to bottom,
/// transposed, or given a quarter turn either way. Box-local coordinates run <c>x</c> rightward and <c>z</c>
/// downward from the box's top-left corner, so <see cref="Clockwise"/> takes the top edge to the right one. A
/// transposing turn swaps the box's width and height (<see cref="BoxTurns.Extent"/>).
/// </summary>
public enum BoxTurn
{
    /// <summary>The shape as it is.</summary>
    None,
    /// <summary>Left and right swapped: <c>(x, z) → (W − x, z)</c>.</summary>
    FlipX,
    /// <summary>Top and bottom swapped: <c>(x, z) → (x, H − z)</c>.</summary>
    FlipZ,
    /// <summary>Mirrored across the top-left diagonal: <c>(x, z) → (z, x)</c>.</summary>
    Transpose,
    /// <summary>A quarter turn taking the top edge to the right: <c>(x, z) → (H − z, x)</c>.</summary>
    Clockwise,
    /// <summary>A quarter turn taking the top edge to the left: <c>(x, z) → (z, W − x)</c>.</summary>
    Anticlockwise,
}

/// <summary>What a <see cref="BoxTurn"/> does to a point, a rectangle of cells, a direction and the box itself,
/// for a box <c>width</c> × <c>height</c> before the turn.</summary>
public static class BoxTurns
{
    /// <summary>Whether the turn swaps the box's width and height.</summary>
    public static bool Transposes(this BoxTurn turn) =>
        turn is BoxTurn.Transpose or BoxTurn.Clockwise or BoxTurn.Anticlockwise;

    /// <summary>The box's extent after the turn.</summary>
    public static (int Width, int Height) Extent(this BoxTurn turn, int width, int height) =>
        turn.Transposes() ? (height, width) : (width, height);

    /// <summary>A box-local position after the turn.</summary>
    public static (double X, double Z) Point(this BoxTurn turn, double x, double z, int width, int height) => turn switch
    {
        BoxTurn.FlipX => (width - x, z),
        BoxTurn.FlipZ => (x, height - z),
        BoxTurn.Transpose => (z, x),
        BoxTurn.Clockwise => (height - z, x),
        BoxTurn.Anticlockwise => (z, width - x),
        _ => (x, z),
    };

    /// <summary>A rectangle of cells after the turn: its two corners turned and re-bounded.</summary>
    public static CellRect Rect(this BoxTurn turn, CellRect rect, int width, int height)
    {
        var (x1, z1) = turn.Point(rect.X, rect.Z, width, height);
        var (x2, z2) = turn.Point(rect.MaxX, rect.MaxZ, width, height);
        int minX = (int)Math.Min(x1, x2), minZ = (int)Math.Min(z1, z2);
        return new CellRect(minX, minZ, (int)Math.Max(x1, x2) - minX, (int)Math.Max(z1, z2) - minZ);
    }

    /// <summary>A direction after the turn — the turn's linear part, which a box's size does not enter.</summary>
    public static (int Dx, int Dz) Direction(this BoxTurn turn, int dx, int dz) => turn switch
    {
        BoxTurn.FlipX => (-dx, dz),
        BoxTurn.FlipZ => (dx, -dz),
        BoxTurn.Transpose => (dz, dx),
        BoxTurn.Clockwise => (-dz, dx),
        BoxTurn.Anticlockwise => (dz, -dx),
        _ => (dx, dz),
    };
}
