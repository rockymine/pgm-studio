using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using PgmStudio.Data.Schema;
using PgmStudio.Minecraft;

namespace PgmStudio.Data.Features;

/// <summary>
/// Persists / loads the gathered monument candidates (F9, <c>docs/contracts/monument-candidate-store.md</c>).
/// Ingest writes them (delete-then-insert per map, like the feature rows); the authoring suggestion endpoint
/// reads them back and runs <c>MonumentSuggester.Score</c> — no world access. Maps the domain
/// <see cref="MonumentCandidate"/> (PgmStudio.Minecraft) ↔ the <see cref="MonumentCandidateRow"/> table.
/// </summary>
public static class MonumentCandidateStore
{
    public static MonumentCandidateRow ToRow(long mapId, MonumentCandidate c) => new()
    {
        MapId = mapId, CandX = c.X, CandY = c.Y, CandZ = c.Z, Source = c.Source,
        PedestalId = c.PedestalId, PedestalData = c.PedestalData, CapId = c.CapId, CapData = c.CapData,
        ColorHint = c.ColorHint, SignX = c.SignX, SignY = c.SignY, SignZ = c.SignZ,
        SignFacing = c.SignFacing, SignText = c.SignText,
        StandHeadColor = c.StandHeadColor, StandName = c.StandName, Evidence = c.Evidence,
    };

    public static MonumentCandidate ToCandidate(MonumentCandidateRow r) => new(
        r.CandX, r.CandY, r.CandZ, r.Source, r.PedestalId, r.PedestalData, r.CapId, r.CapData,
        r.ColorHint, r.SignX, r.SignY, r.SignZ, r.SignFacing, r.SignText,
        r.StandHeadColor, r.StandName, r.Evidence);

    /// <summary>Replace a map's candidates (idempotent re-gather). Returns the row count written.</summary>
    public static async Task<int> WriteAsync(PgmDb db, long mapId, IReadOnlyList<MonumentCandidate> candidates, CancellationToken ct = default)
    {
        await db.MonumentCandidates.Where(x => x.MapId == mapId).DeleteAsync(ct);
        var rows = candidates.Select(c => ToRow(mapId, c)).ToList();
        if (rows.Count > 0) await db.BulkCopyAsync(rows, ct);
        return rows.Count;
    }

    /// <summary>Load a map's gathered candidates as domain records (for <c>Score</c>).</summary>
    public static async Task<List<MonumentCandidate>> ReadAsync(PgmDb db, long mapId, CancellationToken ct = default) =>
        (await db.MonumentCandidates.Where(x => x.MapId == mapId).ToListAsync(ct)).Select(ToCandidate).ToList();
}
