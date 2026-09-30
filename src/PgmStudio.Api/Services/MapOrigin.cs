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
/// <para><b>Every map has an owner from its first moment</b> — the person who originated it, who may always
/// edit it whoever it later credits (<c>docs/access.md</c>) — and it is credited to them as its author, which
/// they may take back from the map's metadata without ceasing to own it. An open studio's local admin has no
/// account, so a map it originates belongs and is credited to nobody.</para>
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
        MapRepository repo, string name, string stage, MapOriginator? originator, CancellationToken ct,
        long? planSource = null)
    {
        var slug = await repo.UniqueSlugAsync(Slugs.Of(name), ct);
        return (await RowAsync(repo, slug, name, stage, originator, originator?.Uuid, planSource), slug);
    }

    /// <summary>A map at exactly <paramref name="slug"/>, replacing whatever is stored there — the foreign
    /// keys cascade, so the old map's artifacts go with it. A replaced map keeps its owner and counts its
    /// revision on from the one it replaces, so a note written against the old board reads as older than the
    /// new one, and its artifacts are numbered above every revision the old one's reached, so a tab that read
    /// the old board cannot name the new one.</summary>
    public static async Task<long> ReplacingAsync(
        MapRepository repo, MapArtifactStore artifacts, string slug, string name, string stage,
        MapOriginator? originator, CancellationToken ct)
    {
        var owner = originator?.Uuid;
        long revision = 1, floor = 0;
        if (await repo.GetBySlugAsync(slug, ct) is { } existing)
        {
            owner = existing.OwnerUuid ?? owner;
            revision = existing.Revision + 1;
            floor = Math.Max(existing.ArtifactRevisionFloor, await artifacts.HighestRevisionAsync(existing.Id, ct));
            await repo.DeleteMapAsync(existing.Id, ct);
        }
        return await RowAsync(repo, slug, name, stage, originator, owner, planSource: null, revision, floor);
    }

    /// <summary>A map at a slug the caller has already established is free — a world import, which refuses a
    /// taken slug outright rather than suffixing past it, because the slug is where the world's files sit.
    /// </summary>
    public static Task<long> AtAsync(
        MapRepository repo, string slug, string name, string stage, MapOriginator? originator) =>
        RowAsync(repo, slug, name, stage, originator, originator?.Uuid, planSource: null);

    /// <summary>The row itself. Every map is <c>ctw</c> at birth — the gamemode is derived from the objective
    /// modules a map ends up carrying, and the column holds the author's original label, which a map that has
    /// not been authored yet does not have.</summary>
    private static async Task<long> RowAsync(
        MapRepository repo, string slug, string name, string stage, MapOriginator? originator, string? owner,
        long? planSource, long revision = 1, long artifactFloor = 0)
    {
        var now = DateTime.UtcNow;
        var mapId = await repo.InsertAsync(new MapRow
        {
            Slug = slug, Name = name, Gamemode = "ctw", Stage = stage,
            PlanSourceId = planSource, CreatedAt = now, UpdatedAt = now, OwnerUuid = owner, Revision = revision,
            ArtifactRevisionFloor = artifactFloor,
        });
        if (originator is not null)
            await repo.InsertAsync(new AuthorRow
            {
                MapId = mapId, Uuid = originator.Uuid, Name = originator.Name, Role = "author",
            });
        return mapId;
    }
}

/// <summary>The signed-in person a map is originated by: it is owned under their uuid and credited to them
/// under their Minecraft name.</summary>
public sealed record MapOriginator(string Uuid, string? Name);
