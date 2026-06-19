namespace PgmStudio.Pgm;

using Dict = Dictionary<string, object?>;

/// <summary>
/// 2D bounds reflect + rotate about an origin (doc-dict <c>bounds_2d</c> of shape <c>{min:{x,z},
/// max:{x,z}}</c>) for the symmetry-authoring counterpart baker. The point primitives are the canonical
/// <see cref="PgmStudio.Contracts.Symmetry"/> ones — shared with the WASM client so there's a single
/// formula source. (Remaining server geometry sites still to fold in — see TODO A4.)
/// </summary>
public static class Geometry2d
{
    /// <summary>Reflect a point across the plane through (ox,oz) with horizontal normal (nx,nz).</summary>
    public static (double x, double z) ReflectPoint(double px, double pz, double nx, double nz, double ox, double oz)
        => Contracts.Symmetry.ReflectPoint(px, pz, nx, nz, ox, oz);

    /// <summary>Rotate a point CCW about (ox,oz); exact for 90° multiples.</summary>
    public static (double x, double z) RotatePoint(double px, double pz, int degrees, double ox, double oz)
        => Contracts.Symmetry.RotatePoint(px, pz, degrees, ox, oz);

    /// <summary>Reflect a bounds_2d AABB across a mirror plane and re-bound (all four corners).</summary>
    public static Dict ReflectBounds2d(Dict bounds, double nx, double nz, double ox, double oz)
        => CornersToBounds(bounds, (x, z) => ReflectPoint(x, z, nx, nz, ox, oz));

    /// <summary>Rotate a bounds_2d AABB about (ox,oz) and re-bound (all four corners).</summary>
    public static Dict RotateBounds2d(Dict bounds, int degrees, double ox, double oz)
        => CornersToBounds(bounds, (x, z) => RotatePoint(x, z, degrees, ox, oz));

    public static Dict Bounds(double minX, double minZ, double maxX, double maxZ) => new()
    {
        ["min"] = new Dict { ["x"] = Math.Min(minX, maxX), ["z"] = Math.Min(minZ, maxZ) },
        ["max"] = new Dict { ["x"] = Math.Max(minX, maxX), ["z"] = Math.Max(minZ, maxZ) },
    };

    private static Dict CornersToBounds(Dict bounds, Func<double, double, (double, double)> tf)
    {
        var mn = bounds["min"] as Dict ?? new Dict();
        var mx = bounds["max"] as Dict ?? new Dict();
        double minX = Num(mn.GetValueOrDefault("x")), minZ = Num(mn.GetValueOrDefault("z")),
               maxX = Num(mx.GetValueOrDefault("x")), maxZ = Num(mx.GetValueOrDefault("z"));
        var corners = new[] { tf(minX, minZ), tf(minX, maxZ), tf(maxX, minZ), tf(maxX, maxZ) };
        return Bounds(corners.Min(c => c.Item1), corners.Min(c => c.Item2), corners.Max(c => c.Item1), corners.Max(c => c.Item2));
    }

    private static double Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => 0 };
}
