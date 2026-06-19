using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Layer;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>Shared surface-parquet → pixels / block-types projection (B4 + B9).</summary>
internal static class LayerData
{
    /// <summary>Parallel xs/zs/colors arrays + bounds for a column set (caller ensures non-empty).</summary>
    public static Dict Pixels(IReadOnlyList<SurfaceCell> cells)
    {
        var colorCache = new Dictionary<(int, int), string>();
        var xs = new int[cells.Count];
        var zs = new int[cells.Count];
        var colors = new string[cells.Count];
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        for (var i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            xs[i] = c.X; zs[i] = c.Z;
            var key = (c.BlockId, c.BlockData);
            if (!colorCache.TryGetValue(key, out var hex)) colorCache[key] = hex = BlockColors.Hex(c.BlockId, c.BlockData);
            colors[i] = hex;
            if (c.X < minX) minX = c.X; if (c.X > maxX) maxX = c.X;
            if (c.Z < minZ) minZ = c.Z; if (c.Z > maxZ) maxZ = c.Z;
        }
        return new Dict
        {
            ["xs"] = xs, ["zs"] = zs, ["colors"] = colors,
            ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ,
        };
    }

    /// <summary>One entry per distinct block_id (count summed across data variants, colour/name from
    /// the dominant variant), sorted by count desc. Port of <c>_block_types_from_parquet</c>.</summary>
    public static List<Dict> BlockTypes(IReadOnlyList<SurfaceCell> cells)
    {
        var pairCounts = new Dictionary<(int id, int data), int>();
        foreach (var c in cells) pairCounts[(c.BlockId, c.BlockData)] = pairCounts.GetValueOrDefault((c.BlockId, c.BlockData)) + 1;

        var totals = new Dictionary<int, int>();
        var dominant = new Dictionary<int, (int data, int count)>();
        // Iterate (id, data) ascending so ties pick the lowest data variant (matches pandas stable sort).
        foreach (var ((id, data), count) in pairCounts.OrderBy(kv => kv.Key.id).ThenBy(kv => kv.Key.data))
        {
            totals[id] = totals.GetValueOrDefault(id) + count;
            if (!dominant.TryGetValue(id, out var cur) || count > cur.count) dominant[id] = (data, count);
        }

        return totals.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
            .Select(kv =>
            {
                var data = dominant[kv.Key].data;
                return new Dict
                {
                    ["block_id"] = kv.Key,
                    ["name"] = BlockColors.Name(kv.Key, data),
                    ["color"] = BlockColors.Hex(kv.Key, data),
                    ["count"] = kv.Value,
                };
            }).ToList();
    }
}

/// <summary>
/// GET /api/map/{slug}/layers/top-surface — per-column surface colour overlay (B4). Reads the cached
/// <c>layer.parquet</c> artifact, maps each column's (block_id, block_data) to a hex colour, and
/// returns parallel xs/zs/colors arrays + the bounds. Mirrors the reference <c>layer_top_surface</c>;
/// unblocks the "Blocks" canvas overlay (C6).
/// </summary>
public sealed class TopSurfaceEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/layers/top-surface"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var art = await db.Artifacts.FirstOrDefaultAsync(
            a => a.MapId == map.Id && a.Kind == ArtifactKind.LayerParquet, ct);
        if (art is null) { await Send.NotFoundAsync(ct); return; }

        var cells = await SurfaceLayer.ReadAsync(art.Data);
        if (cells.Count == 0) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(LayerData.Pixels(cells), ct);
    }
}

/// <summary>
/// GET /api/map/{slug}/segments?axis=x|z — side-view depth profile (B5). Projects the map's vertical
/// solid segments onto a 2D (primary × y) grid via <see cref="SideView"/>; feeds the Build-Regions
/// side-view canvas (C7). Mirrors the reference <c>get_segments</c>.
/// </summary>
public sealed class SegmentsEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/segments"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var axis = HttpContext.Request.Query["axis"].ToString();
        if (string.IsNullOrEmpty(axis)) axis = "nz";
        if (axis is not ("x" or "z" or "nz" or "pz" or "nx" or "px"))
        {
            await Send.ResponseAsync(new Dict { ["error"] = "axis must be one of nz/pz/nx/px (or legacy x/z)" }, 400, ct);
            return;
        }

        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        // Optional world-window filter (xmin/xmax/zmin/zmax) → a localised slice for the mini side-view
        // (a point's column + neighbours, or a rectangle's footprint). Absent params = the whole map.
        int? Q(string k) => int.TryParse(HttpContext.Request.Query[k], out var v) ? v : null;
        int? xmin = Q("xmin"), xmax = Q("xmax"), zmin = Q("zmin"), zmax = Q("zmax");
        var q = db.LayerSegments.Where(s => s.MapId == map.Id);
        if (xmin is int a) q = q.Where(s => s.WorldX >= a);
        if (xmax is int b) q = q.Where(s => s.WorldX <= b);
        if (zmin is int c) q = q.Where(s => s.WorldZ >= c);
        if (zmax is int d) q = q.Where(s => s.WorldZ <= d);

        var rows = await q.ToListAsync(ct);
        var result = SideView.Build(rows.Select(r => (r.WorldX, r.WorldZ, r.WorldYStart, r.WorldYEnd)), axis);
        if (result is null) { await Send.ResponseAsync(new Dict { ["error"] = "no segment data" }, 404, ct); return; }

        await Send.OkAsync(new Dict
        {
            ["axis"] = result.Axis,
            ["primary_min"] = result.PrimaryMin,
            ["primary_count"] = result.PrimaryCount,
            ["y_min"] = result.YMin,
            ["y_count"] = result.YCount,
            ["depth"] = result.Depth,
        }, ct);
    }
}
