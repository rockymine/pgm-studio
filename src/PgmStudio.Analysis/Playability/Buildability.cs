using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using PgmStudio.Analysis.Region;

namespace PgmStudio.Analysis.Playability;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Per-column buildability verdict (port of studio/services/buildability.py, C14): region geometry
/// × the Y=0 layer × apply-rule order (last rule wins). Verdict codes index <see cref="Classes"/>.
/// </summary>
public static class Buildability
{
    public static readonly string[] Classes = ["buildable", "never", "void_denied", "restricted"];

    /// <summary>Canonical legend colours (allow/deny story), shared by API + UI overlay.</summary>
    public static readonly Dictionary<string, string> ClassColors = new()
    {
        ["buildable"] = "#4caf50", ["never"] = "#c62828", ["void_denied"] = "#f57c00", ["restricted"] = "#fbc02d",
    };
    private const byte Never = 1, Void = 2, Restricted = 3;
    private static readonly string[] BlockEvents = ["block_place", "block"];

    public sealed record Result(
        int MinX, int MinZ, int MaxX, int MaxZ, int Width, int Height,
        byte[] Verdict, Dictionary<string, int> Counts, bool HasY0);

    /// <summary>Classify a block-filter value → never | void | allow | other.</summary>
    public static string ClassifyFilter(string value, Dict filters, HashSet<string>? seen = null)
    {
        seen ??= [];
        if (string.IsNullOrEmpty(value) || seen.Contains(value)) return "other";
        if (value == "never") return "never";
        if (value is "always" or "allow") return "allow";
        if (value == "deny(void)" || (value.Contains("void") && !filters.ContainsKey(value))) return "void";
        if (filters.GetValueOrDefault(value) is not Dict f) return "other";
        var t = f.GetValueOrDefault("type") as string;
        if (t == "void") return "void";
        if (t == "never") return "never";
        var next = new HashSet<string>(seen) { value };
        if (t is "not" or "deny" or "allow") return ClassifyFilter(f.GetValueOrDefault("child") as string ?? "", filters, next);
        if (t is "any" or "all" or "one")
        {
            var kinds = AsList(f.GetValueOrDefault("children")).Select(c => ClassifyFilter(c as string ?? "", filters, next)).ToList();
            if (kinds.Contains("void")) return "void";
            if (kinds.Contains("never")) return "never";
        }
        return "other";
    }

    public static (int minX, int minZ, int maxX, int maxZ) RegionBbox(Dict data, int margin)
    {
        var xs = new List<double>();
        var zs = new List<double>();
        foreach (var r in AsDict(data.GetValueOrDefault("regions")).Values.OfType<Dict>())
        {
            if (AsDict(r.GetValueOrDefault("bounds_2d")) is { Count: > 0 } b)
            {
                var mn = AsDict(b.GetValueOrDefault("min"));
                var mx = AsDict(b.GetValueOrDefault("max"));
                if (Num(mn.GetValueOrDefault("x")) is { } a && Num(mn.GetValueOrDefault("z")) is { } c
                    && Num(mx.GetValueOrDefault("x")) is { } d && Num(mx.GetValueOrDefault("z")) is { } e)
                { xs.Add(a); xs.Add(d); zs.Add(c); zs.Add(e); }
            }
        }
        if (xs.Count == 0) return (-64, -64, 64, 64);
        return ((int)xs.Min() - margin, (int)zs.Min() - margin, (int)xs.Max() + margin, (int)zs.Max() + margin);
    }

    public static Result Compute(Dict data, HashSet<(int, int)>? y0Columns,
        (int minX, int minZ, int maxX, int maxZ)? bbox = null, int margin = 16)
    {
        var regions = AsDict(data.GetValueOrDefault("regions"));
        var filters = AsDict(data.GetValueOrDefault("filters"));
        var rules = AsList(data.GetValueOrDefault("apply_rules")).OfType<Dict>().ToList();

        var (minX, minZ, maxX, maxZ) = bbox ?? RegionBbox(data, margin);
        int nx = maxX - minX, nz = maxZ - minZ;
        var n = nx * nz;
        var boundsD = ((double)minX, (double)minZ, (double)maxX, (double)maxZ);

        var hasY0 = y0Columns is not null;
        bool[]? voidMask = null;
        if (hasY0)
        {
            voidMask = new bool[n];
            Array.Fill(voidMask, true);                  // void everywhere…
            foreach (var (x, z) in y0Columns!)           // …except columns with a Y=0 block
            {
                int ix = x - minX, iz = z - minZ;
                if (ix >= 0 && ix < nx && iz >= 0 && iz < nz) voidMask[iz * nx + ix] = false;
            }
        }

        var verdict = new byte[n];

        bool[]? Mask(object? refVal)
        {
            if (refVal is null) { var all = new bool[n]; Array.Fill(all, true); return all; }
            var region = refVal is string s ? regions.GetValueOrDefault(s) as Dict : refVal as Dict;
            var geom = RegionGeometry2d.ToGeometry(region, boundsD, regions);
            if (geom is null || geom.IsEmpty) return null;
            var prep = PreparedGeometryFactory.Prepare(geom);
            var mask = new bool[n];
            for (var iz = 0; iz < nz; iz++)
            for (var ix = 0; ix < nx; ix++)
                mask[iz * nx + ix] = prep.CoversCell(minX + ix, minZ + iz);
            return mask;
        }

        foreach (var rule in rules)
        {
            var inreg = Mask(rule.GetValueOrDefault("region"));
            if (inreg is null) continue;
            foreach (var ev in BlockEvents)
            {
                if (rule.GetValueOrDefault(ev) is not string val || val.Length == 0) continue;
                if (regions.ContainsKey(val))
                {
                    var gate = Mask(val);
                    if (gate is not null)
                        for (var i = 0; i < n; i++) if (inreg[i] && !gate[i]) verdict[i] = Never;
                    continue;
                }
                var kind = ClassifyFilter(val, filters);
                if (kind == "never")
                    for (var i = 0; i < n; i++) { if (inreg[i]) verdict[i] = Never; }
                else if (kind == "void" && voidMask is not null)
                    for (var i = 0; i < n; i++) { if (inreg[i] && voidMask[i]) verdict[i] = Void; }
                else if (kind == "other")
                    for (var i = 0; i < n; i++) { if (inreg[i]) verdict[i] = Restricted; }
            }
        }

        var counts = new Dictionary<string, int>();
        for (byte c = 0; c < Classes.Length; c++) counts[Classes[c]] = verdict.Count(v => v == c);
        return new Result(minX, minZ, maxX, maxZ, nx, nz, verdict, counts, hasY0);
    }

    private static Dict AsDict(object? o) => o as Dict ?? new Dict();
    private static List<object?> AsList(object? o) => o as List<object?> ?? [];
    private static double? Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => null };
}
