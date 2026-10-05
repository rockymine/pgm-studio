using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
}

/// <summary>
/// POST /api/map/import-url — import from a URL (docs/tools/configure.md, the Import phase). Server-side: fetch a zipped
/// Minecraft world from an <b>allowlisted</b> host, safely extract only <c>region/*.mca</c>, create the map
/// row, and scan it into MariaDB (reusing <see cref="WorldFeatureWriter"/>). The browser never sees the zip.
/// <para><b>Safeguards:</b> https-only + host allowlist (SSRF) · no redirects · download size cap · zip
/// magic-byte check · zip-slip-safe (basename-only dest paths) + zip-bomb-safe (per-entry/total/count caps)
/// extraction · requires <c>region/*.mca</c> · sanitised + unique slug · rolls back row + files on any failure.</para>
/// </summary>
public sealed class ImportUrlEndpoint(MapRepository repo, WorldFeatureWriter writer, ImportPolicy policy, IHttpClientFactory httpFactory)
    : EndpointWithoutRequest<WorldScanDto>
{
    private static readonly Regex RegionMca = new(@"(^|/)region/[^/\\]+\.mca$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex McaName   = new(@"^r\.-?\d+\.-?\d+\.mca$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SlugStrip = new("[^a-z0-9_-]", RegexOptions.Compiled);

    public override void Configure()
    {
        Post("/map/import-url");
        Description(b => b.Accepts<ImportUrlRequest>("application/json").Refuses(403, 413, 415, 422, 502));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var raw = await RawBody.ReadAsync(HttpContext, ct);
        JsonObject body;
        try { body = (JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw) as JsonObject) ?? new JsonObject(); }
        catch (JsonException fault) { await Refusals.UnreadableAsync(HttpContext, "invalid json body", fault, ct); return; }
        var url = body["url"]?.GetValue<string>();

        // ── 1. URL safeguards (SSRF) ──
        if (string.IsNullOrWhiteSpace(url))
        {
            await Refusals.UnreadableAsync(HttpContext, "no url given",
                "the request's `url` is absent", ct, field: "url");
            return;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await Refusals.UnreadableAsync(HttpContext, "invalid url",
                $"the request's `url` is '{url}', not an absolute url", ct, field: "url");
            return;
        }
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            await Refusals.UnreadableAsync(HttpContext, "https url required",
                $"the request's `url` has the scheme '{uri.Scheme}', not https", ct, field: "url");
            return;
        }
        if (!policy.HostAllowed(uri.Host))
        {
            await Refusals.WriteAsync(HttpContext, 403, "host not allowed",
                [new Finding(ImportRules.HostNotAllowed,
                    $"host '{uri.Host}' of the request's `url` is not one of the hosts the import fetches from",
                    Field: "url")], ct);
            return;
        }

        // ── 2. slug (sanitised, auto-uniquified) ──
        // The URL's last segment is the world's own name, so independent imports of the same map collide;
        // suffix to the next free slug (rockymine → rockymine-2) rather than rejecting the import.
        var baseSlug = Sanitize(body["slug"]?.GetValue<string>() ?? LastSegment(uri));
        if (baseSlug.Length == 0)
            {
                await Refusals.UnreadableAsync(HttpContext, "no slug in the url",
                    "the last segment of the request's `url` leaves nothing a slug can be made of", ct, field: "url");
                return;
            }
        var slug = await repo.UniqueSlugAsync(baseSlug, ct);

        var slugDir = Path.Combine(policy.Root, slug);
        var regionDir = Path.Combine(slugDir, "region");
        var tmpZip = Path.Combine(Path.GetTempPath(), $"pgm-import-{Guid.NewGuid():N}.zip");
        long? mapId = null;
        try
        {
            // ── 3. download (allowlisted host, no redirects, timeout, size-capped) ──
            var client = httpFactory.CreateClient("import");
            using var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                await Refusals.WriteAsync(HttpContext, 502, "download failed",
                    [new Finding(ImportRules.DownloadFailed,
                        $"host '{uri.Host}' answered {(int)resp.StatusCode} for the request's `url`, "
                        + "not between 200 and 299", Field: "url")], ct);
                return;
            }
            if (resp.Content.Headers.ContentLength is { } len && len > policy.MaxDownloadBytes)
            {
                await Refusals.WriteAsync(HttpContext, 413, "download too large",
                    [new Finding(ImportRules.DownloadTooLarge,
                        $"the archive at the request's `url` is {len} bytes, more than {policy.MaxDownloadBytes} bytes",
                        Field: "url")], ct);
                return;
            }
            await using (var net = await resp.Content.ReadAsStreamAsync(ct))
            await using (var file = File.Create(tmpZip))
                await CopyCappedAsync(net, file, policy.MaxDownloadBytes, ct);

            // ── 4. zip magic ──
            if (!await IsZipAsync(tmpZip, ct))
            {
                await Refusals.WriteAsync(HttpContext, 415, "not a zip archive",
                    [new Finding(ImportRules.NotAnArchive,
                        "the file at the request's `url` does not begin with a zip header", Field: "url")], ct);
                return;
            }

            // ── 5. safe extract: ONLY region/*.mca, basename-only dest (zip-slip), bounded (zip-bomb) ──
            var mca = SafeExtractRegionMca(tmpZip, regionDir, policy);
            if (mca == 0)
            {
                TryDeleteDir(slugDir);
                await Refusals.WriteAsync(HttpContext, 422, "nothing to import",
                    [new Finding(ImportRules.NoRegions,
                        "the archive at the request's `url` has no region file", Field: "url")], ct);
                return;
            }

            // ── 6. create record + scan into MariaDB ──
            mapId = await MapOrigin.AtAsync(repo, slug, slug, MapStage.Configure, Callers.OriginatorOf(HttpContext));
            var c = await writer.WriteAsync(mapId.Value, regionDir, ct);

            await Send.OkAsync(WorldScans.Of(slug, c) with { McaFiles = mca }, ct);
        }
        catch (Exception ex)
        {
            // Roll back so a failed import leaves nothing behind.
            if (mapId is { } id) { try { await repo.DeleteMapAsync(id, ct); } catch { /* best effort */ } }
            TryDeleteDir(slugDir);
            Logger.LogError(ex, "import-url failed for slug {Slug}", slug);
            await Refusals.WriteAsync(HttpContext, 500, "import failed",
                [new Finding(RequestRules.Unhandled,
                    $"the import of map '{slug}' failed, and what it had written was rolled back")], ct);
        }
        finally { try { File.Delete(tmpZip); } catch { /* ignore */ } }
    }


    private static string Sanitize(string s)
    {
        var slug = SlugStrip.Replace(s.Trim().ToLowerInvariant(), "").Trim('-', '_');
        return slug.Length > 64 ? slug[..64] : slug;
    }

    private static string LastSegment(Uri uri) =>
        Uri.UnescapeDataString(uri.AbsolutePath.TrimEnd('/').Split('/').LastOrDefault() ?? "");

    private static async Task CopyCappedAsync(Stream src, Stream dst, long max, CancellationToken ct)
    {
        var buf = new byte[81920]; long total = 0; int n;
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            total += n;
            if (total > max) throw new InvalidOperationException("download exceeded size cap");
            await dst.WriteAsync(buf.AsMemory(0, n), ct);
        }
    }

    private static async Task<bool> IsZipAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var sig = new byte[4];
        if (await fs.ReadAsync(sig.AsMemory(0, 4), ct) < 4) return false;
        // PK\x03\x04 (local file header) or PK\x05\x06 (empty-archive end-of-central-directory)
        return sig[0] == 0x50 && sig[1] == 0x4B && ((sig[2] == 0x03 && sig[3] == 0x04) || (sig[2] == 0x05 && sig[3] == 0x06));
    }

    /// <summary>Extract ONLY <c>region/*.mca</c> entries, flattened to <c>&lt;regionDir&gt;/&lt;basename&gt;</c>
    /// (we choose the path from the basename, so a crafted entry path can't escape — zip-slip), bounded by
    /// per-entry / total-uncompressed / entry-count caps (zip-bomb). Returns the number extracted.</summary>
    private static int SafeExtractRegionMca(string zipPath, string regionDir, ImportPolicy p)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count > p.MaxEntries) throw new InvalidOperationException("too many zip entries");

        Directory.CreateDirectory(regionDir);
        long totalUncompressed = 0; int extracted = 0;
        foreach (var e in zip.Entries)
        {
            if (e.FullName.Length == 0 || e.FullName.EndsWith('/')) continue;   // directory entry
            if (!RegionMca.IsMatch(e.FullName)) continue;                       // only region/*.mca
            var name = Path.GetFileName(e.Name);                               // basename ONLY → defeats zip-slip
            if (!McaName.IsMatch(name)) continue;                              // r.X.Z.mca naming
            if (e.Length > p.MaxEntryBytes) throw new InvalidOperationException("zip entry too large");

            var dest = Path.Combine(regionDir, name);
            using (var es = e.Open())
            using (var fs = File.Create(dest))
                totalUncompressed += CopyCapped(es, fs, p.MaxEntryBytes);      // real bytes (defeats a lying Length)
            if (totalUncompressed > p.MaxUncompressedBytes) throw new InvalidOperationException("uncompressed size cap exceeded");
            extracted++;
        }
        return extracted;
    }

    private static long CopyCapped(Stream src, Stream dst, long max)
    {
        var buf = new byte[81920]; long total = 0; int n;
        while ((n = src.Read(buf, 0, buf.Length)) > 0)
        {
            total += n;
            if (total > max) throw new InvalidOperationException("zip entry exceeded size cap");
            dst.Write(buf, 0, n);
        }
        return total;
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
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
