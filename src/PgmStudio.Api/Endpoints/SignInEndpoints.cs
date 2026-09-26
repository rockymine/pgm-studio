using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PgmStudio.Api.Access;
using PgmStudio.Contracts;
using PgmStudio.Data.Access;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>What the sign-in routes share: the refusal for a studio with no Discord application, sending the
/// browser to Discord, and the one kind of place a browser is sent back to.</summary>
internal static class SignIn
{
    public static async Task<bool> UnavailableAsync(HttpContext http, IConfiguration configuration, CancellationToken ct)
    {
        if (DiscordSignIn.Configured(configuration)) return false;
        await Refusals.WriteAsync(http, 503, "sign-in unavailable",
            [new Finding(RequestRules.SignInUnavailable,
                "this studio has no Discord application configured, so nobody can sign in to it")], ct);
        return true;
    }

    /// <summary>The <c>Challenge</c> that sends the browser to Discord, carrying the invitation if there is one
    /// and the page to come back to.</summary>
    public static IResult Challenge(HttpContext http, string? invite)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = $"/api/auth/discord/complete?returnUrl={Uri.EscapeDataString(ReturnUrl(http))}",
        };
        if (invite is not null) properties.Items[DiscordSignIn.InviteItem] = invite;
        return Results.Challenge(properties, [DiscordSignIn.Scheme]);
    }

    /// <summary>The page to land on once signed in: the <c>returnUrl</c> asked for when it is a path on this
    /// studio, and the start page otherwise, so a sign-in link cannot be made to send anyone elsewhere.</summary>
    public static string ReturnUrl(HttpContext http) =>
        http.Request.Query["returnUrl"].ToString() is { Length: > 0 } asked
        && asked.StartsWith('/') && !asked.StartsWith("//") && !asked.StartsWith("/\\")
            ? asked
            : "/";
}

/// <summary>GET /api/auth/discord — sign in with a Discord account already bound to someone on the whitelist.
/// Sends the browser to Discord, and back to <c>returnUrl</c> once signed in.</summary>
public sealed class DiscordSignInEndpoint(IConfiguration configuration) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/auth/discord");
        Description(b => b.ClearDefaultProduces(204).Produces(302).Refuses(503));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await SignIn.UnavailableAsync(HttpContext, configuration, ct)) return;
        await Send.ResultAsync(SignIn.Challenge(HttpContext, invite: null));
    }
}

/// <summary>GET /api/auth/invite/{code} — follow an invitation: the Discord account that signs in through it is
/// bound to the person it was issued for.</summary>
public sealed class InviteFollowEndpoint(IConfiguration configuration, StudioUserStore users) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/auth/invite/{code}");
        Description(b => b.ClearDefaultProduces(204).Produces(302).Refuses(404, 503));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await SignIn.UnavailableAsync(HttpContext, configuration, ct)) return;
        var code = Route<string>("code") ?? "";
        if (await users.GetByInviteAsync(DiscordSignIn.HashOf(code), DateTime.UtcNow, ct) is null)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no such invitation",
                [new Finding(RequestRules.NoSuchSubject,
                    "this invitation is not open — it was used, replaced or has lapsed; ask an admin for another")], ct);
            return;
        }
        await Send.ResultAsync(SignIn.Challenge(HttpContext, code));
    }
}

/// <summary>GET /api/auth/discord/complete — where a sign-in lands once Discord has answered: decides who the
/// Discord account is, writes the session, and sends the browser on. The Discord application never names this
/// route; it names <c>/api/auth/discord/callback</c>, which the Discord scheme answers and forwards here.</summary>
public sealed class DiscordCompleteEndpoint(StudioUserStore users) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/auth/discord/complete");
        Description(b => b.ClearDefaultProduces(204).Produces(302).Refuses(401, 403));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var answered = await HttpContext.AuthenticateAsync(DiscordSignIn.ExternalScheme);
        await HttpContext.SignOutAsync(DiscordSignIn.ExternalScheme);
        if (!answered.Succeeded
            || answered.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value is not { Length: > 0 } discordId)
        {
            await Refusals.WriteAsync(HttpContext, 401, "not signed in",
                [new Finding(RequestRules.SignedOut, "Discord did not answer who is signing in; start again")], ct);
            return;
        }

        answered.Properties!.Items.TryGetValue(DiscordSignIn.InviteItem, out var invite);
        if (await DiscordSignIn.ResolveAsync(users, discordId, invite, ct) is not { } person)
        {
            await Refusals.WriteAsync(HttpContext, 403, "not permitted",
                [new Finding(RequestRules.NotPermitted, invite is null
                    ? "this Discord account signs in as nobody on the studio's whitelist; ask an admin for an invitation"
                    : "this invitation is not open any more; ask an admin for another")], ct);
            return;
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, DiscordSignIn.SessionOf(person));
        await Send.RedirectAsync(SignIn.ReturnUrl(HttpContext));
    }
}

/// <summary>POST /api/auth/sign-out — end the session. Open to anyone, signed in or not.</summary>
public sealed class SignOutEndpoint : EndpointWithoutRequest<AppliedDto>
{
    public override void Configure()
    {
        Post("/auth/sign-out");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await Send.OkAsync(new AppliedDto(), ct);
    }
}

/// <summary>POST /api/users/{uuid}/invite — open an invitation for someone on the whitelist, replacing any open
/// one, and answer the link to hand them. Admin only.</summary>
public sealed class UserInviteEndpoint(StudioUserStore users) : EndpointWithoutRequest<InviteDto>
{
    public override void Configure()
    {
        Post("/users/{uuid}/invite");
        Policies(AccessPolicies.Admin);
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var uuid = Route<string>("uuid") ?? "";
        var (code, hash) = DiscordSignIn.NewInvite();
        var expiresAt = DateTime.UtcNow + DiscordSignIn.InviteLifetime;
        if (!await users.OpenInviteAsync(uuid, hash, expiresAt, ct))
        {
            await Refusals.NotFoundAsync(HttpContext, "whitelisted person", ct, uuid);
            return;
        }
        var link = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/api/auth/invite/{code}";
        await Send.OkAsync(new InviteDto(link, expiresAt), ct);
    }
}
