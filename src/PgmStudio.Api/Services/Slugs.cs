using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PgmStudio.Api.Services;

/// <summary>
/// What a map is called in a URL.
///
/// <para>A map the studio makes with no slug stated takes <see cref="Fresh"/>, a random id, rather than one
/// from its name: a new map is named "Untitled sketch" until its author names it, and a slug never follows a
/// rename, so a name-made slug would leave every new map at <c>untitled-sketch-n</c> for good.</para>
///
/// <para><b>Two derivations, and the difference is deliberate.</b> A name an author typed becomes a slug one
/// way and a folder on disk becomes one another way, because the two are different things: an author's name
/// is prose and its punctuation means nothing, while a world folder is a filename an author chose and an
/// underscore in it is a character rather than a separator. They sit together so the difference is visible
/// rather than discovered — a name that slugified two ways depending on which door it came through would
/// name two maps.</para>
/// </summary>
public static class Slugs
{
    /// <summary>From a name an author typed. Lowercase, every run of anything else collapsed to a hyphen,
    /// and never empty — a name made entirely of punctuation still has to be reachable.</summary>
    public static string Of(string name)
    {
        var slug = NotAName.Replace(name.ToLowerInvariant(), "-").Trim('-');
        return slug.Length > 0 ? slug : "map";
    }

    /// <summary>From a world folder on disk. An underscore survives, because it is part of the name whoever
    /// built the world gave it; the length is capped because the slug becomes a directory the studio writes
    /// under, and a filesystem has an opinion about that which a map name does not.</summary>
    public static string OfFolder(string folder)
    {
        var slug = NotAFolder.Replace(folder.Trim().ToLowerInvariant(), "-").Trim('-', '_');
        return slug.Length > 64 ? slug[..64] : slug;
    }

    /// <summary>How many characters a <see cref="Fresh"/> id has.</summary>
    public const int FreshLength = 16;

    private const string FreshAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>A random id of <see cref="FreshLength"/> lowercase letters and digits, for a map made with no
    /// slug stated.</summary>
    public static string Fresh() =>
        new(Enumerable.Range(0, FreshLength).Select(_ => FreshAlphabet[RandomNumberGenerator.GetInt32(FreshAlphabet.Length)]).ToArray());

    private static readonly Regex NotAName = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex NotAFolder = new("[^a-z0-9_]+", RegexOptions.Compiled);
}
