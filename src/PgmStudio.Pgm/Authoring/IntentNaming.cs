using System.Text.RegularExpressions;

namespace PgmStudio.Pgm.Authoring;

/// <summary>Shared naming for the declarative generators: the stable id slug derived from a team id
/// (e.g. <c>red-team</c> → <c>red</c>), used for region/filter ids (<c>only-red</c>, <c>red-spawn</c>,
/// <c>red-wool</c>), and the <c>&lt;team&gt;</c> id the document itself carries. Slug comes from the id,
/// never the raw colour (which may be multi-word).</summary>
internal static class IntentNaming
{
    public static string Slug(string teamId)
    {
        var s = teamId.Trim().ToLowerInvariant();
        if (s.EndsWith("-team")) s = s[..^5];
        s = Regex.Replace(s, "[^a-z0-9]+", "-").Trim('-');
        return s.Length > 0 ? s : teamId;
    }

    /// <summary>The id a team carries in the document — the slug with a <c>-team</c> suffix, so
    /// <c>&lt;team id="red-team"&gt;</c> rather than <c>&lt;team id="red"&gt;</c>. A bare colour reads as the
    /// colour itself everywhere it is referenced (<c>team="red"</c>, <c>&lt;team&gt;red&lt;/team&gt;</c> inside a
    /// filter), which is exactly what a colour attribute already says; the suffix makes an id name a team.
    /// Every reference the generators emit goes through here, and <see cref="Slug"/> strips the suffix again,
    /// so region and filter ids stay <c>red-spawn</c> / <c>only-red</c> whichever form the intent uses.</summary>
    public static string TeamId(string teamId) => $"{Slug(teamId)}-team";
}
