using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;

namespace PgmStudio.Analysis;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Resolves a region dict (xml_data.json shape) to a 2D footprint geometry — a NetTopologySuite
/// port of region_encoder._dict_to_shapely. Circles use a 32-segments-per-quadrant buffer (shapely
/// resolution=32); compounds use boolean union/difference/intersection; mirror/translate are affine.
/// </summary>
public static class RegionGeometry2d
{
    private static readonly GeometryFactory Gf = new();
    private const int QuadrantSegments = 32;

    public static Geometry? ToGeometry(Dict? region, (double minX, double minZ, double maxX, double maxZ) bounds, Dict registry)
    {
        if (region is null) return null;
        var t = region.GetValueOrDefault("type") as string;

        switch (t)
        {
            case "rectangle" or "cuboid":
            {
                var mn = NonEmpty(AsDict(region.GetValueOrDefault("min")), AsDict(AsDict(region.GetValueOrDefault("bounds_2d")).GetValueOrDefault("min")));
                var mx = NonEmpty(AsDict(region.GetValueOrDefault("max")), AsDict(AsDict(region.GetValueOrDefault("bounds_2d")).GetValueOrDefault("max")));
                if (Num(mn.GetValueOrDefault("x")) is not { } mnx || Num(mn.GetValueOrDefault("z")) is not { } mnz
                    || Num(mx.GetValueOrDefault("x")) is not { } mxx || Num(mx.GetValueOrDefault("z")) is not { } mxz)
                    return null;
                return Box(Math.Min(mnx, mxx), Math.Min(mnz, mxz), Math.Max(mnx, mxx), Math.Max(mnz, mxz));
            }
            case "cylinder": return Disc(AsDict(region.GetValueOrDefault("base")), region);
            case "circle": return Disc(AsDict(region.GetValueOrDefault("center")), region);
            case "sphere": return Disc(AsDict(region.GetValueOrDefault("origin")), region);
            case "block":
            {
                var p = AsDict(region.GetValueOrDefault("position"));
                var x = Num(p.GetValueOrDefault("x")) ?? 0; var z = Num(p.GetValueOrDefault("z")) ?? 0;
                return Box(x, z, x + 1, z + 1);
            }
            case "point":
            {
                var p = AsDict(region.GetValueOrDefault("position"));
                var x = Num(p.GetValueOrDefault("x")) ?? 0; var z = Num(p.GetValueOrDefault("z")) ?? 0;
                return Box(x - 0.5, z - 0.5, x + 0.5, z + 0.5);
            }
            case "half":
            {
                var o = AsDict(region.GetValueOrDefault("origin"));
                var n = AsDict(region.GetValueOrDefault("normal"));
                return HalfPlane(Num(o.GetValueOrDefault("x")) ?? 0, Num(o.GetValueOrDefault("z")) ?? 0,
                                 Num(n.GetValueOrDefault("x")) ?? 0, Num(n.GetValueOrDefault("z")) ?? 0, bounds);
            }
            case "union" or "complement" or "intersect" or "negative":
                return Compound(t, region, bounds, registry);
            case "mirror":
            {
                var src = ToGeometry(registry.GetValueOrDefault(region.GetValueOrDefault("source_id") as string ?? "") as Dict, bounds, registry);
                if (src is null || src.IsEmpty) return null;
                var o = AsDict(region.GetValueOrDefault("origin"));
                var n = AsDict(region.GetValueOrDefault("normal"));
                return Reflect(src, Num(n.GetValueOrDefault("x")) ?? 0, Num(n.GetValueOrDefault("z")) ?? 0,
                               Num(o.GetValueOrDefault("x")) ?? 0, Num(o.GetValueOrDefault("z")) ?? 0);
            }
            case "translate":
            {
                var src = ToGeometry(registry.GetValueOrDefault(region.GetValueOrDefault("source_id") as string ?? "") as Dict, bounds, registry);
                if (src is null || src.IsEmpty) return null;
                var off = AsDict(region.GetValueOrDefault("offset"));
                return AffineTransformation.TranslationInstance(Num(off.GetValueOrDefault("x")) ?? 0, Num(off.GetValueOrDefault("z")) ?? 0).Transform(src);
            }
            case "reference":
            {
                var refId = region.GetValueOrDefault("ref_id") as string;
                return refId is not null && registry.GetValueOrDefault(refId) is Dict r ? ToGeometry(r, bounds, registry) : null;
            }
            default: return null;
        }
    }

    private static Geometry? Disc(Dict center, Dict region)
    {
        if (Num(region.GetValueOrDefault("radius")) is not { } r || r <= 0) return null;
        var cx = Num(center.GetValueOrDefault("x")) ?? 0;
        var cz = Num(center.GetValueOrDefault("z")) ?? 0;
        return Gf.CreatePoint(new Coordinate(cx, cz)).Buffer(r, QuadrantSegments);
    }

    private static Geometry? Compound(string t, Dict region, (double, double, double, double) bounds, Dict registry)
    {
        var children = AsList(region.GetValueOrDefault("children"));
        var geoms = children.Select(c => ToGeometry(ResolveRef(c, registry), bounds, registry)).ToList();

        switch (t)
        {
            case "union":
            {
                var valid = geoms.Where(g => g is not null && !g.IsEmpty).ToArray();
                return valid.Length == 0 ? null : UnaryUnion(valid);
            }
            case "complement":
            {
                if (geoms.Count == 0 || geoms[0] is not { IsEmpty: false } baseG) return null;
                var rest = geoms.Skip(1).Where(g => g is not null && !g.IsEmpty).ToArray();
                var result = rest.Length > 0 ? baseG.Difference(UnaryUnion(rest)!) : baseG;
                return result.IsEmpty ? null : result;
            }
            case "intersect":
            {
                if (geoms.Count == 0 || geoms[0] is null) return null;
                var result = geoms[0]!;
                foreach (var g in geoms.Skip(1)) if (g is not null && !g.IsEmpty) result = result.Intersection(g);
                return result.IsEmpty ? null : result;
            }
            default: // negative
            {
                var (minX, minZ, maxX, maxZ) = bounds;
                var mapBox = Box(minX, minZ, maxX, maxZ);
                var valid = geoms.Where(g => g is not null && !g.IsEmpty).ToArray();
                var result = valid.Length > 0 ? mapBox.Difference(UnaryUnion(valid)!) : mapBox;
                return result.IsEmpty ? null : result;
            }
        }
    }

    private static Geometry? HalfPlane(double ox, double oz, double nx, double nz, (double minX, double minZ, double maxX, double maxZ) b)
    {
        if (nx == 0 && nz == 0) return null;
        var poly = new[] { (b.minX, b.minZ), (b.maxX, b.minZ), (b.maxX, b.maxZ), (b.minX, b.maxZ) };
        double Dist(double x, double z) => nx * (x - ox) + nz * (z - oz);
        (double, double) Cross((double x, double z) p1, (double x, double z) p2)
        {
            double d1 = Dist(p1.x, p1.z), d2 = Dist(p2.x, p2.z);
            if (Math.Abs(d1 - d2) < 1e-10) return p1;
            var s = d1 / (d1 - d2);
            return (p1.x + s * (p2.x - p1.x), p1.z + s * (p2.z - p1.z));
        }
        var outPts = new List<(double x, double z)>();
        var n = poly.Length;
        for (var i = 0; i < n; i++)
        {
            var curr = poly[i];
            var prev = poly[(i - 1 + n) % n];
            var currIn = Dist(curr.Item1, curr.Item2) >= 0;
            var prevIn = Dist(prev.Item1, prev.Item2) >= 0;
            if (currIn) { if (!prevIn) outPts.Add(Cross(prev, curr)); outPts.Add(curr); }
            else if (prevIn) outPts.Add(Cross(prev, curr));
        }
        if (outPts.Count < 3) return null;
        var ring = outPts.Select(p => new Coordinate(p.x, p.z)).ToList();
        ring.Add(ring[0]);
        return Gf.CreatePolygon(ring.ToArray());
    }

    private static Geometry Reflect(Geometry geom, double nx, double nz, double ox, double oz)
    {
        var n2 = nx * nx + nz * nz;
        if (n2 == 0) return geom;
        double r00 = 1 - 2 * nx * nx / n2, r01 = -2 * nx * nz / n2, r11 = 1 - 2 * nz * nz / n2;
        // AffineTransformation(m00,m01,m02, m10,m11,m12): x'=m00*x+m01*z+m02, z'=m10*x+m11*z+m12
        var at = new AffineTransformation(r00, r01, ox - r00 * ox - r01 * oz, r01, r11, oz - r01 * ox - r11 * oz);
        return at.Transform(geom);
    }

    private static Polygon Box(double minX, double minZ, double maxX, double maxZ)
        => (Polygon)Gf.ToGeometry(new Envelope(minX, maxX, minZ, maxZ));

    private static Geometry? UnaryUnion(IReadOnlyCollection<Geometry?> geoms)
        => geoms.Count == 0 ? null : Gf.BuildGeometry(geoms.Where(g => g is not null).Cast<Geometry>().ToList()).Union();

    private static Dict? ResolveRef(object? c, Dict registry) => c switch
    {
        Dict d => d,
        string s => registry.GetValueOrDefault(s) as Dict,
        _ => null,
    };

    private static Dict AsDict(object? o) => o as Dict ?? new Dict();
    private static List<object?> AsList(object? o) => o as List<object?> ?? [];
    private static Dict NonEmpty(Dict a, Dict b) => a.Count > 0 ? a : b;
    private static double? Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => null };
}
