using PgmStudio.Contracts;
using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Client.Models;

/// <summary>Region geometry edits shared by the canvas (drag-resize) and the inspector (typed coords).
/// Routes a single field change to the right PATCH, then updates the node in place so the canvas and
/// the inspector agree without a full reload. Returns the new 2D footprint {min_x,min_z,max_x,max_z}
/// (for refreshing the canvas shape), or null on failure.</summary>
public static class RegionEdits
{
    /// <summary>PATCH a whole footprint (a resize drag gives all four values).</summary>
    public static Task<Dictionary<string, double>?> SetBoundsAsync(
        HttpClient http, string slug, RegionNode node, double minX, double minZ, double maxX, double maxZ)
        => PatchAsync(http, slug, node, Coords(new Dictionary<string, object?>
        {
            ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ,
        }));

    /// <summary>PATCH one inspector coord field. Every key a region carries — footprint, cuboid min_y/max_y,
    /// point x/y/z, cylinder base/radius/height — goes under the same <c>coords</c> object, and the stored
    /// region's type decides which of them it reads.</summary>
    public static Task<Dictionary<string, double>?> SetCoordAsync(
        HttpClient http, string slug, RegionNode node, string key, double value)
        => PatchAsync(http, slug, node, Coords(new Dictionary<string, object?> { [key] = value }), key, value);

    private static Dictionary<string, object?> Coords(Dictionary<string, object?> numbers)
        => new() { ["coords"] = numbers };

    private static async Task<Dictionary<string, double>?> PatchAsync(
        HttpClient http, string slug, RegionNode node, Dictionary<string, object?> body, string? coordKey = null, double coordValue = 0)
    {
        using var resp = await MapEdits.PatchRegion(http, slug, node.Id, body);
        if (!resp.IsSuccessStatusCode) return null;
        // the edited coord (e.g. cuboid min_y) lives only in Coords; the response carries the new footprint
        if (coordKey is not null) node.Coords[coordKey] = coordValue;
        // A rename answers no bounds at all — only a move has a footprint to hand back.
        var patched = await resp.Content.ReadFromJsonAsync<RegionPatchedDto>();
        var nb = patched?.Bounds is { } b
            ? new Dictionary<string, double>
              { ["min_x"] = b.MinX, ["min_z"] = b.MinZ, ["max_x"] = b.MaxX, ["max_z"] = b.MaxZ }
            : new Dictionary<string, double>();
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

    private static double ToDouble(object? o) => o switch { double d => d, long l => l, int i => i, _ => 0 };
}
