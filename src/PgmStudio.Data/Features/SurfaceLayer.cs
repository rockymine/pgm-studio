using System.Text.Json.Serialization;
using Parquet.Serialization;

namespace PgmStudio.Data.Features;

/// <summary>One surface-scan cell read from the cached <c>layer.parquet</c> artifact blob.</summary>
public readonly record struct SurfaceCell(int X, int Z, int BlockId, int BlockData);

/// <summary>Reads the raw <c>layer.parquet</c> artifact (stored as a blob in <c>map_artifact</c>)
/// into surface cells. Mirrors the columns the pipeline writes (world_x/world_z/block_id/block_data).</summary>
public static class SurfaceLayer
{
    public static async Task<List<SurfaceCell>> ReadAsync(byte[] parquet)
    {
        await using var stream = new MemoryStream(parquet, writable: false);
        var result = await ParquetSerializer.DeserializeUntypedAsync(stream);
        var cells = new List<SurfaceCell>(result.Data.Count);
        foreach (var row in result.Data)
        {
            var d = row.ToDictionary(kv => kv.Key, kv => kv.Value);
            cells.Add(new SurfaceCell(
                Convert.ToInt32(d["world_x"]), Convert.ToInt32(d["world_z"]),
                Convert.ToInt32(d["block_id"]), Convert.ToInt32(d["block_data"])));
        }
        return cells;
    }

    /// <summary>Serialize cells to a parquet blob (same columns as the canonical layer.parquet) for caching.</summary>
    public static async Task<byte[]> WriteAsync(IReadOnlyList<SurfaceCell> cells)
    {
        var rows = cells.Select(c => new Row { WorldX = c.X, WorldZ = c.Z, BlockId = c.BlockId, BlockData = c.BlockData }).ToList();
        using var ms = new MemoryStream();
        if (rows.Count > 0) await ParquetSerializer.SerializeAsync(rows, ms);
        return ms.ToArray();
    }

    private sealed class Row
    {
        [JsonPropertyName("world_x")] public int WorldX { get; set; }
        [JsonPropertyName("world_z")] public int WorldZ { get; set; }
        [JsonPropertyName("block_id")] public int BlockId { get; set; }
        [JsonPropertyName("block_data")] public int BlockData { get; set; }
    }
}
