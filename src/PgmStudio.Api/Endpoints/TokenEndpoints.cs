using FastEndpoints;
using PgmStudio.Api.Access;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Access;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/users/me/tokens — the tokens that sign in as the caller, newest first. Empty for a
/// caller with no account: a visitor, or the local admin of an open studio.</summary>
public sealed class MyTokensEndpoint(Callers callers, StudioTokenStore tokens) : EndpointWithoutRequest<List<StudioTokenDto>>
{
    public override void Configure() => Get("/users/me/tokens");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = await callers.OfAsync(HttpContext, ct);
        await Send.OkAsync(caller.Uuid is { } uuid ? [.. (await tokens.ListAsync(uuid, ct)).Select(Dto)] : [], ct);
    }

    internal static StudioTokenDto Dto(Data.Schema.StudioTokenRow row) =>
        new(row.Id, row.Label, row.CreatedAt, row.LastUsedAt);
}

/// <summary>POST /api/users/me/tokens — issue a token that signs in as the caller. The answer is the only
/// one that carries the token. 403 to a request signed in by a token, so a token cannot outlive its own
/// revocation by issuing another; 404 for the local admin of an open studio, who has no account to act
/// as.</summary>
public sealed class MyTokenIssueEndpoint(Callers callers, StudioTokenStore tokens, StudioUserStore users)
    : Endpoint<StudioTokenRequest, StudioTokenIssuedDto>
{
    public override void Configure()
    {
        Post("/users/me/tokens");
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(StudioTokenRequest request, CancellationToken ct)
    {
        var caller = await callers.OfAsync(HttpContext, ct);
        if (caller.ViaToken)
        {
            await Refusals.WriteAsync(HttpContext, 403, "not permitted",
                [new Finding(RequestRules.NotPermitted,
                    "a token cannot issue a token — issue one from a browser signed in as its person")], ct);
            return;
        }
        if (caller.Uuid is not { } uuid)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no account",
                [new Finding(RequestRules.NoSuchSubject,
                    "this caller has no Minecraft account for a token to act as — an open studio's local admin "
                    + "needs none, since every request to it is already the admin")], ct);
            return;
        }
        await Send.OkAsync(await TokenIssue.IssueAsync(tokens, users, uuid, request.Label, ct), ct);
    }
}

/// <summary>DELETE /api/users/me/tokens/{id} — revoke one of the caller's tokens; a request carrying it is
/// signed in as nobody from then on. Anyone on the whitelist revokes their own. 404 for a token that is not
/// theirs.</summary>
public sealed class MyTokenRevokeEndpoint(Callers callers, StudioTokenStore tokens) : EndpointWithoutRequest<AppliedDto>
{
    public override void Configure()
    {
        Delete("/users/me/tokens/{id}");
        Policies(AccessPolicies.Member);
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = await callers.OfAsync(HttpContext, ct);
        var id = Route<long>("id");
        if (caller.Uuid is not { } uuid || !await tokens.RevokeAsync(uuid, id, ct))
        {
            await Refusals.NotFoundAsync(HttpContext, "token", ct);
            return;
        }
        await Send.OkAsync(new AppliedDto(), ct);
    }
}

/// <summary>POST /api/users/{uuid}/tokens — issue a token that signs in as someone on the whitelist, for an
/// agent acting as them. Admin only, which a token never is. 404 for a uuid the whitelist does not
/// hold.</summary>
public sealed class UserTokenIssueEndpoint(StudioTokenStore tokens, StudioUserStore users)
    : Endpoint<StudioTokenRequest, StudioTokenIssuedDto>
{
    public override void Configure()
    {
        Post("/users/{uuid}/tokens");
        Policies(AccessPolicies.Admin);
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(StudioTokenRequest request, CancellationToken ct)
    {
        var uuid = Route<string>("uuid") ?? "";
        if (await users.GetAsync(uuid, ct) is null)
        {
            await Refusals.NotFoundAsync(HttpContext, "whitelisted person", ct, uuid);
            return;
        }
        await Send.OkAsync(await TokenIssue.IssueAsync(tokens, users, uuid, request.Label, ct), ct);
    }
}

/// <summary>Issuing a token, the same for the caller's own and an admin's for someone else.</summary>
internal static class TokenIssue
{
    public const int MaxLabel = 100;

    public static async Task<StudioTokenIssuedDto> IssueAsync(
        StudioTokenStore tokens, StudioUserStore users, string uuid, string? label, CancellationToken ct)
    {
        var named = (label ?? "").Trim() is { Length: > 0 } stated ? stated[..Math.Min(stated.Length, MaxLabel)] : "token";
        var (token, hash) = StudioSecret.New(TokenAccessHandler.Prefix);
        var row = await tokens.IssueAsync(uuid, hash, named, ct);
        var actsAs = (await users.GetAsync(uuid, ct))?.Name ?? uuid;
        return new StudioTokenIssuedDto(row.Id, row.Label, token, actsAs);
    }
}
