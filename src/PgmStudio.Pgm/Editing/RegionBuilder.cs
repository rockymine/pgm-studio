namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Build/update region dicts in the <b>canonical</b> xml_data.json shape (nested min/max/base/center/
/// position), so created/edited regions round-trip through the codec + DB. (Python's region_builder
/// emits a flat min_x form that only its analysis fallback tolerates; ours must be canonical.)
/// </summary>
public static class RegionBuilder
{
    public static Dict Bounds(double minX, double minZ, double maxX, double maxZ)
        => new() { ["min"] = new Dict { ["x"] = minX, ["z"] = minZ }, ["max"] = new Dict { ["x"] = maxX, ["z"] = maxZ } };

    public static Dict BuildRegionDict(string type, Dict body, string regionId)
    {
        switch (type)
        {
            case "rectangle":
            {
                double minX = RInt(body["min_x"]), minZ = RInt(body["min_z"]), maxX = RInt(body["max_x"]), maxZ = RInt(body["max_z"]);
                return new Dict { ["id"] = regionId, ["type"] = "rectangle", ["bounds_2d"] = Bounds(minX, minZ, maxX, maxZ) };
            }
            case "cuboid":
            {
                double minX = RInt(body["min_x"]), minZ = RInt(body["min_z"]), maxX = RInt(body["max_x"]), maxZ = RInt(body["max_z"]);
                double minY = RInt(body.GetValueOrDefault("min_y") ?? 0L), maxY = RInt(body.GetValueOrDefault("max_y") ?? 256L);
                return new Dict
                {
                    ["id"] = regionId, ["type"] = "cuboid",
                    ["min"] = new Dict { ["x"] = minX, ["y"] = minY, ["z"] = minZ },
                    ["max"] = new Dict { ["x"] = maxX, ["y"] = maxY, ["z"] = maxZ },
                    ["bounds_2d"] = Bounds(minX, minZ, maxX, maxZ),
                };
            }
            case "point" or "block":
            {
                double px = RInt(body["x"]), pz = RInt(body["z"]), py = RInt(body.GetValueOrDefault("y") ?? 64L);
                var bounds = type == "block" ? Bounds(px, pz, px + 1, pz + 1) : Bounds(px - 0.5, pz - 0.5, px + 0.5, pz + 0.5);
                return new Dict { ["id"] = regionId, ["type"] = type, ["position"] = new Dict { ["x"] = px, ["y"] = py, ["z"] = pz }, ["bounds_2d"] = bounds };
            }
            case "cylinder":
            {
                double bx = F(body["base_x"]), bz = F(body["base_z"]), by = F(body.GetValueOrDefault("base_y") ?? 64L), r = F(body["radius"]), h = F(body.GetValueOrDefault("height") ?? 10L);
                return new Dict { ["id"] = regionId, ["type"] = "cylinder", ["base"] = new Dict { ["x"] = bx, ["y"] = by, ["z"] = bz }, ["radius"] = r, ["height"] = h, ["bounds_2d"] = Bounds(bx - r, bz - r, bx + r, bz + r) };
            }
            case "circle":
            {
                double cx = F(body["center_x"]), cz = F(body["center_z"]), r = F(body["radius"]);
                return new Dict { ["id"] = regionId, ["type"] = "circle", ["center"] = new Dict { ["x"] = cx, ["z"] = cz }, ["radius"] = r, ["bounds_2d"] = Bounds(cx - r, cz - r, cx + r, cz + r) };
            }
            default: throw EditException.BadRequest($"unsupported type '{type}'");
        }
    }

    /// <summary>Union bounds_2d from children. Returns (bounds_2d or null, minX, minZ, maxX, maxZ).</summary>
    public static (Dict? bounds, double minX, double minZ, double maxX, double maxZ) BuildUnionBounds(IEnumerable<Dict> children)
    {
        var bounded = children.Where(c => c.GetValueOrDefault("bounds_2d") is Dict).ToList();
        if (bounded.Count == 0) return (null, 0, 0, 0, 0);
        double minX = bounded.Min(c => Min(c, "x")), minZ = bounded.Min(c => Min(c, "z"));
        double maxX = bounded.Max(c => Max(c, "x")), maxZ = bounded.Max(c => Max(c, "z"));
        return (Bounds(minX, minZ, maxX, maxZ), minX, minZ, maxX, maxZ);

        static double Min(Dict c, string k) => F(((Dict)((Dict)c["bounds_2d"]!)["min"]!)[k]);
        static double Max(Dict c, string k) => F(((Dict)((Dict)c["bounds_2d"]!)["max"]!)[k]);
    }

    /// <summary>Apply a coords update in-place + recompute bounds_2d. Returns new bounds_2d, or null if 2D unchanged.</summary>
    public static Dict? ApplyCoordUpdate(Dict region, string type, Dict coords)
    {
        switch (type)
        {
            case "rectangle":
            {
                var b = (Dict)region["bounds_2d"]!;
                var mn = (Dict)b["min"]!; var mx = (Dict)b["max"]!;
                if (coords.ContainsKey("min_x")) mn["x"] = coords["min_x"];
                if (coords.ContainsKey("min_z")) mn["z"] = coords["min_z"];
                if (coords.ContainsKey("max_x")) mx["x"] = coords["max_x"];
                if (coords.ContainsKey("max_z")) mx["z"] = coords["max_z"];
                var nb = Bounds(F(mn["x"]), F(mn["z"]), F(mx["x"]), F(mx["z"]));
                region["bounds_2d"] = nb;
                return nb;
            }
            case "cuboid":
            {
                var mn = (Dict)region["min"]!; var mx = (Dict)region["max"]!;
                if (coords.ContainsKey("min_y")) mn["y"] = coords["min_y"];
                if (coords.ContainsKey("max_y")) mx["y"] = coords["max_y"];
                return null;   // Y-only; 2D footprint unchanged
            }
            case "cylinder":
            {
                var baseD = Ensure(region, "base");
                if (coords.ContainsKey("base_x")) baseD["x"] = coords["base_x"];
                if (coords.ContainsKey("base_y")) baseD["y"] = coords["base_y"];
                if (coords.ContainsKey("base_z")) baseD["z"] = coords["base_z"];
                if (coords.ContainsKey("radius")) region["radius"] = coords["radius"];
                if (coords.ContainsKey("height")) region["height"] = coords["height"];
                double bx = F(baseD.GetValueOrDefault("x") ?? 0L), bz = F(baseD.GetValueOrDefault("z") ?? 0L), r = F(region.GetValueOrDefault("radius") ?? 0L);
                var nb = Bounds(bx - r, bz - r, bx + r, bz + r); region["bounds_2d"] = nb; return nb;
            }
            case "circle":
            {
                var center = Ensure(region, "center");
                if (coords.ContainsKey("center_x")) center["x"] = coords["center_x"];
                if (coords.ContainsKey("center_z")) center["z"] = coords["center_z"];
                if (coords.ContainsKey("radius")) region["radius"] = coords["radius"];
                double cx = F(center.GetValueOrDefault("x") ?? 0L), cz = F(center.GetValueOrDefault("z") ?? 0L), r = F(region.GetValueOrDefault("radius") ?? 0L);
                var nb = Bounds(cx - r, cz - r, cx + r, cz + r); region["bounds_2d"] = nb; return nb;
            }
            case "block" or "point":
            {
                var pos = Ensure(region, "position");
                foreach (var k in new[] { "x", "y", "z" }) if (coords.ContainsKey(k)) pos[k] = coords[k];
                double px = F(pos.GetValueOrDefault("x") ?? 0L), pz = F(pos.GetValueOrDefault("z") ?? 0L);
                var nb = type == "block" ? Bounds(px, pz, px + 1, pz + 1) : Bounds(px - 0.5, pz - 0.5, px + 0.5, pz + 0.5);
                region["bounds_2d"] = nb; return nb;
            }
            default: return null;
        }
    }

    private static Dict Ensure(Dict d, string k) { if (d.GetValueOrDefault(k) is not Dict sub) { sub = new Dict(); d[k] = sub; } return sub; }
    private static double F(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, string s when double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p) => p, _ => 0 };
    private static double RInt(object? v) => Math.Round(F(v), MidpointRounding.ToEven);
}
