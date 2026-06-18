using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Client.Models;

/// <summary>Region geometry edits shared by the canvas (drag-resize) and the inspector (typed coords).
/// Routes a single field change to the right PATCH, then updates the node in place so the canvas and
/// the inspector agree without a full reload. Returns the new 2D footprint {min_x,min_z,max_x,max_z}
/// (for refreshing the canvas shape), or null on failure.</summary>
public static class RegionEdits
{
    private static readonly string[] Footprint = { "min_x", "min_z", "max_x", "max_z" };

    /// <summary>PATCH a whole footprint (a resize drag gives all four values).</summary>
    public static Task<Dictionary<string, double>?> SetBoundsAsync(
        HttpClient http, string slug, RegionNode node, double minX, double minZ, double maxX, double maxZ)
    {
        var b = new Dictionary<string, object?> { ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ };
        return PatchAsync(http, slug, node, new Dictionary<string, object?> { ["bounds"] = b });
    }

    /// <summary>PATCH one inspector coord field. Footprint keys (min_x/min_z/max_x/max_z) go through the
    /// bounds path (sent with the region's current footprint); all other keys (cuboid min_y/max_y,
    /// point x/y/z, cylinder base/radius/height, …) go through the coords path.</summary>
    public static Task<Dictionary<string, double>?> SetCoordAsync(
        HttpClient http, string slug, RegionNode node, string key, double value)
    {
        Dictionary<string, object?> body;
        if (Footprint.Contains(key))
        {
            var b = new Dictionary<string, object?>();
            foreach (var k in Footprint) b[k] = Cur(node, k);
            b[key] = value;
            body = new Dictionary<string, object?> { ["bounds"] = b };
        }
        else
        {
            body = new Dictionary<string, object?> { ["coords"] = new Dictionary<string, object?> { [key] = value } };
        }
        return PatchAsync(http, slug, node, body, key, value);
    }

    private static async Task<Dictionary<string, double>?> PatchAsync(
        HttpClient http, string slug, RegionNode node, Dictionary<string, object?> body, string? coordKey = null, double coordValue = 0)
    {
        var resp = await http.PatchAsJsonAsync($"api/map/{slug}/regions/{node.Id}", body);
        if (!resp.IsSuccessStatusCode) return null;
        // the edited coord (e.g. cuboid min_y) lives only in Coords; the response carries the new footprint
        if (coordKey is not null) node.Coords[coordKey] = coordValue;
        var res = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var nb = new Dictionary<string, double>();
        if (res.TryGetProperty("bounds", out var rb) && rb.ValueKind == JsonValueKind.Object)
            foreach (var k in Footprint)
                if (rb.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number) nb[k] = v.GetDouble();
        WriteFootprint(node, nb);
        return nb;
    }

    // Mirror the footprint into Bounds and (where present) Coords, so the inspector reads fresh values.
    private static void WriteFootprint(RegionNode node, Dictionary<string, double> nb)
    {
        if (nb.Count == 0) return;
        node.Bounds ??= new();
        foreach (var (k, v) in nb)
        {
            node.Bounds[k] = v;
            if (node.Coords.ContainsKey(k)) node.Coords[k] = v;
        }
    }

    private static double Cur(RegionNode node, string k) => ToDouble(node.Coords.GetValueOrDefault(k) ?? node.Bounds?.GetValueOrDefault(k));

    private static double ToDouble(object? o) => o switch { double d => d, long l => l, int i => i, _ => 0 };
}
