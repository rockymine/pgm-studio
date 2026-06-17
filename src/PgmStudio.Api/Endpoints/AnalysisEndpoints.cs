using System.Text.Json.Nodes;
using FastEndpoints;
using PgmStudio.Analysis;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;
using PgmStudio.Pgm;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>Shared loader: map row + the reconstructed document, or null (→ 404) when absent.</summary>
internal static class AnalysisLoad
{
    public static async Task<(MapRow map, Dict doc)?> LoadAsync(MapRepository repo, MapReader reader, string slug, CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) return null;
        return (map, await reader.ReadDocAsync(map, ct));
    }
}

/// <summary>GET /api/map/{slug}/regions — derived region facets + category counts.</summary>
public sealed class RegionsEndpoint(MapRepository repo, MapReader reader) : EndpointWithoutRequest<RegionsDto>
{
    public override void Configure() { Get("/map/{slug}/regions"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Send.NotFoundAsync(ct); return; }
        var (_, doc) = loaded.Value;

        var facets = RegionCategorizer.DeriveFacets(doc);
        var dto = new RegionsDto(
            facets.ToDictionary(kv => kv.Key, kv => new RegionFacetDto(kv.Value.Category, kv.Value.Roles, kv.Value.Subtype)),
            facets.Values.GroupBy(f => f.Category).ToDictionary(g => g.Key, g => g.Count()));
        await Send.OkAsync(dto, ct);
    }
}

/// <summary>GET /api/map/{slug}/buildability — per-column verdict grid.</summary>
public sealed class BuildabilityEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest<BuildabilityDto>
{
    public override void Configure() { Get("/map/{slug}/buildability"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Send.NotFoundAsync(ct); return; }
        var (map, doc) = loaded.Value;

        var y0 = (await feature.SegmentsAsync(map.Id, ct))?.Y0Columns();
        var res = Buildability.Compute(doc, y0);
        var rows = Enumerable.Range(0, res.Height)
            .Select(iz => string.Concat(Enumerable.Range(0, res.Width).Select(ix => (char)('0' + res.Verdict[iz * res.Width + ix])))).ToList();
        await Send.OkAsync(new BuildabilityDto(
            new BoundsDto(res.MinX, res.MinZ, res.MaxX, res.MaxZ), res.Width, res.Height,
            Buildability.Classes, Buildability.ClassColors, res.Counts, rows, res.HasY0), ct);
    }
}

/// <summary>GET /api/map/{slug}/traversability — spawn↔wool connectivity.</summary>
public sealed class TraversabilityEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest<TraversabilityDto>
{
    public override void Configure() { Get("/map/{slug}/traversability"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Send.NotFoundAsync(ct); return; }
        var (map, doc) = loaded.Value;

        var segs = await feature.SegmentsAsync(map.Id, ct);
        var res = Traversability.Check(doc, segs?.SurfaceColumns(), segs?.Y0Columns());
        await Send.OkAsync(new TraversabilityDto(
            res.Connected, res.ComponentCount, res.Severity, res.Message, res.HaveLayers,
            res.Points.Select(p => new NavPointDto(p.Kind, p.Name, p.X, p.Z, p.Component)).ToList(),
            res.Isolated.Select(i => new IsolatedPointDto(i.Kind, i.Name)).ToList()), ct);
    }
}

/// <summary>GET /api/map/{slug}/wool-availability — per declared wool, is it obtainable?</summary>
public sealed class WoolAvailabilityEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest<WoolAvailabilityResponseDto>
{
    public override void Configure() { Get("/map/{slug}/wool-availability"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Send.NotFoundAsync(ct); return; }
        var (map, doc) = loaded.Value;

        var have = await feature.HasScanAsync(map.Id, ct);
        var sources = await feature.WoolSourcesAsync(map.Id, doc, ct);
        var wools = WoolSources.CheckAvailability(doc, sources)
            .Select(e => new WoolAvailabilityDto(e.WoolId, e.Color, e.Obtainable, e.Repeatable, e.OneTime, e.Severity, e.SourceTypes, e.Message)).ToList();
        await Send.OkAsync(new WoolAvailabilityResponseDto(wools, have), ct);
    }
}

/// <summary>GET /api/map/{slug}/monument-obstruction — each wool monument's block must be air; a
/// pre-existing block there blocks wool placement (PGM warns on load).</summary>
public sealed class MonumentObstructionEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest<MonumentObstructionResponseDto>
{
    public override void Configure() { Get("/map/{slug}/monument-obstruction"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Send.NotFoundAsync(ct); return; }
        var (map, doc) = loaded.Value;

        var segs = await feature.SegmentsAsync(map.Id, ct);
        var monuments = WoolSources.CheckMonumentObstruction(doc, segs)
            .Select(c => new MonumentObstructionDto(c.WoolColor, c.Team, c.MonumentId, c.X, c.Y, c.Z, c.Obstructed, c.Severity, c.Message)).ToList();
        await Send.OkAsync(new MonumentObstructionResponseDto(monuments, segs is not null), ct);
    }
}

/// <summary>POST /api/map/{slug}/wool-sources — wool colours found inside a drawn rectangle
/// (body: <c>{ bounds: { minX, minZ, maxX, maxZ } }</c>).</summary>
public sealed class WoolSourcesInRegionEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/wool-sources"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Send.NotFoundAsync(ct); return; }
        var (map, doc) = loaded.Value;

        using var sr = new StreamReader(HttpContext.Request.Body);
        var b = (JsonNode.Parse(await sr.ReadToEndAsync(ct)) as JsonObject)?["bounds"] as JsonObject;
        if (b?["minX"] is null || b["minZ"] is null || b["maxX"] is null || b["maxZ"] is null)
        {
            await Send.ResponseAsync(new Dict { ["error"] = "bounds {minX,minZ,maxX,maxZ} required" }, 400, ct);
            return;
        }

        var have = await feature.HasScanAsync(map.Id, ct);
        var sources = await feature.WoolSourcesAsync(map.Id, doc, ct);
        var colors = WoolSources.SourcesInRegion(doc, sources,
                b["minX"]!.GetValue<double>(), b["minZ"]!.GetValue<double>(), b["maxX"]!.GetValue<double>(), b["maxZ"]!.GetValue<double>())
            .Select(c => new WoolColorSummaryDto(c.Color, c.Total, c.SourceTypes, c.Repeatable, c.OneTime,
                c.Sources.Select(s => new WoolSourceDto(s.Type, s.Color, s.X, s.Y, s.Z, s.Count)).ToList())).ToList();
        await Send.OkAsync(new WoolSourcesResponseDto(colors, have), ct);
    }
}

/// <summary>GET /api/map/{slug}/wool-suggestions — wool colours in the world not yet declared as objectives.</summary>
public sealed class WoolSuggestionsEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest<WoolSuggestionsResponseDto>
{
    public override void Configure() { Get("/map/{slug}/wool-suggestions"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Send.NotFoundAsync(ct); return; }
        var (map, doc) = loaded.Value;

        var have = await feature.HasScanAsync(map.Id, ct);
        var sources = await feature.WoolSourcesAsync(map.Id, doc, ct);
        var suggestions = WoolSources.SuggestWools(doc, sources)
            .Select(s => new WoolSuggestionDto(s.Color, s.Total, s.SourceTypes)).ToList();
        await Send.OkAsync(new WoolSuggestionsResponseDto(suggestions, have), ct);
    }
}

/// <summary>POST /api/map/{slug}/resources — iron/gold/diamond blocks (optionally in a drawn rect,
/// body <c>{ bounds?: { minX, minZ, maxX, maxZ } }</c>) + how many a &lt;renewable&gt; already covers.</summary>
public sealed class ResourcesInRegionEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/resources"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Send.NotFoundAsync(ct); return; }
        var (map, doc) = loaded.Value;

        using var sr = new StreamReader(HttpContext.Request.Body);
        (double, double, double, double)? bounds = null;
        if ((JsonNode.Parse(await sr.ReadToEndAsync(ct)) as JsonObject)?["bounds"] is JsonObject b)   // bounds is optional
        {
            if (b["minX"] is null || b["minZ"] is null || b["maxX"] is null || b["maxZ"] is null)
            {
                await Send.ResponseAsync(new Dict { ["error"] = "bounds {minX,minZ,maxX,maxZ} required" }, 400, ct);
                return;
            }
            bounds = (b["minX"]!.GetValue<double>(), b["minZ"]!.GetValue<double>(), b["maxX"]!.GetValue<double>(), b["maxZ"]!.GetValue<double>());
        }

        var have = await feature.HasScanAsync(map.Id, ct);
        var blocks = await feature.ResourceBlocksAsync(map.Id, ct);
        var resources = ResourceSources.ResourcesInRegion(doc, blocks, bounds)
            .Select(r => new ResourceTypeSummaryDto(r.Type, r.Total, r.Renewable, r.AllRenewable,
                r.Sources.Select(s => new ResourceBlockDto(s.Type, s.X, s.Y, s.Z)).ToList())).ToList();
        await Send.OkAsync(new ResourceSourcesResponseDto(resources, have), ct);
    }
}
