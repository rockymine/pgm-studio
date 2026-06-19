using System.Text.RegularExpressions;

namespace PgmStudio.Pgm.Authoring;

/// <summary>Shared naming for the declarative generators: the stable id slug derived from a team id
/// (e.g. <c>red-team</c> → <c>red</c>), used for region/filter ids (<c>only-red</c>, <c>red-spawn</c>,
/// <c>red-wool</c>). Slug comes from the id, never the raw colour (which may be multi-word).</summary>
internal static class IntentNaming
{
    public static string Slug(string teamId)
    {
        var s = teamId.Trim().ToLowerInvariant();
        if (s.EndsWith("-team")) s = s[..^5];
        s = Regex.Replace(s, "[^a-z0-9]+", "-").Trim('-');
        return s.Length > 0 ? s : teamId;
    }
}
