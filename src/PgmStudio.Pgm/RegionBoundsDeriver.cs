using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm;

/// <summary>
/// Fills in the derived <see cref="Region.Bounds2d"/> for compound/transform regions when
/// reconstructing a map from storage. The XML parser computes these at parse time
/// (<c>RegionParser</c>), but a DB round-trip only persists primitive bounds — so after rebuilding
/// the registry we recompute union/negative/complement/intersect (union of children),
/// mirror (reflect the source AABB) and translate (offset the source AABB). Same math as the parser.
/// </summary>
public static class RegionBoundsDeriver
{
    public static void Derive(IReadOnlyDictionary<string, Region> registry)
    {
        // Compounds nest, so iterate to a fixpoint (children resolved before their parents).
        for (var pass = 0; pass < 64; pass++)
        {
            var changed = false;
            foreach (var region in registry.Values)
            {
                if (region.Bounds2d is not null) continue;
                var b = region.Type switch
                {
                    "union" or "negative" or "complement" or "intersect" => UnionBounds(region.Children, registry),
                    "mirror" => MirrorBounds(region, registry),
                    "translate" => TranslateBounds(region, registry),
                    _ => null,
                };
                if (b is not null) { region.Bounds2d = b; changed = true; }
            }
            if (!changed) break;
        }
    }

    private static Bounds2d? UnionBounds(List<string>? childIds, IReadOnlyDictionary<string, Region> registry)
    {
        if (childIds is null) return null;
        double minX = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        var found = false;
        foreach (var cid in childIds)
            if (registry.GetValueOrDefault(cid)?.Bounds2d is { } b)
            {
                // NaN-tolerant like Python's min/max (a mirror of an oo/-oo source yields NaN).
                minX = MinPy(minX, b.MinX); minZ = MinPy(minZ, b.MinZ);
                maxX = MaxPy(maxX, b.MaxX); maxZ = MaxPy(maxZ, b.MaxZ);
                found = true;
            }
        return found ? Bounds2d.Of(minX, minZ, maxX, maxZ) : null;
    }

    private static Bounds2d? MirrorBounds(Region m, IReadOnlyDictionary<string, Region> registry)
    {
        if (m.SourceId is not { } sid || registry.GetValueOrDefault(sid)?.Bounds2d is not { } b) return null;
        double nx = m.NormalX ?? 0, nz = m.NormalZ ?? 0, ox = m.OriginX ?? 0, oz = m.OriginZ ?? 0;
        var c = new[]
        {
            Symmetry.ReflectPoint(b.MinX, b.MinZ, nx, nz, ox, oz),
            Symmetry.ReflectPoint(b.MinX, b.MaxZ, nx, nz, ox, oz),
            Symmetry.ReflectPoint(b.MaxX, b.MinZ, nx, nz, ox, oz),
            Symmetry.ReflectPoint(b.MaxX, b.MaxZ, nx, nz, ox, oz),
        };
        return Bounds2d.Of(c.Min(p => p.X), c.Min(p => p.Z), c.Max(p => p.X), c.Max(p => p.Z));
    }

    private static Bounds2d? TranslateBounds(Region t, IReadOnlyDictionary<string, Region> registry)
    {
        if (t.SourceId is not { } sid || registry.GetValueOrDefault(sid)?.Bounds2d is not { } b) return null;
        double dx = t.OffsetX ?? 0, dz = t.OffsetZ ?? 0;
        return Bounds2d.Of(b.MinX + dx, b.MinZ + dz, b.MaxX + dx, b.MaxZ + dz);
    }

    private static double MinPy(double acc, double v) => double.IsNaN(v) ? acc : Math.Min(acc, v);
    private static double MaxPy(double acc, double v) => double.IsNaN(v) ? acc : Math.Max(acc, v);
}
