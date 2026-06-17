using System.IO.Compression;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// POST /api/map/import-url — B8 import-from-url (new-map-authoring.md §12). Server-side: fetch a zipped
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
        using var reader = new StreamReader(HttpContext.Request.Body);
        var raw = await reader.ReadToEndAsync(ct);
        JsonObject body;
        try { body = (JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw) as JsonObject) ?? new JsonObject(); }
        catch { await Fail(400, "invalid json body", ct); return; }
        var url = body["url"]?.GetValue<string>();

        // ── 1. URL safeguards (SSRF) ──
        if (string.IsNullOrWhiteSpace(url)) { await Fail(400, "url is required", ct); return; }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) { await Fail(400, "invalid url", ct); return; }
        if (uri.Scheme != Uri.UriSchemeHttps) { await Fail(400, "https url required", ct); return; }
        if (!policy.HostAllowed(uri.Host)) { await Fail(403, $"host not allowed: {uri.Host}", ct); return; }

        // ── 2. slug (sanitised + unique) ──
        var slug = Sanitize(body["slug"]?.GetValue<string>() ?? LastSegment(uri));
        if (slug.Length == 0) { await Fail(400, "could not derive a valid slug", ct); return; }
        if (await repo.GetBySlugAsync(slug, ct) is not null) { await Fail(409, $"map '{slug}' already exists", ct); return; }

        var slugDir = Path.Combine(policy.Root, slug);
        var regionDir = Path.Combine(slugDir, "region");
        var tmpZip = Path.Combine(Path.GetTempPath(), $"pgm-import-{Guid.NewGuid():N}.zip");
        long? mapId = null;
        try
        {
            // ── 3. download (allowlisted host, no redirects, timeout, size-capped) ──
            var client = httpFactory.CreateClient("import");
            using var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) { await Fail(502, $"download failed ({(int)resp.StatusCode})", ct); return; }
            if (resp.Content.Headers.ContentLength is { } len && len > policy.MaxDownloadBytes) { await Fail(413, "download too large", ct); return; }
            await using (var net = await resp.Content.ReadAsStreamAsync(ct))
            await using (var file = File.Create(tmpZip))
                await CopyCappedAsync(net, file, policy.MaxDownloadBytes, ct);

            // ── 4. zip magic ──
            if (!await IsZipAsync(tmpZip, ct)) { await Fail(415, "not a zip archive", ct); return; }

            // ── 5. safe extract: ONLY region/*.mca, basename-only dest (zip-slip), bounded (zip-bomb) ──
            var mca = SafeExtractRegionMca(tmpZip, regionDir, policy);
            if (mca == 0) { TryDeleteDir(slugDir); await Fail(422, "archive has no region/*.mca", ct); return; }

            // ── 6. create record + scan into MariaDB ──
            mapId = await repo.InsertAsync(new MapRow { Slug = slug, Name = slug, Gamemode = "ctw" });
            var c = await writer.WriteAsync(mapId.Value, regionDir, ct);

            await Send.OkAsync(new Dict
            {
                ["ok"] = true, ["slug"] = slug, ["mca_files"] = mca,
                ["wool_blocks"] = c.WoolBlocks, ["resource_blocks"] = c.ResourceBlocks, ["chest_items"] = c.ChestItems,
                ["spawner_blocks"] = c.SpawnerBlocks, ["islands"] = c.Islands, ["monument_candidates"] = c.MonumentCandidates,
            }, ct);
        }
        catch (Exception ex)
        {
            // Roll back so a failed import leaves nothing behind.
            if (mapId is { } id) { try { await repo.DeleteMapAsync(id, ct); } catch { /* best effort */ } }
            TryDeleteDir(slugDir);
            Logger.LogError(ex, "import-url failed for slug {Slug}", slug);
            await Fail(500, "import failed", ct);
        }
        finally { try { File.Delete(tmpZip); } catch { /* ignore */ } }
    }

    private async Task Fail(int code, string msg, CancellationToken ct) =>
        await Send.ResponseAsync(new Dict { ["error"] = msg }, code, ct);

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
