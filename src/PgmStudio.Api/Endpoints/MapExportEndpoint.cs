using System.IO.Compression;
using System.Text;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Http;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

using PgmStudio.Minecraft.Anvil;

/// <summary>
/// GET /api/map/{slug}/export — the Configure export action. For a <b>sketch-originated</b> map (one with a
/// stored sketch layout) it returns a ZIP of a <c>{slug}/</c> folder containing <c>map.xml</c>,
/// <c>level.dat</c>, and <c>region/*.mca</c> — a real, playable world synthesised from the sketch columns +
/// intent (docs/world-export/sketch-world-export.md). For any other map it returns the plain <c>map.xml</c>
/// (those already ship a world). Shares the gate + compose pipeline with <see cref="MapXmlEndpoint"/> via
/// <see cref="MapExportLoader"/>, diverging only to bundle the region files for a sketch map.
/// </summary>
public sealed class MapExportEndpoint(MapRepository repo, MapReader reader, FeatureData feature, PgmDb db, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/export");
        AllowAnonymous();
        Description(b => b.WorldZipOrMapXml());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }

        var doc = await reader.ReadDocAsync(map, ct);
        var layoutBytes = await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct);
        var result = await MapExportLoader.ComposeAsync(map.Id, doc, layoutBytes, feature, artifacts, ct);
        if (result.IsError)
        {
            await Refusals.WriteAsync(HttpContext, result.ErrorStatus!.Value, result.Error!, result.Findings!, ct);
            return;
        }

        // Non-sketch maps: XML only (they already ship a real world).
        if (result.World is null)
        {
            HttpContext.Response.ContentType = "application/xml; charset=utf-8";
            HttpContext.Response.Headers.ContentDisposition = ContentDispositionHeader.Attachment($"{slug}.xml");
            await HttpContext.Response.WriteAsync(result.Xml!, ct);
            return;
        }

        // Sketch-originated: bundle the synthesised world with the XML. An IO or region-encoding failure
        // here escapes to Program.cs's unhandled-fault middleware, which answers the same envelope every
        // other refusal does (RQ2) rather than a raw exception.
        var zip = BuildWorldZip(slug, result.Xml!, result.World);

        HttpContext.Response.ContentType = "application/zip";
        HttpContext.Response.Headers.ContentDisposition = ContentDispositionHeader.Attachment($"{slug}.zip");
        await HttpContext.Response.Body.WriteAsync(zip, ct);
    }

    /// <summary>Write the world to a temp folder, then zip <c>{slug}/map.xml</c> + <c>level.dat</c> +
    /// <c>region/*.mca</c> in memory.</summary>
    private static byte[] BuildWorldZip(string slug, string xml, SketchWorld built)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "world_" + Guid.NewGuid().ToString("N"));
        try
        {
            var regionDir = Path.Combine(tmp, "region");
            AnvilRegionWriter.Write(built.World, regionDir);
            // Beside the voxels, not inside them (B133) — the sidecar travels in the same zip a downloaded
            // world does, so a render taken from the download later still gets the recorded reading.
            WorldProvenanceFile.Write(built.Provenance, regionDir);
            DressingReportFile.Write(built.Declines, regionDir);
            LevelDatWriter.Write(tmp, slug, built.SpawnX, built.SpawnY, built.SpawnZ,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var xmlEntry = archive.CreateEntry($"{slug}/map.xml");
                using (var s = xmlEntry.Open()) s.Write(Encoding.UTF8.GetBytes(xml));

                AddFile(archive, Path.Combine(tmp, "level.dat"), $"{slug}/level.dat");
                foreach (var mca in Directory.GetFiles(regionDir, "*.mca"))
                    AddFile(archive, mca, $"{slug}/region/{Path.GetFileName(mca)}");
                // Both sidecars travel: provenance says what landed, the decline report says what did not,
                // and the second is the only record an HTTP caller ever gets of a dropped prop. The report is
                // written only when something dropped, so its absence is the answer "everything stood".
                foreach (var sidecar in new[] { "provenance.json", "dressing-report.json" })
                {
                    var path = Path.Combine(regionDir, sidecar);
                    if (File.Exists(path)) AddFile(archive, path, $"{slug}/region/{sidecar}");
                }
            }
            return ms.ToArray();
        }
        finally
        {
            if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true);
        }
    }

    private static void AddFile(ZipArchive archive, string path, string entryName)
    {
        var entry = archive.CreateEntry(entryName);
        using var s = entry.Open();
        using var f = File.OpenRead(path);
        f.CopyTo(s);
    }
}
