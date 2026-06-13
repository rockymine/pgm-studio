using NetTopologySuite.Geometries;

namespace PgmStudio.Analysis;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Encodes the flat region registry into the authoring split (B4a) for the editor: <c>primitives</c>
/// (leaf shapes you drew) and <c>composed</c> (structures grouping them, with apply-rule wiring),
/// each node carrying derived category, type-specific coords, and a Shapely/NTS-derived
/// <c>polygon_2d</c>. Port of <c>region_encoder.encode_region_authoring</c> (+ its node helpers).
/// Geometry is delegated to <see cref="RegionGeometry2d"/> (the <c>_dict_to_shapely</c> port).
/// </summary>
public static class RegionAuthoringEncoder
{
    private static readonly string[] CategoryOrder =
        ["spawn", "observer_spawn", "wool", "build", "mechanic", "other"];

    private static readonly HashSet<string> PolygonTypes =
        ["circle", "half", "complement", "union", "intersect", "negative", "mirror", "translate"];

    private static readonly HashSet<string> PrimitiveTypes =
        ["rectangle", "cuboid", "cylinder", "circle", "sphere", "block", "point"];

    private static readonly string[] EventKeys =
    [
        "enter", "leave", "block", "block_break", "block_place", "block_physics",
        "block_place_against", "use", "filter", "kit", "lend_kit", "velocity", "message",
    ];

    /// <summary>{primitives:[node], composed:[node]} for every named region in the registry.</summary>
    public static Dict EncodeAuthoring(
        Dict regionsDict, IReadOnlyDictionary<string, string> categories,
        List<object?>? applyRules, (double minX, double minZ, double maxX, double maxZ)? bounds)
    {
        var primitives = new List<object?>();
        var composed = new List<object?>();
        foreach (var (regionId, regionObj) in regionsDict)
        {
            if (regionObj is not Dict region) continue;
            var cat = categories.TryGetValue(regionId, out var c) && CategoryOrder.Contains(c) ? c : "other";
            var node = AuthoringNode(region, regionId, cat, bounds, regionsDict, applyRules);
            var t = region.GetValueOrDefault("type") as string ?? "";
            (PrimitiveTypes.Contains(t) ? primitives : composed).Add(node);
        }
        return new Dict { ["primitives"] = primitives, ["composed"] = composed };
    }

    private static Dict AuthoringNode(
        Dict region, string regionId, string category,
        (double minX, double minZ, double maxX, double maxZ)? bounds, Dict registry, List<object?>? applyRules)
    {
        var t = region.GetValueOrDefault("type") as string ?? "unknown";
        var node = new Dict
        {
            ["id"] = regionId,
            ["type"] = t,
            ["label"] = string.IsNullOrEmpty(regionId) ? $"({t})" : regionId,
            ["category"] = category,
            ["bounds"] = EncodeBounds(region),
            ["coords"] = EncodeCoords(region),
            ["member_ids"] = MemberIds(region),
            ["wiring"] = WiringFor(regionId, applyRules),
        };
        if (bounds is { } b && PolygonTypes.Contains(t))
        {
            var poly = ComputePolygon2d(region, b, registry);
            if (poly is not null)
            {
                node["polygon_2d"] = poly;
                if (node["bounds"] is null && poly["exterior"] is List<object?> ext && ext.Count > 0)
                {
                    var xs = ext.Select(p => ((List<object?>)p!)[0]).Select(Convert.ToDouble).ToList();
                    var zs = ext.Select(p => ((List<object?>)p!)[1]).Select(Convert.ToDouble).ToList();
                    node["bounds"] = new Dict { ["min_x"] = xs.Min(), ["min_z"] = zs.Min(), ["max_x"] = xs.Max(), ["max_z"] = zs.Max() };
                }
            }
        }
        return node;
    }

    // ── bounds / coords (port of _encode_bounds / _encode_coords) ──────────────────

    private static Dict? EncodeBounds(Dict region)
    {
        if (region.GetValueOrDefault("bounds_2d") is not Dict b2) return null;
        var mn = b2.GetValueOrDefault("min") as Dict ?? new();
        var mx = b2.GetValueOrDefault("max") as Dict ?? new();
        if (!mn.ContainsKey("x") || !mn.ContainsKey("z")) return null;
        // Pass values through raw (a coordinate may be the "oo"/"-oo" string), like Python's _encode_bounds.
        object? minX = mn["x"], minZ = mn["z"], maxX = mx.GetValueOrDefault("x"), maxZ = mx.GetValueOrDefault("z");
        var t = region.GetValueOrDefault("type") as string;
        if (NumEq(maxX, minX) && NumEq(maxZ, minZ))
        {
            if (t == "block") { maxX = (Num(minX) ?? 0) + 1; maxZ = (Num(minZ) ?? 0) + 1; }
            else if (t == "point")
            {
                minX = (Num(minX) ?? 0) - 0.5; minZ = (Num(minZ) ?? 0) - 0.5;
                maxX = (Num(maxX) ?? 0) + 0.5; maxZ = (Num(maxZ) ?? 0) + 0.5;
            }
        }
        return new Dict { ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ };
    }

    private static bool NumEq(object? a, object? b) =>
        Num(a) is { } x && Num(b) is { } y ? x == y : Equals(a, b);

    private static Dict? EncodeCoords(Dict region)
    {
        var t = region.GetValueOrDefault("type") as string;
        Dict B2(string k) => region.GetValueOrDefault("bounds_2d") is Dict b ? b.GetValueOrDefault(k) as Dict ?? new() : new();
        switch (t)
        {
            case "rectangle":
            case "cuboid":
            {
                var mn = region.GetValueOrDefault("min") as Dict ?? B2("min");
                var mx = region.GetValueOrDefault("max") as Dict ?? B2("max");
                return new Dict
                {
                    ["min_x"] = mn.Count > 0 ? Num(mn.GetValueOrDefault("x")) : Num(region.GetValueOrDefault("min_x")),
                    ["min_y"] = mn.Count > 0 ? Num(mn.GetValueOrDefault("y")) : Num(region.GetValueOrDefault("min_y")),
                    ["min_z"] = mn.Count > 0 ? Num(mn.GetValueOrDefault("z")) : Num(region.GetValueOrDefault("min_z")),
                    ["max_x"] = mx.Count > 0 ? Num(mx.GetValueOrDefault("x")) : Num(region.GetValueOrDefault("max_x")),
                    ["max_y"] = mx.Count > 0 ? Num(mx.GetValueOrDefault("y")) : Num(region.GetValueOrDefault("max_y")),
                    ["max_z"] = mx.Count > 0 ? Num(mx.GetValueOrDefault("z")) : Num(region.GetValueOrDefault("max_z")),
                };
            }
            case "cylinder":
            {
                var bse = region.GetValueOrDefault("base") as Dict ?? new();
                return new Dict
                {
                    ["base_x"] = Num(bse.GetValueOrDefault("x")), ["base_y"] = Num(bse.GetValueOrDefault("y")), ["base_z"] = Num(bse.GetValueOrDefault("z")),
                    ["radius"] = Num(region.GetValueOrDefault("radius")), ["height"] = Num(region.GetValueOrDefault("height")),
                };
            }
            case "circle":
            {
                var ctr = region.GetValueOrDefault("center") as Dict ?? new();
                return new Dict { ["center_x"] = Num(ctr.GetValueOrDefault("x")), ["center_z"] = Num(ctr.GetValueOrDefault("z")), ["radius"] = Num(region.GetValueOrDefault("radius")) };
            }
            case "sphere":
            {
                var org = region.GetValueOrDefault("origin") as Dict ?? new();
                return new Dict { ["origin_x"] = Num(org.GetValueOrDefault("x")), ["origin_y"] = Num(org.GetValueOrDefault("y")), ["origin_z"] = Num(org.GetValueOrDefault("z")), ["radius"] = Num(region.GetValueOrDefault("radius")) };
            }
            case "block":
            case "point":
            {
                var pos = region.GetValueOrDefault("position") as Dict ?? new();
                return new Dict { ["x"] = Num(pos.GetValueOrDefault("x")), ["y"] = Num(pos.GetValueOrDefault("y")), ["z"] = Num(pos.GetValueOrDefault("z")) };
            }
            case "reference":
                return new Dict { ["ref_id"] = region.GetValueOrDefault("ref_id") as string ?? "" };
            case "half":
            {
                var org = region.GetValueOrDefault("origin") as Dict ?? new();
                var nrm = region.GetValueOrDefault("normal") as Dict ?? new();
                return new Dict
                {
                    ["origin_x"] = Num(org.GetValueOrDefault("x")), ["origin_y"] = Num(org.GetValueOrDefault("y")), ["origin_z"] = Num(org.GetValueOrDefault("z")),
                    ["normal_x"] = Num(nrm.GetValueOrDefault("x")), ["normal_y"] = Num(nrm.GetValueOrDefault("y")), ["normal_z"] = Num(nrm.GetValueOrDefault("z")),
                };
            }
            case "mirror":
            {
                var org = region.GetValueOrDefault("origin") as Dict ?? new();
                var nrm = region.GetValueOrDefault("normal") as Dict ?? new();
                return new Dict
                {
                    ["source_id"] = SourceId(region),
                    ["origin_x"] = Num(org.GetValueOrDefault("x")), ["origin_y"] = Num(org.GetValueOrDefault("y")), ["origin_z"] = Num(org.GetValueOrDefault("z")),
                    ["normal_x"] = Num(nrm.GetValueOrDefault("x")), ["normal_y"] = Num(nrm.GetValueOrDefault("y")), ["normal_z"] = Num(nrm.GetValueOrDefault("z")),
                };
            }
            case "translate":
            {
                var off = region.GetValueOrDefault("offset") as Dict ?? new();
                return new Dict
                {
                    ["source_id"] = SourceId(region),
                    ["offset_x"] = Num(off.GetValueOrDefault("x")), ["offset_y"] = Num(off.GetValueOrDefault("y")), ["offset_z"] = Num(off.GetValueOrDefault("z")),
                };
            }
            default:
                return null;
        }
    }

    // ── structure / wiring (port of _member_ids / _wiring_for) ─────────────────────

    private static List<object?> MemberIds(Dict region)
    {
        var t = region.GetValueOrDefault("type") as string;
        var ids = new List<object?>();
        if (t is "union" or "negative" or "complement" or "intersect")
            foreach (var c in region.GetValueOrDefault("children") as List<object?> ?? [])
            {
                if (c is string s) ids.Add(s);
                else if (c is Dict cd && cd.GetValueOrDefault("id") is string cid && cid.Length > 0) ids.Add(cid);
            }
        else if (t is "mirror" or "translate")
        {
            var sid = SourceId(region);
            if (sid.Length > 0) ids.Add(sid);
        }
        else if (t == "reference")
        {
            var rid = region.GetValueOrDefault("ref_id") as string ?? "";
            if (rid.Length > 0) ids.Add(rid);
        }
        return ids;
    }

    private static List<object?> WiringFor(string regionId, List<object?>? applyRules)
    {
        var outList = new List<object?>();
        foreach (var ruleObj in applyRules ?? [])
        {
            if (ruleObj is not Dict rule || rule.GetValueOrDefault("region") as string != regionId) continue;
            foreach (var k in EventKeys)
            {
                var v = rule.GetValueOrDefault(k);
                if (v is null) continue;
                if (v is string sv && sv.Length == 0) continue;
                if (v is bool bv && !bv) continue;
                outList.Add(new Dict { ["event"] = k, ["value"] = ValueStr(v), ["rule_id"] = rule.GetValueOrDefault("id") });
            }
        }
        return outList;
    }

    // ── polygon_2d (port of _shapely_to_polygon_2d) ────────────────────────────────

    private static Dict? ComputePolygon2d(Dict region, (double, double, double, double) bounds, Dict registry)
    {
        try { return ShapelyToPolygon2d(RegionGeometry2d.ToGeometry(region, bounds, registry)); }
        catch { return null; }
    }

    private static Dict? ShapelyToPolygon2d(Geometry? geom)
    {
        if (geom is null || geom.IsEmpty) return null;
        List<Polygon> polys;
        if (geom is GeometryCollection coll)
            polys = Enumerable.Range(0, coll.NumGeometries).Select(coll.GetGeometryN).OfType<Polygon>().Where(p => !p.IsEmpty).ToList();
        else if (geom is Polygon p) polys = [p];
        else return null;
        if (polys.Count == 0) return null;

        var encoded = polys.Select(pg => new Dict
        {
            ["exterior"] = Ring(pg.ExteriorRing),
            ["holes"] = new List<object?>(Enumerable.Range(0, pg.NumInteriorRings).Select(i => (object?)Ring(pg.GetInteriorRingN(i)))),
        }).ToList();

        return new Dict
        {
            ["polygons"] = encoded.Cast<object?>().ToList(),
            ["exterior"] = encoded[0]["exterior"],
            ["holes"] = encoded[0]["holes"],
        };
    }

    // The footprint plane is X/Y in NTS (boxes built as Envelope(x,_,z,_)), so z lives in Coordinate.Y.
    private static List<object?> Ring(LineString ring) =>
        ring.Coordinates.Select(c => (object?)new List<object?> { Math.Round(c.X, 2), Math.Round(c.Y, 2) }).ToList();

    // ── helpers ────────────────────────────────────────────────────────────────────

    private static string SourceId(Dict region) =>
        region.GetValueOrDefault("source_id") as string is { Length: > 0 } s ? s
        : region.GetValueOrDefault("ref_region_id") as string ?? "";

    private static string ValueStr(object? v) => v switch
    {
        string s => s,
        bool b => b ? "True" : "False",
        double d => d == Math.Floor(d) ? ((long)d).ToString() : d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => v?.ToString() ?? "",
    };

    private static double? Num(object? v) => v switch
    {
        double d => d, long l => l, int i => i, float f => f, _ => null,
    };

    // ── region tree (port of region_encoder.encode_region_tree + _encode_node) ──────
    // The canvas renders from this grouped, nested view (vs the flat authoring split above).

    private static readonly IReadOnlyDictionary<string, string> CategoryLabels = new Dictionary<string, string>
    {
        ["spawn"] = "Spawn", ["observer_spawn"] = "Observer Spawn", ["wool"] = "Wool",
        ["build"] = "Build", ["mechanic"] = "Mechanics", ["other"] = "Other",
    };

    /// <summary>Root regions grouped into thematic categories, each a recursive node tree (render input).</summary>
    public static List<object?> EncodeTree(
        Dict regionsDict, IReadOnlyDictionary<string, string> categories,
        (double minX, double minZ, double maxX, double maxZ)? bounds,
        IReadOnlyDictionary<string, RegionFacet>? facets = null)
    {
        var namedChildIds = new HashSet<string>();
        foreach (var ro in regionsDict.Values)
            if (ro is Dict region) CollectNamedChildIds(region, namedChildIds);

        var roots = new List<(string id, Dict node)>();
        foreach (var (regionId, ro) in regionsDict)
            if (ro is Dict region && !namedChildIds.Contains(regionId))
                roots.Add((regionId, EncodeNode(region, bounds, regionsDict, facets)));

        var groups = new Dictionary<string, List<object?>>();
        foreach (var (regionId, node) in roots)
        {
            var cat = categories.TryGetValue(regionId, out var c) && CategoryOrder.Contains(c) ? c : "other";
            (groups.TryGetValue(cat, out var list) ? list : groups[cat] = new()).Add(node);
        }

        var ordered = CategoryOrder.Concat(groups.Keys.Where(c => !CategoryOrder.Contains(c)));
        return ordered.Where(groups.ContainsKey).Select(cat => (object?)new Dict
        {
            ["name"] = cat,
            ["label"] = CategoryLabels.GetValueOrDefault(cat, Capitalise(cat)),
            ["regions"] = groups[cat],
        }).ToList();
    }

    // Spatial-access events recorded in the roles facet as "<event>=<filter>"; the rule wiring the
    // editor cares about (excludes the rule_container/rule_group/time_gated flags).
    private static readonly HashSet<string> SpatialEvents = ["enter", "block", "block_break", "block_place"];

    private static List<object?>? WiringEvents(RegionFacet? facet)
    {
        if (facet is null) return null;
        var wiring = facet.Roles
            .Where(r => r.IndexOf('=') is var i && i > 0 && SpatialEvents.Contains(r[..i]))
            .Cast<object?>().ToList();
        return wiring.Count > 0 ? wiring : null;
    }

    private static void CollectNamedChildIds(Dict region, HashSet<string> outSet)
    {
        foreach (var child in region.GetValueOrDefault("children") as List<object?> ?? [])
        {
            if (child is string s) outSet.Add(s);
            else if (child is Dict cd)
            {
                if (cd.GetValueOrDefault("id") as string is { Length: > 0 } cid) outSet.Add(cid);
                CollectNamedChildIds(cd, outSet);
            }
        }
    }

    private static Dict EncodeNode(Dict region, (double, double, double, double)? bounds, Dict registry,
        IReadOnlyDictionary<string, RegionFacet>? facets = null)
    {
        var xmlId = region.GetValueOrDefault("id") as string ?? "";
        var t = region.GetValueOrDefault("type") as string ?? "unknown";
        var label = xmlId.Length > 0 ? xmlId : $"({t})";
        if (t == "reference") label = $"→ {region.GetValueOrDefault("ref_id") as string ?? "?"}";

        var children = new List<object?>();
        foreach (var child in region.GetValueOrDefault("children") as List<object?> ?? [])
        {
            if (child is string s) { if (registry.GetValueOrDefault(s) is Dict cr) children.Add(EncodeNode(cr, bounds, registry, facets)); }
            else if (child is Dict cd) children.Add(EncodeNode(cd, bounds, registry, facets));
        }
        var rawSource = ResolveSource(region, registry);
        var sourceNode = rawSource is not null ? EncodeNode(rawSource, bounds, registry, facets) : null;

        var facet = xmlId.Length > 0 ? facets?.GetValueOrDefault(xmlId) : null;
        var node = new Dict
        {
            ["id"] = xmlId,
            ["type"] = t,
            ["label"] = label,
            ["bounds"] = EncodeBounds(region),
            ["coords"] = EncodeCoords(region),
            ["is_negative"] = t == "negative",
            ["synthetic_id"] = xmlId.Length == 0,
            // the region's OWN derived category — distinct from the group it renders under: build regions
            // and objective/spawn zones nest inside rule-containers (not-build-area, spawns) in "other",
            // so a consumer needs the per-node category to surface them (e.g. the build-region tree).
            ["category"] = facet?.Category,
            // subtype refines the category (spawn → point|protection, wool → room|monument|spawner).
            ["subtype"] = facet?.Subtype,
            // the spatial filter wiring on this region ("enter=<f>", "block_break=<f>", …), for display
            // (the first event) and R1; empty for unwired regions like monuments/spawners.
            ["wiring"] = WiringEvents(facet),
            ["children"] = children,
            ["source"] = sourceNode,
        };
        if (bounds is { } b && PolygonTypes.Contains(t))
        {
            var poly = ComputePolygon2d(region, b, registry);
            if (poly is not null)
            {
                node["polygon_2d"] = poly;
                if (node["bounds"] is null && poly["exterior"] is List<object?> ext && ext.Count > 0)
                {
                    var xs = ext.Select(p => Convert.ToDouble(((List<object?>)p!)[0])).ToList();
                    var zs = ext.Select(p => Convert.ToDouble(((List<object?>)p!)[1])).ToList();
                    node["bounds"] = new Dict { ["min_x"] = xs.Min(), ["min_z"] = zs.Min(), ["max_x"] = xs.Max(), ["max_z"] = zs.Max() };
                }
            }
        }
        return node;
    }

    private static Dict? ResolveSource(Dict region, Dict registry)
    {
        if (region.GetValueOrDefault("source") is Dict src) return src;
        var sid = region.GetValueOrDefault("source_id") as string is { Length: > 0 } s ? s
            : region.GetValueOrDefault("ref_region_id") as string ?? "";
        return sid.Length > 0 ? registry.GetValueOrDefault(sid) as Dict : null;
    }

    private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
