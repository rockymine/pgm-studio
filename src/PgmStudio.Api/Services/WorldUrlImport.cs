using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PgmStudio.Api.Endpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// A world fetched from a download link: into a new map (<see cref="NewAsync"/>), or over the world an
/// imported map already has (<see cref="IntoAsync"/>). Both go through one fetch, so the same link is refused
/// the same way whichever map it is for.
///
/// <para><b>The fetch's safeguards:</b> https only, a host on <see cref="ImportPolicy.AllowedHosts"/>, no
/// redirects, a download size cap, a zip header, and an extraction that takes only <c>region/*.mca</c>, names
/// each file by its basename (zip-slip) and caps every entry, the total and the count (zip-bomb). The browser
/// never sees the archive.</para>
/// </summary>
public static partial class WorldUrlImport
{
    [GeneratedRegex(@"(^|/)region/[^/\\]+\.mca$", RegexOptions.IgnoreCase)] private static partial Regex RegionMca();
    [GeneratedRegex(@"^r\.-?\d+\.-?\d+\.mca$", RegexOptions.IgnoreCase)] private static partial Regex McaName();
    [GeneratedRegex("[^a-z0-9_-]")] private static partial Regex SlugStrip();

    /// <summary>Fetch the world at <paramref name="url"/> and originate a map from it at the Configure stage,
    /// under the slug stated or the link's last segment, suffixed past a slug already taken. A fault leaves no
    /// map and no files behind.</summary>
    public static async Task<WorldImported> NewAsync(
        MapRepository repo, WorldFeatureWriter writer, ImportPolicy policy, IHttpClientFactory http, ILogger logger,
        string? url, string? statedSlug, MapOriginator? originator, CancellationToken ct)
    {
        var (uri, unaddressable) = Address(url, policy);
        if (uri is null) return new(unaddressable);

        // The link's last segment is the world's own name, so independent imports of one world collide; the
        // slug is suffixed past one already taken (rockymine → rockymine-2) rather than refused.
        var baseSlug = Sanitize(statedSlug ?? LastSegment(uri));
        if (baseSlug.Length == 0)
            return Refuse(400, "no slug in the url", RequestRules.Unreadable,
                "the last segment of the request's `url` leaves nothing a slug can be made of");
        var slug = await repo.UniqueSlugAsync(baseSlug, ct);

        var worldDir = Path.Combine(policy.Root, slug);
        var regionDir = Path.Combine(worldDir, "region");
        long? mapId = null;
        try
        {
            var (refused, regionFiles) = await FetchAsync(uri, regionDir, policy, http, ct);
            if (refused is not null) { TryDeleteDir(worldDir); return new(refused); }

            mapId = await MapOrigin.AtAsync(repo, slug, slug, MapStage.Configure, originator);
            var counts = await writer.WriteAsync(mapId.Value, regionDir, ct);
            return new(null, WorldScans.Of(slug, counts) with { McaFiles = regionFiles });
        }
        catch (Exception fault)
        {
            if (mapId is { } id) { try { await repo.DeleteMapAsync(id, ct); } catch { /* best effort */ } }
            TryDeleteDir(worldDir);
            logger.LogError(fault, "import-url failed for {Slug}", slug);
            return Refuse(500, "import failed", RequestRules.Unhandled,
                $"the import of map '{slug}' failed, and what it had written was rolled back", field: null);
        }
    }

    /// <summary>
    /// Fetch the world at <paramref name="url"/> over the world <paramref name="map"/> was imported from, and
    /// read it again. What the map states is left as it is — its intent, its document, its notes and its
    /// changes — and its scan, island outlines and suggestions are replaced by the new world's.
    ///
    /// <para>The new world is fetched beside the old one and scanned before anything is replaced. The scan is
    /// one write, so a fetch or a scan that fails leaves the map with the world and the scan it had. Only then
    /// does the new world take the old one's folder. A symmetry the author has not confirmed was detected off
    /// the old islands, so it is dropped to be detected again; a confirmed one is the author's answer and
    /// stays.</para>
    /// </summary>
    public static async Task<WorldImported> IntoAsync(
        MapRow map, WorldFeatureWriter writer, MapArtifactStore artifacts, PgmDb db, ImportPolicy policy,
        MapsRoots roots, IHttpClientFactory http, ILogger logger, string? url, CancellationToken ct)
    {
        if (await artifacts.HasAsync(map.Id, ArtifactKind.SketchLayoutJson, ct))
            return Refuse(409, "the map is drawn", ImportRules.NotImported,
                $"map '{map.Slug}' is a sketch, and its world is built from its layout", field: null);

        var worldDir = Path.Combine(policy.Root, map.Slug);
        var regionDir = Path.Combine(worldDir, "region");
        if (roots.RegionDir(map.Slug) is { } heldAt && Path.GetFullPath(heldAt) != Path.GetFullPath(regionDir))
            return Refuse(409, "the world is not an import", ImportRules.NotImported,
                $"the world of map '{map.Slug}' sits in a corpus folder, not under the imports root", field: null);

        var (uri, unaddressable) = Address(url, policy);
        if (uri is null) return new(unaddressable);

        var staging = Path.Combine(policy.Root, $".reimport-{map.Slug}-{Guid.NewGuid():N}");
        var stagedRegion = Path.Combine(staging, "region");
        try
        {
            var (refused, regionFiles) = await FetchAsync(uri, stagedRegion, policy, http, ct);
            if (refused is not null) return new(refused);

            var counts = await writer.WriteAsync(map.Id, stagedRegion, ct);
            if (await SymmetryStore.LoadAsync(db, map.Id, ct) is { Status: "unconfirmed" })
                await SymmetryStore.DeleteAsync(db, map.Id, ct);

            Directory.CreateDirectory(worldDir);
            if (Directory.Exists(regionDir)) Directory.Move(regionDir, Path.Combine(staging, "replaced"));
            Directory.Move(stagedRegion, regionDir);
            return new(null, WorldScans.Of(map.Slug, counts) with { McaFiles = regionFiles });
        }
        catch (Exception fault)
        {
            logger.LogError(fault, "re-import failed for {Slug}", map.Slug);
            return Refuse(500, "import failed", RequestRules.Unhandled,
                $"the import into map '{map.Slug}' failed, and the map keeps the world it had", field: null);
        }
        finally { TryDeleteDir(staging); }
    }

    /// <summary>The link a request states, checked before anything is fetched: present, absolute, https, and
    /// on a host the studio fetches from.</summary>
    private static (Uri? Uri, Refusal? Refusal) Address(string? url, ImportPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (null, Refusal.At(400, "no url given",
                new Finding(RequestRules.Unreadable, "the request's `url` is absent", Field: "url")));
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return (null, Refusal.At(400, "invalid url",
                new Finding(RequestRules.Unreadable, $"the request's `url` is '{url}', not an absolute url", Field: "url")));
        if (uri.Scheme != Uri.UriSchemeHttps)
            return (null, Refusal.At(400, "https url required",
                new Finding(RequestRules.Unreadable, $"the request's `url` has the scheme '{uri.Scheme}', not https",
                    Field: "url")));
        if (!policy.HostAllowed(uri.Host))
            return (null, Refusal.At(403, "host not allowed",
                new Finding(ImportRules.HostNotAllowed,
                    $"host '{uri.Host}' of the request's `url` is not one of the hosts the import fetches from",
                    Field: "url")));
        return (uri, null);
    }

    /// <summary>Download the archive at <paramref name="uri"/> and extract its region files into
    /// <paramref name="regionDir"/>: the refusal where the host or the archive is one the studio will not
    /// take, or the number of region files extracted. A cap broken mid-stream throws.</summary>
    private static async Task<(Refusal? Refusal, int RegionFiles)> FetchAsync(
        Uri uri, string regionDir, ImportPolicy policy, IHttpClientFactory http, CancellationToken ct)
    {
        var archive = Path.Combine(Path.GetTempPath(), $"pgm-import-{Guid.NewGuid():N}.zip");
        try
        {
            using var response = await http.CreateClient("import").GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return (Refusal.At(502, "download failed", new Finding(ImportRules.DownloadFailed,
                    $"host '{uri.Host}' answered {(int)response.StatusCode} for the request's `url`, "
                    + "not between 200 and 299", Field: "url")), 0);
            if (response.Content.Headers.ContentLength is { } length && length > policy.MaxDownloadBytes)
                return (Refusal.At(413, "download too large", new Finding(ImportRules.DownloadTooLarge,
                    $"the archive at the request's `url` is {length} bytes, more than {policy.MaxDownloadBytes} bytes",
                    Field: "url")), 0);
            await using (var network = await response.Content.ReadAsStreamAsync(ct))
            await using (var file = File.Create(archive))
                await CopyCappedAsync(network, file, policy.MaxDownloadBytes, ct);

            if (!await IsZipAsync(archive, ct))
                return (Refusal.At(415, "not a zip archive", new Finding(ImportRules.NotAnArchive,
                    "the file at the request's `url` does not begin with a zip header", Field: "url")), 0);

            var regionFiles = ExtractRegionFiles(archive, regionDir, policy);
            if (regionFiles == 0)
                return (Refusal.At(422, "nothing to import", new Finding(ImportRules.NoRegions,
                    "the archive at the request's `url` has no region file", Field: "url")), 0);
            return (null, regionFiles);
        }
        finally { try { File.Delete(archive); } catch { /* ignore */ } }
    }

    private static string Sanitize(string text)
    {
        var slug = SlugStrip().Replace(text.Trim().ToLowerInvariant(), "").Trim('-', '_');
        return slug.Length > 64 ? slug[..64] : slug;
    }

    private static string LastSegment(Uri uri) =>
        Uri.UnescapeDataString(uri.AbsolutePath.TrimEnd('/').Split('/').LastOrDefault() ?? "");

    private static async Task CopyCappedAsync(Stream source, Stream target, long cap, CancellationToken ct)
    {
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > cap) throw new InvalidOperationException("download exceeded size cap");
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    private static async Task<bool> IsZipAsync(string path, CancellationToken ct)
    {
        await using var file = File.OpenRead(path);
        var signature = new byte[4];
        if (await file.ReadAsync(signature.AsMemory(0, 4), ct) < 4) return false;
        // PK\x03\x04 (local file header) or PK\x05\x06 (empty-archive end-of-central-directory)
        return signature[0] == 0x50 && signature[1] == 0x4B
            && ((signature[2] == 0x03 && signature[3] == 0x04) || (signature[2] == 0x05 && signature[3] == 0x06));
    }

    /// <summary>Extract only <c>region/*.mca</c> entries, flattened to <c>&lt;regionDir&gt;/&lt;basename&gt;</c>
    /// (the path is chosen from the basename, so a crafted entry path cannot escape), bounded by per-entry,
    /// total-uncompressed and entry-count caps. Returns the number extracted.</summary>
    private static int ExtractRegionFiles(string archivePath, string regionDir, ImportPolicy policy)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > policy.MaxEntries) throw new InvalidOperationException("too many zip entries");

        Directory.CreateDirectory(regionDir);
        long totalUncompressed = 0; var extracted = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.Length == 0 || entry.FullName.EndsWith('/')) continue;
            if (!RegionMca().IsMatch(entry.FullName)) continue;
            var name = Path.GetFileName(entry.Name);
            if (!McaName().IsMatch(name)) continue;
            if (entry.Length > policy.MaxEntryBytes) throw new InvalidOperationException("zip entry too large");

            using (var source = entry.Open())
            using (var target = File.Create(Path.Combine(regionDir, name)))
                totalUncompressed += CopyCapped(source, target, policy.MaxEntryBytes);
            if (totalUncompressed > policy.MaxUncompressedBytes)
                throw new InvalidOperationException("uncompressed size cap exceeded");
            extracted++;
        }
        return extracted;
    }

    private static long CopyCapped(Stream source, Stream target, long cap)
    {
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > cap) throw new InvalidOperationException("zip entry exceeded size cap");
            target.Write(buffer, 0, read);
        }
        return total;
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
    }

    private static WorldImported Refuse(int status, string error, string rule, string message, string? field = "url") =>
        new(Refusal.At(status, error, new Finding(rule, message, Field: field)));
}
