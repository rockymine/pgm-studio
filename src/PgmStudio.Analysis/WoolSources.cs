using NetTopologySuite.Geometries;

namespace PgmStudio.Analysis;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Wool source detection + availability (port of studio/services/wool_sources.py, C12). Operates on
/// a plain source list (block/chest/spawner) + the map doc; the parquet/DB I/O lives in the caller.
/// </summary>
public static class WoolSources
{
    /// <summary>A physical wool source, or a PGM &lt;spawner&gt; module (which carries a region geom).</summary>
    public sealed record Source(string Type, string Color, int X, int Y, int Z, int Count, Geometry? Geom = null);

    public sealed record SourceOut(string Type, string Color, int X, int Y, int Z, int Count);
    public sealed record ColorSummary(string Color, int Total, List<string> SourceTypes, bool Repeatable, bool OneTime, List<SourceOut> Sources);
    public sealed record AvailabilityEntry(string WoolId, string Color, bool Obtainable, bool Repeatable, bool OneTime, string Severity, List<string> SourceTypes, string Message);
    public sealed record Suggestion(string Color, int Total, List<string> SourceTypes);
    public sealed record MonumentCheck(string WoolColor, string Team, string MonumentId, int X, int Y, int Z, bool Obstructed, string Severity, string Message);

    private static readonly GeometryFactory Gf = new();

    // ── summaries ─────────────────────────────────────────────────────────────────

    /// <summary>Wool colours inside a drawn rectangle (world X/Z bounds) — the POST /wool-sources query.
    /// Computes the renewable geoms from the doc, so the caller only supplies the bounds + sources.</summary>
    public static List<ColorSummary> SourcesInRegion(
        Dict data, IEnumerable<Source> sources, double minX, double minZ, double maxX, double maxZ,
        (double, double, double, double)? mapBbox = null) =>
        SummarizeSources(sources, Gf.ToGeometry(new Envelope(minX, maxX, minZ, maxZ)), RenewableGeoms(data, mapBbox));

    public static List<ColorSummary> SummarizeSources(IEnumerable<Source> sources, Geometry? regionGeom, List<Geometry> renewableGeoms)
    {
        var order = new List<string>();
        var byColor = new Dictionary<string, (int total, HashSet<string> types, bool repeatable, List<SourceOut> srcs)>();
        foreach (var s in sources)
        {
            if (!InRegion(s, regionGeom)) continue;
            if (!byColor.TryGetValue(s.Color, out var e)) { e = (0, [], false, []); order.Add(s.Color); }
            e.total += s.Count;
            e.types.Add(s.Type);
            if (IsRenewable(s, renewableGeoms)) e.repeatable = true;
            e.srcs.Add(new SourceOut(s.Type, s.Color, s.X, s.Y, s.Z, s.Count));
            byColor[s.Color] = e;
        }
        return order.Select(c => byColor[c])
            .Zip(order, (e, c) => new ColorSummary(c, e.total, e.types.OrderBy(x => x, StringComparer.Ordinal).ToList(), e.repeatable, !e.repeatable, e.srcs))
            .OrderBy(e => e.Color, StringComparer.Ordinal).ToList();
    }

    public static List<AvailabilityEntry> CheckAvailability(Dict data, List<Source> sources, (double, double, double, double)? mapBbox = null)
    {
        var regions = AsDict(data.GetValueOrDefault("regions"));
        var bbox = mapBbox ?? MapBbox(regions);
        var renewable = RenewableGeoms(data, mapBbox);
        var physical = sources.Where(s => s.Type != "pgm_spawner").ToList();
        var pgmColors = sources.Where(s => s.Type == "pgm_spawner").Select(s => s.Color).ToHashSet();
        var dyeColors = IndirectDyeColors(data);
        var outp = new List<AvailabilityEntry>();

        foreach (var w in AsList(data.GetValueOrDefault("wools")).OfType<Dict>())
        {
            var color = WoolColors.Normalize(w.GetValueOrDefault("color") as string ?? "");
            var roomId = w.GetValueOrDefault("wool_room_region") as string;
            var room = roomId is not null ? regions.GetValueOrDefault(roomId) as Dict : null;
            var roomGeom = room is not null ? RegionGeometry2d.ToGeometry(room, bbox, regions) : null;
            var phys = SummarizeSources(physical, roomGeom, renewable).FirstOrDefault(e => e.Color == color);
            var hasPgm = pgmColors.Contains(color);
            var woolId = w.GetValueOrDefault("id") as string ?? "";

            if (phys is null && !hasPgm)
            {
                if (dyeColors.Contains(color))
                    outp.Add(new AvailabilityEntry(woolId, color, false, false, false, "warning", ["dye_spawner"],
                        $"{color} wool has no direct source, but a dye spawner suggests an indirect sheep/dye mechanic — can't auto-verify; confirm manually"));
                else
                    outp.Add(new AvailabilityEntry(woolId, color, false, false, false, "error", [],
                        $"{color} wool has no obtainable source — add a wool spawner{(room is not null ? "" : " (and declare its wool-room region)")}"));
                continue;
            }

            var types = (phys?.SourceTypes ?? []).Concat(hasPgm ? ["pgm_spawner"] : Array.Empty<string>()).ToList();
            var repeatable = hasPgm || (phys?.Repeatable ?? false);
            var oneTime = !repeatable;
            var joined = string.Join("/", types);
            var message = oneTime
                ? $"{color} wool is obtainable but only one-time ({joined}) — consider a renewable or spawner"
                : $"{color} wool is obtainable ({joined})";
            outp.Add(new AvailabilityEntry(woolId, color, true, repeatable, oneTime, oneTime ? "info" : "ok", types, message));
        }
        return outp;
    }

    public static List<Suggestion> SuggestWools(Dict data, List<Source> sources, (double, double, double, double)? mapBbox = null)
    {
        var declared = AsList(data.GetValueOrDefault("wools")).OfType<Dict>()
            .Select(w => WoolColors.Normalize(w.GetValueOrDefault("color") as string ?? "")).ToHashSet();
        var renewable = RenewableGeoms(data, mapBbox);
        return SummarizeSources(sources, null, renewable)
            .Where(e => !declared.Contains(e.Color))
            .Select(e => new Suggestion(e.Color, e.Total, e.SourceTypes)).ToList();
    }

    public static List<MonumentCheck> CheckMonumentObstruction(Dict data, SegmentIndex? segments)
    {
        var outp = new List<MonumentCheck>();
        foreach (var w in AsList(data.GetValueOrDefault("wools")).OfType<Dict>())
        {
            var color = WoolColors.Normalize(w.GetValueOrDefault("color") as string ?? "");
            foreach (var m in AsList(w.GetValueOrDefault("monuments")).OfType<Dict>())
            {
                var loc = AsDict(m.GetValueOrDefault("location"));
                if (Num(loc.GetValueOrDefault("x")) is not { } lx || Num(loc.GetValueOrDefault("y")) is not { } ly || Num(loc.GetValueOrDefault("z")) is not { } lz)
                    continue;
                int x = (int)lx, y = (int)ly, z = (int)lz;
                var obstructed = segments is not null && segments.IsSolid(x, y, z);
                outp.Add(new MonumentCheck(color, m.GetValueOrDefault("team") as string ?? "", m.GetValueOrDefault("id") as string ?? "",
                    x, y, z, obstructed, obstructed ? "error" : "ok",
                    obstructed
                        ? $"{color} monument at ({x},{y},{z}) is obstructed by a block — the wool can't be placed (PGM warns on load); clear it to air"
                        : $"{color} monument at ({x},{y},{z}) is clear"));
            }
        }
        return outp;
    }

    public static List<Source> PgmSpawnerSources(Dict data, (double, double, double, double)? mapBbox = null)
    {
        var regions = AsDict(data.GetValueOrDefault("regions"));
        var bbox = mapBbox ?? MapBbox(regions);
        var outp = new List<Source>();
        foreach (var sp in AsList(data.GetValueOrDefault("spawners")).OfType<Dict>())
        {
            var geoms = new List<Geometry>();
            foreach (var key in new[] { "spawn_region", "player_region" })
                if (sp.GetValueOrDefault(key) is string rid && regions.GetValueOrDefault(rid) is Dict reg
                    && RegionGeometry2d.ToGeometry(reg, bbox, regions) is { IsEmpty: false } g) geoms.Add(g);
            Geometry? geom = geoms.Count == 0 ? null : geoms.Aggregate((a, b) => a.Union(b));
            int cx = geom is null ? 0 : (int)Math.Round(geom.Centroid.X, MidpointRounding.ToEven);
            int cz = geom is null ? 0 : (int)Math.Round(geom.Centroid.Y, MidpointRounding.ToEven);
            foreach (var item in AsList(sp.GetValueOrDefault("items")).OfType<Dict>())
            {
                if (!((item.GetValueOrDefault("material") as string ?? "").ToLowerInvariant().Contains("wool"))) continue;
                if (Num(item.GetValueOrDefault("damage")) is not { } dmg) continue;
                if (!WoolColors.WoolDamageToColor.TryGetValue((int)dmg, out var color)) continue;
                var count = Num(item.GetValueOrDefault("amount")) is { } a and not 0 ? (int)a : 1;
                outp.Add(new Source("pgm_spawner", color, cx, 0, cz, count, geom));
            }
        }
        return outp;
    }

    public static HashSet<string> IndirectDyeColors(Dict data)
    {
        var outp = new HashSet<string>();
        foreach (var sp in AsList(data.GetValueOrDefault("spawners")).OfType<Dict>())
            foreach (var item in AsList(sp.GetValueOrDefault("items")).OfType<Dict>())
            {
                var mat = (item.GetValueOrDefault("material") as string ?? "").ToLowerInvariant();
                if (mat.Contains("wool")) continue;
                if (!mat.Contains("ink") && !mat.Contains("dye")) continue;
                var color = Num(item.GetValueOrDefault("damage")) is { } d ? WoolColors.DyeDamageToColor.GetValueOrDefault((int)d) : "black";
                if (!string.IsNullOrEmpty(color)) outp.Add(color!);
            }
        return outp;
    }

    // ── geometry helpers ────────────────────────────────────────────────────────────
    internal static (double, double, double, double) MapBbox(Dict regions)
    {
        var xs = new List<double>(); var zs = new List<double>();
        foreach (var r in regions.Values.OfType<Dict>())
        {
            var b = AsDict(r.GetValueOrDefault("bounds_2d"));
            if (b.Count == 0) continue;
            var mn = AsDict(b.GetValueOrDefault("min")); var mx = AsDict(b.GetValueOrDefault("max"));
            if (Num(mn.GetValueOrDefault("x")) is { } a && Num(mn.GetValueOrDefault("z")) is { } c
                && Num(mx.GetValueOrDefault("x")) is { } d && Num(mx.GetValueOrDefault("z")) is { } e)
            { xs.Add(a); xs.Add(d); zs.Add(c); zs.Add(e); }
        }
        if (xs.Count == 0) return (-256, -256, 256, 256);
        return (xs.Min() - 8, zs.Min() - 8, xs.Max() + 8, zs.Max() + 8);
    }

    internal static List<Geometry> RenewableGeoms(Dict data, (double, double, double, double)? mapBbox = null)
    {
        var regions = AsDict(data.GetValueOrDefault("regions"));
        var bbox = mapBbox ?? MapBbox(regions);
        var geoms = new List<Geometry>();
        foreach (var rn in AsList(data.GetValueOrDefault("renewables")).OfType<Dict>())
            if (rn.GetValueOrDefault("region_id") is string rid && regions.GetValueOrDefault(rid) is Dict reg
                && RegionGeometry2d.ToGeometry(reg, bbox, regions) is { IsEmpty: false } g) geoms.Add(g);
        return geoms;
    }

    private static bool InRegion(Source s, Geometry? geom)
    {
        if (geom is null) return true;
        if (s.Geom is not null) return s.Geom.Intersects(geom);          // pgm spawner covers a region
        return geom.Contains(new Point(s.X + 0.5, s.Z + 0.5));            // physical point
    }

    private static bool IsRenewable(Source s, List<Geometry> renewableGeoms)
    {
        if (s.Type is "spawner" or "pgm_spawner") return true;
        if (s.Type == "block") return renewableGeoms.Any(g => InRegion(s, g));
        return false;
    }

    private static Dict AsDict(object? o) => o as Dict ?? new Dict();
    private static List<object?> AsList(object? o) => o as List<object?> ?? [];
    private static double? Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => null };
}
