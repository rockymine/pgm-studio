using FastEndpoints;
using PgmStudio.Api.Access;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Access;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/me — who the request is signed in as, and the role that decides what it may write.</summary>
public sealed class MeEndpoint(Callers callers, AccessOptions access) : EndpointWithoutRequest<CallerDto>
{
    public override void Configure() => Get("/me");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = await callers.OfAsync(HttpContext, ct);
        await Send.OkAsync(new CallerDto(access.Mode, caller.SignedIn, caller.Uuid, caller.Name, caller.Role, caller.MayNote,
                                         caller.IsOwner), ct);
    }
}

/// <summary>GET /api/map/{slug}/access — whether the request may change this map, so a client can open it
/// read-only rather than let an edit be refused.</summary>
public sealed class MapAccessEndpoint(Callers callers, MapRepository maps) : EndpointWithoutRequest<MapAccessDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/access");
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await maps.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var caller = await callers.OfAsync(HttpContext, ct);
        await Send.OkAsync(new MapAccessDto(await callers.MayEditAsync(caller, map, ct)), ct);
    }
}

/// <summary>GET /api/users — the whitelist, by name, each marked where the server names them an owner. Admin
/// only.</summary>
public sealed class UserListEndpoint(StudioUserStore users, AccessOptions access) : EndpointWithoutRequest<List<StudioUserDto>>
{
    public override void Configure()
    {
        Get("/users");
        Policies(AccessPolicies.Admin);
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync([.. (await users.ListAsync(ct)).Select(row => UserPutEndpoint.Dto(row, access))], ct);
}

/// <summary>POST /api/users — put a Minecraft account on the whitelist in a role, or change the role of one
/// already on it. The player is resolved to the account first, so the whitelist holds the uuid an author is
/// credited under. Admin only, and within what <see cref="WhitelistKeeping"/> lets the caller change: only an
/// owner makes someone an admin or changes an admin's role.</summary>
public sealed class UserPutEndpoint(StudioUserStore users, PlayerLookup players, Callers callers, AccessOptions access)
    : Endpoint<StudioUserRequest, StudioUserDto>
{
    public override void Configure()
    {
        Post("/users");
        Policies(AccessPolicies.Admin);
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(StudioUserRequest request, CancellationToken ct)
    {
        if (!StudioRoles.IsValid(request.Role))
        {
            await Refusals.UnreadableAsync(HttpContext, "no such role",
                $"the request's `role` '{request.Role}' is not one of {string.Join(", ", StudioRoles.All)}",
                ct, field: "role");
            return;
        }
        if (await players.ResolveAsync(request.Player, ct) is not { } account)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no player",
                [new Finding(RequestRules.NoSuchSubject,
                    $"player '{request.Player}' names a Minecraft account that does not exist",
                    Field: "player")], ct);
            return;
        }
        var caller = await callers.OfAsync(HttpContext, ct);
        if (WhitelistKeeping.RefusalFor(caller, access, account.Uuid, await users.GetAsync(account.Uuid, ct),
                WhitelistKeeping.Change.Put, request.Role) is { } refused)
        {
            await WhitelistKeeping.RefuseAsync(HttpContext, refused, ct);
            return;
        }
        await Send.OkAsync(Dto(await users.PutAsync(account.Uuid, account.Name, request.Role, ct), access), ct);
    }

    internal static StudioUserDto Dto(StudioUserRow row, AccessOptions access) => new(
        row.Uuid, row.Name, row.Role, row.CreatedAt, row.DiscordId is not null,
        row.InviteExpiresAt > DateTime.UtcNow ? row.InviteExpiresAt : null, access.Admins.Contains(row.Uuid));
}

/// <summary>DELETE /api/users/{uuid} — take a person off the whitelist; they keep the credits they have and
/// write nothing more. Admin only, and within <see cref="WhitelistKeeping"/>: only an owner removes an admin,
/// and an owner is removed only by the server.</summary>
public sealed class UserRemoveEndpoint(StudioUserStore users, Callers callers, AccessOptions access)
    : EndpointWithoutRequest<AppliedDto>
{
    public override void Configure()
    {
        Delete("/users/{uuid}");
        Policies(AccessPolicies.Admin);
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var uuid = Route<string>("uuid") ?? "";
        var caller = await callers.OfAsync(HttpContext, ct);
        if (await users.GetAsync(uuid, ct) is { } person
            && WhitelistKeeping.RefusalFor(caller, access, uuid, person, WhitelistKeeping.Change.Remove) is { } refused)
        {
            await WhitelistKeeping.RefuseAsync(HttpContext, refused, ct);
            return;
        }
        if (!await users.RemoveAsync(uuid, ct))
        {
            await Refusals.NotFoundAsync(HttpContext, "whitelisted person", ct, uuid);
            return;
        }
        await Send.OkAsync(new AppliedDto(), ct);
    }
}
