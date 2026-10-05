using Microsoft.Extensions.Logging;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Domain;
using PgmStudio.Api.Endpoints;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>What importing a world folder came to: a refusal, or the map that now exists and what was read
/// out of its region files.</summary>
public sealed record WorldImported(Refusal? Refusal, WorldScanDto? Scan = null);

/// <summary>
/// Originating a map from a world somebody else built: read the region files under a folder the studio can
/// see, and store what is in them as a map at the Configure stage.
///
/// <para><b>Four things are checked before anything is written</b>, and each is a different answer. The
/// folder must be one path segment — the imports root is a boundary and a name that could climb out of it is
/// the request's fault, not a missing world. It must hold a <c>region/</c>, or there is nothing there. It
/// must <em>not</em> hold a <c>map.xml</c>, because a world that already carries one is a map rather than
/// ground to originate one from, and the studio deliberately does not read a foreign <c>map.xml</c>. And the
/// slug it would take must be free, refused outright rather than suffixed past, because the slug is where the
/// world's files sit.</para>
///
/// <para><b>A fault leaves nothing behind.</b> The map row is written before the scan, so a scan that throws
/// would otherwise leave an empty map on the dashboard; the row is deleted and the fault is answered as the
/// studio's own.</para>
/// </summary>
public static class WorldFolderImport
{
    public static async Task<WorldImported> FromAsync(
        MapRepository repo, WorldFeatureWriter writer, ImportPolicy policy, ILogger logger,
        string folder, string? statedSlug, MapOriginator? originator, CancellationToken ct)
    {
        folder = folder.Trim();
        // A candidate folder is a single path segment under the imports root — anything that could escape it
        // is refused before it is joined onto the root.
        if (folder.Length == 0 || folder.Contains('/') || folder.Contains('\\') || folder.Contains(".."))
            return Refuse(400, "invalid folder", RequestRules.Unreadable,
                $"the request's `folder` is '{folder}', not one path segment under the imports root", "folder");

        var worldDir = Path.Combine(policy.Root, folder);
        var regionDir = Path.Combine(worldDir, "region");
        if (!Directory.Exists(regionDir))
            return Refuse(404, "no such world folder", RequestRules.NoSuchSubject,
                $"world folder '{folder}' is not under the imports root, or has no `region` folder", "folder");

        if (File.Exists(Path.Combine(worldDir, "map.xml")))
            return Refuse(422, "not a new-map candidate", ImportRules.AlreadyAMap,
                $"world folder '{folder}' has a map.xml", "folder");

        if (!Directory.EnumerateFiles(regionDir, "*.mca").Any())
            return Refuse(422, "nothing to import", ImportRules.NoRegions,
                $"world folder '{folder}' has a `region` folder with no region file", "folder");

        var slug = Slugs.OfFolder(statedSlug ?? folder);
        if (slug.Length == 0)
            return Refuse(400, "no slug given", RequestRules.Unreadable,
                "neither the request's `slug` nor its `folder` leaves anything a slug can be made of", "slug");

        if (await repo.GetBySlugAsync(slug, ct) is not null)
            return Refuse(409, "slug already taken", RequestRules.Conflict,
                $"map '{slug}' already exists", "slug");

        long? mapId = null;
        try
        {
            mapId = await MapOrigin.AtAsync(repo, slug, slug, MapStage.Configure, originator);
            var counts = await writer.WriteAsync(mapId.Value, regionDir, ct);
            return new(null, WorldScans.Of(slug, counts));
        }
        catch (Exception fault)
        {
            if (mapId is { } id) { try { await repo.DeleteMapAsync(id, ct); } catch { /* best effort */ } }
            logger.LogError(fault, "import-folder failed for {Slug}", slug);
            return Refuse(500, "import failed", RequestRules.Unhandled,
                $"the import of world folder '{folder}' failed, and what it had written was rolled back", field: null);
        }
    }

    private static WorldImported Refuse(int status, string error, string rule, string message, string? field) =>
        new(Refusal.At(status, error, new Finding(rule, message, Field: field)));
}
