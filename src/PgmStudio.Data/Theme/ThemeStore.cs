using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Theme;

/// <summary>
/// Persistence for the terrain-paint theme / style library (the M0011 tables, B44). Thin over linq2db: a
/// <see cref="StyleRow"/> is one reusable material recipe, a <see cref="ThemeRow"/> plus its
/// <see cref="ThemeBucketRow"/> bindings is a composition of styles. The store stays row-level (strings +
/// scalars) — turning a theme's rows into the <c>TerrainTheme</c> the painter consumes is a composition-root
/// concern, so it lives a layer up where the material model is reachable.
/// </summary>
public sealed class ThemeStore(PgmDb db)
{
    // ── styles ──────────────────────────────────────────────────────────────────
    /// <summary>Styles, newest first, optionally one <see cref="StyleKind"/> — the "every voronoi" browse.</summary>
    public Task<List<StyleRow>> ListStylesAsync(string? kind = null, CancellationToken ct = default)
        => (kind is null ? db.Styles : db.Styles.Where(s => s.Kind == kind))
            .OrderByDescending(s => s.Id).ToListAsync(ct);

    public Task<StyleRow?> GetStyleAsync(long id, CancellationToken ct = default)
        => db.Styles.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<long> CreateStyleAsync(StyleRow row, CancellationToken ct = default)
    {
        row.CreatedAt = DateTime.UtcNow;
        return db.InsertWithInt64IdentityAsync(row, token: ct);
    }

    public Task<int> UpdateStyleAsync(long id, string name, string kind, string paramsJson, CancellationToken ct = default)
        => db.Styles.Where(s => s.Id == id)
            .Set(s => s.Name, name).Set(s => s.Kind, kind).Set(s => s.Params, paramsJson)
            .UpdateAsync(ct);

    public Task<int> DeleteStyleAsync(long id, CancellationToken ct = default)
        => db.Styles.Where(s => s.Id == id).DeleteAsync(ct);

    // ── themes ──────────────────────────────────────────────────────────────────
    public Task<List<ThemeRow>> ListThemesAsync(CancellationToken ct = default)
        => db.Themes.OrderByDescending(t => t.Id).ToListAsync(ct);

    public Task<ThemeRow?> GetThemeAsync(long id, CancellationToken ct = default)
        => db.Themes.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<List<ThemeBucketRow>> GetBucketsAsync(long themeId, CancellationToken ct = default)
        => db.ThemeBuckets.Where(b => b.ThemeId == themeId).OrderBy(b => b.Bucket).ToListAsync(ct);

    /// <summary>A theme's bucket bindings joined to the styles they reference — the full read a caller needs to
    /// recompose the theme's material graph.</summary>
    public async Task<List<(ThemeBucketRow Bucket, StyleRow Style)>> GetBucketStylesAsync(long themeId, CancellationToken ct = default)
    {
        var rows = await (from b in db.ThemeBuckets
                          join s in db.Styles on b.StyleId equals s.Id
                          where b.ThemeId == themeId
                          select new { b, s }).ToListAsync(ct);
        return rows.Select(r => (r.b, r.s)).ToList();
    }

    /// <summary>Create a theme with its bucket bindings in one transaction, returning the new theme id.</summary>
    public async Task<long> CreateThemeAsync(ThemeRow theme, IEnumerable<ThemeBucketRow> buckets, CancellationToken ct = default)
    {
        theme.CreatedAt = DateTime.UtcNow;
        await using var tx = await db.BeginTransactionAsync(ct);
        var id = await db.InsertWithInt64IdentityAsync(theme, token: ct);
        foreach (var bucket in buckets)
        {
            bucket.ThemeId = id;
            await db.InsertAsync(bucket, token: ct);
        }
        await tx.CommitAsync(ct);
        return id;
    }

    public Task<int> DeleteThemeAsync(long id, CancellationToken ct = default)
        => db.Themes.Where(t => t.Id == id).DeleteAsync(ct);   // theme_bucket cascades (M0011)
}
