using System.Text.Json;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>
/// What a success carried besides its answer — the mirror of <see cref="ServerRefusal"/>, on the other side
/// of the status line.
///
/// <para>Every 2xx JSON object under <c>/api</c> answers <c>warnings</c> when a gate remarked on something
/// and carries no such key when none did: a complaint the author may ignore, or a decline saying one piece
/// of what they wrote is not in what was built. It rides on the answer rather than in it, written by
/// middleware, so no response record has a field for it — which is why reading it is one place's job rather
/// than each caller's, exactly as picking a refusal's sentence is.</para>
///
/// <para><see cref="Carried"/> is the cheap half: the header restates the count and the rule ids, so a
/// caller deciding whether to look need not parse a body that may be megabytes. <see cref="AnsweredAsync"/>
/// is the whole read, and takes the body <b>once</b> — a response's content is a stream, so the answer and
/// the key beside it cannot be read in two passes.</para>
/// </summary>
public static class ServerWarnings
{
    /// <summary>The header the server restates them in, and the key they ride under.</summary>
    private const string Header = "Pgm-Warnings";
    private const string Key = "warnings";

    /// <summary>The options the wire is written with, so a finding reads back the way it was sent.</summary>
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <summary>The rule ids the header names, or empty where the answer carried none. Reads no body, which
    /// is the point: an answer that is megabytes says whether it is worth opening in its headers.</summary>
    public static IReadOnlyList<string> Carried(HttpResponseMessage response) =>
        response.Headers.TryGetValues(Header, out var values) && values.FirstOrDefault() is { } restated
            ? [.. restated.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)]
            : [];

    /// <summary>The answer and what rode on it, from one read of the body. The answer is default where the
    /// body is not the shape asked for; the warnings are empty where none were carried, which is the
    /// ordinary case and not a failure.</summary>
    public static async Task<(T? Answer, IReadOnlyList<Finding> Warnings)> AnsweredAsync<T>(
        HttpResponseMessage response)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); }
        catch (JsonException) { return (default, []); }

        using (document)
        {
            var root = document.RootElement;
            // The answer is read off the same root the key sits on: `warnings` is a member of the success
            // rather than a wrapper around it, so a record that has no field for it simply skips it.
            var answer = Read<T>(root);
            var warnings = root.ValueKind == JsonValueKind.Object && root.TryGetProperty(Key, out var carried)
                ? Read<List<Finding>>(carried) ?? []
                : [];
            return (answer, warnings);
        }
    }

    private static T? Read<T>(JsonElement element)
    {
        try { return element.Deserialize<T>(Wire); }
        catch (JsonException) { return default; }
    }
}
