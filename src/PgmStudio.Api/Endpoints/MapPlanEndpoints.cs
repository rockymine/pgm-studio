using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/map/{slug}/plan — the plan a map was built from, as it was stored, or {} where it was built
/// from none. History: nothing writes it and nothing reads it again.</summary>
public sealed class MapPlanGetEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/plan");
        // The blob as stored; see SketchGetEndpoint for why the shape is declared rather than sent.
        Description(b => b.Produces<PlanModel>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var data = await artifacts.LoadAsync(map.Id, ArtifactKind.PlanJson, ct);
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.PlanJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(JsonSerializer.Deserialize<JsonElement>(data ?? "{}"u8.ToArray()), ct);
    }
}
