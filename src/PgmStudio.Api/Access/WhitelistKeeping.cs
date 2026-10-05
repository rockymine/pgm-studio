using PgmStudio.Api.Endpoints;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Access;

/// <summary>
/// What a caller may do to one person on the whitelist. An <see cref="Caller.IsOwner">owner</see> is a uuid
/// <c>Access:Admins</c> names, signed in from a browser, or an open studio's local admin; both are granted by
/// the server alone, never through the studio. An owner keeps the whole whitelist but another owner. An admin
/// keeps the members: adds, re-roles and removes them, issues a token acting as one, and invites one no Discord
/// account signs in as yet. So no admin makes or unmakes an admin, and none redirects an account somebody
/// already signs in with, since following an invitation binds the account to whoever follows it.
/// </summary>
public static class WhitelistKeeping
{
    /// <summary>The four ways a caller changes someone's place on the whitelist.</summary>
    public enum Change { Put, Remove, Invite, IssueToken }

    /// <summary>Why <paramref name="caller"/> may not make <paramref name="change"/> to the person
    /// <paramref name="targetUuid"/> names, or null where they may. <paramref name="target"/> is that person's
    /// row, null where the whitelist does not hold them yet; <paramref name="role"/> is the role a
    /// <see cref="Change.Put"/> gives them.</summary>
    public static string? RefusalFor(Caller caller, AccessOptions access, string targetUuid, StudioUserRow? target,
                                     Change change, string? role = null)
    {
        if (access.Admins.Contains(targetUuid) && !(caller.IsOwner && (caller.Uuid is null || caller.Is(targetUuid))))
            return $"person '{targetUuid}' is an owner named in the server's configuration, which no request changes";
        if (caller.IsOwner) return null;
        if (target?.Role == StudioRoles.Admin)
            return $"person '{targetUuid}' is an admin, and the request is signed in as someone who is not an owner";
        if (change == Change.Put && role == StudioRoles.Admin)
            return $"the request gives person '{targetUuid}' the role `{role}`, and is signed in as someone who is not "
                   + "an owner";
        if (change == Change.Invite && target?.DiscordId is not null)
            return $"the request invites person '{target.Name}', who already signs in with Discord, and is signed in "
                   + "as someone who is not an owner";
        return null;
    }

    /// <summary>Refuse a change <see cref="RefusalFor"/> turned away, <c>RQ8</c> at 403.</summary>
    public static Task RefuseAsync(HttpContext http, string reason, CancellationToken ct) =>
        Refusals.WriteAsync(http, 403, "not permitted", [new Finding(RequestRules.NotPermitted, reason)], ct);
}
