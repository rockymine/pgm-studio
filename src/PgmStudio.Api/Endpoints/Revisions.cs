using Microsoft.Net.Http.Headers;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Which version of a document a caller read, and what a write does about it.
///
/// <para><b>The studio stores by replacing.</b> An edit reads the whole map document, patches it and writes
/// the whole document back; an artifact is one row a save replaces. Two callers doing that at once keep only
/// the second, and nothing in either request or either response notices — no conflict, no finding, no trace.
/// One person in one browser tab never meets it; an agent driving the API while a tab is open on the same
/// slug meets it on the first collision.</para>
///
/// <para><b>A read answers the revision, a write may state one.</b> The revision rides as an <c>ETag</c> and
/// comes back as an <c>If-Match</c>, which is what those two headers are for. A request with no
/// <c>If-Match</c> states no precondition and writes, exactly as it did before there was a revision to state
/// — protection a caller opts into by having read first, which is the only way it can mean anything.</para>
///
/// <para><b>A stale write is 409 and not 412.</b> <c>412 Precondition Failed</c> is the header's own status,
/// and this answers <c>RQ5</c> at 409 instead, because the studio has one refusal envelope and <c>RQ5</c> is
/// already the rule for a request that conflicts with what is stored (<c>docs/refusals.md</c>). A caller
/// writing one parser for the studio is the whole point of there being one shape, and a second status with a
/// different body for the same class of fault costs exactly that.</para>
/// </summary>
internal static class Revisions
{
    /// <summary>The revision an <c>If-Match</c> names, or null where the request states no precondition.
    /// A header that is not a revision at all names none either — it cannot match, and the write it guards is
    /// refused by the same <c>RQ5</c> a stale one is rather than passing as unguarded.</summary>
    public static long? Expected(HttpContext http)
    {
        var stated = http.Request.Headers.IfMatch.ToString();
        if (stated.Length == 0) return null;
        return long.TryParse(stated.Trim('"', 'W', '/', ' '), out var revision) ? revision : Unmatchable;
    }

    /// <summary>Whether the request guards its write at all.</summary>
    public static bool Guarded(HttpContext http) => http.Request.Headers.IfMatch.Count > 0;

    /// <summary>A revision no document is ever at, so a malformed <c>If-Match</c> fails its match rather than
    /// being read as an absent one.</summary>
    private const long Unmatchable = -1;

    /// <summary>Answer the revision the document is at, so a caller can hold it and guard its next write.</summary>
    public static void Answer(HttpContext http, long revision) =>
        http.Response.Headers[HeaderNames.ETag] = $"\"{revision}\"";

    /// <summary>The status a stale write answers. <c>RQ5</c>'s own, for the reason above.</summary>
    public const int StaleStatus = 409;

    /// <summary>The refusal for a write against a revision the document no longer has: what was expected,
    /// what is stored, and what to do about it — read it again and re-apply, because the studio has no way to
    /// merge two whole-document writes and guessing at one would lose whichever half it guessed against.
    /// <para>A value rather than a written response, so a route that answers its own body and one that hands
    /// a body back for a caller to send can both use it — the same split <see cref="Refusals.Of"/> has.</para>
    /// </summary>
    public static Contracts.RefusalDto Stale(HttpContext http, string what, long? stored) =>
        Refusals.Of("stale write",
            [new Finding(RequestRules.Conflict,
                stored is { } now
                    ? $"this {what} has been replaced since it was read — the If-Match states "
                      + $"{Expected(http)} and it is at {now}; read it again and re-apply the change"
                    : $"this map holds no {what} to replace, so the If-Match matches nothing")]);

    /// <summary>The same refusal, written to the response.</summary>
    public static Task StaleAsync(HttpContext http, string what, long? stored, CancellationToken ct)
    {
        http.Response.StatusCode = StaleStatus;
        return http.Response.WriteAsJsonAsync(Stale(http, what, stored), ct);
    }
}
