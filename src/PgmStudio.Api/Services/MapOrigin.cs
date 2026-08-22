using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// Bringing a map row into existence, which every way into the studio does exactly once.
///
/// <para><b>Six callers, one row.</b> A plan created blank, a plan authored from a generator candidate, a
/// sketch, a folder import, a URL import and a load from documents all begin here and differ only in the
/// stage they start at, what they seed afterwards, and whether the slug they want is theirs to take. What a
/// map row says about itself the moment it exists — the gamemode it is born at, the two timestamps a
/// newest-touched-first list is ordered by — is the same sentence for all six, and is said here.</para>
///
/// <para><b>Two ways to take a slug, and the difference is a product statement.</b> Everything an author
/// originates suffixes past a collision, because two sketches called "Weirgate" are two maps. A load from
/// documents replaces instead, because the documents name one map and loading them twice is a reload.</para>
/// </summary>
public static class MapOrigin
{
    /// <summary>A map under the first free slug from <paramref name="name"/> — <c>weirgate</c>, then
    /// <c>weirgate-2</c>. Answers the id and the slug it actually took, which is not always the one asked
    /// for.</summary>
    public static async Task<(long Id, string Slug)> UnderFreeSlugAsync(
        MapRepository repo, string name, string stage, CancellationToken ct, long? planSource = null)
    {
        var slug = await repo.UniqueSlugAsync(Slugs.Of(name), ct);
        return (await RowAsync(repo, slug, name, stage, planSource), slug);
    }

    /// <summary>A map at exactly <paramref name="slug"/>, replacing whatever is stored there — the foreign
    /// keys cascade, so the old map's artifacts go with it.</summary>
    public static async Task<long> ReplacingAsync(
        MapRepository repo, string slug, string name, string stage, CancellationToken ct)
    {
        if (await repo.GetBySlugAsync(slug, ct) is { } existing) await repo.DeleteMapAsync(existing.Id, ct);
        return await RowAsync(repo, slug, name, stage, planSource: null);
    }

    /// <summary>A map at a slug the caller has already established is free — a world import, which refuses a
    /// taken slug outright rather than suffixing past it, because the slug is where the world's files sit.
    /// </summary>
    public static Task<long> AtAsync(MapRepository repo, string slug, string name, string stage) =>
        RowAsync(repo, slug, name, stage, planSource: null);

    /// <summary>The row itself. Every map is <c>ctw</c> at birth — the gamemode is derived from the objective
    /// modules a map ends up carrying, and the column holds the author's original label, which a map that has
    /// not been authored yet does not have.</summary>
    private static Task<long> RowAsync(
        MapRepository repo, string slug, string name, string stage, long? planSource)
    {
        var now = DateTime.UtcNow;
        return repo.InsertAsync(new MapRow
        {
            Slug = slug, Name = name, Gamemode = "ctw", Stage = stage,
            PlanSourceId = planSource, CreatedAt = now, UpdatedAt = now,
        });
    }
}
