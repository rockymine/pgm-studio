namespace PgmStudio.Geom;

/// <summary>
/// The 1.8 sprint jump, a tick at a time: each tick the player moves by its velocity, then the vertical speed
/// falls by gravity and drag and the horizontal speed takes the air's push and drag. What a walk asks of it is
/// one number per rise — how far a jump carries before the feet come back down to that height — and whether a
/// gap fits inside it once the player's own width is taken off.
/// </summary>
public static class Jump
{
    private const double Gravity = 0.08;
    private const double VerticalDrag = 0.98;
    private const double AirDrag = 0.91;
    private const double SprintSpeed = 0.48;
    private const double JumpSpeed = 0.42;
    private const double AirPush = 0.026;

    /// <summary>A player's width, in blocks: a gap is cleared when it is shorter than the reach less this.</summary>
    public const double PlayerWidth = 0.6;

    /// <summary>The widest gap a jump is tried over, in blocks, edge to edge. A level jump clears 3.85 and a
    /// jump down a block 4.52, so a jump down more than that is still held to this.</summary>
    public const int MaxGap = 4;

    /// <summary>How much open air a jump needs over its take-off, in blocks: the feet rise a block and a quarter
    /// and the player is 1.8 tall.</summary>
    public const int Headroom = 3;

    /// <summary>The lowest rise the table holds; a jump down further reaches as far as one down this much.</summary>
    private const int DeepestRise = -64;

    private static readonly double[] Reaches =
        [.. Enumerable.Range(DeepestRise, 1 - DeepestRise + 1).Select(rise => Simulate(rise))];

    /// <summary>The horizontal distance a sprint jump carries before the feet come back down to
    /// <paramref name="rise"/> blocks over the take-off (negative for lower), or zero for a rise it never
    /// reaches.</summary>
    public static double Reach(int rise) => rise > 1 ? 0 : Reaches[Math.Max(rise, DeepestRise) - DeepestRise];

    /// <summary>Whether a gap of <paramref name="gap"/> blocks, edge to edge, is cleared by a jump landing
    /// <paramref name="rise"/> blocks over the take-off.</summary>
    public static bool Clears(double gap, int rise) => gap <= Reach(rise) - PlayerWidth;

    private static double Simulate(int rise)
    {
        double x = 0, y = 0, top = 0, best = 0;
        double horizontal = SprintSpeed, vertical = JumpSpeed;
        for (var tick = 0; tick < 400; tick++)
        {
            x += horizontal;
            y += vertical;
            top = Math.Max(top, y);
            vertical = (vertical - Gravity) * VerticalDrag;
            horizontal = (horizontal + AirPush) * AirDrag;
            if (vertical < 0 && y < rise) return top >= rise ? best : 0;
            best = x;
        }
        return best;
    }
}
