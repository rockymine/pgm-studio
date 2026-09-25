using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// Whether a posted document binds onto the record it is stored as, asked before it is stored.
///
/// <para>A reader that gives up on one property gives up on the whole document, so a lenient read of a body
/// that states one field in the wrong shape answers a default instance — an intent with no teams, no spawns and
/// no objectives, which every reader downstream agrees is a valid empty one. The refusal belongs where the
/// binder gives up, naming the path it gave up at (<c>RQ1</c>).</para>
/// </summary>
public static class DocumentBinding
{
    /// <summary>The <c>RQ1</c> finding for a document <paramref name="read"/> cannot bind, its field the JSON
    /// path the binder stopped at under <paramref name="member"/> — or null when it binds. A blank document
    /// binds as nothing and is not this question's.</summary>
    public static Finding? Unbindable(string member, string json, Action<string> read)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            read(json);
            return null;
        }
        catch (Exception fault) when (fault is JsonException or NotSupportedException)
        {
            var path = fault is DocumentFault document ? document.Field : Relative((fault as JsonException)?.Path);
            var field = path.Length == 0 ? member : member.Length == 0 ? path : $"{member}.{path}";
            var where = field.Length == 0 ? "the document" : $"`{field}`";
            return new Finding(RequestRules.Unreadable,
                $"{where} will not read: {Sentence(fault.Message)} — nothing was stored", Field: field.Length == 0 ? null : field);
        }
    }

    /// <summary>A binder's JSON path as the document's own: <c>$.modes[0]</c> is <c>modes[0]</c>.</summary>
    private static string Relative(string? path) =>
        string.IsNullOrEmpty(path) || path == "$" ? "" : path.TrimStart('$').TrimStart('.');

    /// <summary>The binder's sentence without the position suffix it appends, which the field already
    /// states.</summary>
    private static string Sentence(string message)
    {
        var at = message.IndexOf(" Path:", StringComparison.Ordinal);
        return (at < 0 ? message : message[..at]).TrimEnd('.', ' ');
    }
}
