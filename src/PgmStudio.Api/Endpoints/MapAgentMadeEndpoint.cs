using FastEndpoints;
using PgmStudio.Api.Access;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;

namespace PgmStudio.Api.Endpoints;

/// <summary>PATCH /api/map/{slug}/agent-made — mark a map as made by an agent, which keeps it to the admins and
/// its owner, or take the mark off. Admin only, from a browser, since a token is never an admin
/// (<c>docs/access.md</c>, <i>Agent-made maps</i>).</summary>
public sealed class MapAgentMadeEndpoint(MapRepository repo) : Endpoint<MapAgentMadeRequest, AppliedDto>
{
    public override void Configure()
    {
        Patch("/map/{slug}/agent-made");
        Policies(AccessPolicies.Admin);
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(MapAgentMadeRequest request, CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        await repo.SetAgentMadeAsync(map.Id, request.AgentMade, ct);
        await Send.OkAsync(new AppliedDto(), ct);
    }
}
