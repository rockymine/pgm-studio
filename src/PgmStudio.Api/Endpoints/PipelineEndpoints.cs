using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Domain;
using PgmStudio.Pgm;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// POST /api/map/{slug}/scan-world — scan the map's Minecraft world (<c>&lt;root&gt;/&lt;slug&gt;/region</c>)
/// and (re)write its relational feature rows (wools/resources/chests/spawners/layer_segments).
/// The world-import half of the pipeline; the xml half is the importer / map editors.
/// </summary>
public sealed class ScanWorldEndpoint(MapRepository repo, WorldFeatureWriter writer, MapsRoots roots)
    : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/scan-world"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }

        var regionDir = roots.RegionDir(slug);
        if (regionDir is null)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no world for this map",
                [new Finding(RequestRules.NoSuchSubject,
                    $"no world folder for '{slug}' under the configured maps roots, so there is nothing on "
                    + "disk to read")], ct);
            return;
        }

        // A corpus map states what it deletes before play; island detection subtracts exactly that instead of
        // guessing at a build floor by material. A map with no XML (every zip/folder import) erases nothing.
        var erased = PhantomErasure.None;
        if (roots.MapXmlPath(slug) is { } mapXml)
        {
            try { erased = PhantomErasure.From(MapParser.Parse(mapXml)); }
            catch (UnsupportedMapException) { /* unreadable XML states nothing; read the world as it sits */ }
        }

        var c = await writer.WriteAsync(map.Id, regionDir, erased, ct);
        await Send.OkAsync(new Dict
        {
            ["ok"] = true,
            ["slug"] = slug,
            ["region_dir"] = regionDir,
            ["wool_blocks"] = c.WoolBlocks,
            ["resource_blocks"] = c.ResourceBlocks,
            ["chest_items"] = c.ChestItems,
            ["spawner_blocks"] = c.SpawnerBlocks,
            ["layer_segments"] = c.LayerSegments,
            ["islands"] = c.Islands,
            ["monument_candidates"] = c.MonumentCandidates,
            ["core_candidates"] = c.CoreCandidates,
        }, ct);
    }
}
