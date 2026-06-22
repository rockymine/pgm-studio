namespace PgmStudio.Geom;

/// <summary>
/// Facing math in Minecraft's yaw convention: yaw 0 looks toward +Z (south) and increases clockwise
/// (90 → -X / west, 180 → -Z / north, 270 ≡ -90 → +X / east). Used to aim authored spawns — a team
/// spawn looks at the map middle, the observer looks at a team spawn.
/// </summary>
public static class Heading
{
    /// <summary>Yaw in degrees (range (-180, 180]) for an entity at (x,z) looking toward (tx,tz).
    /// Returns 0 when the target coincides with the source.</summary>
    public static double YawTo(double x, double z, double tx, double tz)
    {
        double dx = tx - x, dz = tz - z;
        if (dx == 0 && dz == 0) return 0;
        var deg = Math.Atan2(-dx, dz) * 180.0 / Math.PI;   // [-180, 180]
        return deg <= -180.0 ? deg + 360.0 : deg;          // collapse -180 → 180 for a stable (-180, 180]
    }
}
