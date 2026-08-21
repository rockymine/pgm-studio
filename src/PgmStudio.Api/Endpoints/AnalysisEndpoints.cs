using PgmStudio.Export;
using System.Text.Json.Nodes;
using FastEndpoints;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;

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
        if (loaded is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
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
        if (loaded is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var (map, doc) = loaded.Value;

        var y0 = (await feature.SegmentsAsync(map.Id, ct))?.Y0Columns();
        // Clip the void/buildable geometry against the canonical map box (the surface-layer extent saved at
        // scan), not a per-pass region-AABB-plus-margin; falls back to that margin box when there's no scan.
        var bb = (await feature.MapBboxAsync(map.Id, ct))?.bounds;
        var res = Buildability.Compute(doc, y0,
            bb is { } v ? ((int)v.Item1, (int)v.Item2, (int)v.Item3, (int)v.Item4) : null);
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
        if (loaded is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var (map, doc) = loaded.Value;

        var segs = await feature.SegmentsAsync(map.Id, ct);
        var res = Traversability.Check(doc, segs?.SurfaceColumns(), segs?.Y0Columns());
        await Send.OkAsync(new TraversabilityDto(
            res.Connected, res.ComponentCount, res.Severity, res.Message, res.HaveLayers,
            res.Points.Select(p => new NavPointDto(p.Kind, p.Name, p.X, p.Z, p.Component)).ToList(),
            res.Isolated.Select(i => new IsolatedPointDto(i.Kind, i.Name, i.For)).ToList()), ct);
    }
}

/// <summary>GET /api/map/{slug}/coverage — where the ground is lived on and where it is dead: the traffic
/// corridors between every pair of waypoints, each waypoint's own ring, the prop-decorated fringe, and the
/// dead patches named with coordinates. <c>?format=png</c> answers the same grid as the coverage picture.</summary>
public sealed class CoverageEndpoint(MapRepository repo, MapReader reader, FeatureData feature, MapArtifactStore artifacts) : EndpointWithoutRequest<CoverageDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/coverage");
        AllowAnonymous();
        Description(b => b.AlsoPng());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var (map, doc) = loaded.Value;

        var segs = await feature.SegmentsAsync(map.Id, ct);
        var layoutBytes = await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct);
        var decor = layoutBytes is null
            ? []
            : DressingScope.DecorCells(System.Text.Encoding.UTF8.GetString(layoutBytes));
        var res = GroundCoverage.Read(doc, segs?.SurfaceColumns() ?? [], segs?.Y0Columns(), decor);

        // One picture, so the view name has nothing to select and is not read — this is the one PNG route
        // with no view to get wrong.
        if (await PngAnswer.AnsweredAsync(HttpContext, "coverage", "it draws one view",
                _ => CoverageRender.Png(res), ct)) return;

        var rows = Enumerable.Range(0, res.Height)
            .Select(iz => string.Concat(Enumerable.Range(0, res.Width)
                .Select(ix => (char)('0' + res.Cells[iz * res.Width + ix])))).ToList();
        // base-36 a digit at a time, so the traffic grid travels the same shape as the class grid
        static char Digit(int n) => n <= 0 ? '0' : n < 10 ? (char)('0' + n) : n < 36 ? (char)('a' + n - 10) : 'z';
        var traffic = Enumerable.Range(0, res.Height)
            .Select(iz => string.Concat(Enumerable.Range(0, res.Width)
                .Select(ix => Digit(res.Traffic[iz * res.Width + ix])))).ToList();
        await Send.OkAsync(new CoverageDto(
            new BoundsDto(res.MinX, res.MinZ, res.MinX + res.Width, res.MinZ + res.Height),
            res.Width, res.Height, GroundCoverage.Classes, GroundCoverage.ClassColors, rows,
            res.GroundCells, res.ReachedCells, res.DecoratedCells, res.DeadCells, res.DeadShare,
            res.DeadPatches.Select(p => new CoveragePatchDto(p.Area, p.CentroidX, p.CentroidZ, p.NearestReachedBlocks)).ToList(),
            res.UnnamedDeadPatches, res.HaveRoutes,
            traffic, res.Journeys, res.Traffic.Length == 0 ? 0 : res.Traffic.Max()), ct);
    }
}

/// <summary>GET /api/map/{slug}/kit-reach — can a fresh spawn bridge to each wool with only the
/// placeable blocks its spawn kit grants? (budget-aware traversability).</summary>
public sealed class KitReachEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest<KitReach.Result>
{
    public override void Configure() { Get("/map/{slug}/kit-reach"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var (map, doc) = loaded.Value;

        var segs = await feature.SegmentsAsync(map.Id, ct);
        // Walkable ground = the cleaned-base footprint (floating masses pruned), so a build floating over
        // void can't serve as a free shortcut up to the wool-room level in the Y-agnostic 2D grid.
        var walkable = segs is null ? null : PgmStudio.Analysis.Footprint.IslandDetector.CleanedBaseFootprint(segs.BaseColumns());
        var res = KitReach.Check(doc, walkable, segs?.Y0Columns());
        await Send.OkAsync(res, ct);
    }
}

/// <summary>GET /api/map/{slug}/wool-availability — per declared wool, is it obtainable?</summary>
public sealed class WoolAvailabilityEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest<WoolAvailabilityResponseDto>
{
    public override void Configure() { Get("/map/{slug}/wool-availability"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var (map, doc) = loaded.Value;

        var have = await feature.HasScanAsync(map.Id, ct);
        var sources = await feature.WoolSourcesAsync(map.Id, doc, ct);
        var wools = WoolSources.CheckAvailability(doc, sources, (await feature.MapBboxAsync(map.Id, ct))?.bounds)
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
        if (loaded is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var (map, doc) = loaded.Value;

        var segs = await feature.SegmentsAsync(map.Id, ct);
        var monuments = WoolSources.CheckMonumentObstruction(doc, segs)
            .Select(c => new MonumentObstructionDto(c.WoolColor, c.Team, c.MonumentId, c.X, c.Y, c.Z, c.Obstructed, c.Severity, c.Message)).ToList();
        await Send.OkAsync(new MonumentObstructionResponseDto(monuments, segs is not null), ct);
    }
}

/// <summary>POST /api/map/{slug}/wool-sources — wool colours found inside a drawn rectangle
/// (body: <c>{ bounds: { minX, minZ, maxX, maxZ } }</c>).</summary>
public sealed class WoolSourcesInRegionEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest<WoolSourcesResponseDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/wool-sources"); AllowAnonymous();
        Description(b => b.Accepts<WoolSearchRequest>("application/json"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var (map, doc) = loaded.Value;

        var b = (JsonNode.Parse(await RawBody.ReadAsync(HttpContext, ct)) as JsonObject)?["bounds"] as JsonObject;
        if (b?["minX"] is null || b["minZ"] is null || b["maxX"] is null || b["maxZ"] is null)
        {
            // Naming the four fields alone read as though they were the body; they are nested, and a caller
            // posting them flat got this same sentence back and no way to tell the two apart.
            await Refusals.UnreadableAsync(HttpContext, "no rectangle given",
                "the rectangle to search is required, as {\"bounds\": {\"minX\", \"minZ\", \"maxX\", "
                + "\"maxZ\"}} — the four corners nested under 'bounds', not beside it", ct, field: "bounds");
            return;
        }

        var have = await feature.HasScanAsync(map.Id, ct);
        var sources = await feature.WoolSourcesAsync(map.Id, doc, ct);
        var colors = WoolSources.SourcesInRegion(doc, sources,
                b["minX"]!.GetValue<double>(), b["minZ"]!.GetValue<double>(), b["maxX"]!.GetValue<double>(), b["maxZ"]!.GetValue<double>(),
                (await feature.MapBboxAsync(map.Id, ct))?.bounds)
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
        if (loaded is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var (map, doc) = loaded.Value;

        var have = await feature.HasScanAsync(map.Id, ct);
        var sources = await feature.WoolSourcesAsync(map.Id, doc, ct);
        var suggestions = WoolSources.SuggestWools(doc, sources, (await feature.MapBboxAsync(map.Id, ct))?.bounds)
            .Select(s => new WoolSuggestionDto(s.Color, s.Total, s.SourceTypes)).ToList();
        await Send.OkAsync(new WoolSuggestionsResponseDto(suggestions, have), ct);
    }
}

/// <summary>POST /api/map/{slug}/resources — iron/gold/diamond blocks (optionally in a drawn rect,
/// body <c>{ bounds?: { minX, minZ, maxX, maxZ } }</c>) + how many a &lt;renewable&gt; already covers.</summary>
public sealed class ResourcesInRegionEndpoint(MapRepository repo, MapReader reader, FeatureData feature) : EndpointWithoutRequest<ResourceSourcesResponseDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/resources"); AllowAnonymous();
        Description(b => b.Accepts<ResourceSearchRequest>("application/json"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var (map, doc) = loaded.Value;

        (double, double, double, double)? bounds = null;
        if ((JsonNode.Parse(await RawBody.ReadAsync(HttpContext, ct)) as JsonObject)?["bounds"] is JsonObject b)   // bounds is optional
        {
            if (b["minX"] is null || b["minZ"] is null || b["maxX"] is null || b["maxZ"] is null)
            {
                // Here bounds is optional — omitting it reads the whole map — so this fires only for one
                // that is present and short of a corner, which is a different fault from the one above.
                await Refusals.UnreadableAsync(HttpContext, "rectangle missing a corner",
                    "'bounds' was given but is missing a corner: it takes minX, minZ, maxX and maxZ. Omit "
                    + "'bounds' entirely to read the whole map", ct, field: "bounds");
                return;
            }
            bounds = (b["minX"]!.GetValue<double>(), b["minZ"]!.GetValue<double>(), b["maxX"]!.GetValue<double>(), b["maxZ"]!.GetValue<double>());
        }

        var have = await feature.HasScanAsync(map.Id, ct);
        var blocks = await feature.ResourceBlocksAsync(map.Id, ct);
        var resources = ResourceSources.ResourcesInRegion(doc, blocks, bounds, (await feature.MapBboxAsync(map.Id, ct))?.bounds)
            .Select(r => new ResourceTypeSummaryDto(r.Type, r.Total, r.Renewable, r.AllRenewable,
                r.Sources.Select(s => new ResourceBlockDto(s.Type, s.X, s.Y, s.Z)).ToList())).ToList();
        await Send.OkAsync(new ResourceSourcesResponseDto(resources, have), ct);
    }
}
