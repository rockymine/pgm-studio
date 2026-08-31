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
/// stored sketch layout) it returns a ZIP holding <c>map.xml</c>, <c>level.dat</c> and <c>region/*.mca</c> at
/// its top — a world directory's own contents, which is what a server is handed — a real, playable world
/// synthesised from the sketch columns +
/// intent (docs/world-export/sketch-world-export.md). For any other map it returns the plain <c>map.xml</c>
/// (those already ship a world). Shares the gate + compose pipeline with <see cref="MapXmlEndpoint"/> via
/// <see cref="MapExportLoader"/>, diverging only to bundle the region files for a sketch map.
/// </summary>
public sealed class MapExportEndpoint(MapRepository repo, MapReader reader, FeatureData feature, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/export");
        AllowAnonymous();
        Description(b => b.WorldZipOrMapXml().Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var doc = await reader.ReadDocAsync(map, ct);
        var layoutBytes = await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct);
        var result = await MapExportLoader.ComposeAsync(map.Id, doc, layoutBytes, feature, artifacts, ct);
        if (result.Refusal is { } refusal)
        {
            await Refusals.WriteAsync(HttpContext, refusal, ct);
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

        // What the build did not place, before a byte of the zip is written: a header cannot be set after the
        // response has started, and this is the route where props actually drop.
        Complaints.Add(HttpContext, result.World.Declines);

        // Sketch-originated: bundle the synthesised world with the XML. An IO or region-encoding failure
        // here escapes to Program.cs's unhandled-fault middleware, which answers the same envelope every
        // other refusal does (RQ2) rather than a raw exception.
        var zip = BuildWorldZip(slug, result.Xml!, result.World);

        HttpContext.Response.ContentType = "application/zip";
        HttpContext.Response.Headers.ContentDisposition = ContentDispositionHeader.Attachment($"{slug}.zip");
        await HttpContext.Response.Body.WriteAsync(zip, ct);
    }

    /// <summary>Write the world to a temp folder, then zip <c>map.xml</c> + <c>level.dat</c> +
    /// <c>region/*.mca</c> in memory. The archive is flat: what a server is handed is a world directory, so
    /// its contents sit at the archive's top and the download is unpacked into a folder of the caller's
    /// choosing rather than into one named for the slug.</summary>
    private static byte[] BuildWorldZip(string slug, string xml, BuiltWorld built)
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
                var xmlEntry = archive.CreateEntry("map.xml");
                using (var s = xmlEntry.Open()) s.Write(Encoding.UTF8.GetBytes(xml));

                AddFile(archive, Path.Combine(tmp, "level.dat"), "level.dat");
                foreach (var mca in Directory.GetFiles(regionDir, "*.mca"))
                    AddFile(archive, mca, $"region/{Path.GetFileName(mca)}");
                // Both sidecars travel: provenance says what landed, the decline report says what did not,
                // in full — the rule, the cell and the prop for each. The response header answers the same
                // question in one line for a caller that never unzips. The report is written only when
                // something dropped, so its absence is the answer "everything stood".
                foreach (var sidecar in new[] { "provenance.json", "dressing-report.json" })
                {
                    var path = Path.Combine(regionDir, sidecar);
                    if (File.Exists(path)) AddFile(archive, path, $"region/{sidecar}");
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
