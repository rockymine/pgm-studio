using System.Security.Claims;
using PgmStudio.Data.Access;
using PgmStudio.Data.Schema;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Access;

/// <summary>
/// Who a request is: signed out, signed in as an account the whitelist does not hold, or a person on it in
/// one of <see cref="StudioRoles"/>. The local admin of an open studio is the last kind with no account.
/// </summary>
public sealed record Caller(string? Uuid, string? Name, string? Role)
{
    public static readonly Caller SignedOut = new(null, null, null);

    public bool SignedIn => Uuid is not null || Role is not null;
    public bool Whitelisted => Role is not null;
    public bool IsAdmin => Role == StudioRoles.Admin;
}

/// <summary>
/// Resolves the <see cref="Caller"/> behind a request and answers what they may do. The role comes from the
/// whitelist on every request rather than from the session, so a change to the whitelist holds at once.
/// </summary>
public sealed class Callers(AccessOptions access, StudioUserStore users)
{
    private const string Resolved = "pgm-studio/caller";

    /// <summary>The caller behind <paramref name="http"/>, resolved once per request.</summary>
    public async Task<Caller> OfAsync(HttpContext http, CancellationToken ct = default)
    {
        if (http.Items.TryGetValue(Resolved, out var kept) && kept is Caller caller) return caller;
        caller = await ResolveAsync(http.User, ct);
        http.Items[Resolved] = caller;
        return caller;
    }

    private async Task<Caller> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.Identity is not { IsAuthenticated: true }) return Caller.SignedOut;
        if (principal.HasClaim(claim => claim.Type == StudioClaims.LocalAdmin))
            return new(null, principal.FindFirstValue(StudioClaims.Name), StudioRoles.Admin);
        if (principal.FindFirstValue(StudioClaims.Uuid) is not { Length: > 0 } uuid) return Caller.SignedOut;

        var name = principal.FindFirstValue(StudioClaims.Name);
        if (access.Admins.Contains(uuid)) return new(uuid, name, StudioRoles.Admin);
        var row = await users.GetAsync(uuid, ct);
        return new(uuid, row?.Name ?? name, row?.Role);
    }

    /// <summary>Whether <paramref name="caller"/> may change <paramref name="map"/>: an admin may change any
    /// map, and a person on the whitelist may change one they own or are credited as an author of.</summary>
    public async Task<bool> MayEditAsync(Caller caller, MapRow map, CancellationToken ct = default)
    {
        if (caller.IsAdmin) return true;
        if (!caller.Whitelisted || caller.Uuid is null) return false;
        return string.Equals(map.OwnerUuid, caller.Uuid, StringComparison.OrdinalIgnoreCase)
            || await users.IsAuthorOfAsync(map.Id, caller.Uuid, ct);
    }

    /// <summary>The uuid a map originated by this request is owned under, or null for the local admin.</summary>
    public static string? OwnerOf(HttpContext http) => http.User.FindFirstValue(StudioClaims.Uuid);
}
