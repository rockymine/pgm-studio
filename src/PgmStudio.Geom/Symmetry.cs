namespace PgmStudio.Geom;

/// <summary>
/// The one canonical 2-D symmetry transform — reflect/rotate a point or an axis-aligned rectangle about a
/// centre, plus the mode→orbit helpers the authoring phases use. Lives in the dependency-free
/// <c>PgmStudio.Geom</c> leaf so the <b>same</b> formulas serve the WASM client (orbit previews +
/// island/point-aware assignment), the server generator, and the analysis symmetry detector — no
/// per-runtime C# copies. The JS <c>geometry/symmetry.js</c> mirrors these exactly (the live-canvas twin).
/// </summary>
public static class Symmetry
{
    /// <summary>Orbit order: <c>rot_90</c> ⇒ 4, any other known mode ⇒ 2, none/unknown ⇒ 1.</summary>
    public static int Order(string? mode) => string.IsNullOrEmpty(mode) ? 1 : mode == "rot_90" ? 4 : 2;

    /// <summary>Reflect a point across the plane through (ox,oz) with horizontal normal (nx,nz).</summary>
    public static (double X, double Z) ReflectPoint(double px, double pz, double nx, double nz, double ox, double oz)
    {
        var n2 = nx * nx + nz * nz;
        if (n2 == 0) return (px, pz);
        var d = 2.0 * ((px - ox) * nx + (pz - oz) * nz) / n2;
        return (px - nx * d, pz - nz * d);
    }

    /// <summary>Rotate a point CCW about (ox,oz); exact for 90° multiples.</summary>
    public static (double X, double Z) RotatePoint(double px, double pz, int degrees, double ox, double oz)
    {
        double dx = px - ox, dz = pz - oz;
        (double rx, double rz) = (((degrees % 360) + 360) % 360) switch
        {
            90 => (-dz, dx),
            180 => (-dx, -dz),
            270 => (dz, -dx),
            _ => (dx, dz),
        };
        return (ox + rx, oz + rz);
    }

    /// <summary>Transform a point by a single <b>concrete axis</b> about (cx,cz): the four mirrors and the
    /// three rotations (incl. <c>rot_270</c>, the third image of a <c>rot_90</c> orbit). This is the C#
    /// twin of JS <c>applySymmetry</c>; unknown axis ⇒ identity.</summary>
    public static (double X, double Z) Apply(double x, double z, string axis, double cx, double cz) => axis switch
    {
        "rot_90"    => RotatePoint(x, z, 90, cx, cz),
        "rot_180"   => RotatePoint(x, z, 180, cx, cz),
        "rot_270"   => RotatePoint(x, z, 270, cx, cz),
        "mirror_x"  => ReflectPoint(x, z, 1, 0, cx, cz),
        "mirror_z"  => ReflectPoint(x, z, 0, 1, cx, cz),
        "mirror_d1" => ReflectPoint(x, z, 1, -1, cx, cz),
        "mirror_d2" => ReflectPoint(x, z, 1, 1, cx, cz),
        _ => (x, z),
    };

    /// <summary>The <c>k</c>-th orbit image of a point under a named symmetry mode about (cx,cz).</summary>
    public static (double X, double Z) Point(double x, double z, string? mode, double cx, double cz, int k) => mode switch
    {
        "rot_90" => RotatePoint(x, z, 90 * k, cx, cz),
        "rot_180" => RotatePoint(x, z, 180, cx, cz),
        "mirror_x" => ReflectPoint(x, z, 1, 0, cx, cz),
        "mirror_z" => ReflectPoint(x, z, 0, 1, cx, cz),
        "mirror_d1" => ReflectPoint(x, z, 1, -1, cx, cz),
        "mirror_d2" => ReflectPoint(x, z, 1, 1, cx, cz),
        _ => (x, z),
    };

    /// <summary>The <c>k</c>-th orbit image of an axis-aligned rectangle: transform the four corners and
    /// re-bound (a rotation becomes a new AABB). Raw — the caller rounds.</summary>
    public static (double MinX, double MinZ, double MaxX, double MaxZ) Rect(
        double minX, double minZ, double maxX, double maxZ, string? mode, double cx, double cz, int k)
    {
        (double x, double z)[] corners =
        [
            Point(minX, minZ, mode, cx, cz, k), Point(minX, maxZ, mode, cx, cz, k),
            Point(maxX, minZ, mode, cx, cz, k), Point(maxX, maxZ, mode, cx, cz, k),
        ];
        double nx = double.MaxValue, nz = double.MaxValue, xx = double.MinValue, xz = double.MinValue;
        foreach (var (x, z) in corners)
        {
            nx = Math.Min(nx, x); nz = Math.Min(nz, z);
            xx = Math.Max(xx, x); xz = Math.Max(xz, z);
        }
        return (nx, nz, xx, xz);
    }
}
