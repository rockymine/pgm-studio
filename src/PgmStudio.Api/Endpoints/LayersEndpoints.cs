using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

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
            if (!colorCache.TryGetValue(key, out var hex))
                colorCache[key] = hex = BlockColors.Hex(c.BlockId, c.BlockData);
            colors[i] = hex;
            if (c.X < minX) minX = c.X; if (c.X > maxX) maxX = c.X;
            if (c.Z < minZ) minZ = c.Z; if (c.Z > maxZ) maxZ = c.Z;
        }

        await Send.OkAsync(new Dict
        {
            ["xs"] = xs, ["zs"] = zs, ["colors"] = colors,
            ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ,
        }, ct);
    }
}
