using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Repositories;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/maps — the dashboard map list.</summary>
public sealed class MapsListEndpoint(MapRepository repo) : EndpointWithoutRequest<List<MapSummary>>
{
    public override void Configure()
    {
        Get("/maps");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var maps = await repo.ListAsync(ct);
        await Send.OkAsync(
            maps.Select(m => new MapSummary(m.Slug, m.Name, m.Gamemode, m.Version, m.Objective)).ToList(), ct);
    }
}
