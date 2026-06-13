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
