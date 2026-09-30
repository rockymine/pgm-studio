using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>Who a request is signed in as, and what that lets it do.</summary>
/// <param name="Mode">How this studio decides who may write.</param>
/// <param name="SignedIn">Whether the request names anyone. An open studio's requests always do.</param>
/// <param name="Uuid">The Minecraft uuid signed in, or null — signed out, or an open studio's local admin.</param>
/// <param name="Name">Their Minecraft name, or null where nobody is signed in.</param>
/// <param name="Role">Their role on the whitelist, or null where they are not on it and may write nothing.</param>
/// <param name="Notes">Whether the request may read and answer map notes: an admin, or a token carrying the
/// notes permission.</param>
/// <param name="Owner">Whether the request is an owner: an admin the server's configuration names, signed in
/// from a browser, who alone makes and unmakes admins.</param>
public sealed record CallerDto(
    [property: WordSet(typeof(AccessModes))] string Mode,
    bool SignedIn,
    string? Uuid,
    string? Name,
    [property: WordSet(typeof(StudioRoles))] string? Role,
    bool Notes = false,
    bool Owner = false);

/// <summary>One person on the whitelist.</summary>
/// <param name="Uuid">The Minecraft uuid they are credited and signed in under.</param>
/// <param name="Name">Their Minecraft name when they were added.</param>
/// <param name="Role">What they may do.</param>
/// <param name="AddedAt">When they were first put on the whitelist.</param>
/// <param name="SignsIn">Whether a Discord account is bound to them, so they can sign in.</param>
/// <param name="InviteExpiresAt">When their open invitation lapses, in UTC, or null where none is open.</param>
/// <param name="Owner">Whether the server's configuration names them an owner, whom only the server changes.</param>
public sealed record StudioUserDto(
    string Uuid,
    string Name,
    [property: WordSet(typeof(StudioRoles))] string Role,
    DateTime AddedAt,
    bool SignsIn,
    DateTime? InviteExpiresAt,
    bool Owner = false);

/// <summary>Put a person on the whitelist, or change the role of one already on it.</summary>
/// <param name="Player">Their Minecraft name or uuid; the studio resolves it to the account.</param>
/// <param name="Role">The role they get.</param>
public sealed record StudioUserRequest(
    string Player,
    [property: WordSet(typeof(StudioRoles))] string Role);

/// <summary>An invitation: the link that binds the first Discord account to follow it to one person on the
/// whitelist, and when it lapses.</summary>
/// <param name="Link">The one-time sign-in link to hand the person. It is shown once and stored only as a hash.</param>
/// <param name="ExpiresAt">When it stops working, in UTC.</param>
public sealed record InviteDto(string Link, DateTime ExpiresAt);

/// <summary>What the request may do to one map.</summary>
/// <param name="MayEdit">Whether its writes would be accepted: an admin, the map's owner, or an author it
/// credits. The client opens the map read-only where this is false.</param>
public sealed record MapAccessDto(bool MayEdit);

/// <summary>Issue a token for a caller without a browser.</summary>
/// <param name="Label">What the token is for — the agent or the machine that will hold it. Up to 100
/// characters; blank names it <c>token</c>.</param>
/// <param name="Notes">Whether the token may read and answer map notes. Only an admin's token may carry it.</param>
public sealed record StudioTokenRequest(string? Label, bool Notes = false);

/// <summary>One token, as its holder lists it. The token itself is never answered again after it is issued.</summary>
/// <param name="Id">What revokes it.</param>
/// <param name="Label">What it was issued for.</param>
/// <param name="IssuedAt">When it was issued, in UTC.</param>
/// <param name="LastUsedAt">When it last signed a request in, in UTC to the minute, or null where it never has.</param>
/// <param name="Notes">Whether it carries the notes permission.</param>
public sealed record StudioTokenDto(long Id, string Label, DateTime IssuedAt, DateTime? LastUsedAt, bool Notes = false);

/// <summary>A token just issued: the one answer that carries it.</summary>
/// <param name="Id">What revokes it.</param>
/// <param name="Label">What it was issued for.</param>
/// <param name="Token">The token, sent as <c>Authorization: Bearer &lt;token&gt;</c>. The studio keeps only its
/// hash, so this is the only time it can be read.</param>
/// <param name="ActsAs">The Minecraft name of the person it signs in as.</param>
/// <param name="Notes">Whether it carries the notes permission.</param>
public sealed record StudioTokenIssuedDto(long Id, string Label, string Token, string ActsAs, bool Notes = false);
