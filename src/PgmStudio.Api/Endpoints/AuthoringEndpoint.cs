using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Region;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;
using PgmStudio.Minecraft.Palette;

/// <summary>GET /api/map/{slug}/regions/tree — category-grouped nested region tree (canvas render input).</summary>
public sealed class RegionsTreeEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/regions/tree");
        AllowAnonymous();
        // Declared rather than sent as the record: the tree is built in Analysis, which cannot see Contracts,
        // so mapping it here would be a second walk of the same recursion free to disagree with the first.
        // RegionTreeShapeTests holds the record to what the encoder actually writes instead.
        Description(b => b.Produces<RegionTreeDto>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }

        var doc = await reader.ReadDocAsync(map, ct);
        var regions = doc.GetValueOrDefault("regions") as Dict ?? new();
        var cats = RegionCategorizer.Categorize(doc);
        var facets = RegionCategorizer.DeriveFacets(doc);
        var bbox = await MapBounds.ResolveAsync(artifacts, map.Id, ct);

        // editor drafts (E10), pruned to regions that still exist (entity-replace keeps keys stable).
        var allDrafts = await RegionDrafts.LoadAsync(artifacts, map.Id, ct);
        var drafts = allDrafts.Where(kv => regions.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);

        await Send.OkAsync(new Dict
        {
            ["groups"] = RegionAuthoringEncoder.EncodeTree(regions, cats, bbox?.bounds, facets, drafts),
            ["bounding_box"] = bbox?.dict,
        }, ct);
    }
}

/// <summary>GET /api/map/{slug}/islands — the detected island polygons (from the islands_json artifact).</summary>
public sealed class IslandsEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/islands");
        AllowAnonymous();
        // Declared rather than sent as the record: the blob is answered exactly as the scan wrote it, and
        // re-serialising it through IslandDto would drop whatever a newer detection put there.
        Description(b => b.Produces<List<IslandDto>>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var data = await artifacts.LoadAsync(map.Id, ArtifactKind.IslandsJson, ct);
        if (data is null) { await Refusals.NotFoundAsync(HttpContext, "island decomposition", ct); return; }

        using var jd = JsonDocument.Parse(data);
        await Send.OkAsync(jd.RootElement.Clone(), ct);
    }
}

/// <summary>GET /api/map/{slug}/scan-summary — per-feature breakdowns for the import brief: wool blocks
/// grouped by colour (with a swatch hex) and resource blocks grouped by type, each ordered by count.</summary>
public sealed class ScanSummaryEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest<ScanSummaryDto>
{
    private static readonly Dictionary<string, int> WoolDamage =
        BlockColors.BlockDamageToColor.ToDictionary(kv => kv.Value, kv => kv.Key);

    public override void Configure() { Get("/map/{slug}/scan-summary"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }

        var wool = (await db.WoolBlocks.Where(w => w.MapId == map.Id)
                .GroupBy(w => w.Color).Select(g => new { Color = g.Key, Count = g.Count() }).ToListAsync(ct))
            .OrderByDescending(g => g.Count)
            .Select(g =>
            {
                var slug = BlockColors.Normalize(g.Color);
                var hex = WoolDamage.TryGetValue(slug, out var dmg) ? BlockPalette.Hex(35, dmg) : "#888888";
                return new WoolColorCountDto(slug, TitleCase(slug), hex, g.Count);
            }).ToList();

        var resources = (await db.ResourceBlocks.Where(r => r.MapId == map.Id)
                .GroupBy(r => r.ResourceType).Select(g => new { Type = g.Key, Count = g.Count() }).ToListAsync(ct))
            .OrderByDescending(g => g.Count)
            .Select(g => new ResourceTypeCountDto(g.Type, TitleCase(g.Type), g.Count)).ToList();

        // chest_item rows are per-slot; the chest count is the distinct chest positions holding them.
        var chestCount = await db.ChestItems.Where(c => c.MapId == map.Id)
            .Select(c => new { c.WorldX, c.WorldZ, c.WorldY }).Distinct().CountAsync(ct);
        var chestItemCount = await db.ChestItems.CountAsync(c => c.MapId == map.Id, ct);

        await Send.OkAsync(new ScanSummaryDto(wool, resources, chestCount, chestItemCount), ct);
    }

    private static string TitleCase(string slug) => string.Join(' ',
        slug.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
