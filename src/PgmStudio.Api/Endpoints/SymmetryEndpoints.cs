using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Footprint;
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

    /// <summary>The chosen mode for a detection: highest-confidence detected mode, ties broken by
    /// display-strength rank. Null when nothing is detected.</summary>
    public static (string? Type, double? Confidence) Primary(SymmetryDetector.Result r)
    {
        var detected = r.Modes.Where(m => m.Detected).ToList();
        if (detected.Count == 0) return (null, null);
        var best = detected.OrderByDescending(m => m.Confidence)
            .ThenByDescending(m => Order.GetValueOrDefault(m.Type)).First();
        return (best.Type, best.Confidence);
    }

    /// <summary>Serialize the candidate modes to the persisted <c>modes_json</c> form.</summary>
    public static string ModesJson(SymmetryDetector.Result r)
    {
        var modes = new JsonArray();
        foreach (var m in r.Modes)
            modes.Add(new JsonObject { ["type"] = m.Type, ["detected"] = m.Detected, ["confidence"] = m.Confidence });
        return modes.ToJsonString();
    }

    public static string CenterCell(double cx, double cz) => $"{AxisWidth(cx)}x{AxisWidth(cz)}";

    private static int AxisWidth(double coord)
    {
        var frac = ((coord % 1.0) + 1.0) % 1.0;     // Python-style non-negative modulo
        return Math.Abs(frac - 0.5) < 1e-6 ? 1 : 2;
    }
}

/// <summary>Read/write the <c>symmetry</c> table and reconstruct the symmetry.json API shape from a row
/// (docs/contracts/new-map-authoring.md §6b) — replaces the <c>symmetry_json</c> artifact as the source.</summary>
internal static class SymmetryStore
{
    public static Task<SymmetryRow?> LoadAsync(PgmDb db, long mapId, CancellationToken ct)
        => db.Symmetries.FirstOrDefaultAsync(s => s.MapId == mapId, ct);

    public static async Task SaveAsync(PgmDb db, SymmetryRow row, CancellationToken ct)
    {
        row.UpdatedAt = DateTime.UtcNow;
        await db.Symmetries.Where(s => s.MapId == row.MapId).DeleteAsync(ct);
        await db.InsertAsync(row, token: ct);
    }

    public static Task DeleteAsync(PgmDb db, long mapId, CancellationToken ct)
        => db.Symmetries.Where(s => s.MapId == mapId).DeleteAsync(ct);

    /// <summary>Build a row from a fresh detection result.</summary>
    public static SymmetryRow FromDetection(long mapId, SymmetryDetector.Result r, string status)
    {
        var (ptype, pconf) = SymmetrySupport.Primary(r);
        return new SymmetryRow
        {
            MapId = mapId, Status = status,
            CenterX = r.Cx, CenterZ = r.Cz,
            PrimaryType = ptype, PrimaryConfidence = pconf, PrimaryUserOverride = false,
            ModesJson = SymmetrySupport.ModesJson(r),
        };
    }

    /// <summary>Reconstruct the historic symmetry.json shape (status/modes/center/center_cell/primary).</summary>
    public static JsonObject ToJson(SymmetryRow row)
    {
        JsonObject? center = row.CenterX is { } cx && row.CenterZ is { } cz
            ? new JsonObject { ["cx"] = cx, ["cz"] = cz } : null;
        JsonObject? primary = null;
        if (row.PrimaryType is { } pt)
        {
            primary = new JsonObject { ["type"] = pt, ["confidence"] = row.PrimaryConfidence ?? 0.0 };
            if (row.PrimaryUserOverride) primary["user_override"] = true;
        }
        JsonNode? centerCell = center is null ? null : SymmetrySupport.CenterCell(row.CenterX!.Value, row.CenterZ!.Value);
        return new JsonObject
        {
            ["status"] = row.Status,
            ["modes"] = JsonNode.Parse(row.ModesJson),
            ["center"] = center,
            ["center_cell"] = centerCell,
            ["primary"] = primary,
        };
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

        var existing = await SymmetryStore.LoadAsync(db, map.Id, ct);
        if (existing is not null) { await Send.OkAsync(SymmetryStore.ToJson(existing), ct); return; }

        var islandsArt = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandsJson, ct);
        if (islandsArt is null) { await Send.NotFoundAsync(ct); return; }

        var exclude = await ExcludedIslandsAsync(map.Id, ct);
        var islands = SymmetrySupport.ParseIslands(islandsArt.Data, exclude);
        var result = SymmetryDetector.Detect(islands);
        var row = SymmetryStore.FromDetection(map.Id, result, "unconfirmed");
        await SymmetryStore.SaveAsync(db, row, ct);
        await Send.OkAsync(SymmetryStore.ToJson(row), ct);
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

        var row = await SymmetryStore.LoadAsync(db, map.Id, ct)
            ?? new SymmetryRow { MapId = map.Id, Status = "unconfirmed", ModesJson = "[]" };

        var status = payload["status"]?.GetValue<string>();
        if (status is "confirmed" or "none") row.Status = status;

        if (payload["confirmed_type"] is { } ctNode)
        {
            var confirmedType = ctNode.GetValue<string>();
            if (!SymmetrySupport.ValidTypes.Contains(confirmedType))
            {
                await Send.ResponseAsync(new Dict { ["error"] = $"Invalid symmetry type: {confirmedType}" }, 400, ct);
                return;
            }
            row.PrimaryType = confirmedType; row.PrimaryConfidence = 1.0; row.PrimaryUserOverride = true;
        }
        else if (status == "none") { row.PrimaryType = null; row.PrimaryConfidence = null; row.PrimaryUserOverride = false; }

        if (payload.ContainsKey("cx") || payload.ContainsKey("cz"))
        {
            row.CenterX = payload["cx"]?.GetValue<double>() ?? row.CenterX ?? 0.0;
            row.CenterZ = payload["cz"]?.GetValue<double>() ?? row.CenterZ ?? 0.0;
        }

        await SymmetryStore.SaveAsync(db, row, ct);
        await Send.OkAsync(new Dict { ["ok"] = true }, ct);
    }
}
