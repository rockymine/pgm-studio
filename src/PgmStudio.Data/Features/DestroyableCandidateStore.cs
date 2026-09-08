using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using PgmStudio.Data.Schema;
using PgmStudio.Geom;
using PgmStudio.Analysis.Suggest;

namespace PgmStudio.Data.Features;

/// <summary>
/// Persists / loads the destroyables <c>DestroyableSuggester.Gather</c> proposed. Ingest writes them,
/// delete-then-insert per map like the feature rows; the configure tier reads them back and needs no world
/// access, which is the whole point — the <c>.mca</c> files are gone by then.
///
/// <para>The row carries the readings as well as the box, because a destroyable's proposal is not
/// self-evident the way a core's is. A sealed lava container is a core; a mass of ender stone is a goal only
/// relative to what is around it, so the surface that asks a person to confirm one has to be able to say
/// <i>why</i> — nothing else of this material within ten blocks, five above the ground around it — and that
/// sentence is only writable if the readings survive the world.</para>
/// </summary>
public static class DestroyableCandidateStore
{
    public static DestroyableCandidateRow ToRow(long mapId, DestroyableSuggestion destroyable) => new()
    {
        MapId = mapId,
        MinX = destroyable.Structure.MinX, MinY = destroyable.Structure.MinY, MinZ = destroyable.Structure.MinZ,
        MaxX = destroyable.Structure.MaxX, MaxY = destroyable.Structure.MaxY, MaxZ = destroyable.Structure.MaxZ,
        Materials = destroyable.Materials, Blocks = destroyable.Blocks,
        SameNearby = destroyable.SameNearby, Elevation = destroyable.Elevation,
    };

    public static DestroyableSuggestion ToSuggestion(DestroyableCandidateRow row) => new(
        new BlockBox(row.MinX, row.MinY, row.MinZ, row.MaxX, row.MaxY, row.MaxZ),
        row.Materials, row.Blocks, row.SameNearby, row.Elevation);

    /// <summary>Replace a map's destroyable candidates (idempotent re-gather). Returns the row count
    /// written.</summary>
    public static async Task<int> WriteAsync(PgmDb db, long mapId, IReadOnlyList<DestroyableSuggestion> destroyables,
                                             CancellationToken ct = default)
    {
        await db.DestroyableCandidates.Where(row => row.MapId == mapId).DeleteAsync(ct);
        var rows = destroyables.Select(d => ToRow(mapId, d)).ToList();
        if (rows.Count > 0) await db.BulkCopyAsync(rows, ct);
        return rows.Count;
    }

    /// <summary>Load a map's proposed destroyables, most convincing first: the isolation signal is what
    /// separates a goal from decoration, so the least-surrounded mass is the one a reader should meet first,
    /// and height over the ring breaks the tie.</summary>
    public static async Task<List<DestroyableSuggestion>> ReadAsync(PgmDb db, long mapId, CancellationToken ct = default) =>
        [.. (await db.DestroyableCandidates.Where(row => row.MapId == mapId)
                     .OrderBy(row => row.SameNearby).ThenByDescending(row => row.Elevation)
                     .ThenBy(row => row.MinY).ThenBy(row => row.MinX).ThenBy(row => row.MinZ)
                     .ToListAsync(ct))
            .Select(ToSuggestion)];
}
