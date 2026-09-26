using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using PgmStudio.Data.Access;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Access;

/// <summary>
/// Signing in with Discord (<c>docs/access.md</c>). Discord answers who a person is on Discord and nothing
/// else; which Minecraft account that is comes from an invitation an admin issued, which binds the Discord
/// account the first time it is followed. The studio asks Discord for <c>identify</c> alone.
/// </summary>
public static class DiscordSignIn
{
    /// <summary>The OAuth scheme that sends a browser to Discord and reads it back.</summary>
    public const string Scheme = "discord";

    /// <summary>The short-lived cookie holding what Discord answered until the studio has decided who that
    /// is; the session is written only once it has.</summary>
    public const string ExternalScheme = "discord-external";

    /// <summary>Where Discord sends the browser back; the Discord application lists it as a redirect.</summary>
    public const string CallbackPath = "/api/auth/discord/callback";

    /// <summary>The property an invitation's code rides in, inside the state Discord hands back.</summary>
    public const string InviteItem = "invite";

    /// <summary>How long an invitation stays open.</summary>
    public static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    /// <summary>Whether this studio has a Discord application to sign in with.</summary>
    public static bool Configured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Discord:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Discord:ClientSecret"]);

    /// <summary>Register the Discord scheme and the cookie it signs into. The application is read from
    /// <c>Discord:ClientId</c> and <c>Discord:ClientSecret</c> when the scheme is first used; an unconfigured
    /// studio registers placeholders, and the routes that would challenge refuse before they do.</summary>
    public static AuthenticationBuilder AddDiscordSignIn(this AuthenticationBuilder builder)
    {
        builder.AddCookie(ExternalScheme, cookie =>
        {
            cookie.Cookie.Name = "pgm-studio.discord";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Lax;
            cookie.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });
        builder.AddOAuth(Scheme, oauth =>
        {
            oauth.SignInScheme = ExternalScheme;
            oauth.CallbackPath = CallbackPath;
            oauth.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
            oauth.TokenEndpoint = "https://discord.com/api/oauth2/token";
            oauth.UserInformationEndpoint = "https://discord.com/api/users/@me";
            oauth.Scope.Add("identify");
            oauth.UsePkce = true;
            // The browser comes back from Discord on a top-level GET, which a Lax cookie is sent on.
            oauth.CorrelationCookie.SameSite = SameSiteMode.Lax;
            oauth.Events.OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();
                using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                context.Identity!.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.RootElement.GetProperty("id").GetString()!));
                context.Identity.AddClaim(new Claim(ClaimTypes.Name, user.RootElement.GetProperty("username").GetString() ?? ""));
            };
        });
        builder.Services.AddOptions<OAuthOptions>(Scheme).Configure<IConfiguration>((oauth, configuration) =>
        {
            oauth.ClientId = configuration["Discord:ClientId"] is { Length: > 0 } id ? id : "unconfigured";
            oauth.ClientSecret = configuration["Discord:ClientSecret"] is { Length: > 0 } secret ? secret : "unconfigured";
        });
        return builder;
    }

    /// <summary>A fresh invitation code, and the hash the whitelist stores it under.</summary>
    public static (string Code, string Hash) NewInvite()
    {
        var code = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (code, HashOf(code));
    }

    public static string HashOf(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    /// <summary>
    /// Who a Discord account signs in as. With an invitation, the person it was issued for — binding the account
    /// to them and closing it; without one, the person the account is already bound to. Null where neither
    /// holds: the invitation lapsed or was never issued, or the account is bound to nobody.
    /// </summary>
    public static async Task<StudioUserRow?> ResolveAsync(
        StudioUserStore users, string discordId, string? invite, CancellationToken ct)
    {
        if (invite is { Length: > 0 })
        {
            if (await users.GetByInviteAsync(HashOf(invite), DateTime.UtcNow, ct) is not { } invited) return null;
            await users.BindDiscordAsync(invited.Uuid, discordId, ct);
            return invited;
        }
        return await users.GetByDiscordAsync(discordId, ct);
    }

    /// <summary>The session for a person on the whitelist: their uuid and their name, and nothing else.</summary>
    public static ClaimsPrincipal SessionOf(StudioUserRow user) =>
        new(new ClaimsIdentity(
            [new Claim(StudioClaims.Uuid, user.Uuid), new Claim(StudioClaims.Name, user.Name)],
            CookieAuthenticationDefaults.AuthenticationScheme));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
