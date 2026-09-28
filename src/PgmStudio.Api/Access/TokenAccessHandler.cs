using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PgmStudio.Data.Access;

namespace PgmStudio.Api.Access;

/// <summary>
/// The scheme a caller without a browser signs in under: <c>Authorization: Bearer &lt;token&gt;</c>, read as the
/// person on the whitelist the token was issued for. The request carries their uuid and nothing else, the same
/// as a session, so what they may do is decided by the whitelist exactly as it is for them in a browser. A
/// token the studio does not hold fails the request's sign-in rather than reading it as signed out, so a
/// revoked token is refused a write instead of being mistaken for a visitor.
/// </summary>
public sealed class TokenAccessHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    StudioTokenStore tokens, StudioUserStore users)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "token";

    /// <summary>What a token starts with, so one is recognisable wherever it is pasted — a log, a secret
    /// scanner, an environment.</summary>
    public const string Prefix = "pgms_";

    private const string Bearer = "Bearer ";

    /// <summary>Whether the request names a token, which is what sends it to this scheme.</summary>
    public static bool Carries(HttpRequest request) =>
        request.Headers.Authorization.ToString().StartsWith(Bearer, StringComparison.OrdinalIgnoreCase);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();

        var token = await tokens.GetByHashAsync(StudioSecret.HashOf(header[Bearer.Length..].Trim()), Context.RequestAborted);
        if (token is null) return AuthenticateResult.Fail("the studio holds no such token");
        await tokens.TouchAsync(token, DateTime.UtcNow, Context.RequestAborted);

        var name = (await users.GetAsync(token.UserUuid, Context.RequestAborted))?.Name ?? "";
        List<Claim> claims =
        [
            new(StudioClaims.Uuid, token.UserUuid), new(StudioClaims.Name, name),
            new(StudioClaims.TokenLabel, token.Label),
        ];
        if (token.Notes) claims.Add(new Claim(StudioClaims.Notes, "true"));
        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
