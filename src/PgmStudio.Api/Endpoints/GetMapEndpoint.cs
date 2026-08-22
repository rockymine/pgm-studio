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
public sealed class GetMapEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}");
        AllowAnonymous();
        // Declared rather than sent as the record: the document is the codec's encoding of the whole
        // contract, so mapping it here would be a second codec free to disagree with the first.
        // MapDocumentShapeTests holds the record to what the serializer writes instead.
        Description(b => b.Produces<MapDocumentDto>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        // The revision this document is at, so an edit made from it can state what it was made against.
        if (await writer.RevisionAsync(map.Id, ct) is { } revision) Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(await reader.ReadDocAsync(map, ct), ct);
    }
}

/// <summary>GET /api/map/{slug}/layers — which authoring layers this one map holds. The list carries the
/// same four facts per row; a tool asks here about the map it has open, so it can tell an origination from
/// a rebuild before offering the action rather than after performing it.</summary>
public sealed class MapLayersEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<MapLayers>
{
    public override void Configure() { Get("/map/{slug}/layers"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var kinds = await artifacts.KindsAsync(map.Id, ct);
        await Send.OkAsync(new MapLayers(
            kinds.Contains(ArtifactKind.PlanJson),
            kinds.Contains(ArtifactKind.SketchLayoutJson),
            kinds.Contains(ArtifactKind.LayerParquet),
            kinds.Contains(ArtifactKind.MapIntentJson)), ct);
    }
}
