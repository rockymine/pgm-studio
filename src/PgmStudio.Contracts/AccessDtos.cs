using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>Who a request is signed in as, and what that lets it do.</summary>
/// <param name="Mode">How this studio decides who may write.</param>
/// <param name="SignedIn">Whether the request names anyone. An open studio's requests always do.</param>
/// <param name="Uuid">The Minecraft uuid signed in, or null — signed out, or an open studio's local admin.</param>
/// <param name="Name">Their Minecraft name, or null where nobody is signed in.</param>
/// <param name="Role">Their role on the whitelist, or null where they are not on it and may write nothing.</param>
public sealed record CallerDto(
    [property: WordSet(typeof(AccessModes))] string Mode,
    bool SignedIn,
    string? Uuid,
    string? Name,
    [property: WordSet(typeof(StudioRoles))] string? Role);

/// <summary>One person on the whitelist.</summary>
/// <param name="Uuid">The Minecraft uuid they are credited and signed in under.</param>
/// <param name="Name">Their Minecraft name when they were added.</param>
/// <param name="Role">What they may do.</param>
/// <param name="AddedAt">When they were first put on the whitelist.</param>
public sealed record StudioUserDto(
    string Uuid,
    string Name,
    [property: WordSet(typeof(StudioRoles))] string Role,
    DateTime AddedAt);

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
