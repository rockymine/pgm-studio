using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;

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
    /// <summary>The url names a host the import does not fetch from. The allowlist is the studio's SSRF
    /// guard — an import fetches server-side, so an arbitrary host would make the studio a proxy into
    /// whatever its network can reach. 403.</summary>
    /// <remarks>Host the archive somewhere the studio already fetches from, or download it and import the world with <c>POST /api/map/import-folder</c> instead.</remarks>
    public const string HostNotAllowed = "IM1";

    /// <summary>The host answered, and not with the archive: a 404, a 403, a 5xx. Nothing about the request
    /// is wrong, so it is reported as what it is — 502, the upstream's answer rather than the studio's.</summary>
    /// <remarks>Open the url yourself. A release asset behind a login or a redirect answers this, and the import follows no redirects by design.</remarks>
    public const string DownloadFailed = "IM2";

    /// <summary>The archive states more bytes than the import will fetch. 413, before anything is
    /// downloaded.</summary>
    /// <remarks>Trim the archive to the world's <c>region/</c> folder — that is all the import reads — or import it from the imports root as a folder.</remarks>
    public const string DownloadTooLarge = "IM3";

    /// <summary>What arrived is not a zip: it does not begin with the header. 415, before extraction, so a
    /// login page served as HTML fails here rather than inside the unpacker.</summary>
    /// <remarks>The url must serve the archive itself. A page that links to it, or a login wall, arrives as HTML and reads exactly like this.</remarks>
    public const string NotAnArchive = "IM4";

    /// <summary>There is no world in it. The import reads <c>region/*.mca</c> and nothing else, so an archive
    /// or folder without them carries nothing the studio can scan. 422.</summary>
    /// <remarks>Check that the archive holds the world folder's <c>region/</c> directory rather than the server directory above it.</remarks>
    public const string NoRegions = "IM5";

    /// <summary>The folder is a map already: it carries a <c>map.xml</c>. Importing originates a <b>new</b>
    /// map from a world, and one that already has a map document is a map to open rather than a world to
    /// originate from. 422.</summary>
    /// <remarks>A folder with a <c>map.xml</c> is a finished map; nothing here reads it. Point the import at a world folder that has only its terrain.</remarks>
    public const string AlreadyAMap = "IM6";
}

/// <summary>
/// POST /api/map/import-url — B8 import-from-url (docs/tools/configure.md, the Import phase). Server-side: fetch a zipped
/// Minecraft world from an <b>allowlisted</b> host, safely extract only <c>region/*.mca</c>, create the map
/// row, and scan it into MariaDB (reusing <see cref="WorldFeatureWriter"/>). The browser never sees the zip.
/// <para><b>Safeguards:</b> https-only + host allowlist (SSRF) · no redirects · download size cap · zip
/// magic-byte check · zip-slip-safe (basename-only dest paths) + zip-bomb-safe (per-entry/total/count caps)
/// extraction · requires <c>region/*.mca</c> · sanitised + unique slug · rolls back row + files on any failure.</para>
/// </summary>
public sealed class ImportUrlEndpoint(MapRepository repo, WorldFeatureWriter writer, ImportPolicy policy, IHttpClientFactory httpFactory)
    : EndpointWithoutRequest
{
    private static readonly Regex RegionMca = new(@"(^|/)region/[^/\\]+\.mca$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex McaName   = new(@"^r\.-?\d+\.-?\d+\.mca$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SlugStrip = new("[^a-z0-9_-]", RegexOptions.Compiled);

    public override void Configure() { Post("/map/import-url"); AllowAnonymous(); }

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
                "the archive to import is stated as url=<https url>", ct, field: "url");
            return;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await Refusals.UnreadableAsync(HttpContext, "invalid url",
                $"'{url}' is not an absolute url", ct, field: "url");
            return;
        }
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            await Refusals.UnreadableAsync(HttpContext, "https url required",
                $"the url states scheme '{uri.Scheme}'; the import fetches over https alone", ct, field: "url");
            return;
        }
        if (!policy.HostAllowed(uri.Host))
        {
            await Refusals.WriteAsync(HttpContext, 403, "host not allowed",
                [new Finding(ImportRules.HostNotAllowed,
                    $"'{uri.Host}' is not one of the hosts the import fetches from", Field: "url")], ct);
            return;
        }

        // ── 2. slug (sanitised, auto-uniquified) ──
        // The URL's last segment is the world's own name, so independent imports of the same map collide;
        // suffix to the next free slug (rockymine → rockymine-2) rather than rejecting the import.
        var baseSlug = Sanitize(body["slug"]?.GetValue<string>() ?? LastSegment(uri));
        if (baseSlug.Length == 0)
            {
                await Refusals.UnreadableAsync(HttpContext, "no slug in the url",
                    "the url's last segment names the world, and this one leaves nothing a slug can be made "
                    + "of — state one as slug=", ct, field: "url");
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
                        $"the host answered {(int)resp.StatusCode} for that url", Field: "url")], ct);
                return;
            }
            if (resp.Content.Headers.ContentLength is { } len && len > policy.MaxDownloadBytes)
            {
                await Refusals.WriteAsync(HttpContext, 413, "download too large",
                    [new Finding(ImportRules.DownloadTooLarge,
                        $"the archive states {len} bytes, past the {policy.MaxDownloadBytes} the import will "
                        + "fetch", Field: "url")], ct);
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
                        "what the url served does not begin with a zip header", Field: "url")], ct);
                return;
            }

            // ── 5. safe extract: ONLY region/*.mca, basename-only dest (zip-slip), bounded (zip-bomb) ──
            var mca = SafeExtractRegionMca(tmpZip, regionDir, policy);
            if (mca == 0)
            {
                TryDeleteDir(slugDir);
                await Refusals.WriteAsync(HttpContext, 422, "nothing to import",
                    [new Finding(ImportRules.NoRegions,
                        "the archive carries no region/*.mca, so there is no world in it to read")], ct);
                return;
            }

            // ── 6. create record + scan into MariaDB ──
            mapId = await repo.InsertAsync(new MapRow { Slug = slug, Name = slug, Gamemode = "ctw", Stage = MapStage.Configure });
            var c = await writer.WriteAsync(mapId.Value, regionDir, ct);

            await Send.OkAsync(new Dict
            {
                ["ok"] = true, ["slug"] = slug, ["mca_files"] = mca,
                ["wool_blocks"] = c.WoolBlocks, ["resource_blocks"] = c.ResourceBlocks, ["chest_items"] = c.ChestItems,
                ["spawner_blocks"] = c.SpawnerBlocks, ["islands"] = c.Islands, ["monument_candidates"] = c.MonumentCandidates,
                ["core_candidates"] = c.CoreCandidates,
            }, ct);
        }
        catch (Exception ex)
        {
            // Roll back so a failed import leaves nothing behind.
            if (mapId is { } id) { try { await repo.DeleteMapAsync(id, ct); } catch { /* best effort */ } }
            TryDeleteDir(slugDir);
            Logger.LogError(ex, "import-url failed for slug {Slug}", slug);
            await Refusals.WriteAsync(HttpContext, 500, "import failed",
                [new Finding(RequestRules.Unhandled,
                    "the import did not finish and what it had written has been rolled back — the fault is the "
                    + "studio's own and the detail is in the server log")], ct);
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
/// <c>map.xml</c> and not already a map: the new-map import candidates (B8 "open a local folder" source).
/// </summary>
public sealed class ImportCandidatesEndpoint(MapRepository repo, ImportPolicy policy) : EndpointWithoutRequest
{
    public override void Configure() { Get("/maps/import-candidates"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var existing = (await repo.ListAsync(ct)).Select(m => m.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<Dict>();
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
                var slug = ImportSlug.Of(folder);
                if (slug.Length == 0 || existing.Contains(slug)) continue;          // skip unsluggable / already-imported
                candidates.Add(new Dict { ["folder"] = folder, ["slug"] = slug, ["region_files"] = mca });
            }
        candidates.Sort((a, b) => string.Compare((string)a["folder"]!, (string)b["folder"]!, StringComparison.Ordinal));
        await Send.OkAsync(candidates, ct);
    }
}

/// <summary>
/// POST /api/map/import-folder { slug } — import a local xml-less world (B8 "open a folder"): resolve
/// <c>&lt;root&gt;/&lt;slug&gt;/region</c> via <see cref="MapsRoots"/> (only configured roots — no client path),
/// create the map row, and scan into MariaDB. The slug must be a real candidate (region/*.mca, no map.xml,
/// not already a map). Rolls back the row on failure.
/// </summary>
public sealed class ImportFolderEndpoint(MapRepository repo, WorldFeatureWriter writer, ImportPolicy policy) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/import-folder"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var raw = await RawBody.ReadAsync(HttpContext, ct);
        JsonObject body;
        try { body = (JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw) as JsonObject) ?? new(); }
        catch (JsonException fault) { await Refusals.UnreadableAsync(HttpContext, "invalid json body", fault, ct); return; }

        var folder = (body["folder"]?.GetValue<string>() ?? "").Trim();
        // candidate folders are single path segments under the imports root — reject anything that could escape it.
        if (folder.Length == 0 || folder.Contains('/') || folder.Contains('\\') || folder.Contains(".."))
        {
            await Refusals.UnreadableAsync(HttpContext, "invalid folder",
                "the world to import is named by one path segment under the imports root, with no separators "
                + "and no '..'", ct, field: "folder");
            return;
        }
        var worldDir = Path.Combine(policy.Root, folder);
        var regionDir = Path.Combine(worldDir, "region");
        if (!Directory.Exists(regionDir))
        {
            await Refusals.NotFoundAsync(HttpContext, "world folder", ct, named: folder);
            return;
        }
        if (File.Exists(Path.Combine(worldDir, "map.xml")))
        {
            await Refusals.WriteAsync(HttpContext, 422, "not a new-map candidate",
                [new Finding(ImportRules.AlreadyAMap,
                    $"'{folder}' carries a map.xml, so it is a map already rather than a world to originate "
                    + "one from", Field: "folder")], ct);
            return;
        }
        if (!Directory.EnumerateFiles(regionDir, "*.mca").Any())
        {
            await Refusals.WriteAsync(HttpContext, 422, "nothing to import",
                [new Finding(ImportRules.NoRegions,
                    $"'{folder}/region' carries no *.mca, so there is no world in it to read", Field: "folder")], ct);
            return;
        }

        var slug = ImportSlug.Of(body["slug"]?.GetValue<string>() ?? folder);
        if (slug.Length == 0)
        {
            await Refusals.UnreadableAsync(HttpContext, "no slug given",
                "neither the stated slug nor the folder name leaves anything a slug can be made of", ct,
                field: "slug");
            return;
        }
        if (await repo.GetBySlugAsync(slug, ct) is not null)
        {
            await Refusals.ConflictAsync(HttpContext, "slug already taken",
                $"a map is already stored under '{slug}' — state another with slug=", ct, holding: [slug]);
            return;
        }

        long? mapId = null;
        try
        {
            mapId = await repo.InsertAsync(new MapRow { Slug = slug, Name = slug, Gamemode = "ctw", Stage = MapStage.Configure });
            var c = await writer.WriteAsync(mapId.Value, regionDir, ct);
            await Send.OkAsync(new Dict
            {
                ["ok"] = true, ["slug"] = slug, ["wool_blocks"] = c.WoolBlocks, ["resource_blocks"] = c.ResourceBlocks,
                ["chest_items"] = c.ChestItems, ["spawner_blocks"] = c.SpawnerBlocks, ["islands"] = c.Islands, ["monument_candidates"] = c.MonumentCandidates,
                ["core_candidates"] = c.CoreCandidates,
            }, ct);
        }
        catch (Exception ex)
        {
            if (mapId is { } id) { try { await repo.DeleteMapAsync(id, ct); } catch { /* best effort */ } }
            Logger.LogError(ex, "import-folder failed for {Slug}", slug);
            await Refusals.WriteAsync(HttpContext, 500, "import failed",
                [new Finding(RequestRules.Unhandled,
                    "the import did not finish and what it had written has been rolled back — the fault is the "
                    + "studio's own and the detail is in the server log")], ct);
        }
    }

}

/// <summary>Folder name → a valid map slug (lowercase <c>[a-z0-9_]</c>; spaces/punctuation collapse to '-').</summary>
internal static class ImportSlug
{
    private static readonly Regex NonSlug = new("[^a-z0-9_]+", RegexOptions.Compiled);
    public static string Of(string s)
    {
        var slug = NonSlug.Replace(s.Trim().ToLowerInvariant(), "-").Trim('-', '_');
        return slug.Length > 64 ? slug[..64] : slug;
    }
}
