using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;

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
        var bbox = await IslandsBboxAsync(map.Id, ct);

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

    private async Task<((double, double, double, double) bounds, Dict dict)?> IslandsBboxAsync(long mapId, CancellationToken ct)
        => await IslandsBboxAsync(db, mapId, ct);
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
        var bbox = await RegionsAuthoringEndpoint.IslandsBboxAsync(db, map.Id, ct);

        await Send.OkAsync(new Dict
        {
            ["groups"] = RegionAuthoringEncoder.EncodeTree(regions, cats, bbox?.bounds),
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
