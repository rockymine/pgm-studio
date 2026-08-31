namespace PgmStudio.Vocabulary;

/// <summary>
/// What a map's author may be called. PGM takes a person two ways — as an <b>account</b>, a
/// <c>uuid</c> the server resolves to a player, or as a <b>pseudonym</b>, the <c>&lt;author&gt;</c>
/// element's own text — and either alone is a whole author. So a name Mojang does not know is not a
/// failed lookup: it is the second kind, and the only thing that decides whether it may be stored is
/// whether it is a name at all.
///
/// <para>Two questions, and they are different. <see cref="IsAccountName"/> asks whether a string is
/// shaped like a Minecraft account, which is what says a lookup is worth making; a string that is not
/// cannot be an account whatever Mojang answers, so nothing is asked of it. <see cref="IsWritable"/>
/// asks whether a string may be stored as a person's name at all, which is the wider set: an account
/// name is one, and so is <c>Opus 5</c>.</para>
///
/// <para>The rule lives here because the browser and the API must agree about it exactly. The client
/// decides what to do with a row as it is typed and the API decides what reaches the document, and a
/// name one of them accepts and the other silently drops is the fault this exists to end.</para>
/// </summary>
public static class AuthorNames
{
    /// <summary>Mojang's own bound on an account name, and the shortest one it issues.</summary>
    public const int AccountMin = 3;
    public const int AccountMax = 16;

    /// <summary>How long a pseudonym may be. Generous enough for a person's full name or a model's
    /// identifier, short enough that the field cannot be pasted into.</summary>
    public const int MaxLength = 32;

    /// <summary>The punctuation a pseudonym may carry beyond letters, digits and single spaces. A model
    /// states its subversion with a point (<c>Haiku 4.5</c>), a person may be written with a hyphen or an
    /// apostrophe, and an account name carries an underscore. Nothing here can open a tag or break a
    /// line.</summary>
    private const string Punctuation = ".,-_'";

    /// <summary>Whether <paramref name="name"/> is shaped like a Minecraft account name — 3 to 16 of
    /// letters, digits and underscore, which is Mojang's own rule. A string this answers false for is not
    /// worth a lookup: no account is called that.</summary>
    public static bool IsAccountName(string? name) =>
        name is { Length: >= AccountMin and <= AccountMax }
        && name.All(c => c is '_' || (c < 128 && char.IsLetterOrDigit(c)));

    /// <summary>Whether <paramref name="name"/> may be stored as a person's name — an account name or a
    /// pseudonym. Letters, digits, single interior spaces and <c>.,-_'</c>, up to
    /// <see cref="MaxLength"/>, with no leading or trailing space and no run of two.</summary>
    public static bool IsWritable(string? name)
    {
        if (name is not { Length: > 0 and <= MaxLength }) return false;
        if (name[0] == ' ' || name[^1] == ' ') return false;
        for (var at = 0; at < name.Length; at++)
        {
            var c = name[at];
            if (c == ' ') { if (name[at - 1] == ' ') return false; continue; }
            if (!char.IsLetterOrDigit(c) && !Punctuation.Contains(c)) return false;
        }
        return true;
    }

    /// <summary>Why <paramref name="name"/> may not be stored, or null where it may — the sentence a gate
    /// puts in a finding and the editor puts under the field, so the two say the same thing.</summary>
    public static string? Refuse(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "a person needs a name";
        if (name.Length > MaxLength) return $"a name is at most {MaxLength} characters, and this is {name.Length}";
        if (name[0] == ' ' || name[^1] == ' ') return "a name does not begin or end with a space";
        if (name.Contains("  ")) return "a name carries single spaces";
        return IsWritable(name)
            ? null
            : $"a name is letters, digits, spaces and {Punctuation} — nothing else";
    }
}
