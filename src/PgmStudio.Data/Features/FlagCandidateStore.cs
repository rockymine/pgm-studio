using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using PgmStudio.Data.Schema;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Data.Features;

/// <summary>
/// Persists and loads the banners a scan found. The import writes them, delete-then-insert per map like the
/// feature rows; the configure tier reads them back with no world access, since the <c>.mca</c> files are gone
/// by then. A banner needs no scoring: PGM loads a flag only where one stands, so the row is the suggestion.
/// </summary>
public static class FlagCandidateStore
{
    public static FlagCandidateRow ToRow(long mapId, BannerFeature banner) => new()
    {
        MapId = mapId, WorldX = banner.WorldX, WorldY = banner.WorldY, WorldZ = banner.WorldZ,
        Wall = banner.Wall, Facing = banner.Facing, Color = banner.Color,
        PatternsJson = JsonSerializer.Serialize(banner.Patterns),
    };

    public static BannerFeature ToBanner(FlagCandidateRow row) => new(
        row.WorldX, row.WorldY, row.WorldZ, row.Wall, row.Facing, row.Color,
        JsonSerializer.Deserialize<List<string>>(row.PatternsJson) ?? []);

    /// <summary>Replace a map's banners (idempotent re-gather). Returns the row count written.</summary>
    public static async Task<int> WriteAsync(PgmDb db, long mapId, IReadOnlyList<BannerFeature> banners,
                                             CancellationToken ct = default)
    {
        await db.FlagCandidates.Where(row => row.MapId == mapId).DeleteAsync(ct);
        var rows = banners.Select(banner => ToRow(mapId, banner)).ToList();
        if (rows.Count > 0) await db.BulkCopyAsync(rows, ct);
        return rows.Count;
    }

    /// <summary>Load a map's banners, ordered so a suggestion list is stable between requests.</summary>
    public static async Task<List<BannerFeature>> ReadAsync(PgmDb db, long mapId, CancellationToken ct = default) =>
        [.. (await db.FlagCandidates.Where(row => row.MapId == mapId)
                     .OrderBy(row => row.WorldY).ThenBy(row => row.WorldX).ThenBy(row => row.WorldZ)
                     .ToListAsync(ct))
            .Select(ToBanner)];
}
