using PgmStudio.Contracts;
using FastEndpoints;
using PgmStudio.Api.Services;

using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/minecraft/player?name=|uuid= — resolve a Minecraft username or UUID to {uuid, name}. The editor
/// uses it to turn a typed username into the canonical uuid a <c>map.xml</c> stores (and to resolve a stored
/// uuid back to a name).
///
/// <para>404 means <b>no account is called that</b>, which is not the same as an error: PGM takes a person as
/// an account or as a pseudonym, so the editor keeps the typed name and stores it as the second kind. A name
/// that is not shaped like an account, and a host that could not be reached, both answer the same way and for
/// the same reason — the credits stand either way.</para>
/// </summary>
public sealed class PlayerLookupEndpoint(PlayerLookup players) : EndpointWithoutRequest<PlayerDto>
{
    public override void Configure() { Get("/minecraft/player"); AllowAnonymous(); Description(b => b.Refuses(404)); }

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
        if (await players.ResolveAsync(query, ct) is { } found)
        {
            await Send.OkAsync(new PlayerDto(found.Uuid, found.Name), ct);
            return;
        }
        await Refusals.WriteAsync(HttpContext, 404, "no player",
            [new Finding(RequestRules.NoSuchSubject,
                $"no Minecraft account is called '{query}' — store it as a pseudonym instead, which PGM reads "
                + "as a whole author")], ct);
    }
}
