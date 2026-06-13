using LinqToDB;
using LinqToDB.Async;

namespace PgmStudio.Data.Repositories;

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

    public Task<MapRow?> GetByIdAsync(long id, CancellationToken ct = default)
        => db.Maps.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<List<MapRow>> ListAsync(CancellationToken ct = default)
        => db.Maps.OrderBy(m => m.Slug).ToListAsync(ct);

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
