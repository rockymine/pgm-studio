using NetTopologySuite.Geometries;
using PgmStudio.Domain;
using PgmStudio.Analysis.Scan;
using PgmStudio.Analysis.Region;

namespace PgmStudio.Analysis.Playability;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Wool source detection + availability. Operates on
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
    /// <summary>What one monument's block is, in the terms a wool is placed in. A wool goes <b>into</b> the
    /// block, so it needs both halves: <paramref name="Clear"/> that nothing already stands in it, and
    /// <paramref name="Support"/> that a block touches one of its six faces, since a block is placed against
    /// any face of a neighbour. <paramref name="Pedestal"/> says the support is the one below, which is the
    /// shape a monument is normally built in. The same answer <c>GET /map/{slug}/block-seat</c> gives for a
    /// single block while a monument is being placed by hand.</summary>
    public sealed record MonumentSeat(string WoolColor, string Team, string MonumentId, int X, int Y, int Z,
        bool Clear, bool Support, bool Pedestal, string Severity, string Message);

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
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var bbox = mapBbox ?? MapBbox(regions);
        var renewable = RenewableGeoms(data, mapBbox);
        var physical = sources.Where(s => s.Type != "pgm_spawner").ToList();
        var pgmColors = sources.Where(s => s.Type == "pgm_spawner").Select(s => s.Color).ToHashSet();
        var dyeColors = IndirectDyeColors(data);
        var outp = new List<AvailabilityEntry>();

        foreach (var w in MapDoc.AsList(data.GetValueOrDefault("wools")).OfType<Dict>())
        {
            var color = BlockColors.Normalize(w.GetValueOrDefault("color") as string ?? "");
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
        var declared = MapDoc.AsList(data.GetValueOrDefault("wools")).OfType<Dict>()
            .Select(w => BlockColors.Normalize(w.GetValueOrDefault("color") as string ?? "")).ToHashSet();
        var renewable = RenewableGeoms(data, mapBbox);
        return SummarizeSources(sources, null, renewable)
            .Where(e => !declared.Contains(e.Color))
            .Select(e => new Suggestion(e.Color, e.Total, e.SourceTypes)).ToList();
    }

    /// <summary>Whether each wool monument's block can hold the wool won on it: clear, and standing on
    /// something. A column with no scan says neither, and is reported as seated rather than as a fault — a
    /// map the studio has not read must not come back as a map with a hundred bad monuments.</summary>
    public static List<MonumentSeat> CheckMonumentSeats(Dict data, SegmentIndex? segments)
    {
        var outp = new List<MonumentSeat>();
        foreach (var w in MapDoc.AsList(data.GetValueOrDefault("wools")).OfType<Dict>())
        {
            var color = BlockColors.Normalize(w.GetValueOrDefault("color") as string ?? "");
            foreach (var m in MapDoc.AsList(w.GetValueOrDefault("monuments")).OfType<Dict>())
            {
                var loc = MapDoc.AsDict(m.GetValueOrDefault("location"));
                if (MapDoc.Num(loc.GetValueOrDefault("x")) is not { } lx || MapDoc.Num(loc.GetValueOrDefault("y")) is not { } ly || MapDoc.Num(loc.GetValueOrDefault("z")) is not { } lz)
                    continue;
                // Floored, never cast: PGM reads this block through `BlockRegion`'s getBlockX/Y/Z, which
                // floor, and the studio stores the monument's coordinate raw because of it
                // (docs/pgm/new-map-authoring.md §4). A cast truncates toward zero, so a monument written at
                // the block centre — `46.5,10,-191.5`, the corpus idiom — lands a block off in x or z
                // wherever the coordinate is negative, and the column read is the neighbour's.
                int x = (int)Math.Floor(lx), y = (int)Math.Floor(ly), z = (int)Math.Floor(lz);
                // A map with no terrain layer, and a column the scan never reached inside one, say the same
                // thing: nothing. Both are reported as seated rather than as a fault.
                var unread = segments is null || !segments.Scanned(x, z);
                var clear = unread || segments!.IsAir(x, y, z);
                var pedestal = unread || segments!.IsSolid(x, y - 1, z);
                // A block is placed against any face of a neighbour, so all six are asked. A monument hung
                // from a ceiling or set into a wall plays exactly like one on a pedestal.
                var support = unread || pedestal
                    || segments!.IsSolid(x, y + 1, z)
                    || segments.IsSolid(x - 1, y, z) || segments.IsSolid(x + 1, y, z)
                    || segments.IsSolid(x, y, z - 1) || segments.IsSolid(x, y, z + 1);

                var (severity, sentence) = (clear, support) switch
                {
                    (false, _) => ("error", "is obstructed by a block — the wool can't be placed (PGM warns on load); clear it to air"),
                    (_, false) => ("error", "has nothing on any of its six faces — a wool is placed against a block, and there is none to place against"),
                    _ => ("ok", pedestal ? "is clear, and stands on a pedestal" : "is clear, and is placed against the block that holds it"),
                };
                outp.Add(new MonumentSeat(color, m.GetValueOrDefault("team") as string ?? "", m.GetValueOrDefault("id") as string ?? "",
                    x, y, z, clear, support, pedestal, severity, $"{color} monument at ({x},{y},{z}) {sentence}"));
            }
        }
        return outp;
    }

    public static List<Source> PgmSpawnerSources(Dict data, (double, double, double, double)? mapBbox = null)
    {
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var bbox = mapBbox ?? MapBbox(regions);
        var outp = new List<Source>();
        foreach (var sp in MapDoc.AsList(data.GetValueOrDefault("spawners")).OfType<Dict>())
        {
            var geoms = new List<Geometry>();
            foreach (var key in new[] { "spawn_region", "player_region" })
                if (sp.GetValueOrDefault(key) is string rid && regions.GetValueOrDefault(rid) is Dict reg
                    && RegionGeometry2d.ToGeometry(reg, bbox, regions) is { IsEmpty: false } g) geoms.Add(g);
            Geometry? geom = geoms.Count == 0 ? null : geoms.Aggregate((a, b) => a.Union(b));
            int cx = geom is null ? 0 : (int)Math.Round(geom.Centroid.X, MidpointRounding.ToEven);
            int cz = geom is null ? 0 : (int)Math.Round(geom.Centroid.Y, MidpointRounding.ToEven);
            foreach (var item in MapDoc.AsList(sp.GetValueOrDefault("items")).OfType<Dict>())
            {
                if (!((item.GetValueOrDefault("material") as string ?? "").ToLowerInvariant().Contains("wool"))) continue;
                if (MapDoc.Num(item.GetValueOrDefault("damage")) is not { } dmg) continue;
                if (!BlockColors.BlockDamageToColor.TryGetValue((int)dmg, out var color)) continue;
                var count = MapDoc.Num(item.GetValueOrDefault("amount")) is { } a and not 0 ? (int)a : 1;
                outp.Add(new Source("pgm_spawner", color, cx, 0, cz, count, geom));
            }
        }
        return outp;
    }

    public static HashSet<string> IndirectDyeColors(Dict data)
    {
        var outp = new HashSet<string>();
        foreach (var sp in MapDoc.AsList(data.GetValueOrDefault("spawners")).OfType<Dict>())
            foreach (var item in MapDoc.AsList(sp.GetValueOrDefault("items")).OfType<Dict>())
            {
                var mat = (item.GetValueOrDefault("material") as string ?? "").ToLowerInvariant();
                if (mat.Contains("wool")) continue;
                if (!mat.Contains("ink") && !mat.Contains("dye")) continue;
                var color = MapDoc.Num(item.GetValueOrDefault("damage")) is { } d ? BlockColors.DyeDamageToColor.GetValueOrDefault((int)d) : "black";
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
            var b = MapDoc.AsDict(r.GetValueOrDefault("bounds_2d"));
            if (b.Count == 0) continue;
            var mn = MapDoc.AsDict(b.GetValueOrDefault("min")); var mx = MapDoc.AsDict(b.GetValueOrDefault("max"));
            if (MapDoc.Num(mn.GetValueOrDefault("x")) is { } a && MapDoc.Num(mn.GetValueOrDefault("z")) is { } c
                && MapDoc.Num(mx.GetValueOrDefault("x")) is { } d && MapDoc.Num(mx.GetValueOrDefault("z")) is { } e)
            { xs.Add(a); xs.Add(d); zs.Add(c); zs.Add(e); }
        }
        if (xs.Count == 0) return (-256, -256, 256, 256);
        return (xs.Min() - 8, zs.Min() - 8, xs.Max() + 8, zs.Max() + 8);
    }

    internal static List<Geometry> RenewableGeoms(Dict data, (double, double, double, double)? mapBbox = null)
    {
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var bbox = mapBbox ?? MapBbox(regions);
        var geoms = new List<Geometry>();
        foreach (var rn in MapDoc.AsList(data.GetValueOrDefault("renewables")).OfType<Dict>())
            if (rn.GetValueOrDefault("region_id") is string rid && regions.GetValueOrDefault(rid) is Dict reg
                && RegionGeometry2d.ToGeometry(reg, bbox, regions) is { IsEmpty: false } g) geoms.Add(g);
        return geoms;
    }

    private static bool InRegion(Source s, Geometry? geom)
    {
        if (geom is null) return true;
        if (s.Geom is not null) return s.Geom.Intersects(geom);          // pgm spawner covers a region
        return geom.CoversCell(s.X, s.Z);                                // physical point
    }

    private static bool IsRenewable(Source s, List<Geometry> renewableGeoms)
    {
        if (s.Type is "spawner" or "pgm_spawner") return true;
        if (s.Type == "block") return renewableGeoms.Any(g => InRegion(s, g));
        return false;
    }

}
