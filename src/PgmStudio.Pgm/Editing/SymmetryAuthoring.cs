namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Symmetry-aware authoring (F3, port of studio/services/symmetry_authoring.py): create the
/// counterpart(s) of a source region for a symmetry mode about the centre.
/// - reflections (mirror_x/z/d1/d2) persist as a native PGM <c>mirror</c> region chained by source_id;
/// - rot_180 = two perpendicular mirrors (so it emits two chained mirror regions);
/// - rot_90 has no PGM rotation type, so it <b>bakes</b> a concrete primitive with rotated geometry.
/// New regions are tracked as category "other" (until wired). n-fold rot_n (n∉{2,4}) is unsupported.
/// </summary>
public static class SymmetryAuthoring
{
    private static readonly Dictionary<string, (double nx, double nz)> ModeNormals = new()
    {
        ["mirror_x"] = (1.0, 0.0), ["mirror_z"] = (0.0, 1.0), ["mirror_d1"] = (1.0, -1.0), ["mirror_d2"] = (1.0, 1.0),
    };
    private static readonly HashSet<string> Bakeable = ["rectangle", "cuboid", "cylinder", "circle", "sphere", "point", "block"];

    /// <summary>Create the counterpart(s) of <paramref name="sourceId"/>; returns {counterpart, created:[ids]}.</summary>
    public static Dict CreateCounterpart(Dict data, string sourceId, string mode, double cx, double cz, string category = "other")
    {
        var regions = Regions(data);
        if (!regions.ContainsKey(sourceId)) throw EditException.BadRequest($"source region '{sourceId}' not found");

        if (ModeNormals.ContainsKey(mode))
        {
            var rid = MakeMirror(data, sourceId, mode, cx, cz, category);
            return Result(rid, rid);
        }
        if (mode == "rot_180")
        {
            var m1 = MakeMirror(data, sourceId, "mirror_x", cx, cz, category);
            var m2 = MakeMirror(data, m1, "mirror_z", cx, cz, category);   // ⟂ composition = 180° turn
            return Result(m2, m1, m2);
        }
        if (mode == "rot_90")
        {
            var rid = BakeRot90(data, sourceId, cx, cz, category);
            return Result(rid, rid);
        }
        throw EditException.BadRequest(
            $"unsupported mode '{mode}' (n-fold rot_n is out of scope; use mirror_x/z/d1/d2, rot_180, or rot_90)");
    }

    /// <summary>
    /// Fill the symmetry orbit of <paramref name="sourceId"/> (F3): create every counterpart implied by
    /// the symmetry mode so an authored region appears in all symmetric positions at once. rot_90 chains
    /// 3 quarter turns (1→4); mirrors and rot_180 add a single counterpart (1→2). Counterparts inherit
    /// <paramref name="category"/> so they show in the step that drew the source. Returns {created:[ids]}.
    /// </summary>
    public static Dict CreateOrbit(Dict data, string sourceId, string mode, double cx, double cz, string category = "other")
    {
        var regions = Regions(data);
        if (!regions.ContainsKey(sourceId)) throw EditException.BadRequest($"source region '{sourceId}' not found");

        var created = new List<object?>();
        if (mode == "rot_90")
        {
            var cur = sourceId;                                   // chain quarter turns: 90°, 180°, 270°
            for (var i = 0; i < 3; i++)
            {
                var r = CreateCounterpart(data, cur, "rot_90", cx, cz, category);
                cur = (string)r["counterpart"]!;
                created.AddRange((List<object?>)r["created"]!);
            }
        }
        else
        {
            var r = CreateCounterpart(data, sourceId, mode, cx, cz, category);
            created.AddRange((List<object?>)r["created"]!);
        }
        return new Dict { ["created"] = created };
    }

    private static string MakeMirror(Dict data, string sourceId, string mode, double cx, double cz, string category = "other")
    {
        var regions = Regions(data);
        var (nx, nz) = ModeNormals[mode];
        var src = (Dict)regions[sourceId]!;
        var newId = FreshId(regions, "mirror");
        var region = new Dict
        {
            ["id"] = newId, ["type"] = "mirror", ["source_id"] = sourceId,
            ["origin"] = new Dict { ["x"] = cx, ["y"] = 0.0, ["z"] = cz },
            ["normal"] = new Dict { ["x"] = nx, ["y"] = 0.0, ["z"] = nz },
        };
        if (src.GetValueOrDefault("bounds_2d") is Dict b) region["bounds_2d"] = Geometry2d.ReflectBounds2d(b, nx, nz, cx, cz);
        regions[newId] = region;
        Track(data, newId, category);
        return newId;
    }

    private static string BakeRot90(Dict data, string sourceId, double cx, double cz, string category = "other")
    {
        var regions = Regions(data);
        var src = (Dict)regions[sourceId]!;
        var stype = src.GetValueOrDefault("type") as string ?? "";
        if (!Bakeable.Contains(stype))
            throw EditException.BadRequest($"rot_90 bake not supported for region type '{stype}' (primitives only; group/transform sources are out of scope)");

        var newId = FreshId(regions, stype);
        var region = new Dict { ["id"] = newId, ["type"] = stype };

        Dict Rot(Dict pt)
        {
            var (rx, rz) = Geometry2d.RotatePoint(Num(pt.GetValueOrDefault("x")), Num(pt.GetValueOrDefault("z")), 90, cx, cz);
            var o = new Dict { ["x"] = rx, ["z"] = rz };
            if (pt.ContainsKey("y")) o["y"] = pt["y"];
            return o;
        }

        switch (stype)
        {
            case "rectangle": break;   // geometry is bounds_2d only
            case "cuboid":
            {
                var mn = (Dict)src["min"]!; var mx = (Dict)src["max"]!;
                var rb = Geometry2d.RotateBounds2d(
                    Geometry2d.Bounds(Num(mn["x"]), Num(mn["z"]), Num(mx["x"]), Num(mx["z"])), 90, cx, cz);
                var rmn = (Dict)rb["min"]!; var rmx = (Dict)rb["max"]!;
                region["min"] = new Dict { ["x"] = rmn["x"], ["y"] = mn.GetValueOrDefault("y"), ["z"] = rmn["z"] };
                region["max"] = new Dict { ["x"] = rmx["x"], ["y"] = mx.GetValueOrDefault("y"), ["z"] = rmx["z"] };
                break;
            }
            case "cylinder":
                region["base"] = Rot((Dict)src["base"]!); region["radius"] = src["radius"];
                if (src.ContainsKey("height")) region["height"] = src["height"];
                break;
            case "circle":
                region["center"] = Rot((Dict)src["center"]!); region["radius"] = src["radius"];
                break;
            case "sphere":
                region["origin"] = Rot((Dict)src["origin"]!); region["radius"] = src["radius"];
                break;
            default:   // point, block
                region["position"] = Rot((Dict)src["position"]!);
                break;
        }

        if (src.GetValueOrDefault("bounds_2d") is Dict b) region["bounds_2d"] = Geometry2d.RotateBounds2d(b, 90, cx, cz);
        regions[newId] = region;
        Track(data, newId, category);
        return newId;
    }

    private static Dict Result(string counterpart, params string[] created)
        => new() { ["counterpart"] = counterpart, ["created"] = created.Cast<object?>().ToList() };

    private static Dict Regions(Dict data)
    {
        if (data.GetValueOrDefault("regions") is not Dict r) { r = new Dict(); data["regions"] = r; }
        return r;
    }

    private static string FreshId(Dict regions, string prefix)
    {
        var i = 1; while (regions.ContainsKey($"{prefix}_{i}")) i++; return $"{prefix}_{i}";
    }

    private static void Track(Dict data, string id, string category)
    {
        if (data.GetValueOrDefault("region_categories") is not Dict cats) { cats = new Dict(); data["region_categories"] = cats; }
        var key = string.IsNullOrWhiteSpace(category) ? "other" : category;
        if (cats.GetValueOrDefault(key) is not List<object?> list) { list = []; cats[key] = list; }
        list.Add(id);
    }

    private static double Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => 0 };
}
