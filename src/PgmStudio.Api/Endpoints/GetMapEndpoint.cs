using FastEndpoints;
using PgmStudio.Data.Repositories;
using PgmStudio.Pgm;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/map/{slug} — the full map document (the xml_data.json shape), reconstructed from the
/// relational rows. Mirrors the Python app's get_map.
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
        if (doc is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(doc, ct);
    }
}
