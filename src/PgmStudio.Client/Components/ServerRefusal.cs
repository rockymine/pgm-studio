using System.Net.Http.Json;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

/// <summary>
/// A refusal the server answered, as the one sentence a toast shows.
///
/// <para>Every route under <c>/api</c> says no in one shape — <c>{error, message, findings[]}</c> — and the
/// two halves are not interchangeable: <c>error</c> is the gate's short label (<i>no such subject</i>,
/// <i>conflicting edit</i>), and <c>message</c> is the findings' own sentences, which is the half naming the
/// region, the id or the field an author has to act on. A toast wants the sentence, so this is the one place
/// that picks it, rather than each phase deciding.</para>
/// </summary>
public static class ServerRefusal
{
    /// <summary>The sentence for an unsuccessful response: the findings' own, the gate's label where a route
    /// answered no findings, and the status where the body is not a refusal at all.</summary>
    public static async Task<string> SentenceAsync(HttpResponseMessage response)
    {
        var fallback = $"error {(int)response.StatusCode}";
        try
        {
            var refusal = await response.Content.ReadFromJsonAsync<RefusalDto>();
            if (refusal is null) return fallback;
            if (!string.IsNullOrWhiteSpace(refusal.Message)) return refusal.Message;
            return string.IsNullOrWhiteSpace(refusal.Error) ? fallback : refusal.Error;
        }
        catch
        {
            return fallback;
        }
    }
}
