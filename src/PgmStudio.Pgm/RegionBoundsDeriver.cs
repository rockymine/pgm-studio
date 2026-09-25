using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm;

/// <summary>
/// Fills in the derived <see cref="Region.Bounds2d"/> for compound/transform regions when
/// reconstructing a map from storage. The XML parser computes these at parse time
/// (<c>RegionParser</c>), but a DB round-trip only persists primitive bounds — so after rebuilding
/// the registry we recompute union/negative/complement/intersect (union of children),
/// mirror (reflect the source AABB) and translate (offset the source AABB). The parser takes its bounds from
/// the same three operations.
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
        => childIds is null ? null
            : Union(childIds.Select(cid => registry.GetValueOrDefault(cid)?.Bounds2d).OfType<Bounds2d>());

    private static Bounds2d? MirrorBounds(Region m, IReadOnlyDictionary<string, Region> registry)
        => m.SourceId is { } sid && registry.GetValueOrDefault(sid)?.Bounds2d is { } b
            ? Mirror(b, m.NormalX ?? 0, m.NormalZ ?? 0, m.OriginX ?? 0, m.OriginZ ?? 0)
            : null;

    private static Bounds2d? TranslateBounds(Region t, IReadOnlyDictionary<string, Region> registry)
        => t.SourceId is { } sid && registry.GetValueOrDefault(sid)?.Bounds2d is { } b
            ? Translate(b, t.OffsetX ?? 0, t.OffsetZ ?? 0)
            : null;

    /// <summary>The box around every child box; null where there is none.</summary>
    public static Bounds2d? Union(IEnumerable<Bounds2d> boxes)
    {
        double minX = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        var found = false;
        foreach (var b in boxes)
        {
            minX = Math.Min(minX, b.MinX); minZ = Math.Min(minZ, b.MinZ);
            maxX = Math.Max(maxX, b.MaxX); maxZ = Math.Max(maxZ, b.MaxZ);
            found = true;
        }
        return found ? Bounds2d.Of(minX, minZ, maxX, maxZ) : null;
    }

    /// <summary>The source box reflected across the mirror plane (PGM <c>&lt;mirror&gt;</c>): all four corners
    /// through the canonical <see cref="Symmetry"/> transform, then re-bound — exact for axis-aligned and 45°
    /// normals. An unbounded side cannot go through the corner transform (it multiplies a zero component by
    /// infinity), so an axis-aligned normal flips its own axis alone and a diagonal one leaves the reflection
    /// unbounded.</summary>
    public static Bounds2d Mirror(Bounds2d b, double nx, double nz, double ox, double oz)
    {
        if (double.IsInfinity(b.MinX) || double.IsInfinity(b.MinZ) || double.IsInfinity(b.MaxX) || double.IsInfinity(b.MaxZ))
        {
            if (nz == 0 && nx != 0) return Bounds2d.Of(2 * ox - b.MaxX, b.MinZ, 2 * ox - b.MinX, b.MaxZ);
            if (nx == 0 && nz != 0) return Bounds2d.Of(b.MinX, 2 * oz - b.MaxZ, b.MaxX, 2 * oz - b.MinZ);
            return Bounds2d.Of(double.NegativeInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
        }
        var c = new[]
        {
            Symmetry.ReflectPoint(b.MinX, b.MinZ, nx, nz, ox, oz),
            Symmetry.ReflectPoint(b.MinX, b.MaxZ, nx, nz, ox, oz),
            Symmetry.ReflectPoint(b.MaxX, b.MinZ, nx, nz, ox, oz),
            Symmetry.ReflectPoint(b.MaxX, b.MaxZ, nx, nz, ox, oz),
        };
        return Bounds2d.Of(c.Min(p => p.X), c.Min(p => p.Z), c.Max(p => p.X), c.Max(p => p.Z));
    }

    /// <summary>The source box moved by the offset.</summary>
    public static Bounds2d Translate(Bounds2d b, double dx, double dz)
        => Bounds2d.Of(b.MinX + dx, b.MinZ + dz, b.MaxX + dx, b.MaxZ + dz);
}
