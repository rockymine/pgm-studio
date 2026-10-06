using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using PgmStudio.Api.Access;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// What the import refuses that is neither the request's shape nor the studio's own fault: the world it was
/// pointed at is one the studio will not or cannot take.
///
/// <para>These are the import's own, the way <c>SK*</c> are the sketch document's — a host outside the
/// allowlist, an archive that never arrived, one too large to fetch, one that is not an archive, and one
/// carrying no world. Each has its own remedy, which is why each has an id rather than sharing one: the
/// status alone says the import stopped and never which of the six things to change.</para>
/// </summary>
internal static class ImportRules
{
    /// <summary>The url of an import request is not on the list of hosts the studio fetches from.</summary>
    /// <remarks>Either send the request again with <c>url</c> set to an archive on an allowed host, or ask the
    /// person who runs the studio to add the host to <c>Import:AllowedHosts</c>.</remarks>
    [Rule(RuleCategory.Forbidden, RuleConcern.Request, RuleConcern.Studio)]
    public const string HostNotAllowed = "IM1";

    /// <summary>The status the host answered for the url of an import request is not between 200 and 299.</summary>
    /// <remarks>Ask the person who hosts the archive to serve it at the <c>url</c> without a login or a redirect,
    /// then send the request again.</remarks>
    [Rule(RuleCategory.Unavailable, RuleConcern.Request, RuleConcern.Studio)]
    public const string DownloadFailed = "IM2";

    /// <summary>The archive an import request names is more than 256 mebibytes.</summary>
    /// <remarks>Send the request again with <c>url</c> set to an archive that holds only the <c>region</c> folder
    /// of the world.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Request, RuleConcern.Studio)]
    public const string DownloadTooLarge = "IM3";

    /// <summary>The file the url of an import request serves is not a zip archive.</summary>
    /// <remarks>Send the request again with <c>url</c> set to the zip archive itself, not a page that links to it
    /// or a login page.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Request)]
    public const string NotAnArchive = "IM4";

    /// <summary>The archive or folder an import names has no region file.</summary>
    /// <remarks>Send the request again with <c>url</c> or <c>folder</c> set to the world folder that holds the
    /// <c>region</c> folder, not the server folder above it.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Request, RuleConcern.World)]
    public const string NoRegions = "IM5";

    /// <summary>The folder an import names has a map.xml.</summary>
    /// <remarks>Either send the request again with <c>folder</c> set to a world folder with no map.xml, or ask the
    /// person who runs the studio to move the map.xml out of the folder.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request, RuleConcern.World)]
    public const string AlreadyAMap = "IM6";

    /// <summary>The map a world is imported into has a world the studio did not import.</summary>
    /// <remarks>Either send the request again for a map that was imported from a download link, or send
    /// <c>POST /map/import-url</c> to import the world as a new map.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request, RuleConcern.World)]
    public const string NotImported = "IM7";
}

/// <summary>
/// POST /api/map/import-url — import from a URL (docs/tools/configure.md, the Import phase): fetch a zipped
/// Minecraft world from an allowlisted host, create the map row, and scan it into MariaDB
/// (<see cref="WorldUrlImport.NewAsync"/>).
/// </summary>
public sealed class ImportUrlEndpoint(MapRepository repo, WorldFeatureWriter writer, ImportPolicy policy, IHttpClientFactory httpFactory)
    : EndpointWithoutRequest<WorldScanDto>
{
    public override void Configure()
    {
        Post("/map/import-url");
        Description(b => b.Accepts<ImportUrlRequest>("application/json").Refuses(403, 413, 415, 422, 502));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await ImportBody.ReadAsync(HttpContext, ct) is not { } body) return;
        var imported = await WorldUrlImport.NewAsync(repo, writer, policy, httpFactory, Logger,
            body["url"]?.GetValue<string>(), body["slug"]?.GetValue<string>(), Callers.OriginatorOf(HttpContext), ct);
        if (imported.Refusal is { } refusal) { await Refusals.WriteAsync(HttpContext, refusal, ct); return; }
        await Send.OkAsync(imported.Scan!, ct);
    }
}

/// <summary>
/// POST /api/map/{slug}/import-url — import a world from a URL into a map that was imported from one, over the
/// world it has (<see cref="WorldUrlImport.IntoAsync"/>): the map's scan is read from the new world, and what
/// the map states is left as it is.
/// </summary>
public sealed class MapImportUrlEndpoint(
    MapRepository repo, WorldFeatureWriter writer, MapArtifactStore artifacts, PgmDb db, ImportPolicy policy,
    MapsRoots roots, IHttpClientFactory httpFactory) : EndpointWithoutRequest<WorldScanDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/import-url");
        Description(b => b.Accepts<ImportUrlRequest>("application/json").Refuses(403, 404, 409, 413, 415, 422, 502));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if (await ImportBody.ReadAsync(HttpContext, ct) is not { } body) return;
        var imported = await WorldUrlImport.IntoAsync(map, writer, artifacts, db, policy, roots, httpFactory, Logger,
            body["url"]?.GetValue<string>(), ct);
        if (imported.Refusal is { } refusal) { await Refusals.WriteAsync(HttpContext, refusal, ct); return; }
        await Send.OkAsync(imported.Scan!, ct);
    }
}

/// <summary>The body both URL imports take, read as a JSON object, or null once the refusal is written.</summary>
internal static class ImportBody
{
    public static async Task<JsonObject?> ReadAsync(HttpContext http, CancellationToken ct)
    {
        var raw = await RawBody.ReadAsync(http, ct);
        try { return (JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw) as JsonObject) ?? new JsonObject(); }
        catch (JsonException fault) { await Refusals.UnreadableAsync(http, "invalid json body", fault, ct); return null; }
    }
}

/// <summary>
/// GET /api/maps/import-candidates — world folders under the maps roots with <c>region/*.mca</c> but no
/// <c>map.xml</c> and not already a map: the new-map import candidates (the "open a local folder" source).
/// </summary>
public sealed class ImportCandidatesEndpoint(MapRepository repo, ImportPolicy policy)
    : EndpointWithoutRequest<List<ImportCandidateDto>>
{
    public override void Configure() { Get("/maps/import-candidates"); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var existing = (await repo.ListAsync(ct)).Select(m => m.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<ImportCandidateDto>();
        // Candidates live only in the dedicated imports root — never the curated xml corpus.
        if (Directory.Exists(policy.Root))
            foreach (var dir in Directory.EnumerateDirectories(policy.Root))
            {
                var folder = Path.GetFileName(dir);
                if (File.Exists(Path.Combine(dir, "map.xml"))) continue;           // already an xml map
                var region = Path.Combine(dir, "region");
                if (!Directory.Exists(region)) continue;
                var mca = Directory.EnumerateFiles(region, "*.mca").Count();
                if (mca == 0) continue;
                var slug = Slugs.OfFolder(folder);
                if (slug.Length == 0 || existing.Contains(slug)) continue;          // skip unsluggable / already-imported
                candidates.Add(new ImportCandidateDto(folder, slug, mca));
            }
        candidates.Sort((a, b) => string.Compare(a.Folder, b.Folder, StringComparison.Ordinal));
        await Send.OkAsync(candidates, ct);
    }
}

/// <summary>
/// POST /api/map/import-folder { slug } — import a local xml-less world (the "open a folder" source): resolve
/// <c>&lt;root&gt;/&lt;slug&gt;/region</c> via <see cref="MapsRoots"/> (only configured roots — no client path),
/// create the map row, and scan into MariaDB. The slug must be a real candidate (region/*.mca, no map.xml,
/// not already a map). Rolls back the row on failure.
/// </summary>
public sealed class ImportFolderEndpoint(MapRepository repo, WorldFeatureWriter writer, ImportPolicy policy)
    : EndpointWithoutRequest<WorldScanDto>
{
    public override void Configure()
    {
        Post("/map/import-folder");
        Description(b => b.Accepts<ImportFolderRequest>("application/json").Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var raw = await RawBody.ReadAsync(HttpContext, ct);
        JsonObject body;
        try { body = (JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw) as JsonObject) ?? new(); }
        catch (JsonException fault) { await Refusals.UnreadableAsync(HttpContext, "invalid json body", fault, ct); return; }

        var imported = await WorldFolderImport.FromAsync(
            repo, writer, policy, Logger,
            body["folder"]?.GetValue<string>() ?? "", body["slug"]?.GetValue<string>(),
            Callers.OriginatorOf(HttpContext), ct);
        if (imported.Refusal is { } refusal) { await Refusals.WriteAsync(HttpContext, refusal, ct); return; }

        await Send.OkAsync(imported.Scan!, ct);
    }
}
