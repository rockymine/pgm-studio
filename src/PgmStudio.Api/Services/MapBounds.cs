using System.Text.Json.Nodes;
using PgmStudio.Api.Endpoints;
using PgmStudio.Data;

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
        PgmDb db, long mapId, CancellationToken ct)
    {
        var cfg = await ConfigureStore.LoadAsync(db, mapId, ct);
        if (cfg["bounding_box"] is JsonObject b
            && b["min_x"] is { } mnx && b["min_z"] is { } mnz && b["max_x"] is { } mxx && b["max_z"] is { } mxz)
        {
            double minX = mnx.GetValue<double>(), minZ = mnz.GetValue<double>(),
                   maxX = mxx.GetValue<double>(), maxZ = mxz.GetValue<double>();
            return ((minX, minZ, maxX, maxZ),
                new Dict { ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ });
        }
        // No stored surface bbox → the detected-islands AABB (the prior behaviour).
        return await RegionsAuthoringEndpoint.IslandsBboxAsync(db, mapId, ct);
    }
}
