namespace PgmStudio.Minecraft;

/// <summary>
/// Coordinate constraints for anchoring structures on authored positions
/// (docs/contracts/sketch-world-export.md §5): X/Z snap to whole integers (the 2×2 cube centre needs a
/// whole-integer midpoint), Y snaps to the terrain column top (<c>ymax</c>), and a spawn yaw maps to the
/// wall its door faces (the player spawns facing out through the door).
/// </summary>
public static class PositionSnap
{
    /// <summary>Round X/Z to whole integers (halves away from zero, so the result is deterministic — .NET's
    /// default <see cref="Math.Round(double)"/> is banker's rounding).</summary>
    public static (int X, int Z) SnapXZ(double x, double z)
        => ((int)Math.Round(x, MidpointRounding.AwayFromZero), (int)Math.Round(z, MidpointRounding.AwayFromZero));

    /// <summary>The surface top at <paramref name="cell"/> (the first air Y — where a structure floor rests),
    /// or <paramref name="fallback"/> when the cell has no terrain column.</summary>
    public static int SurfaceY((int X, int Z) cell, IReadOnlyDictionary<(int X, int Z), int> surfaceTop, int fallback)
        => surfaceTop.GetValueOrDefault(cell, fallback);

    /// <summary>The wall a spawn-cube door faces, from a Minecraft yaw (0 = +Z / south, 90 = −X / west,
    /// 180 = −Z / north, 270 = +X / east). The door faces the direction the player looks out.</summary>
    public static Facing FacingFromYaw(double yaw)
    {
        var y = ((yaw % 360) + 360) % 360;
        return y switch
        {
            >= 45 and < 135 => Facing.NegX,
            >= 135 and < 225 => Facing.NegZ,
            >= 225 and < 315 => Facing.PosX,
            _ => Facing.PosZ,
        };
    }
}
