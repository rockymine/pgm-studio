using PgmStudio.Data.Map;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// Resolving a typed person to the pair the <c>map.xml</c> stores. Two things stand in front of Mojang, and
/// each of them answers questions Mojang would otherwise be asked for nothing.
///
/// <para><b>The shape of the name.</b> An account name is 3 to 16 of letters, digits and underscore
/// (<see cref="AuthorNames.IsAccountName"/>); no account is called anything else, so a string that is not one
/// is a pseudonym before a request is made, and none is made.</para>
///
/// <para><b>What is already known.</b> A resolved pair barely changes and the studio asks for the same few
/// people constantly, so a kept answer is returned and a fresh one is kept
/// (<see cref="PlayerNameStore"/>).</para>
///
/// <para>Null is the answer for a person who is not an account — the caller keeps their stated name as a
/// pseudonym, which is a whole author in PGM's own model. That is the same answer for a name no account
/// carries and for a host that could not be reached, and deliberately so: an author working offline states
/// the people on their map and the credits stand.</para>
/// </summary>
public sealed class PlayerLookup(MojangClient mojang, PlayerNameStore kept)
{
    /// <summary>The account behind a typed name or uuid, or null where there is none.</summary>
    public async Task<(string Uuid, string Name)?> ResolveAsync(string nameOrUuid, CancellationToken ct = default)
    {
        var asked = nameOrUuid.Trim();
        if (asked.Length == 0) return null;
        if (!AuthorNames.IsAccountName(asked) && !LooksLikeUuid(asked)) return null;

        if (await kept.LookAsync(asked, ct) is { } known) return known;

        try
        {
            var (uuid, name) = await mojang.LookupAsync(asked, ct);
            await kept.KeepAsync(uuid, name, ct);
            return (uuid, name);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>A dashed or undashed 32-hex uuid. Asked apart from the account-name shape because a uuid is
    /// neither 3–16 characters nor made only of letters and digits, and it is a legal thing to look up.</summary>
    private static bool LooksLikeUuid(string value)
    {
        var bare = value.Replace("-", "");
        return bare.Length == 32 && bare.All(Uri.IsHexDigit);
    }
}
