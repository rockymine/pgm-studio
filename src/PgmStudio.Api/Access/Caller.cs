using System.Security.Claims;
using PgmStudio.Api.Services;
using PgmStudio.Data.Access;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Access;

/// <summary>
/// Who a request is: signed out, signed in as an account the whitelist does not hold, or a person on it in
/// one of <see cref="StudioRoles"/>. The local admin of an open studio is the last kind with no account.
/// <paramref name="ViaToken"/> is a request signed in by a token rather than a browser session, and
/// <paramref name="Token"/> that token's label. <paramref name="MayNote"/> is whether the request may read and
/// answer map notes: an admin in a browser, or a token carrying the notes permission whose person is an admin.
/// <paramref name="IsOwner"/> is an admin the server itself names — a uuid in <c>Access:Admins</c> signed in
/// from a browser, or an open studio's local admin — who alone makes and unmakes admins
/// (<see cref="WhitelistKeeping"/>).
/// </summary>
public sealed record Caller(string? Uuid, string? Name, string? Role, bool ViaToken = false, bool MayNote = false,
                            string? Token = null, bool IsOwner = false)
{
    public static readonly Caller SignedOut = new(null, null, null);

    public bool SignedIn => Uuid is not null || Role is not null;
    public bool Whitelisted => Role is not null;
    public bool IsAdmin => Role == StudioRoles.Admin;

    /// <summary>Whether this caller is the person <paramref name="uuid"/> names.</summary>
    public bool Is(string uuid) => string.Equals(Uuid, uuid, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>The caller a principal is. A token is capped at <see cref="StudioRoles.Member"/>: it acts as
    /// its person on their maps and nothing wider, so a token that leaks from the environment holding it can
    /// neither keep the whitelist, nor issue invitations, nor change a map its person does not own. The notes
    /// permission is the one thing a token lifts the cap for, and only where its person is an admin.</summary>
    private async Task<Caller> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.Identity is not { IsAuthenticated: true }) return Caller.SignedOut;
        if (principal.HasClaim(claim => claim.Type == StudioClaims.LocalAdmin))
            return new(null, principal.FindFirstValue(StudioClaims.Name), StudioRoles.Admin, MayNote: true, IsOwner: true);
        if (principal.FindFirstValue(StudioClaims.Uuid) is not { Length: > 0 } uuid) return Caller.SignedOut;

        var viaToken = principal.Identity.AuthenticationType == TokenAccessHandler.SchemeName;
        var name = principal.FindFirstValue(StudioClaims.Name);
        var named = access.Admins.Contains(uuid);
        var row = named ? null : await users.GetAsync(uuid, ct);
        var role = named ? StudioRoles.Admin : row?.Role;
        var mayNote = role == StudioRoles.Admin && (!viaToken || principal.HasClaim(claim => claim.Type == StudioClaims.Notes));
        if (viaToken && role == StudioRoles.Admin) role = StudioRoles.Member;
        return new(uuid, row?.Name ?? name, role, viaToken, mayNote,
                   viaToken ? principal.FindFirstValue(StudioClaims.TokenLabel) : null, IsOwner: named && !viaToken);
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

    /// <summary>The person a map originated by this request belongs to and is credited to, or null for the
    /// local admin, who has no account.</summary>
    public static MapOriginator? OriginatorOf(HttpContext http) =>
        http.User.FindFirstValue(StudioClaims.Uuid) is { Length: > 0 } uuid
            ? new MapOriginator(uuid, http.User.FindFirstValue(StudioClaims.Name))
            : null;

    /// <summary>Stamp a writing request's changes to a map's documents with who is making them, read off the
    /// principal it signed in as: the account, and the token's label where a token signed it in.</summary>
    public static Task StampWritesAsync(HttpContext http, RequestDelegate next)
    {
        if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
            http.RequestServices.GetRequiredService<MapArtifactStore>().Stamp = StampOf(http.User);
        return next(http);
    }

    private static ChangeStamp StampOf(ClaimsPrincipal principal) =>
        principal.Identity is not { IsAuthenticated: true } identity
            ? ChangeStamp.Unknown
            : new(principal.FindFirstValue(StudioClaims.Uuid) is { Length: > 0 } uuid ? uuid : null,
                  principal.FindFirstValue(StudioClaims.Name),
                  identity.AuthenticationType == TokenAccessHandler.SchemeName
                      ? principal.FindFirstValue(StudioClaims.TokenLabel) : null);
}
