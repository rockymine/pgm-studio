using System.Globalization;
using System.Xml.Linq;
using PgmStudio.Domain;

namespace PgmStudio.Pgm;

/// <summary>
/// Builds a flat region registry from a &lt;regions&gt; element (port of region_parser.py).
/// Composite regions reference children by id; anonymous regions get stable synthetic ids
/// <c>{parent_id}__anon_{index}</c>.
/// </summary>
internal sealed class RegionParser
{
    private readonly Dictionary<string, Region> _registry = new();

    public Dictionary<string, Region> Registry() => _registry;

    public (Dictionary<string, Region> regions, List<ApplyRule> applyRules) ParseRegionsElem(XElement regionsElem)
    {
        var applyRules = new List<ApplyRule>();
        var applyCount = 0;
        foreach (var child in regionsElem.Elements())
        {
            if (child.Name.LocalName == "apply")
            {
                applyRules.Add(ParseApply(child, applyCount));
                applyCount++;
            }
            else
            {
                var region = ParseRegionNode(child, parentId: "");
                if (region is not null && region.Id.Length > 0)
                    _registry[region.Id] = region;
            }
        }
        return (_registry, applyRules);
    }

    public Region? ParseSpawnRegion(XElement elem, string syntheticId)
    {
        var region = ParseRegionElement(elem, syntheticId);
        if (region is not null)
        {
            if (region.Id.Length == 0) region.Id = syntheticId;
            _registry.TryAdd(region.Id, region);
        }
        return region;
    }

    public Region? ResolveReference(string refId) => _registry.GetValueOrDefault(refId);

    private static string SourceRefId(Region? child)
    {
        if (child is null) return "";
        return child.Type == "reference" ? child.RefId ?? "" : child.Id;
    }

    private Region? ParseRegionNode(XElement elem, string parentId, int index = 0)
    {
        var tag = elem.Name.LocalName;
        var regionId = Xml.Get(elem, "id", "");

        Region? region;
        if (tag == "region")
        {
            // A pure <region id="X"/> reference returns immediately, unregistered (matches
            // Python's in-dispatch early return). A <region>…</region> wrapper falls through
            // to its first child + the synthetic-id assignment below.
            if (regionId.Length > 0 && !elem.Elements().Any())
                return new Region { Id = "", Type = "reference", RefId = regionId };
            region = ParseRegionElement(elem, parentId, index);
        }
        else
        {
            region = tag switch
            {
                "rectangle" => ParseRectangle(elem, regionId),
                "cuboid"    => ParseCuboid(elem, regionId),
                "cylinder"  => ParseCylinder(elem, regionId),
                "circle"    => ParseCircle(elem, regionId),
                "sphere"    => ParseSphere(elem, regionId),
                "block"     => ParseBlock(elem, regionId),
                "point"     => ParsePoint(elem, regionId),
                "union" or "negative" or "complement" or "intersect"
                            => ParseComposite(elem, regionId, parentId, index, tag),
                "everywhere" => new Region { Id = regionId, Type = "everywhere" },
                "above"      => new Region { Id = regionId, Type = "above", AboveY = Coord.Parse(Xml.Get(elem, "y", "0")) ?? 0.0 },
                "half"       => ParseHalf(elem, regionId),
                "mirror"     => ParseMirror(elem, regionId, parentId, index),
                "translate"  => ParseTranslate(elem, regionId, parentId, index),
                _            => null,
            };
        }

        if (region is null) return null;

        if (region.Id.Length == 0)
        {
            if (parentId.Length > 0) region.Id = $"{parentId}__anon_{index}";
            else return region;  // top-level anonymous — skip
        }
        if (region.Id.Length > 0) _registry[region.Id] = region;
        return region;
    }

    private Region? ParseRegionElement(XElement parentElem, string parentId = "", int index = 0)
    {
        foreach (var (child, i) in parentElem.Elements().Select((c, i) => (c, i)))
            return ParseRegionNode(child, parentId, i);
        return null;
    }

    private Region ParseComposite(XElement elem, string regionId, string parentId, int parentIndex, string tag)
    {
        var effectiveId = regionId.Length > 0 ? regionId
            : (parentId.Length > 0 ? $"{parentId}__anon_{parentIndex}" : "");

        var childIds = new List<string>();
        foreach (var (childElem, i) in elem.Elements().Select((c, i) => (c, i)))
        {
            var child = ParseRegionNode(childElem, effectiveId, i);
            if (child is null) continue;
            if (child.Type == "reference") childIds.Add(child.RefId ?? "");
            else if (child.Id.Length > 0) childIds.Add(child.Id);
        }

        var region = new Region { Id = regionId, Type = tag, Children = childIds };
        region.Bounds2d = UnionBounds(childIds);
        return region;
    }

    // ── primitives ──────────────────────────────────────────────────────────────────
    private static Region ParseRectangle(XElement elem, string regionId)
    {
        var min = Xml.Coords2(Xml.Get(elem, "min", "0,0"));
        var max = Xml.Coords2(Xml.Get(elem, "max", "0,0"));
        var r = new Region { Id = regionId, Type = "rectangle", MinX = min[0], MinZ = min[1], MaxX = max[0], MaxZ = max[1] };
        if (min[0] is { } a && min[1] is { } b && max[0] is { } c && max[1] is { } d)
            r.Bounds2d = Bounds2d.Of(a, b, c, d);
        return r;
    }

    private static Region ParseCuboid(XElement elem, string regionId)
    {
        var sizeStr = Xml.Get(elem, "size", "");
        var hasMin = Xml.GetOrNull(elem, "min") is not null;
        var hasMax = Xml.GetOrNull(elem, "max") is not null;
        double?[] minC, maxC;
        if (sizeStr.Length > 0 && hasMin && !hasMax)
        {
            minC = Xml.Coords3(Xml.Get(elem, "min")!);
            var sz = Xml.Coords3(sizeStr);
            maxC = [Add(minC[0], sz[0]), Add(minC[1], sz[1]), Add(minC[2], sz[2])];
        }
        else if (sizeStr.Length > 0 && hasMax && !hasMin)
        {
            maxC = Xml.Coords3(Xml.Get(elem, "max")!);
            var sz = Xml.Coords3(sizeStr);
            minC = [Sub(maxC[0], sz[0]), Sub(maxC[1], sz[1]), Sub(maxC[2], sz[2])];
        }
        else
        {
            minC = Xml.Coords3(Xml.Get(elem, "min", "0,0,0"));
            maxC = Xml.Coords3(Xml.Get(elem, "max", "0,0,0"));
        }
        var r = new Region
        {
            Id = regionId, Type = "cuboid",
            MinX = minC[0], MinY = minC[1], MinZ = minC[2],
            MaxX = maxC[0], MaxY = maxC[1], MaxZ = maxC[2],
        };
        if (minC[0] is { } a && minC[2] is { } b && maxC[0] is { } c && maxC[2] is { } d)
            r.Bounds2d = Bounds2d.Of(a, b, c, d);
        return r;
    }

    private static double? Add(double? a, double? b) => a is not null && b is not null ? a + b : null;
    private static double? Sub(double? a, double? b) => a is not null && b is not null ? a - b : null;

    private static Region ParseCylinder(XElement elem, string regionId)
    {
        var b = Xml.Coords3(Xml.Get(elem, "base", "0,0,0"));
        var radiusStr = Xml.Get(elem, "radius", "0");
        var radius = radiusStr.Length > 0 ? double.Parse(radiusStr, CultureInfo.InvariantCulture) : 0.0;
        var height = Coord.Parse(Xml.Get(elem, "height", "0"));
        var r = new Region
        {
            Id = regionId, Type = "cylinder",
            BaseX = Xml.Or0(b[0]), BaseY = Xml.Or0(b[1]), BaseZ = Xml.Or0(b[2]),
            Radius = radius, Height = height,
        };
        r.Bounds2d = Bounds2d.Of(Xml.Or0(b[0]) - radius, Xml.Or0(b[2]) - radius, Xml.Or0(b[0]) + radius, Xml.Or0(b[2]) + radius);
        return r;
    }

    private static Region ParseCircle(XElement elem, string regionId)
    {
        var c = Xml.Coords2(Xml.Get(elem, "center", "0,0"));
        var radius = double.Parse(Xml.Get(elem, "radius", "0"), CultureInfo.InvariantCulture);
        var r = new Region { Id = regionId, Type = "circle", CenterX = Xml.Or0(c[0]), CenterZ = Xml.Or0(c[1]), Radius = radius };
        r.Bounds2d = Bounds2d.Of(Xml.Or0(c[0]) - radius, Xml.Or0(c[1]) - radius, Xml.Or0(c[0]) + radius, Xml.Or0(c[1]) + radius);
        return r;
    }

    private static Region ParseSphere(XElement elem, string regionId)
    {
        var o = Xml.Coords3(Xml.Get(elem, "origin", "0,0,0"));
        var radius = double.Parse(Xml.Get(elem, "radius", "0"), CultureInfo.InvariantCulture);
        var r = new Region
        {
            Id = regionId, Type = "sphere",
            OriginX = Xml.Or0(o[0]), OriginY = Xml.Or0(o[1]), OriginZ = Xml.Or0(o[2]), Radius = radius,
        };
        r.Bounds2d = Bounds2d.Of(Xml.Or0(o[0]) - radius, Xml.Or0(o[2]) - radius, Xml.Or0(o[0]) + radius, Xml.Or0(o[2]) + radius);
        return r;
    }

    private static Region ParseBlock(XElement elem, string regionId)
    {
        var c = Xml.Coords3(NonEmpty(Xml.Text(elem), "0,0,0"));
        var r = new Region { Id = regionId, Type = "block", PosX = Xml.Or0(c[0]), PosY = Xml.Or0(c[1]), PosZ = Xml.Or0(c[2]) };
        r.Bounds2d = Bounds2d.Of(Xml.Or0(c[0]), Xml.Or0(c[2]), Xml.Or0(c[0]) + 1, Xml.Or0(c[2]) + 1);
        return r;
    }

    private static Region ParsePoint(XElement elem, string regionId)
    {
        var c = Xml.Coords3(NonEmpty(Xml.Text(elem), "0,0,0"));
        var r = new Region { Id = regionId, Type = "point", PosX = Xml.Or0(c[0]), PosY = Xml.Or0(c[1]), PosZ = Xml.Or0(c[2]) };
        r.Bounds2d = Bounds2d.Of(Xml.Or0(c[0]) - 0.5, Xml.Or0(c[2]) - 0.5, Xml.Or0(c[0]) + 0.5, Xml.Or0(c[2]) + 0.5);
        return r;
    }

    private static Region ParseHalf(XElement elem, string regionId)
    {
        var o = Xml.Coords3(Xml.Get(elem, "origin", "0,0,0"));
        var n = Xml.Coords3(Xml.Get(elem, "normal", "0,0,0"));
        return new Region
        {
            Id = regionId, Type = "half",
            OriginX = Xml.Or0(o[0]), OriginY = Xml.Or0(o[1]), OriginZ = Xml.Or0(o[2]),
            NormalX = Xml.Or0(n[0]), NormalY = Xml.Or0(n[1]), NormalZ = Xml.Or0(n[2]),
        };
    }

    private Region ParseMirror(XElement elem, string regionId, string parentId, int index)
    {
        var o = Xml.Coords3(Xml.Get(elem, "origin", "0,0,0"));
        var n = Xml.Coords3(Xml.Get(elem, "normal", "0,0,0"));
        var refId = Xml.Get(elem, "region", "");
        string sourceId;
        if (refId.Length > 0) sourceId = refId;
        else
        {
            var effectiveParent = regionId.Length > 0 ? regionId : (parentId.Length > 0 ? $"{parentId}__anon_{index}" : "");
            sourceId = SourceRefId(ParseRegionElement(elem, effectiveParent, 0));
        }
        var m = new Region
        {
            Id = regionId, Type = "mirror", SourceId = sourceId,
            OriginX = Xml.Or0(o[0]), OriginY = Xml.Or0(o[1]), OriginZ = Xml.Or0(o[2]),
            NormalX = Xml.Or0(n[0]), NormalY = Xml.Or0(n[1]), NormalZ = Xml.Or0(n[2]),
        };
        m.Bounds2d = MirrorBounds(sourceId, Xml.Or0(n[0]), Xml.Or0(n[2]), Xml.Or0(o[0]), Xml.Or0(o[2]));
        return m;
    }

    private Region ParseTranslate(XElement elem, string regionId, string parentId, int index)
    {
        var off = Xml.Coords3(Xml.Get(elem, "offset", "0,0,0"));
        var refId = Xml.Get(elem, "region", "");
        string sourceId;
        if (refId.Length > 0) sourceId = refId;
        else
        {
            var effectiveParent = regionId.Length > 0 ? regionId : (parentId.Length > 0 ? $"{parentId}__anon_{index}" : "");
            sourceId = SourceRefId(ParseRegionElement(elem, effectiveParent, 0));
        }
        var t = new Region
        {
            Id = regionId, Type = "translate", SourceId = sourceId,
            OffsetX = Xml.Or0(off[0]), OffsetY = Xml.Or0(off[1]), OffsetZ = Xml.Or0(off[2]),
        };
        t.Bounds2d = TranslateBounds(sourceId, Xml.Or0(off[0]), Xml.Or0(off[2]));
        return t;
    }

    private ApplyRule ParseApply(XElement elem, int applyIndex)
    {
        var rule = new ApplyRule
        {
            EnterFilter = Xml.Get(elem, "enter", ""),
            LeaveFilter = Xml.Get(elem, "leave", ""),
            BlockFilter = Xml.Get(elem, "block", ""),
            BlockPlaceFilter = Xml.Get(elem, "block-place", ""),
            BlockBreakFilter = Xml.Get(elem, "block-break", ""),
            BlockPhysicsFilter = Xml.Get(elem, "block-physics", ""),
            BlockPlaceAgainstFilter = Xml.Get(elem, "block-place-against", ""),
            UseFilter = Xml.Get(elem, "use", ""),
            FilterId = Xml.Get(elem, "filter", ""),
            RegionId = Xml.Get(elem, "region", ""),
            Kit = Xml.Get(elem, "kit", ""),
            LendKit = Xml.Get(elem, "lend-kit", ""),
            Velocity = Xml.Get(elem, "velocity", ""),
            Message = Xml.Get(elem, "message", ""),
        };
        if (rule.RegionId.Length == 0)
        {
            var syntheticParent = $"__apply_{applyIndex}";
            foreach (var child in elem.Elements())
            {
                var childRegion = ParseRegionNode(child, syntheticParent, 0);
                if (childRegion is not null && childRegion.Id.Length > 0)
                {
                    rule.RegionId = childRegion.Id;
                    break;
                }
            }
        }
        return rule;
    }

    // ── bounds helpers ──────────────────────────────────────────────────────────────
    private Bounds2d? UnionBounds(List<string> childIds)
    {
        double minX = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        var found = false;
        foreach (var cid in childIds)
        {
            if (_registry.GetValueOrDefault(cid)?.Bounds2d is { } b)
            {
                // Python's min/max ignore a NaN operand (a mirror of an infinite source yields NaN);
                // C# Math.Min/Max propagate it — so skip NaN per component to match.
                minX = MinPy(minX, b.MinX); minZ = MinPy(minZ, b.MinZ);
                maxX = MaxPy(maxX, b.MaxX); maxZ = MaxPy(maxZ, b.MaxZ);
                found = true;
            }
        }
        return found ? Bounds2d.Of(minX, minZ, maxX, maxZ) : null;
    }

    private static double MinPy(double acc, double v) => double.IsNaN(v) ? acc : Math.Min(acc, v);
    private static double MaxPy(double acc, double v) => double.IsNaN(v) ? acc : Math.Max(acc, v);

    private Bounds2d? TranslateBounds(string sourceId, double dx, double dz)
    {
        if (_registry.GetValueOrDefault(sourceId)?.Bounds2d is not { } b) return null;
        return Bounds2d.Of(b.MinX + dx, b.MinZ + dz, b.MaxX + dx, b.MaxZ + dz);
    }

    // Reflect the source AABB across the mirror plane (PGM <mirror>): reflect all four corners,
    // then re-bound — exact for axis-aligned and 45° normals (port of geometry.reflect_bounds_2d).
    private Bounds2d? MirrorBounds(string sourceId, double nx, double nz, double ox, double oz)
    {
        if (_registry.GetValueOrDefault(sourceId)?.Bounds2d is not { } b) return null;
        var n2 = nx * nx + nz * nz;
        (double x, double z) Reflect(double px, double pz)
        {
            if (n2 == 0) return (px, pz);
            var d = 2.0 * ((px - ox) * nx + (pz - oz) * nz) / n2;
            return (px - nx * d, pz - nz * d);
        }
        var corners = new[]
        {
            Reflect(b.MinX, b.MinZ), Reflect(b.MinX, b.MaxZ),
            Reflect(b.MaxX, b.MinZ), Reflect(b.MaxX, b.MaxZ),
        };
        return Bounds2d.Of(corners.Min(c => c.x), corners.Min(c => c.z), corners.Max(c => c.x), corners.Max(c => c.z));
    }

    private static string NonEmpty(string s, string def) => string.IsNullOrEmpty(s) ? def : s;
}
