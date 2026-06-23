using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Api.Services;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Setup-activity backend — the island-exclusion + symmetry-confirm slice of the per-map scan
/// configuration (the <c>map_config_json</c> artifact: <c>exclude_islands</c>, plus the studio-chosen
/// <c>scan_layer</c> marker written at world-load). Detection runs on the fixed cleaned-base layer
/// (no user scan-layer or block-exclusion choice, and no world re-scan): excluding an island only
/// recomputes symmetry from the already-detected <c>islands_json</c>.
/// </summary>
internal static class ConfigureStore
{
    public static async Task<JsonObject> LoadAsync(PgmDb db, long mapId, CancellationToken ct)
    {
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.MapConfigJson, ct);
        if (art is not null && JsonNode.Parse(Encoding.UTF8.GetString(art.Data)) is JsonObject o) return o;
        return new JsonObject
        {
            ["scan_layer"] = "surface",
            ["exclude_blocks"] = new JsonArray(),
            ["exclude_islands"] = new JsonArray(),
            ["scan_layer_confirmed"] = false,
        };
    }

    public static async Task SaveAsync(PgmDb db, long mapId, JsonObject cfg, CancellationToken ct)
    {
        await db.Artifacts.Where(a => a.MapId == mapId && a.Kind == ArtifactKind.MapConfigJson).DeleteAsync(ct);
        await db.InsertAsync(new MapArtifactRow { MapId = mapId, Kind = ArtifactKind.MapConfigJson, Data = Encoding.UTF8.GetBytes(cfg.ToJsonString()) }, token: ct);
    }
}

/// <summary>GET /api/configure/{slug}/state — the current scan configuration.</summary>
public sealed class ConfigureStateEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/configure/{slug}/state"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var cfg = await ConfigureStore.LoadAsync(db, map.Id, ct);

        // Step 3 = symmetry: configure is complete once the user confirms/rejects the detection.
        var symRow = await SymmetryStore.LoadAsync(db, map.Id, ct);
        var symmetryStatus = symRow?.Status ?? "unconfirmed";

        await Send.OkAsync(new Dict
        {
            ["scan_layer"] = (cfg["scan_layer"]?.GetValue<string>()) ?? "surface",
            ["exclude_blocks"] = cfg["exclude_blocks"]?.AsArray().Select(n => (object?)n!.GetValue<int>()).ToList() ?? new(),
            ["exclude_islands"] = cfg["exclude_islands"]?.AsArray().Select(n => (object?)n!.GetValue<int>()).ToList() ?? new(),
            ["symmetry_status"] = symmetryStatus,
            ["configure_complete"] = symmetryStatus != "unconfirmed",
        }, ct);
    }
}

/// <summary>PATCH /api/configure/{slug}/exclude-island — toggle one island's exclusion.</summary>
public sealed class ConfigureExcludeIslandEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/configure/{slug}/exclude-island"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        using var r = new StreamReader(HttpContext.Request.Body);
        var body = JsonDocument.Parse(await r.ReadToEndAsync(ct)).RootElement;
        var islandId = body.GetProperty("island_id").GetInt32();
        var excluded = body.GetProperty("excluded").GetBoolean();

        var cfg = await ConfigureStore.LoadAsync(db, map.Id, ct);
        var list = cfg["exclude_islands"]?.AsArray() ?? new JsonArray();
        var ids = list.Select(n => n!.GetValue<int>()).Where(i => i != islandId).ToList();
        if (excluded) ids.Add(islandId);
        cfg["exclude_islands"] = new JsonArray(ids.OrderBy(i => i).Select(i => (JsonNode)i).ToArray());

        await ConfigureStore.SaveAsync(db, map.Id, cfg, ct);
        // Excluded islands feed symmetry detection — drop the cached result so step 3 recomputes.
        await SymmetryStore.DeleteAsync(db, map.Id, ct);
        await Send.OkAsync(new Dict { ["ok"] = true }, ct);
    }
}

/// <summary>Resolves an already-ingested surface layer for a map (no world access).</summary>
internal static class ConfigureLayers
{
    /// <summary>The cells for a layer type from the ingested artifacts, or null when not cached. The
    /// studio-chosen <c>scan_layer</c> is served from the canonical <c>layer.parquet</c>; any other type
    /// from its per-type cache if one was stored. Never scans the world — the hosted tier has no
    /// <c>.mca</c> files, and per-map re-scan/re-detection is out of scope.</summary>
    public static async Task<List<SurfaceCell>?> CellsAsync(PgmDb db, long mapId, string layerType, CancellationToken ct)
    {
        var cfg = await ConfigureStore.LoadAsync(db, mapId, ct);
        var scanLayer = cfg["scan_layer"]?.GetValue<string>() ?? "surface";

        if (layerType == scanLayer)
        {
            var canon = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.LayerParquet, ct);
            if (canon is not null) return await SurfaceLayer.ReadAsync(canon.Data);
        }

        var cached = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == $"layer_{layerType}_parquet", ct);
        return cached is not null ? await SurfaceLayer.ReadAsync(cached.Data) : null;
    }
}
