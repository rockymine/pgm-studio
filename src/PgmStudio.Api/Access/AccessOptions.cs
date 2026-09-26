using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Access;

/// <summary>
/// The access configuration: <c>Access:Mode</c>, one of <see cref="AccessModes"/> and <c>invited</c> where
/// unset, so a deployment that forgets to say is closed rather than open; and <c>Access:Admins</c>, the
/// Minecraft uuids that are admins whatever the whitelist says — which is what makes the first admin, and
/// what keeps the whitelist's keeper from removing themselves out of it.
/// </summary>
public sealed record AccessOptions(string Mode, IReadOnlySet<string> Admins)
{
    /// <summary>The authentication scheme every request is read under, which forwards to the open scheme or to
    /// the session cookie as <see cref="Mode"/> says.</summary>
    public const string Scheme = "access";

    public bool IsOpen => Mode == AccessModes.Open;

    public static AccessOptions From(IConfiguration configuration)
    {
        var stated = configuration["Access:Mode"]?.Trim().ToLowerInvariant();
        var mode = string.IsNullOrEmpty(stated) ? AccessModes.Invited
            : AccessModes.All.Contains(stated) ? stated
            : throw new InvalidOperationException(
                $"Access:Mode is '{stated}'; it takes {string.Join(" or ", AccessModes.All)}.");
        var admins = configuration.GetSection("Access:Admins").Get<string[]>() ?? [];
        return new(mode, admins.Select(uuid => uuid.Trim()).Where(uuid => uuid.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
