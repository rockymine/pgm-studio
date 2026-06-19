using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis;
using PgmStudio.Api.Services;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/map/{slug}/regions/authoring — the B4a authoring split (primitives + composed) plus the
/// island bounding box, the render input for the editor canvas. Port of the Flask
/// <c>get_regions_authoring</c> route: <see cref="RegionAuthoringEncoder"/> over the reconstructed
/// doc + derived categories, with the bbox taken from the map's <c>islands_json</c> artifact.
/// </summary>
public sealed class RegionsAuthoringEndpoint(MapRepository repo, MapReader reader, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/regions/authoring"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var doc = await reader.ReadDocAsync(map, ct);
        var regions = doc.GetValueOrDefault("regions") as Dict ?? new();
        var applyRules = doc.GetValueOrDefault("apply_rules") as List<object?>;
        var cats = RegionCategorizer.Categorize(doc);
        var bbox = await MapBounds.ResolveAsync(db, map.Id, ct);

        var split = RegionAuthoringEncoder.EncodeAuthoring(regions, cats, applyRules, bbox?.bounds);
        split["bounding_box"] = bbox?.dict;
        await Send.OkAsync(split, ct);
    }

    /// <summary>Bounding box over the map's detected islands (from the islands_json artifact), or null.</summary>
    internal static async Task<((double, double, double, double) bounds, Dict dict)?> IslandsBboxAsync(PgmDb db, long mapId, CancellationToken ct)
    {
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.IslandsJson, ct);
        if (art is null) return null;
        using var jd = JsonDocument.Parse(art.Data);
        var arr = jd.RootElement;
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;

        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var e in arr.EnumerateArray())
        {
            var b = e.GetProperty("bounds");
            minX = Math.Min(minX, b[0].GetDouble()); minZ = Math.Min(minZ, b[1].GetDouble());
            maxX = Math.Max(maxX, b[2].GetDouble()); maxZ = Math.Max(maxZ, b[3].GetDouble());
        }
        var dict = new Dict { ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ };
        return ((minX, minZ, maxX, maxZ), dict);
    }
}

/// <summary>GET /api/map/{slug}/regions/tree — category-grouped nested region tree (canvas render input).</summary>
public sealed class RegionsTreeEndpoint(MapRepository repo, MapReader reader, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/regions/tree"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var doc = await reader.ReadDocAsync(map, ct);
        var regions = doc.GetValueOrDefault("regions") as Dict ?? new();
        var cats = RegionCategorizer.Categorize(doc);
        var facets = RegionCategorizer.DeriveFacets(doc);
        var bbox = await MapBounds.ResolveAsync(db, map.Id, ct);

        // editor drafts (E10), pruned to regions that still exist (entity-replace keeps keys stable).
        var allDrafts = await RegionDraftStore.LoadAsync(db, map.Id, ct);
        var drafts = allDrafts.Where(kv => regions.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);

        await Send.OkAsync(new Dict
        {
            ["groups"] = RegionAuthoringEncoder.EncodeTree(regions, cats, bbox?.bounds, facets, drafts),
            ["bounding_box"] = bbox?.dict,
        }, ct);
    }
}

/// <summary>GET /api/map/{slug}/islands — the detected island polygons (from the islands_json artifact).</summary>
public sealed class IslandsEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/islands"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandsJson, ct);
        if (art is null) { await Send.NotFoundAsync(ct); return; }

        using var jd = JsonDocument.Parse(art.Data);
        await Send.OkAsync(jd.RootElement.Clone(), ct);
    }
}

/// <summary>GET /api/map/{slug}/scan-summary — per-feature breakdowns for the import brief: wool blocks
/// grouped by colour (with a swatch hex) and resource blocks grouped by type, each ordered by count.</summary>
public sealed class ScanSummaryEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    private static readonly Dictionary<string, int> WoolDamage =
        WoolColors.WoolDamageToColor.ToDictionary(kv => kv.Value, kv => kv.Key);

    public override void Configure() { Get("/map/{slug}/scan-summary"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var wool = (await db.WoolBlocks.Where(w => w.MapId == map.Id)
                .GroupBy(w => w.Color).Select(g => new { Color = g.Key, Count = g.Count() }).ToListAsync(ct))
            .OrderByDescending(g => g.Count)
            .Select(g =>
            {
                var slug = WoolColors.Normalize(g.Color);
                return new Dict
                {
                    ["color"] = slug,
                    ["name"] = TitleCase(slug),
                    ["hex"] = WoolDamage.TryGetValue(slug, out var dmg) ? PgmStudio.Minecraft.BlockColors.Hex(35, dmg) : "#888888",
                    ["count"] = g.Count,
                };
            }).ToList();

        var resources = (await db.ResourceBlocks.Where(r => r.MapId == map.Id)
                .GroupBy(r => r.ResourceType).Select(g => new { Type = g.Key, Count = g.Count() }).ToListAsync(ct))
            .OrderByDescending(g => g.Count)
            .Select(g => new Dict { ["type"] = g.Type, ["name"] = TitleCase(g.Type), ["count"] = g.Count }).ToList();

        // chest_item rows are per-slot; the chest count is the distinct chest positions holding them.
        var chestCount = await db.ChestItems.Where(c => c.MapId == map.Id)
            .Select(c => new { c.WorldX, c.WorldZ, c.WorldY }).Distinct().CountAsync(ct);
        var chestItemCount = await db.ChestItems.CountAsync(c => c.MapId == map.Id, ct);

        await Send.OkAsync(new Dict
        {
            ["wool_colors"] = wool, ["resource_types"] = resources,
            ["chest_count"] = chestCount, ["chest_items"] = chestItemCount,
        }, ct);
    }

    private static string TitleCase(string slug) => string.Join(' ',
        slug.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
