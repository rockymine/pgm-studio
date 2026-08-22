using PgmStudio.Contracts;
using FastEndpoints;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/map/{slug}/origin — <c>{ sketch: bool }</c>: whether the map originated in the sketch tool
/// (has a stored sketch layout). The Configure wizard reads this to auto-wire the monument step away for
/// sketch-origin maps (their monuments are derived at export, not authored).
/// </summary>
public sealed class MapOriginEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<MapOriginDto>
{
    public override void Configure() { Get("/map/{slug}/origin"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        await Send.OkAsync(new MapOriginDto(await artifacts.HasAsync(map.Id, ArtifactKind.SketchLayoutJson, ct)), ct);
    }
}
