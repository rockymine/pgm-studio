using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/map/{slug} — the full map document (the xml_data.json shape), reconstructed from the
/// relational rows.
/// </summary>
public sealed class GetMapEndpoint(MapReader reader) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var doc = await reader.ReadDocAsync(Route<string>("slug")!, ct);
        if (doc is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        await Send.OkAsync(doc, ct);
    }
}

/// <summary>GET /api/map/{slug}/layers — which authoring layers this one map holds. The list carries the
/// same four facts per row; a tool asks here about the map it has open, so it can tell an origination from
/// a rebuild before offering the action rather than after performing it.</summary>
public sealed class MapLayersEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<MapLayers>
{
    public override void Configure() { Get("/map/{slug}/layers"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }

        var kinds = await artifacts.KindsAsync(map.Id, ct);
        await Send.OkAsync(new MapLayers(
            kinds.Contains(ArtifactKind.PlanJson),
            kinds.Contains(ArtifactKind.SketchLayoutJson),
            kinds.Contains(ArtifactKind.LayerParquet),
            kinds.Contains(ArtifactKind.MapIntentJson)), ct);
    }
}
