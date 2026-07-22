using System.Security.Cryptography;
using System.Text;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Data.Plan;

/// <summary>
/// Persistence for layout plans (the M0008 <c>plan</c> corpus). Thin over linq2db, but — unlike the map
/// repository, whose codec lives a layer up — this store owns the one place that <b>normalizes and hashes</b>
/// a plan document: every write re-serializes the plan through <see cref="PlanModel"/> so the stored JSON is
/// canonical and the <c>content_hash</c> is stable regardless of the caller's formatting.
/// <para>The doctrine it enforces: generated/imported rows are immutable, so editing one <b>forks</b> a new
/// authored row with a <c>parent_id</c> back-reference; only an authored row is updated in place. Dedup is a
/// per-origin content-hash lookup (a re-import or a re-generation of identical geometry returns the existing
/// row rather than duplicating it).</para>
/// </summary>
public sealed class PlanStore(PgmDb db)
{
    public Task<PlanRow?> GetByIdAsync(long id, CancellationToken ct = default)
        => db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <summary>Plans, newest-touched first, optionally one origin.</summary>
    public Task<List<PlanRow>> ListAsync(string? origin = null, CancellationToken ct = default)
        => (origin is null ? db.Plans : db.Plans.Where(p => p.Origin == origin))
            .OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.Id).ToListAsync(ct);

    /// <summary>The first row with this content hash, optionally scoped to one origin — the dedup lookup.</summary>
    public Task<PlanRow?> GetByContentHashAsync(string hash, string? origin = null, CancellationToken ct = default)
        => (origin is null ? db.Plans : db.Plans.Where(p => p.Origin == origin))
            .OrderBy(p => p.Id).FirstOrDefaultAsync(p => p.ContentHash == hash, ct);

    public Task<int> DeleteAsync(long id, CancellationToken ct = default)
        => db.Plans.Where(p => p.Id == id).DeleteAsync(ct);

    /// <summary>
    /// Save a plan edited in the studio. With no <paramref name="sourceId"/> this inserts a fresh authored
    /// row. With one, it loads the source: an <b>authored</b> source is updated in place; a generated or
    /// imported source is left untouched and a new authored row is <b>forked</b> off it (<c>parent_id</c> set
    /// — unless the source has since been deleted, in which case the fork is parentless). Returns the row the
    /// editor now holds.
    /// </summary>
    public async Task<PlanRow> SaveFromEditorAsync(string planJson, long? sourceId, CancellationToken ct = default)
    {
        var (canonical, name, hash) = Canonicalize(planJson);
        var now = DateTime.UtcNow;

        if (sourceId is long sid)
        {
            var source = await GetByIdAsync(sid, ct);
            if (source is { Origin: PlanOrigin.Authored })
            {
                await db.Plans.Where(p => p.Id == sid)
                    .Set(p => p.Name, name).Set(p => p.PlanJson, canonical)
                    .Set(p => p.ContentHash, hash).Set(p => p.UpdatedAt, now)
                    .UpdateAsync(ct);
                return (await GetByIdAsync(sid, ct))!;
            }
            return await InsertAsync(new PlanRow
            {
                Name = name, Origin = PlanOrigin.Authored, PlanJson = canonical, ContentHash = hash,
                ParentId = source is null ? null : sid, CreatedAt = now, UpdatedAt = now,
            });
        }

        return await InsertAsync(new PlanRow
        {
            Name = name, Origin = PlanOrigin.Authored, PlanJson = canonical, ContentHash = hash,
            CreatedAt = now, UpdatedAt = now,
        });
    }

    /// <summary>Persist a composer output with its canonical versioned descriptor. Deduped against existing
    /// generated rows: identical geometry already stored returns that row rather than a duplicate.</summary>
    public async Task<PlanRow> SaveGeneratedAsync(string planJson, ComposeDescriptor descriptor, CancellationToken ct = default)
    {
        var (canonical, name, hash) = Canonicalize(planJson);
        if (await GetByContentHashAsync(hash, PlanOrigin.Generated, ct) is { } existing) return existing;

        var now = DateTime.UtcNow;
        return await InsertAsync(new PlanRow
        {
            Name = name, Origin = PlanOrigin.Generated, PlanJson = canonical, ContentHash = hash,
            RequestJson = descriptor.ToJson(), Seed = descriptor.Seed, ComposerVersion = descriptor.ComposerVersion,
            CreatedAt = now, UpdatedAt = now,
        });
    }

    /// <summary>Persist an imported <c>*.plan.json</c> file. Deduped against existing imported rows (import
    /// identity: the same file imported twice is the same row).</summary>
    public async Task<PlanRow> SaveImportedAsync(string planJson, CancellationToken ct = default)
    {
        var (canonical, name, hash) = Canonicalize(planJson);
        if (await GetByContentHashAsync(hash, PlanOrigin.Imported, ct) is { } existing) return existing;

        var now = DateTime.UtcNow;
        return await InsertAsync(new PlanRow
        {
            Name = name, Origin = PlanOrigin.Imported, PlanJson = canonical, ContentHash = hash,
            CreatedAt = now, UpdatedAt = now,
        });
    }

    private async Task<PlanRow> InsertAsync(PlanRow row)
    {
        row.Id = await db.InsertWithInt64IdentityAsync(row);
        return row;
    }

    /// <summary>Normalize the plan document to its canonical serialization, and derive the list name (from
    /// <c>meta.name</c>) and the content hash. A malformed document is rejected here — callers on the wire
    /// guard the body first, but a direct caller gets a clear failure rather than a corrupt row.</summary>
    private static (string Canonical, string Name, string Hash) Canonicalize(string planJson)
    {
        var model = PlanModel.Parse(planJson)
            ?? throw new ArgumentException("plan document is malformed", nameof(planJson));
        var canonical = model.ToJson();
        return (canonical, model.Meta?.Name ?? "", Sha256Hex(canonical));
    }

    private static string Sha256Hex(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
