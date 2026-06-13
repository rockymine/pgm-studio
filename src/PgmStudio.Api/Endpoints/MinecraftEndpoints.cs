using FastEndpoints;
using PgmStudio.Api.Services;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/minecraft/player?name=|uuid= — resolve a Minecraft username or UUID to {uuid, name}
/// via Mojang. Mirrors the reference studio's <c>routes/minecraft.py</c>; the editor uses it to
/// turn a typed username into a canonical uuid (and to resolve a stored uuid back to a name).
/// </summary>
public sealed class PlayerLookupEndpoint(MojangClient mojang) : EndpointWithoutRequest
{
    public override void Configure() { Get("/minecraft/player"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var uuid = HttpContext.Request.Query["uuid"].ToString().Trim();
        var name = HttpContext.Request.Query["name"].ToString().Trim();
        var query = !string.IsNullOrEmpty(uuid) ? uuid : name;
        if (string.IsNullOrEmpty(query))
        {
            await Send.ResponseAsync(new Dict { ["error"] = "name or uuid required" }, 400, ct);
            return;
        }
        try
        {
            var (u, n) = await mojang.LookupAsync(query, ct);
            await Send.OkAsync(new Dict { ["uuid"] = u, ["name"] = n }, ct);
        }
        catch (Exception ex)
        {
            await Send.ResponseAsync(new Dict { ["error"] = ex.Message }, 404, ct);
        }
    }
}
