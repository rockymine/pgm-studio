using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PgmStudio.Api.Access;

/// <summary>The scheme the <c>open</c> access mode runs under: every request is signed in as the local
/// admin, who has no Minecraft account and so owns nothing it originates.</summary>
public sealed class OpenAccessHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "open";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [new Claim(StudioClaims.LocalAdmin, "true"), new Claim(StudioClaims.Name, "local")], Scheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
