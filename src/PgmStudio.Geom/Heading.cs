namespace PgmStudio.Geom;

/// <summary>
/// Facing math in Minecraft's convention, the one PGM's spawn <c>yaw</c> and <c>pitch</c> are written in: yaw
/// 0 looks toward +Z (south) and turns clockwise, 90 west, 180 north and -90 east, always within (-180, 180];
/// pitch is degrees below the horizon, 90 straight down and -90 straight up.
/// </summary>
public static class Heading
{
    /// <summary>Any yaw in degrees brought into the game's (-180, 180].</summary>
    public static double Wrap(double yaw)
    {
        var turned = ((yaw % 360.0) + 360.0) % 360.0;       // [0, 360)
        return turned > 180.0 ? turned - 360.0 : turned;
    }

    /// <summary>Yaw in degrees (range (-180, 180]) for an entity at (x,z) looking toward (tx,tz).
    /// Returns 0 when the target coincides with the source.</summary>
    public static double YawTo(double x, double z, double tx, double tz)
    {
        double dx = tx - x, dz = tz - z;
        if (dx == 0 && dz == 0) return 0;
        return Wrap(Math.Atan2(-dx, dz) * 180.0 / Math.PI);
    }

    /// <summary>Pitch in degrees (range [-90, 90], positive down) for an eye at (x,y,z) looking toward
    /// (tx,ty,tz). Returns 0 when the target coincides with the eye.</summary>
    public static double PitchTo(double x, double y, double z, double tx, double ty, double tz)
    {
        double dx = tx - x, dy = ty - y, dz = tz - z;
        if (dx == 0 && dy == 0 && dz == 0) return 0;
        return -Math.Atan2(dy, Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI;
    }
}
