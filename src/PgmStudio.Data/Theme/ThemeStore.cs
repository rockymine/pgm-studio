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

    /// <summary>The styles behind a set of ids, in one read — what composing a theme's bindings needs.</summary>
    public Task<List<StyleRow>> GetStylesAsync(IEnumerable<long> ids, CancellationToken ct = default)
    {
        var wanted = ids.Distinct().ToArray();
        return wanted.Length == 0
            ? Task.FromResult(new List<StyleRow>())
            : db.Styles.Where(s => wanted.Contains(s.Id)).ToListAsync(ct);
    }

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

    /// <summary>The names of the themes still binding a style, newest first — empty when nothing does. A style
    /// is shared, so a caller asks this before deleting one and can say which themes would break instead of
    /// letting the binding's foreign key answer.</summary>
    public Task<List<string>> ThemesUsingStyleAsync(long styleId, CancellationToken ct = default)
        => (from b in db.ThemeBuckets
            join t in db.Themes on b.ThemeId equals t.Id
            where b.StyleId == styleId
            orderby t.Id descending
            select t.Name).Distinct().ToListAsync(ct);

    // ── themes ──────────────────────────────────────────────────────────────────
    public Task<List<ThemeRow>> ListThemesAsync(CancellationToken ct = default)
        => db.Themes.OrderByDescending(t => t.Id).ToListAsync(ct);

    public Task<ThemeRow?> GetThemeAsync(long id, CancellationToken ct = default)
        => db.Themes.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<List<ThemeBucketRow>> GetBucketsAsync(long themeId, CancellationToken ct = default)
        => db.ThemeBuckets.Where(b => b.ThemeId == themeId).OrderBy(b => b.Bucket).ToListAsync(ct);

    /// <summary>A theme's bucket bindings with the styles they reference — the full read a caller needs to
    /// recompose the theme's material graph. The style is <b>null</b> for a binding that names none (M0013):
    /// such a row carries only its depth and its toggle, and dropping it — which an inner join would — is how
    /// a theme with the rim switched off would come back with the rim on.</summary>
    public Task<List<(ThemeBucketRow Bucket, StyleRow? Style)>> GetBucketStylesAsync(long themeId, CancellationToken ct = default)
        => BucketStylesAsync(db.ThemeBuckets.Where(b => b.ThemeId == themeId), ct);

    /// <summary>Every theme's bucket bindings with their styles, in one read — what a caller recomposing the
    /// <em>whole</em> library needs. Listing themes with a picture of each is the normal case, and asking per
    /// theme turns one list into a query per row.</summary>
    public Task<List<(ThemeBucketRow Bucket, StyleRow? Style)>> GetAllBucketStylesAsync(CancellationToken ct = default)
        => BucketStylesAsync(db.ThemeBuckets, ct);

    private async Task<List<(ThemeBucketRow Bucket, StyleRow? Style)>> BucketStylesAsync(
        IQueryable<ThemeBucketRow> buckets, CancellationToken ct)
    {
        var rows = await (from b in buckets
                          from s in db.Styles.LeftJoin(s => s.Id == b.StyleId)
                          select new { b, s }).ToListAsync(ct);
        return rows.Select(row => (row.b, (StyleRow?)row.s)).ToList();
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

    /// <summary>Replace a theme's knobs and its whole set of bucket bindings in one transaction, returning false
    /// when the theme id is unknown. The bindings are rewritten rather than merged: a bucket may be rebound to a
    /// different style, and the set itself is what the caller edited, so a diff would only be a slower way to
    /// arrive at the same rows.</summary>
    public async Task<bool> UpdateThemeAsync(long id, ThemeRow theme, IEnumerable<ThemeBucketRow> buckets,
        CancellationToken ct = default)
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        var updated = await db.Themes.Where(t => t.Id == id)
            .Set(t => t.Name, theme.Name)
            .Set(t => t.BedrockRelative, theme.BedrockRelative)
            .Set(t => t.BedrockValue, theme.BedrockValue)
            .Set(t => t.RimEdges, theme.RimEdges)
            .Set(t => t.WallOnTerrainFaces, theme.WallOnTerrainFaces)
            .UpdateAsync(ct);
        if (updated == 0) return false;

        await db.ThemeBuckets.Where(b => b.ThemeId == id).DeleteAsync(ct);
        foreach (var bucket in buckets)
        {
            bucket.ThemeId = id;
            await db.InsertAsync(bucket, token: ct);
        }
        await tx.CommitAsync(ct);
        return true;
    }

    public Task<int> DeleteThemeAsync(long id, CancellationToken ct = default)
        => db.Themes.Where(t => t.Id == id).DeleteAsync(ct);   // theme_bucket cascades (M0011)
}
