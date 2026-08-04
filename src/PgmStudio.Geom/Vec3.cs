namespace PgmStudio.Geom;

/// <summary>
/// A point or direction in continuous world space. The dressing algorithms grow shapes in three dimensions
/// before anything is rounded to a block — a limb centerline, a leaf-cluster centre, a blob's offset from its
/// own middle — and rounding those to integers as they are built is what makes a smooth curve read as a
/// staircase. So the geometry stays continuous here and meets the grid once, at the point a volume is filled.
/// </summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 v, double k) => new(v.X * k, v.Y * k, v.Z * k);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>The same direction at unit length; a zero vector answers straight up, so a caller that has lost
    /// its heading grows upward rather than collapsing to a point.</summary>
    public Vec3 Normalized => Length is var len && len > 1e-9 ? this * (1 / len) : new Vec3(0, 1, 0);

    /// <summary>A direction from a yaw around the vertical axis and a pitch above the horizontal — the frame a
    /// growing limb steers in, where "lean toward vertical" is one term on one angle.</summary>
    public static Vec3 FromYawPitch(double yaw, double pitch)
        => new(Math.Cos(pitch) * Math.Cos(yaw), Math.Sin(pitch), Math.Cos(pitch) * Math.Sin(yaw));
}
