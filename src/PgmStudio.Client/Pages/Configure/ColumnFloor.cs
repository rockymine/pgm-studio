using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Client.Pages.Configure;

/// <summary>
/// Terrain seating for the authoring steps: the Y a spawn or wool rests at in a world column, from the
/// vertical segments (<c>GET /map/{slug}/column-floor</c>).
/// <para><c>column-floor</c> reports the floor <b>block</b> — the topmost solid block at or below the
/// reference Y, <i>inclusive</i> — so a thing resting on that floor is one block above it. The +1 lives
/// here, in one place, so every placement step and the side-view's floor snap agree on what sitting on
/// the ground means.</para>
/// </summary>
internal static class ColumnFloor
{
    /// <summary>The Y a thing rests at in the column containing (<paramref name="x"/>, <paramref name="z"/>),
    /// or null when the column has no segment data (void — the caller keeps whatever it had).
    /// <para><paramref name="refY"/> null ⇒ the topmost surface, for a column the author picked on the
    /// top-down canvas. Otherwise the floor at or below refY — falling back to the floor nearest it when the
    /// column's terrain lies entirely above — which keeps a thing that already has a level on that level: a
    /// wool in a covered room finds its own floor rather than the roof that tops its column.</para></summary>
    public static async Task<int?> RestingYAsync(HttpClient http, string slug, double x, double z, int? refY = null)
    {
        try
        {
            var q = $"api/map/{slug}/column-floor?x={(int)Math.Floor(x)}&z={(int)Math.Floor(z)}";
            if (refY is { } r) q += $"&y={r}";
            var d = await http.GetFromJsonAsync<JsonElement>(q);
            return d.TryGetProperty("y", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() + 1 : null;
        }
        catch { return null; }
    }
}
