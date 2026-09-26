namespace PgmStudio.Vocabulary;

/// <summary>
/// What a person on the studio's whitelist may do. A <c>member</c> originates maps and edits the ones they
/// own or are credited as an author of; an <c>admin</c> edits every map, removes shared library rows and
/// keeps the whitelist. Anyone not on it reads and writes nothing.
/// </summary>
public static class StudioRoles
{
    public const string Member = "member";
    public const string Admin = "admin";

    public static readonly string[] All = [Member, Admin];

    public static bool IsValid(string? role) => role is Member or Admin;
}
