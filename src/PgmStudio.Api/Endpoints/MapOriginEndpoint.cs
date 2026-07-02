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
public sealed class MapOriginEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/origin"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new Dict { ["sketch"] = await SketchStore.HasAsync(db, map.Id, ct) }, ct);
    }
}
