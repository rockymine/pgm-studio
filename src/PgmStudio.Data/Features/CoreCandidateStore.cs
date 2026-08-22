using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Geom;
using PgmStudio.Analysis.Suggest;

namespace PgmStudio.Data.Features;

/// <summary>
/// Persists / loads the cores <c>CoreSuggester.Gather</c> proposed. Ingest writes them, delete-then-insert per
/// map like the feature rows; the configure tier reads them back and needs no world access — which is the
/// whole point, since the <c>.mca</c> files are gone by then.
///
/// <para>Unlike <c>MonumentCandidateStore</c>, nothing is scored later: a core's signature is unambiguous
/// enough that the gather pass already knows the casing and its parameters, so the row <i>is</i> the
/// suggestion.</para>
/// </summary>
public static class CoreCandidateStore
{
    public static CoreCandidateRow ToRow(long mapId, CoreSuggestion core) => new()
    {
        MapId = mapId,
        MinX = core.Casing.MinX, MinY = core.Casing.MinY, MinZ = core.Casing.MinZ,
        MaxX = core.Casing.MaxX, MaxY = core.Casing.MaxY, MaxZ = core.Casing.MaxZ,
        LavaBlocks = core.LavaBlocks, Shell = core.Shell, FloatBlocks = core.Float, OpenTop = core.OpenTop,
    };

    public static CoreSuggestion ToSuggestion(CoreCandidateRow row) => new(
        new BlockBox(row.MinX, row.MinY, row.MinZ, row.MaxX, row.MaxY, row.MaxZ),
        row.LavaBlocks, row.Shell, row.FloatBlocks, row.OpenTop);

    /// <summary>Replace a map's core candidates (idempotent re-gather). Returns the row count written.</summary>
    public static async Task<int> WriteAsync(PgmDb db, long mapId, IReadOnlyList<CoreSuggestion> cores,
                                             CancellationToken ct = default)
    {
        await db.CoreCandidates.Where(row => row.MapId == mapId).DeleteAsync(ct);
        var rows = cores.Select(core => ToRow(mapId, core)).ToList();
        if (rows.Count > 0) await db.BulkCopyAsync(rows, ct);
        return rows.Count;
    }

    /// <summary>Load a map's proposed cores, ordered so a suggestion list is stable between requests.</summary>
    public static async Task<List<CoreSuggestion>> ReadAsync(PgmDb db, long mapId, CancellationToken ct = default) =>
        [.. (await db.CoreCandidates.Where(row => row.MapId == mapId)
                     .OrderBy(row => row.MinY).ThenBy(row => row.MinX).ThenBy(row => row.MinZ)
                     .ToListAsync(ct))
            .Select(ToSuggestion)];
}
