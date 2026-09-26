using System.Security.Claims;

namespace PgmStudio.Api.Access;

/// <summary>The claims a signed-in request carries. A session names the account and nothing else: the role
/// is read from the whitelist on every request, so taking someone off it takes effect at once.</summary>
public static class StudioClaims
{
    /// <summary>The Minecraft uuid of the signed-in person.</summary>
    public const string Uuid = "pgm-studio/uuid";

    /// <summary>Their Minecraft name, for display.</summary>
    public const string Name = ClaimTypes.Name;

    /// <summary>Set only by the open scheme, which is an admin without an account.</summary>
    public const string LocalAdmin = "pgm-studio/local-admin";
}
