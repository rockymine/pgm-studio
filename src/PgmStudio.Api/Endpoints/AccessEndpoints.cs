using FastEndpoints;
using PgmStudio.Api.Access;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Access;
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
        await Send.OkAsync(new CallerDto(access.Mode, caller.SignedIn, caller.Uuid, caller.Name, caller.Role), ct);
    }
}

/// <summary>GET /api/users — the whitelist, by name. Admin only.</summary>
public sealed class UserListEndpoint(StudioUserStore users) : EndpointWithoutRequest<List<StudioUserDto>>
{
    public override void Configure()
    {
        Get("/users");
        Policies(AccessPolicies.Admin);
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync([.. (await users.ListAsync(ct)).Select(UserPutEndpoint.Dto)], ct);
}

/// <summary>POST /api/users — put a Minecraft account on the whitelist in a role, or change the role of one
/// already on it. The player is resolved to the account first, so the whitelist holds the uuid an author is
/// credited under. Admin only.</summary>
public sealed class UserPutEndpoint(StudioUserStore users, PlayerLookup players)
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
                $"a role is one of {string.Join(", ", StudioRoles.All)}", ct, field: "role");
            return;
        }
        if (await players.ResolveAsync(request.Player, ct) is not { } account)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no player",
                [new Finding(RequestRules.NoSuchSubject,
                    $"no Minecraft account answers to '{request.Player}', and the whitelist holds accounts",
                    Field: "player")], ct);
            return;
        }
        await Send.OkAsync(Dto(await users.PutAsync(account.Uuid, account.Name, request.Role, ct)), ct);
    }

    internal static StudioUserDto Dto(StudioUserRow row) => new(row.Uuid, row.Name, row.Role, row.CreatedAt);
}

/// <summary>DELETE /api/users/{uuid} — take a person off the whitelist; they keep the credits they have and
/// write nothing more. An uuid named in <c>Access:Admins</c> stays an admin whatever this removes. Admin
/// only.</summary>
public sealed class UserRemoveEndpoint(StudioUserStore users) : EndpointWithoutRequest<AppliedDto>
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
        if (!await users.RemoveAsync(uuid, ct))
        {
            await Refusals.NotFoundAsync(HttpContext, "whitelisted person", ct, uuid);
            return;
        }
        await Send.OkAsync(new AppliedDto(), ct);
    }
}
