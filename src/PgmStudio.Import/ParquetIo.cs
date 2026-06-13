using Parquet.Serialization;

namespace PgmStudio.Import;

/// <summary>Reads a parquet file into plain rows (column name → boxed value), dtype-agnostic.</summary>
internal static class ParquetIo
{
    public static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var result = await ParquetSerializer.DeserializeUntypedAsync(stream);
        return result.Data.Select(d => d.ToDictionary(kv => kv.Key, kv => (object?)kv.Value)).ToList();
    }

    public static int I(object? v) => v is null ? 0 : Convert.ToInt32(v);
    public static int? IN(object? v) => v is null ? null : Convert.ToInt32(v);
    public static string S(object? v) => v?.ToString() ?? "";
    public static bool? BN(object? v) => v is null ? null : Convert.ToBoolean(v);
}
