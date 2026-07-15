using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Map;

/// <summary>
/// Data access for maps and their child rows. Thin over linq2db — the domain/codec layer
/// (PgmStudio.Pgm) owns translating between these rows and the PGM map contract.
/// </summary>
public sealed class MapRepository(PgmDb db)
{
    /// <summary>Insert a row and return its generated identity.</summary>
    public Task<long> InsertAsync<T>(T row) where T : class => db.InsertWithInt64IdentityAsync(row);

    public Task<MapRow?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => db.Maps.FirstOrDefaultAsync(m => m.Slug == slug, ct);

    /// <summary>A slug not already taken: <paramref name="baseSlug"/> if free, else the first free
    /// <c>baseSlug-2</c>, <c>baseSlug-3</c>, …. A download URL carries the world's own name, so two
    /// imports collide on it — suffix to the next free slug instead of failing.</summary>
    public async Task<string> UniqueSlugAsync(string baseSlug, CancellationToken ct = default)
    {
        var taken = (await db.Maps.Select(m => m.Slug).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseSlug)) return baseSlug;
        for (var i = 2; ; i++)
            if (!taken.Contains($"{baseSlug}-{i}")) return $"{baseSlug}-{i}";
    }

    public Task<MapRow?> GetByIdAsync(long id, CancellationToken ct = default)
        => db.Maps.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<List<MapRow>> ListAsync(CancellationToken ct = default)
        => db.Maps.OrderBy(m => m.Slug).ToListAsync(ct);

    /// <summary>Maps in one lifecycle stage (sketch | configure | edit), most recently touched first.</summary>
    public Task<List<MapRow>> ListByStageAsync(string stage, CancellationToken ct = default)
        => db.Maps.Where(m => m.Stage == stage).OrderByDescending(m => m.UpdatedAt).ThenBy(m => m.Slug).ToListAsync(ct);

    /// <summary>
    /// Every map's gamemodes, keyed by map id — derived from the objective rows it owns, never from the
    /// <c>&lt;gamemode&gt;</c> label, which most maps don't declare and some contradict. Three set lookups
    /// for the whole list rather than a join per map; the derivation itself is
    /// <see cref="Domain.Gamemodes.From"/>, shared with the parser so the two can't drift.
    /// <para>A map with no objective module is absent from the result: it has no gamemode, which is
    /// different from having an unknown one.</para>
    /// </summary>
    public async Task<Dictionary<long, IReadOnlyList<string>>> GamemodesAsync(CancellationToken ct = default)
    {
        var withWools = (await db.Wools.Select(w => w.MapId).Distinct().ToListAsync(ct)).ToHashSet();
        // A phantom is not an objective, so a map whose every destroyable is hidden contributes no DTM.
        var withDestroyables = (await db.Destroyables.Where(d => d.Show).Select(d => d.MapId).Distinct().ToListAsync(ct)).ToHashSet();
        var withCores = (await db.Cores.Select(c => c.MapId).Distinct().ToListAsync(ct)).ToHashSet();

        var result = new Dictionary<long, IReadOnlyList<string>>();
        foreach (var id in withWools.Union(withDestroyables).Union(withCores))
            result[id] = Domain.Gamemodes.From(withWools.Contains(id), withDestroyables.Contains(id), withCores.Contains(id));
        return result;
    }

    /// <summary>Map count per lifecycle stage (for the dashboard landing cards).</summary>
    public async Task<Dictionary<string, int>> StageCountsAsync(CancellationToken ct = default)
        => (await db.Maps.GroupBy(m => m.Stage).Select(g => new { Stage = g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Stage, x => x.Count);

    /// <summary>Advance (or set) a map's lifecycle stage.</summary>
    public Task<int> SetStageAsync(long mapId, string stage, CancellationToken ct = default)
        => db.Maps.Where(m => m.Id == mapId)
            .Set(m => m.Stage, stage).Set(m => m.UpdatedAt, DateTime.UtcNow).UpdateAsync(ct);

    public Task<List<TeamRow>> TeamsForMapAsync(long mapId, CancellationToken ct = default)
        => db.Teams.Where(t => t.MapId == mapId).OrderBy(t => t.Id).ToListAsync(ct);

    public Task<List<RegionRow>> RegionsForMapAsync(long mapId, CancellationToken ct = default)
        => db.Regions.Where(r => r.MapId == mapId).OrderBy(r => r.Id).ToListAsync(ct);

    public Task<List<WoolRow>> WoolsForMapAsync(long mapId, CancellationToken ct = default)
        => db.Wools.Where(w => w.MapId == mapId).OrderBy(w => w.Id).ToListAsync(ct);

    public Task<List<MonumentRow>> MonumentsForWoolAsync(long woolId, CancellationToken ct = default)
        => db.Monuments.Where(m => m.WoolId == woolId).OrderBy(m => m.Id).ToListAsync(ct);

    /// <summary>Delete a map; FK cascade removes all child rows.</summary>
    public Task<int> DeleteMapAsync(long mapId, CancellationToken ct = default)
        => db.Maps.Where(m => m.Id == mapId).DeleteAsync(ct);
}
