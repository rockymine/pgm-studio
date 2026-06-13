using NetTopologySuite.Geometries;

namespace PgmStudio.Analysis;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Resource-block detection for renewable auto-config (port of studio/services/resource_sources.py,
/// C17): group iron/gold/diamond blocks by type, each with how many a &lt;renewable&gt; already covers.
/// </summary>
public static class ResourceSources
{
    public sealed record Block(string Type, int X, int Y, int Z);
    public sealed record BlockOut(string Type, int X, int Y, int Z);
    public sealed record TypeSummary(string Type, int Total, int Renewable, bool AllRenewable, List<BlockOut> Sources);

    public static List<(Geometry geom, string renewFilter)> RenewableRegions(Dict data)
    {
        var regions = AsDict(data.GetValueOrDefault("regions"));
        var b = Buildability.RegionBbox(data, 8);
        var bbox = ((double)b.minX, (double)b.minZ, (double)b.maxX, (double)b.maxZ);
        var outp = new List<(Geometry, string)>();
        foreach (var rn in AsList(data.GetValueOrDefault("renewables")).OfType<Dict>())
            if (rn.GetValueOrDefault("region_id") is string rid && regions.GetValueOrDefault(rid) is Dict reg
                && RegionGeometry2d.ToGeometry(reg, bbox, regions) is { IsEmpty: false } g)
                outp.Add((g, rn.GetValueOrDefault("renew_filter") as string ?? ""));
        return outp;
    }

    private static bool RenewMatches(string resourceType, string renewFilter)
        => string.IsNullOrEmpty(renewFilter) || renewFilter.ToLowerInvariant().Contains(resourceType.Replace("_block", ""));

    private static bool Covered(Block block, List<(Geometry geom, string renewFilter)> renewables)
        => renewables.Any(r => RenewMatches(block.Type, r.renewFilter) && r.geom.Contains(new Point(block.X + 0.5, block.Z + 0.5)));

    public static List<TypeSummary> Summarize(IEnumerable<Block> blocks, Geometry? regionGeom, List<(Geometry, string)>? renewables = null)
    {
        renewables ??= [];
        var order = new List<string>();
        var byType = new Dictionary<string, (int total, int renewable, List<BlockOut> srcs)>();
        foreach (var b in blocks)
        {
            if (regionGeom is not null && !regionGeom.Contains(new Point(b.X + 0.5, b.Z + 0.5))) continue;
            if (!byType.TryGetValue(b.Type, out var e)) { e = (0, 0, []); order.Add(b.Type); }
            e.total++;
            if (Covered(b, renewables)) e.renewable++;
            e.srcs.Add(new BlockOut(b.Type, b.X, b.Y, b.Z));
            byType[b.Type] = e;
        }
        return order.Select(t => (t, e: byType[t]))
            .Select(x => new TypeSummary(x.t, x.e.total, x.e.renewable, x.e.renewable == x.e.total, x.e.srcs))
            .OrderBy(e => e.Type, StringComparer.Ordinal).ToList();
    }

    private static Dict AsDict(object? o) => o as Dict ?? new Dict();
    private static List<object?> AsList(object? o) => o as List<object?> ?? [];
}
