using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Api.Endpoints;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The canonical map bounding box. The real extent is the <b>surface layer</b>'s, computed once at scan
/// and saved in <c>map_config.json</c> (<c>bounding_box</c>); this reads it back. Falls back to the
/// detected-islands AABB for maps scanned before it was stored (or xml-only maps with no surface scan).
/// Used as both the canvas frame and the analysis clip box (the finite map box that <c>half</c>/<c>negative</c>
/// regions are clipped against). Returns null when neither is available.
/// </summary>
internal static class MapBounds
{
    public static async Task<((double, double, double, double) bounds, Dict dict)?> ResolveAsync(
        MapArtifactStore artifacts, long mapId, CancellationToken ct)
    {
        var cfg = await ScanConfig.LoadAsync(artifacts, mapId, ct);
        if (cfg["bounding_box"] is JsonObject b
            && b["min_x"] is { } mnx && b["min_z"] is { } mnz && b["max_x"] is { } mxx && b["max_z"] is { } mxz)
        {
            double minX = mnx.GetValue<double>(), minZ = mnz.GetValue<double>(),
                   maxX = mxx.GetValue<double>(), maxZ = mxz.GetValue<double>();
            return ((minX, minZ, maxX, maxZ),
                new Dict { ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ });
        }
        // No stored surface bbox → the detected-islands AABB.
        return await IslandsBboxAsync(artifacts, mapId, ct);
    }

    /// <summary>Bounding box over the map's detected islands (from the islands_json artifact), or null.</summary>
    private static async Task<((double, double, double, double) bounds, Dict dict)?> IslandsBboxAsync(
        MapArtifactStore artifacts, long mapId, CancellationToken ct)
    {
        var data = await artifacts.LoadAsync(mapId, ArtifactKind.IslandsJson, ct);
        if (data is null) return null;
        using var jd = JsonDocument.Parse(data);
        var arr = jd.RootElement;
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;

        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var e in arr.EnumerateArray())
        {
            var b = e.GetProperty("bounds");
            minX = Math.Min(minX, b[0].GetDouble()); minZ = Math.Min(minZ, b[1].GetDouble());
            maxX = Math.Max(maxX, b[2].GetDouble()); maxZ = Math.Max(maxZ, b[3].GetDouble());
        }
        var dict = new Dict { ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ };
        return ((minX, minZ, maxX, maxZ), dict);
    }
}
