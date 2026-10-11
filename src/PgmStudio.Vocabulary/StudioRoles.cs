namespace PgmStudio.Vocabulary;

/// <summary>
/// What a person on the studio's whitelist may do. An <c>unapproved</c> person signed themselves up and waits for
/// an admin: they originate a few maps and keep their own library entries. A <c>member</c> originates maps and
/// edits the ones they own or are credited as an author of; an <c>admin</c> edits every map, removes shared library rows and
/// keeps the whitelist's members. Making and unmaking an admin is an owner's, whom the server's configuration
/// names rather than a role here. Anyone not on it reads and writes nothing.
/// </summary>
public static class StudioRoles
{
    public const string Unapproved = "unapproved";

    public const string Member = "member";
    public const string Admin = "admin";

    public static readonly string[] All = [Unapproved, Member, Admin];

    public static bool IsValid(string? role) => role is Unapproved or Member or Admin;
}
