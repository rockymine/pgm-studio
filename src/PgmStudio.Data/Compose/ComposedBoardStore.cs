using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Compose;

/// <summary>What a page of the library asks for: one composer version, size band and symmetry, narrowed by the
/// Generator page's filters. <paramref name="WoolCounts"/> takes any one of the counts named;
/// <paramref name="Wools"/> must each be present; <paramref name="Hubs"/> and
/// <paramref name="Frontlines"/> take any one of the forms named.</summary>
public sealed record ComposedBoardQuery(
    string ComposerVersion, string Band, string Symmetry,
    double? MaxScore = null, IReadOnlyList<int>? WoolCounts = null,
    IReadOnlyList<string>? Wools = null, IReadOnlyList<string>? Hubs = null, IReadOnlyList<string>? Frontlines = null);

/// <summary>The composed-board library's rows (<see cref="ComposedBoardRow"/>): read in pages best score first,
/// seed breaking ties, and written by <see cref="ComposedBoardLibrary.FillAsync"/>.</summary>
public sealed class ComposedBoardStore(PgmDb db)
{
    /// <summary>The page of boards <paramref name="query"/> matches from position <paramref name="from"/>, and how
    /// many it matches in all.</summary>
    public async Task<(List<ComposedBoardRow> Rows, int Matching)> PageAsync(
        ComposedBoardQuery query, int from, int count, CancellationToken ct = default)
    {
        var rows = Matching(query);
        var matching = await rows.CountAsync(ct);
        var page = await rows.OrderBy(row => row.Score).ThenBy(row => row.Seed)
            .Skip(from).Take(count).ToListAsync(ct);
        return (page, matching);
    }

    private IQueryable<ComposedBoardRow> Matching(ComposedBoardQuery query)
    {
        var rows = db.ComposedBoards.Where(row => row.ComposerVersion == query.ComposerVersion
                                                  && row.Band == query.Band && row.Symmetry == query.Symmetry);
        if (query.MaxScore is double maxScore) rows = rows.Where(row => row.Score <= maxScore);
        if (query.WoolCounts is { Count: > 0 })
        {
            var counts = query.WoolCounts.ToArray();
            rows = rows.Where(row => counts.Contains(row.WoolCount));
        }
        foreach (var family in query.Wools ?? [])
        {
            var token = ComposedBoardLibrary.WoolToken(family);
            rows = rows.Where(row => row.Wools.Contains(token));
        }
        if (query.Hubs is { Count: > 0 })
        {
            var hubs = query.Hubs.ToArray();
            rows = rows.Where(row => hubs.Contains(row.Hub));
        }
        if (query.Frontlines is { Count: > 0 })
        {
            var fronts = query.Frontlines.ToArray();
            rows = rows.Where(row => fronts.Contains(row.Frontline));
        }
        return rows;
    }

    /// <summary>The wool families, hub and frontline of every board one version holds for a band and symmetry —
    /// what the filter chips count.</summary>
    public async Task<List<(string Wools, string Hub, string Frontline)>> FormsAsync(
        string composerVersion, string band, string symmetry, CancellationToken ct = default) =>
        [
            .. (await db.ComposedBoards
                    .Where(row => row.ComposerVersion == composerVersion && row.Band == band && row.Symmetry == symmetry)
                    .Select(row => new { row.Wools, row.Hub, row.Frontline })
                    .ToListAsync(ct))
                .Select(row => (row.Wools, row.Hub, row.Frontline)),
        ];

    /// <summary>The composer version the feed shows for a band and symmetry: <paramref name="current"/> once it
    /// holds <paramref name="complete"/> boards, else the version most recently filled while the current one is
    /// being composed, else the current one's partial set. Null when the library holds nothing for them.</summary>
    public async Task<string?> ServedVersionAsync(
        string band, string symmetry, string current, int complete, CancellationToken ct = default)
    {
        var versions = await db.ComposedBoards
            .Where(row => row.Band == band && row.Symmetry == symmetry)
            .GroupBy(row => row.ComposerVersion)
            .Select(group => new { Version = group.Key, Count = group.Count(), Newest = group.Max(row => row.CreatedAt) })
            .ToListAsync(ct);
        var mine = versions.FirstOrDefault(version => version.Version == current);
        if (mine is not null && mine.Count >= complete) return current;
        return versions.Where(version => version.Version != current)
                   .OrderByDescending(version => version.Newest).FirstOrDefault()?.Version
               ?? mine?.Version;
    }

    /// <summary>The board one version holds for a band, symmetry, cell and seed.</summary>
    public Task<ComposedBoardRow?> GetAsync(
        string composerVersion, string band, string symmetry, int cell, ulong seed, CancellationToken ct = default) =>
        db.ComposedBoards.FirstOrDefaultAsync(row => row.ComposerVersion == composerVersion && row.Band == band
                                                     && row.Symmetry == symmetry && row.Cell == cell && row.Seed == seed, ct);

    /// <summary>The seeds one version already holds for a band, symmetry and cell.</summary>
    public async Task<HashSet<ulong>> SeedsAsync(
        string composerVersion, string band, string symmetry, int cell, CancellationToken ct = default) =>
        [
            .. await db.ComposedBoards
                .Where(row => row.ComposerVersion == composerVersion && row.Band == band
                              && row.Symmetry == symmetry && row.Cell == cell)
                .Select(row => row.Seed).ToListAsync(ct),
        ];

    public Task InsertAsync(ComposedBoardRow row, CancellationToken ct = default) => db.InsertAsync(row, token: ct);

    /// <summary>Delete every board another composer version made. They are the composer's output under settings
    /// the library states, so the version that replaced them composes the same library again.</summary>
    public Task<int> DeleteOtherVersionsAsync(string current, CancellationToken ct = default) =>
        db.ComposedBoards.Where(row => row.ComposerVersion != current).DeleteAsync(ct);
}
