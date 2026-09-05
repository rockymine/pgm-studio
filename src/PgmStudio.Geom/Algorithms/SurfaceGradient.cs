namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// How steeply a surface of whole-block heights is inclined at a cell — <b>the one formula</b>, so a paint
/// that picks a material by angle, a read-back that prints one, and a rule that measures how much of a board
/// is level cannot answer differently about the same ground.
///
/// <para>Horn's 3×3 gradient: the two central differences taken with the 1-2-1 weighting that reads the
/// diagonals at half the weight of the orthogonals, which gives the inclination of the plane that best fits
/// the nine tops rather than the sharpest single step. A ramp climbing one block a cell answers 45 exactly and
/// level ground answers 0.</para>
///
/// <para><b>The window is what the reading turns on</b>, because a gentle slope on ground quantised to whole
/// blocks is a staircase. Ground falling one block every four cells is four flat treads and a one-block riser,
/// and a window one cell wide reads each riser on its own — 27° on the riser, nothing on the treads. A
/// sustained slope reads its true angle at every window, so widening costs a real slope nothing; what it costs
/// is a face, whose drop is spread over more cells. <see cref="Window"/> is where that trade is settled.</para>
/// </summary>
public static class SurfaceGradient
{
    /// <summary>How far either side of a cell the gradient is read, in cells. <b>Two</b>, because at one an
    /// isolated one-block contour answers 27° and speckles level ground with whatever a mask paints there,
    /// and at three the lip of a six-block face answers 45° and can no longer be told from a walkable ramp.
    /// Two puts the contour at 14° — under any threshold an author would set — and the face at 56°.</summary>
    public const int Window = 2;

    /// <summary>Ground level enough to stand, build and fight on, in degrees. One decade, which is the unit
    /// the incline read prints in, and a rise of less than one block in five cells.</summary>
    public const int Level = 10;

    /// <summary>The inclination at one cell, in whole degrees from level, 0..89. <paramref name="topAt"/>
    /// answers the surface top at an offset from the cell, and answers the cell's own top where there is
    /// nothing there: the void is not a slope, and a coastline is level ground that stops.</summary>
    public static int Degrees(Func<int, int, int> topAt, int window = Window)
    {
        var step = Math.Max(1, window);
        int Top(int dx, int dz) => topAt(dx * step, dz * step);

        var alongX = Top(-1, -1) + 2 * Top(-1, 0) + Top(-1, 1) - Top(1, -1) - 2 * Top(1, 0) - Top(1, 1);
        var alongZ = Top(-1, -1) + 2 * Top(0, -1) + Top(1, -1) - Top(-1, 1) - 2 * Top(0, 1) - Top(1, 1);
        if (alongX == 0 && alongZ == 0) return 0;
        // The 1-2-1 weighting sums to 4 a side and the two sides are 2*step apart, so 8*step is what turns the
        // weighted difference back into blocks of rise per block of run.
        var rise = Math.Sqrt((double)alongX * alongX + (double)alongZ * alongZ) / (8.0 * step);
        return Math.Min(89, (int)Math.Round(Math.Atan(rise) * 180.0 / Math.PI));
    }

    /// <summary>The same reading over a surface map. A cell the surface does not carry reads as level with the
    /// cell asked about.</summary>
    public static int Degrees(IReadOnlyDictionary<(int X, int Z), int> surfaceTop, int x, int z,
                              int window = Window)
    {
        if (!surfaceTop.TryGetValue((x, z), out var self)) return 0;
        return Degrees((dx, dz) => surfaceTop.TryGetValue((x + dx, z + dz), out var top) ? top : self, window);
    }
}
