using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Configure-activity backend — reads/writes the per-map scan configuration (the
/// <c>map_config_json</c> artifact: <c>scan_layer</c>, <c>exclude_blocks</c>, <c>exclude_islands</c>,
/// <c>scan_layer_confirmed</c>). Port of the relevant routes in studio/routes/configure.py — state,
/// scan-layer, exclude-island/-block, and the layer pixels/block-types previews (B9). On-demand
/// re-scan for non-scan layers (y0/bedrock/base) awaits those extractors being ported (P-series).
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
        var symArt = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.SymmetryJson, ct);
        var symmetryStatus = symArt is not null
            ? (System.Text.Json.Nodes.JsonNode.Parse(symArt.Data)?["status"]?.GetValue<string>()) ?? "unconfirmed"
            : "unconfirmed";

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

/// <summary>PATCH /api/configure/{slug}/scan-layer — set scan layer / excluded blocks / confirm.</summary>
public sealed class ConfigureScanLayerEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/configure/{slug}/scan-layer"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var body = await ReadBody(ct);
        var cfg = await ConfigureStore.LoadAsync(db, map.Id, ct);

        if (body.TryGetProperty("scan_layer", out var sl) && sl.ValueKind == JsonValueKind.String) cfg["scan_layer"] = sl.GetString();
        if (body.TryGetProperty("exclude_blocks", out var eb) && eb.ValueKind == JsonValueKind.Array)
            cfg["exclude_blocks"] = new JsonArray(eb.EnumerateArray().Select(x => (JsonNode)x.GetInt32()).ToArray());
        if (body.TryGetProperty("confirmed", out var c) && c.ValueKind is JsonValueKind.True or JsonValueKind.False) cfg["scan_layer_confirmed"] = c.GetBoolean();

        await ConfigureStore.SaveAsync(db, map.Id, cfg, ct);
        await Send.OkAsync(new Dict { ["ok"] = true }, ct);
    }

    private async Task<JsonElement> ReadBody(CancellationToken ct)
    {
        using var r = new StreamReader(HttpContext.Request.Body);
        var s = await r.ReadToEndAsync(ct);
        return string.IsNullOrWhiteSpace(s) ? default : JsonDocument.Parse(s).RootElement.Clone();
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
        await db.Artifacts.Where(a => a.MapId == map.Id && a.Kind == ArtifactKind.SymmetryJson).DeleteAsync(ct);
        await Send.OkAsync(new Dict { ["ok"] = true }, ct);
    }
}

/// <summary>Shared bits for the B9 layer endpoints.</summary>
internal static class ConfigureLayers
{
    public static readonly HashSet<string> ValidTypes = ["surface", "y0", "bedrock", "base"];

    /// <summary>The cached surface cells for a layer type, or null when unavailable. Currently only the
    /// configured <c>scan_layer</c> (the imported <c>layer.parquet</c> artifact) is served; y0/bedrock/
    /// base need their extractors ported (P-series) to regenerate on demand.</summary>
    public static async Task<List<SurfaceCell>?> CellsAsync(PgmDb db, long mapId, string layerType, CancellationToken ct)
    {
        var cfg = await ConfigureStore.LoadAsync(db, mapId, ct);
        var scanLayer = cfg["scan_layer"]?.GetValue<string>() ?? "surface";
        if (layerType != scanLayer) return null;
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.LayerParquet, ct);
        return art is null ? null : await SurfaceLayer.ReadAsync(art.Data);
    }
}

/// <summary>PATCH /api/configure/{slug}/exclude-block — toggle one block id's exclusion (B9).</summary>
public sealed class ConfigureExcludeBlockEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/configure/{slug}/exclude-block"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        using var r = new StreamReader(HttpContext.Request.Body);
        var bodyText = await r.ReadToEndAsync(ct);
        var body = (JsonNode.Parse(string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText) as JsonObject) ?? new JsonObject();

        if (body["block_id"] is not { } bidNode) { await Send.ResponseAsync(new Dict { ["error"] = "block_id is required" }, 400, ct); return; }
        int blockId;
        try { blockId = bidNode.GetValue<int>(); }
        catch { await Send.ResponseAsync(new Dict { ["error"] = "block_id must be an integer" }, 400, ct); return; }
        var excluded = body["excluded"]?.GetValue<bool>() ?? true;

        var cfg = await ConfigureStore.LoadAsync(db, map.Id, ct);
        var ids = (cfg["exclude_blocks"]?.AsArray() ?? new JsonArray()).Select(n => n!.GetValue<int>()).ToList();
        if (excluded) { if (!ids.Contains(blockId)) ids.Add(blockId); }   // append, preserve order (matches reference)
        else ids = ids.Where(b => b != blockId).ToList();
        cfg["exclude_blocks"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray());

        await ConfigureStore.SaveAsync(db, map.Id, cfg, ct);
        await Send.OkAsync(new Dict { ["ok"] = true, ["exclude_blocks"] = ids }, ct);
    }
}

/// <summary>GET /api/configure/{slug}/layers/{type}/pixels — coloured pixel data for the configure
/// canvas preview (B9). 400 on an unknown layer type; 404 when the layer's data isn't available.</summary>
public sealed class ConfigureLayerPixelsEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/configure/{slug}/layers/{type}/pixels"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var layerType = Route<string>("type")!;
        if (!ConfigureLayers.ValidTypes.Contains(layerType))
        { await Send.ResponseAsync(new Dict { ["error"] = $"Unknown layer type: {layerType}" }, 400, ct); return; }
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var cells = await ConfigureLayers.CellsAsync(db, map.Id, layerType, ct);
        if (cells is null || cells.Count == 0)
        { await Send.ResponseAsync(new Dict { ["error"] = "World files not available for this map" }, 404, ct); return; }
        await Send.OkAsync(LayerData.Pixels(cells), ct);
    }
}

/// <summary>GET /api/configure/{slug}/layers/{type}/block-types — block-exclusion list for a layer
/// (B9): one entry per block id, count desc. 400 on unknown type; [] when data is unavailable.</summary>
public sealed class ConfigureLayerBlockTypesEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/configure/{slug}/layers/{type}/block-types"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var layerType = Route<string>("type")!;
        if (!ConfigureLayers.ValidTypes.Contains(layerType))
        { await Send.ResponseAsync(new Dict { ["error"] = $"Unknown layer type: {layerType}" }, 400, ct); return; }
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var cells = await ConfigureLayers.CellsAsync(db, map.Id, layerType, ct);
        await Send.OkAsync(cells is null ? new List<Dict>() : LayerData.BlockTypes(cells), ct);
    }
}
