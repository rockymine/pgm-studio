using System.Text;
using System.Text.Json;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Import;

/// <summary>
/// Derives the Douglas-Peucker simplified island-outline sketch (sketch layout format) from a stored
/// <c>islands_json</c> blob — the shared body behind <c>--store-island-sketch</c> and the per-map refresh
/// in <c>--islands-only</c>. Returns null when the blob carries no usable polygon (nothing to store).
/// </summary>
public static class IslandSketchArtifact
{
    public static byte[]? FromIslandsJson(byte[] islandsJson)
    {
        var islands = new List<(string, IReadOnlyList<double[]>, IReadOnlyList<IReadOnlyList<double[]>>)>();
        try
        {
            using var doc = JsonDocument.Parse(islandsJson);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var rings = el.GetProperty("polygon").GetProperty("coordinates").EnumerateArray()
                    .Select(r => r.EnumerateArray().Select(p => new[] { p[0].GetDouble(), p[1].GetDouble() }).ToList())
                    .ToList();
                if (rings.Count == 0 || rings[0].Count < 3) continue;
                islands.Add((el.GetProperty("id").GetInt32().ToString(),
                    rings[0], rings.Skip(1).Cast<IReadOnlyList<double[]>>().ToList()));
            }
        }
        catch { return null; }
        if (islands.Count == 0) return null;
        return Encoding.UTF8.GetBytes(IslandSimplifier.SimplifyMap(islands).ToJson());
    }
}
