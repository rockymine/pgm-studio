using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>Shared helpers for the symmetry endpoints (B7).</summary>
internal static class SymmetrySupport
{
    public static readonly HashSet<string> ValidTypes = ["rot_90", "rot_180", "mirror_x", "mirror_z"];

    // Display-strength rank for primary tie-breaks (port of _SYMMETRY_ORDER).
    private static readonly Dictionary<string, int> Order = new()
    {
        ["rot_90"] = 4, ["rot_180"] = 2, ["mirror_x"] = 1, ["mirror_z"] = 1, ["mirror_d1"] = 1, ["mirror_d2"] = 1,
    };

    /// <summary>Parse the islands_json artifact into detector inputs, excluding the given ids.</summary>
    public static List<SymmetryDetector.Island> ParseIslands(byte[] islandsJson, ISet<int> exclude)
    {
        var islands = new List<SymmetryDetector.Island>();
        using var doc = JsonDocument.Parse(islandsJson);
        foreach (var isl in doc.RootElement.EnumerateArray())
        {
            var id = isl.GetProperty("id").GetInt32();
            if (exclude.Contains(id)) continue;
            var b = isl.GetProperty("bounds");
            double b0 = b[0].GetDouble(), b1 = b[1].GetDouble(), b2 = b[2].GetDouble(), b3 = b[3].GetDouble();
            var exterior = new List<(double, double)>();
            if (isl.TryGetProperty("polygon", out var poly) && poly.TryGetProperty("coordinates", out var rings)
                && rings.ValueKind == JsonValueKind.Array && rings.GetArrayLength() > 0)
                foreach (var pt in rings[0].EnumerateArray())
                    exterior.Add((pt[0].GetDouble(), pt[1].GetDouble()));
            islands.Add(new SymmetryDetector.Island(
                id, isl.GetProperty("block_count").GetInt32(),
                (b0 + b2) / 2.0, (b1 + b3) / 2.0, exterior, [b0, b1, b2, b3]));
        }
        return islands;
    }

    /// <summary>Serialize a detection result to the symmetry.json shape (status/modes/center/center_cell/primary).</summary>
    public static JsonObject ToJson(SymmetryDetector.Result r, string status)
    {
        var modes = new JsonArray();
        foreach (var m in r.Modes)
            modes.Add(new JsonObject { ["type"] = m.Type, ["detected"] = m.Detected, ["confidence"] = m.Confidence });

        JsonObject? primary = null;
        var detected = r.Modes.Where(m => m.Detected).ToList();
        if (detected.Count > 0)
        {
            var best = detected.OrderByDescending(m => m.Confidence)
                .ThenByDescending(m => Order.GetValueOrDefault(m.Type)).First();
            primary = new JsonObject { ["type"] = best.Type, ["confidence"] = best.Confidence };
        }

        return new JsonObject
        {
            ["status"] = status,
            ["modes"] = modes,
            ["center"] = new JsonObject { ["cx"] = r.Cx, ["cz"] = r.Cz },
            ["center_cell"] = CenterCell(r.Cx, r.Cz),
            ["primary"] = primary,
        };
    }

    private static string CenterCell(double cx, double cz) => $"{AxisWidth(cx)}x{AxisWidth(cz)}";

    private static int AxisWidth(double coord)
    {
        var frac = ((coord % 1.0) + 1.0) % 1.0;     // Python-style non-negative modulo
        return Math.Abs(frac - 0.5) < 1e-6 ? 1 : 2;
    }
}

/// <summary>
/// GET /api/map/{slug}/symmetry — global symmetry of the map's islands (B7). Returns the cached
/// symmetry_json artifact, or computes it on demand from the islands_json artifact (excluding the
/// Configure-excluded islands) and caches it with status "unconfirmed".
/// </summary>
public sealed class SymmetryGetEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/symmetry"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var existing = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.SymmetryJson, ct);
        if (existing is not null)
        {
            using var jd = JsonDocument.Parse(existing.Data);
            await Send.OkAsync(jd.RootElement.Clone(), ct);
            return;
        }

        var islandsArt = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandsJson, ct);
        if (islandsArt is null) { await Send.NotFoundAsync(ct); return; }

        var exclude = await ExcludedIslandsAsync(map.Id, ct);
        var islands = SymmetrySupport.ParseIslands(islandsArt.Data, exclude);
        var result = SymmetryDetector.Detect(islands);
        var json = SymmetrySupport.ToJson(result, "unconfirmed");

        await db.InsertAsync(new MapArtifactRow
        {
            MapId = map.Id, Kind = ArtifactKind.SymmetryJson, Data = Encoding.UTF8.GetBytes(json.ToJsonString()),
        }, token: ct);

        await Send.OkAsync(json, ct);
    }

    private async Task<HashSet<int>> ExcludedIslandsAsync(long mapId, CancellationToken ct)
    {
        var cfg = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.MapConfigJson, ct);
        if (cfg is null) return [];
        var node = JsonNode.Parse(cfg.Data);
        return node?["exclude_islands"]?.AsArray().Select(n => n!.GetValue<int>()).ToHashSet() ?? [];
    }
}

/// <summary>
/// PATCH /api/map/{slug}/symmetry — confirm/reject the detected symmetry (B7). Updates status
/// ("confirmed"/"none"), an optional user-override confirmed_type, and an optional centre override.
/// Mirrors the reference patch_symmetry.
/// </summary>
public sealed class SymmetryPatchEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/map/{slug}/symmetry"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var reader = new StreamReader(HttpContext.Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var payload = (JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body) as JsonObject) ?? new JsonObject();

        var existing = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.SymmetryJson, ct);
        var sym = existing is not null
            ? JsonNode.Parse(existing.Data) as JsonObject ?? new JsonObject()
            : new JsonObject { ["status"] = "unconfirmed", ["modes"] = new JsonArray(), ["primary"] = null, ["center"] = null };

        var status = payload["status"]?.GetValue<string>();
        if (status is "confirmed" or "none") sym["status"] = status;

        if (payload["confirmed_type"] is { } ctNode)
        {
            var confirmedType = ctNode.GetValue<string>();
            if (!SymmetrySupport.ValidTypes.Contains(confirmedType))
            {
                await Send.ResponseAsync(new Dict { ["error"] = $"Invalid symmetry type: {confirmedType}" }, 400, ct);
                return;
            }
            sym["primary"] = new JsonObject { ["type"] = confirmedType, ["confidence"] = 1.0, ["user_override"] = true };
        }
        else if (status == "none") sym["primary"] = null;

        if (payload.ContainsKey("cx") || payload.ContainsKey("cz"))
        {
            var current = sym["center"] as JsonObject;
            sym["center"] = new JsonObject
            {
                ["cx"] = payload["cx"]?.GetValue<double>() ?? current?["cx"]?.GetValue<double>() ?? 0.0,
                ["cz"] = payload["cz"]?.GetValue<double>() ?? current?["cz"]?.GetValue<double>() ?? 0.0,
            };
        }

        await db.Artifacts.Where(a => a.MapId == map.Id && a.Kind == ArtifactKind.SymmetryJson).DeleteAsync(ct);
        await db.InsertAsync(new MapArtifactRow
        {
            MapId = map.Id, Kind = ArtifactKind.SymmetryJson, Data = Encoding.UTF8.GetBytes(sym.ToJsonString()),
        }, token: ct);

        await Send.OkAsync(new Dict { ["ok"] = true }, ct);
    }
}
