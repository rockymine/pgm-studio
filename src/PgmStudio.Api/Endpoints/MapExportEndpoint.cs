using System.IO.Compression;
using System.Text;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Http;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/map/{slug}/export — the Configure export action. For a <b>sketch-originated</b> map (one with a
/// stored sketch layout) it returns a ZIP of a <c>{slug}/</c> folder containing <c>map.xml</c>,
/// <c>level.dat</c>, and <c>region/*.mca</c> — a real, playable world synthesised from the sketch columns +
/// intent (docs/contracts/sketch-world-export.md). For any other map it returns the plain <c>map.xml</c>
/// (those already ship a world). Intent maps must pass the traversability gate first (§9), same as
/// <see cref="MapXmlEndpoint"/>.
/// </summary>
public sealed class MapExportEndpoint(MapRepository repo, MapReader reader, FeatureData feature, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/export"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var doc = await reader.ReadDocAsync(map, ct);
        var isIntent = await IntentStore.HasAsync(db, map.Id, ct);
        var layoutBytes = await SketchStore.LoadAsync(db, map.Id, ct);

        // Playability gate: intent-authored maps must be traversable before export (§9).
        if (isIntent)
        {
            var segs = await feature.SegmentsAsync(map.Id, ct);
            var trav = Traversability.Check(doc, segs?.SurfaceColumns(), segs?.Y0Columns());
            if (!trav.Connected)
            {
                await Send.ResponseAsync(new Dict
                {
                    ["error"] = "not traversable",
                    ["message"] = trav.Message,
                    ["isolated"] = trav.Isolated.Select(i => new Dict { ["kind"] = i.Kind, ["name"] = i.Name }).ToList(),
                }, 409, ct);
                return;
            }
        }

        // Non-sketch maps: XML only (they already ship a real world).
        if (layoutBytes is null)
        {
            IReadOnlySet<int>? surfacePalette = null;
            IReadOnlyList<(string Type, int X, int Y, int Z)> resources = [];
            if (isIntent)
            {
                var surface = await ConfigureLayers.CellsAsync(db, map.Id, "surface", ct);
                surfacePalette = surface?.Select(c => c.BlockId).ToHashSet();
                resources = (await feature.ResourceBlocksAsync(map.Id, ct)).Select(b => (b.Type, b.X, b.Y, b.Z)).ToList();
            }

            string xmlOnly;
            try { xmlOnly = MapXmlComposer.Compose(doc, isIntent, surfacePalette, resources); }
            catch (Exception ex) { await Send.ResponseAsync(new Dict { ["error"] = ex.Message }, 500, ct); return; }

            HttpContext.Response.ContentType = "application/xml; charset=utf-8";
            HttpContext.Response.Headers.ContentDisposition = ContentDispositionHeader.Attachment($"{slug}.xml");
            await HttpContext.Response.WriteAsync(xmlOnly, ct);
            return;
        }

        // Sketch-originated: synthesise the world and bundle it with the XML.
        var intent = await IntentStore.LoadAsync(db, map.Id, ct);
        var built = SketchWorldBuilder.Build(Encoding.UTF8.GetString(layoutBytes), intent);

        // Re-project the resolved intent (snapped spawns + auto-derived monument locations) so the XML
        // agrees with the world, then compose (no scanned surface/resources for a sketch map).
        IntentGenerator.Apply(doc, built.ResolvedIntent);
        string xml;
        try { xml = MapXmlComposer.Compose(doc, isIntent: true, surfaceBlockIds: null, resources: []); }
        catch (Exception ex) { await Send.ResponseAsync(new Dict { ["error"] = ex.Message }, 500, ct); return; }

        var zip = BuildWorldZip(slug, xml, built);
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
            }
            return ms.ToArray();
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
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
