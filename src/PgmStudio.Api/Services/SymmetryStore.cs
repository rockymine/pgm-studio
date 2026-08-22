using System.Text.Json;
using System.Text.Json.Nodes;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Services;

/// <summary>Shared helpers for the symmetry endpoints (B7).</summary>
public static class SymmetrySupport
{
    public static readonly HashSet<string> ValidTypes = ["rot_90", "rot_180", "mirror_x", "mirror_z", "mirror_d1", "mirror_d2"];

    // Display-strength rank for primary tie-breaks.
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
        var frac = ((coord % 1.0) + 1.0) % 1.0;     // non-negative modulo, so -0.5 reads as 0.5
        return Math.Abs(frac - 0.5) < 1e-6 ? 1 : 2;
    }
}

/// <summary>Read/write the <c>symmetry</c> table and reconstruct the symmetry.json API shape from a row
/// (confirmed in Configure's World phase, docs/tools/configure.md) — replaces the <c>symmetry_json</c>
/// artifact as the source.</summary>
public static class SymmetryStore
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
