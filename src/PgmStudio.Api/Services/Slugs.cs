using System.Text.RegularExpressions;

namespace PgmStudio.Api.Services;

/// <summary>What a map is called in a URL. One derivation, because a name that slugified two ways would name
/// two maps.</summary>
public static class Slugs
{
    /// <summary>Lowercase, every run of anything else collapsed to a hyphen, and never empty — a name made
    /// entirely of punctuation still has to be reachable.</summary>
    public static string Of(string name)
    {
        var slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 0 ? slug : "map";
    }
}
