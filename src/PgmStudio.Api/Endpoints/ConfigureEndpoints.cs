using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;
using PgmStudio.Contracts;

/// <summary>
/// The per-map scan configuration document (the <c>map_config_json</c> artifact): the island exclusions
/// and the studio-chosen <c>scan_layer</c> marker written at world-load, plus the surface bounding box the
/// scan measured. Detection runs on the fixed cleaned-base layer (no user scan-layer or block-exclusion
/// choice, and no world re-scan): excluding an island only recomputes symmetry from the already-detected
/// <c>islands_json</c>. A map with no stored document reads as the default below rather than as absent —
/// every field has an answer before the first save.
/// </summary>
internal static class ScanConfig
{
    public static async Task<JsonObject> LoadAsync(MapArtifactStore artifacts, long mapId, CancellationToken ct)
    {
        var data = await artifacts.LoadAsync(mapId, ArtifactKind.MapConfigJson, ct);
        if (data is not null && JsonNode.Parse(Encoding.UTF8.GetString(data)) is JsonObject stored) return stored;
        return new JsonObject
        {
            ["scan_layer"] = "surface",
            ["exclude_blocks"] = new JsonArray(),
            ["exclude_islands"] = new JsonArray(),
            ["scan_layer_confirmed"] = false,
        };
    }

    public static Task SaveAsync(MapArtifactStore artifacts, long mapId, JsonObject cfg, CancellationToken ct)
        => artifacts.SaveAsync(mapId, ArtifactKind.MapConfigJson, Encoding.UTF8.GetBytes(cfg.ToJsonString()), ct);

    /// <summary>The islands the author excluded from detection — the symmetry input, empty when none.</summary>
    public static async Task<HashSet<int>> ExcludedIslandsAsync(MapArtifactStore artifacts, long mapId, CancellationToken ct)
        => (await LoadAsync(artifacts, mapId, ct))["exclude_islands"]?.AsArray()
            .Select(n => n!.GetValue<int>()).ToHashSet() ?? [];
}

/// <summary>GET /api/configure/{slug}/state — the current scan configuration.</summary>
public sealed class ConfigureStateEndpoint(MapRepository repo, PgmDb db, MapArtifactStore artifacts)
    : EndpointWithoutRequest<ConfigureStateDto>
{
    public override void Configure() { Get("/configure/{slug}/state"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var cfg = await ScanConfig.LoadAsync(artifacts, map.Id, ct);

        // Step 3 = symmetry: configure is complete once the user confirms/rejects the detection.
        var symRow = await SymmetryStore.LoadAsync(db, map.Id, ct);
        var symmetryStatus = symRow?.Status ?? "unconfirmed";

        await Send.OkAsync(new ConfigureStateDto(
            cfg["scan_layer"]?.GetValue<string>() ?? "surface",
            [.. cfg["exclude_blocks"]?.AsArray().Select(n => n!.GetValue<int>()) ?? []],
            [.. cfg["exclude_islands"]?.AsArray().Select(n => n!.GetValue<int>()) ?? []],
            symmetryStatus,
            symmetryStatus != "unconfirmed"), ct);
    }
}

/// <summary>PATCH /api/configure/{slug}/exclude-island — toggle one island's exclusion.</summary>
public sealed class ConfigureExcludeIslandEndpoint(MapRepository repo, PgmDb db, MapArtifactStore artifacts)
    : EndpointWithoutRequest<AppliedDto>
{
    public override void Configure()
    {
        Patch("/configure/{slug}/exclude-island"); AllowAnonymous();
        Description(b => b.Accepts<ExcludeIslandRequest>("application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var body = JsonDocument.Parse(await RawBody.ReadAsync(HttpContext, ct)).RootElement;
        var islandId = body.GetProperty("island_id").GetInt32();
        var excluded = body.GetProperty("excluded").GetBoolean();

        var cfg = await ScanConfig.LoadAsync(artifacts, map.Id, ct);
        var list = cfg["exclude_islands"]?.AsArray() ?? new JsonArray();
        var ids = list.Select(n => n!.GetValue<int>()).Where(i => i != islandId).ToList();
        if (excluded) ids.Add(islandId);
        cfg["exclude_islands"] = new JsonArray(ids.OrderBy(i => i).Select(i => (JsonNode)i).ToArray());

        await ScanConfig.SaveAsync(artifacts, map.Id, cfg, ct);
        // Excluded islands feed symmetry detection — drop the cached result so step 3 recomputes.
        await SymmetryStore.DeleteAsync(db, map.Id, ct);
        await Send.OkAsync(new AppliedDto(), ct);
    }
}

/// <summary>Resolves an already-ingested surface layer for a map (no world access).</summary>
internal static class ConfigureLayers
{
    /// <summary>The cells for a layer type from the ingested artifacts, or null when not cached. The
    /// studio-chosen <c>scan_layer</c> is served from the canonical <c>layer.parquet</c>; any other type
    /// from its per-type cache if one was stored. Never scans the world — the hosted tier has no
    /// <c>.mca</c> files, and per-map re-scan/re-detection is out of scope.</summary>
    public static async Task<List<SurfaceCell>?> CellsAsync(
        MapArtifactStore artifacts, long mapId, string layerType, CancellationToken ct)
    {
        var cfg = await ScanConfig.LoadAsync(artifacts, mapId, ct);
        var scanLayer = cfg["scan_layer"]?.GetValue<string>() ?? "surface";

        if (layerType == scanLayer && await artifacts.LoadAsync(mapId, ArtifactKind.LayerParquet, ct) is { } canon)
            return await SurfaceLayer.ReadAsync(canon);

        return await artifacts.LoadAsync(mapId, $"layer_{layerType}_parquet", ct) is { } cached
            ? await SurfaceLayer.ReadAsync(cached) : null;
    }
}
