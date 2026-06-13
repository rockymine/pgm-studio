using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Data.Repositories;

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
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var regionDir = roots.RegionDir(slug);
        if (regionDir is null)
        {
            await Send.ResponseAsync(new Dict { ["error"] = $"no world found for '{slug}' under configured maps roots" }, 404, ct);
            return;
        }

        var c = await writer.WriteAsync(map.Id, regionDir, ct);
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
        }, ct);
    }
}
