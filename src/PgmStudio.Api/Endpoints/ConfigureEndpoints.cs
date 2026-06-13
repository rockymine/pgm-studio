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
/// <c>scan_layer_confirmed</c>). Port of the relevant routes in studio/routes/configure.py.
/// (Re-scan on layer change + block-types/pixels previews + symmetry detection are not yet ported.)
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
        await Send.OkAsync(new Dict
        {
            ["scan_layer"] = (cfg["scan_layer"]?.GetValue<string>()) ?? "surface",
            ["exclude_blocks"] = cfg["exclude_blocks"]?.AsArray().Select(n => (object?)n!.GetValue<int>()).ToList() ?? new(),
            ["exclude_islands"] = cfg["exclude_islands"]?.AsArray().Select(n => (object?)n!.GetValue<int>()).ToList() ?? new(),
            ["configure_complete"] = cfg["scan_layer_confirmed"]?.GetValue<bool>() ?? false,
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
        await Send.OkAsync(new Dict { ["ok"] = true }, ct);
    }
}
