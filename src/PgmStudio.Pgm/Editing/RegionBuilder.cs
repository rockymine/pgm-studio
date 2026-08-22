namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Build/update region dicts in the <b>canonical</b> xml_data.json shape (nested min/max/base/center/
/// position), so created and edited regions round-trip through the codec and the DB. The flat
/// <c>min_x</c> form some readers tolerate is not written here — the canonical shape is the contract.
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
                double minX = RInt(Require(body, "min_x")), minZ = RInt(Require(body, "min_z")), maxX = RInt(Require(body, "max_x")), maxZ = RInt(Require(body, "max_z"));
                return new Dict { ["id"] = regionId, ["type"] = "rectangle", ["bounds_2d"] = Bounds(minX, minZ, maxX, maxZ) };
            }
            case "cuboid":
            {
                double minX = RInt(Require(body, "min_x")), minZ = RInt(Require(body, "min_z")), maxX = RInt(Require(body, "max_x")), maxZ = RInt(Require(body, "max_z"));
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
                // A <block> is an integer block coordinate; a <point> is a free vector (PGM parses it raw),
                // so a player spawn keeps its block-centre .5 — only blocks are rounded.
                bool isBlock = type == "block";
                double px = Coord(Require(body, "x"), isBlock), pz = Coord(Require(body, "z"), isBlock), py = Coord(body.GetValueOrDefault("y") ?? 64L, isBlock);
                var bounds = isBlock ? Bounds(px, pz, px + 1, pz + 1) : Bounds(px - 0.5, pz - 0.5, px + 0.5, pz + 0.5);
                return new Dict { ["id"] = regionId, ["type"] = type, ["position"] = new Dict { ["x"] = px, ["y"] = py, ["z"] = pz }, ["bounds_2d"] = bounds };
            }
            case "cylinder":
            {
                double bx = F(Require(body, "base_x")), bz = F(Require(body, "base_z")), by = F(body.GetValueOrDefault("base_y") ?? 64L), r = F(Require(body, "radius")), h = F(body.GetValueOrDefault("height") ?? 10L);
                return new Dict { ["id"] = regionId, ["type"] = "cylinder", ["base"] = new Dict { ["x"] = bx, ["y"] = by, ["z"] = bz }, ["radius"] = r, ["height"] = h, ["bounds_2d"] = Bounds(bx - r, bz - r, bx + r, bz + r) };
            }
            case "circle":
            {
                double cx = F(Require(body, "center_x")), cz = F(Require(body, "center_z")), r = F(Require(body, "radius"));
                return new Dict { ["id"] = regionId, ["type"] = "circle", ["center"] = new Dict { ["x"] = cx, ["z"] = cz }, ["radius"] = r, ["bounds_2d"] = Bounds(cx - r, cz - r, cx + r, cz + r) };
            }
            default: throw EditException.Unreadable($"unsupported type '{type}'", "type");
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

    /// <summary>Set the numbers a coords body names, in place, and recompute <c>bounds_2d</c> from them.
    /// Returns the new footprint, or null where the body moved nothing horizontal. Every number a region
    /// carries is reachable here, so this is the one writer a patch goes through.</summary>
    public static Dict? ApplyCoordUpdate(Dict region, string type, Dict coords)
    {
        switch (type)
        {
            case "rectangle":
            {
                var b = (Dict)region["bounds_2d"]!;
                var mn = (Dict)b["min"]!; var mx = (Dict)b["max"]!;
                if (Named(coords, "min_x") is { } nx) mn["x"] = nx;
                if (Named(coords, "min_z") is { } nz) mn["z"] = nz;
                if (Named(coords, "max_x") is { } xx) mx["x"] = xx;
                if (Named(coords, "max_z") is { } xz) mx["z"] = xz;
                var nb = Bounds(F(mn["x"]), F(mn["z"]), F(mx["x"]), F(mx["z"]));
                region["bounds_2d"] = nb;
                return nb;
            }
            case "cuboid":
            {
                var mn = (Dict)region["min"]!; var mx = (Dict)region["max"]!;
                foreach (var axis in new[] { "x", "y", "z" })
                {
                    if (Named(coords, $"min_{axis}") is { } lo) mn[axis] = lo;
                    if (Named(coords, $"max_{axis}") is { } hi) mx[axis] = hi;
                }
                // Only the horizontal pair moves the footprint; a Y-only patch leaves bounds_2d as it was.
                if (Named(coords, "min_x") is null && Named(coords, "max_x") is null
                    && Named(coords, "min_z") is null && Named(coords, "max_z") is null) return null;
                var nb = Bounds(F(mn["x"]), F(mn["z"]), F(mx["x"]), F(mx["z"]));
                region["bounds_2d"] = nb;
                return nb;
            }
            case "cylinder":
            {
                var baseD = Ensure(region, "base");
                if (Named(coords, "base_x") is { } bx0) baseD["x"] = bx0;
                if (Named(coords, "base_y") is { } by0) baseD["y"] = by0;
                if (Named(coords, "base_z") is { } bz0) baseD["z"] = bz0;
                if (Named(coords, "radius") is { } cr) region["radius"] = cr;
                if (Named(coords, "height") is { } ch) region["height"] = ch;
                double bx = F(baseD.GetValueOrDefault("x") ?? 0L), bz = F(baseD.GetValueOrDefault("z") ?? 0L), r = F(region.GetValueOrDefault("radius") ?? 0L);
                var nb = Bounds(bx - r, bz - r, bx + r, bz + r); region["bounds_2d"] = nb; return nb;
            }
            case "circle":
            {
                var center = Ensure(region, "center");
                if (Named(coords, "center_x") is { } cx0) center["x"] = cx0;
                if (Named(coords, "center_z") is { } cz0) center["z"] = cz0;
                if (Named(coords, "radius") is { } rr) region["radius"] = rr;
                double cx = F(center.GetValueOrDefault("x") ?? 0L), cz = F(center.GetValueOrDefault("z") ?? 0L), r = F(region.GetValueOrDefault("radius") ?? 0L);
                var nb = Bounds(cx - r, cz - r, cx + r, cz + r); region["bounds_2d"] = nb; return nb;
            }
            case "block" or "point":
            {
                var pos = Ensure(region, "position");
                foreach (var k in new[] { "x", "y", "z" }) if (Named(coords, k) is { } v) pos[k] = v;
                double px = F(pos.GetValueOrDefault("x") ?? 0L), pz = F(pos.GetValueOrDefault("z") ?? 0L);
                var nb = type == "block" ? Bounds(px, pz, px + 1, pz + 1) : Bounds(px - 0.5, pz - 0.5, px + 0.5, pz + 0.5);
                region["bounds_2d"] = nb; return nb;
            }
            default: return null;
        }
    }

    /// <summary>The number a create's coords body gave for a key the chosen type cannot do without —
    /// <c>RQ1</c> where it named none, so a type left short of a number says which one rather than building
    /// a region at the origin.</summary>
    private static object? Require(Dict coords, string key)
        => coords.GetValueOrDefault(key) ?? throw EditException.Unreadable($"coords.{key} required", $"coords.{key}");

    /// <summary>The value a coords body gave for a key, or null where it named none. An explicit
    /// <c>null</c> reads the same as an absent key: a number that is not there is one the patch leaves
    /// alone, and a record serialized with its unset fields written out would otherwise blank every number
    /// it did not mean to set.</summary>
    private static object? Named(Dict coords, string key) => coords.GetValueOrDefault(key);

    private static Dict Ensure(Dict d, string k) { if (d.GetValueOrDefault(k) is not Dict sub) { sub = new Dict(); d[k] = sub; } return sub; }
    private static double F(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, string s when double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p) => p, _ => 0 };
    private static double RInt(object? v) => Math.Round(F(v), MidpointRounding.ToEven);
    // Block coords round to whole blocks; point coords stay as authored (keep the .5 block-centre).
    private static double Coord(object? v, bool isBlock) => isBlock ? RInt(v) : F(v);
}
