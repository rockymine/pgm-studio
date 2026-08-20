using PgmStudio.Contracts;
using FastEndpoints;
using PgmStudio.Api.Services;

using PgmStudio.Domain;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/minecraft/player?name=|uuid= — resolve a Minecraft username or UUID to {uuid, name}
/// via Mojang. The editor uses it to
/// turn a typed username into a canonical uuid (and to resolve a stored uuid back to a name).
/// </summary>
public sealed class PlayerLookupEndpoint(MojangClient mojang) : EndpointWithoutRequest<PlayerDto>
{
    public override void Configure() { Get("/minecraft/player"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var uuid = HttpContext.Request.Query["uuid"].ToString().Trim();
        var name = HttpContext.Request.Query["name"].ToString().Trim();
        var query = !string.IsNullOrEmpty(uuid) ? uuid : name;
        if (string.IsNullOrEmpty(query))
        {
            await Refusals.UnreadableAsync(HttpContext, "no player named",
                "a lookup takes either a name or a uuid, and neither was given", ct);
            return;
        }
        try
        {
            var (u, n) = await mojang.LookupAsync(query, ct);
            await Send.OkAsync(new PlayerDto(u, n), ct);
        }
        catch (Exception ex)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no player",
                [new Finding(RequestRules.NoSuchSubject,
                    $"the Mojang lookup for '{query}' did not answer a player: {ex.Message}")], ct);
        }
    }
}
