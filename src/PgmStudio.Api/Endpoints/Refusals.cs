using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// How every gate in the studio says no.
///
/// <para><b>One envelope, whichever gate refused.</b> A refusal carries the gate's short label, one line for a
/// caller that wants a sentence, and the findings themselves for one that wants to act — so a client rendering
/// one never has to know which endpoint it came from, and a panel reads the fault without branching on the
/// route.</para>
///
/// <para>The status code stays the gate's own: <b>400</b> for a document that is wrong as posted, <b>409</b>
/// for one that is well-formed but conflicts with the map's state, <b>422</b> for one that cannot be
/// processed. That is a fact about the request, not about the fault, so it is the caller's to name.</para>
/// </summary>
internal static class Refusals
{
    /// <summary>The refusal body: the gate's label, the findings, and their sentences joined into one line.
    /// The findings cross as the <see cref="Finding"/> records the gate raised, whichever layer raised
    /// them.</summary>
    public static RefusalDto Of(string error, IEnumerable<Finding> findings)
    {
        var listed = findings as IReadOnlyList<Finding> ?? [.. findings];
        return new RefusalDto(error, Finding.Summarize(listed), listed);
    }

    /// <summary>Answer the refusal an operation handed back. The operation decided the status and named the
    /// gate; this is only the rendering, which is the whole division between the two layers.</summary>
    public static Task WriteAsync(HttpContext http, Refusal refusal, CancellationToken ct) =>
        WriteAsync(http, refusal.Status, refusal.Error, refusal.Findings, ct);

    /// <summary>Write the refusal directly to the response, for an endpoint whose success body is a different
    /// type — <c>Send.ResponseAsync</c> is typed to the endpoint's own response and cannot carry this.</summary>
    public static Task WriteAsync(
        HttpContext http, int status, string error, IEnumerable<Finding> findings, CancellationToken ct)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(Of(error, findings), ct);
    }

    /// <summary>
    /// A document that would not parse, answered the way every other refusal is.
    ///
    /// <para>The reader's own sentence is the half an author can act on — <i>a part is stated as null, and
    /// which one</i> rather than <i>invalid room style JSON</i> — so it rides in <c>message</c> and in an
    /// <c>RQ1</c> finding like any other refusal, instead of being dropped with the exception.</para>
    /// </summary>
    public static Task UnreadableAsync(
        HttpContext http, string error, Exception fault, CancellationToken ct) =>
        WriteAsync(http, 400, error,
            [new Finding(RequestRules.Unreadable, fault.Message,
                Field: (fault as DocumentFault)?.Field)], ct);

    /// <summary>
    /// The same refusal where the endpoint has a sentence rather than an exception: a body that is not the
    /// document the route takes, a required parameter absent, a value outside a closed set. All of them are
    /// <c>RQ1</c> — the request itself could not be acted on — and all of them answer <b>400</b>, because the
    /// caller is the one who can fix it.
    /// <para>The sentence is the half that makes it actionable, so it says what was expected rather than that
    /// something was wrong; <paramref name="field"/> names the parameter where one is nameable, which is what
    /// lets a client put the message beside the input rather than at the top of the page.</para>
    /// </summary>
    public static Task UnreadableAsync(
        HttpContext http, string error, string message, CancellationToken ct, string? field = null) =>
        WriteAsync(http, 400, error, [new Finding(RequestRules.Unreadable, message, Field: field)], ct);

    /// <summary>
    /// The route's subject does not exist: 404, in the envelope, naming what was looked for and the
    /// identifier it was looked for under.
    ///
    /// <para>The body is what makes the two kinds of 404 tellable apart. <c>PUT /map/typo/sketch</c> and
    /// <c>PUT /map/voidwatch/skecth</c> are the same status, and only one of them is a slug the caller can
    /// correct; an empty answer leaves them to guess which they are looking at.</para>
    /// </summary>
    public static Task NotFoundAsync(
        HttpContext http, string what, CancellationToken ct, string? named = null)
    {
        named ??= http.Request.RouteValues.TryGetValue("slug", out var slug) ? slug?.ToString()
                : http.Request.RouteValues.TryGetValue("id", out var id) ? id?.ToString()
                : null;
        return WriteAsync(http, 404, $"no such {what}",
            [new Finding(RequestRules.NoSuchSubject,
                named is { Length: > 0 }
                    ? $"no {what} is stored under '{named}'"
                    : $"this route has no {what} to answer for")], ct);
    }

    /// <summary>The request conflicts with what is stored — a name already taken, a row something still
    /// binds. 409, with the things in the way as the finding's subjects so a caller can act on them.</summary>
    public static Task ConflictAsync(
        HttpContext http, string error, string message, CancellationToken ct, IReadOnlyList<string>? holding = null) =>
        WriteAsync(http, 409, error,
            [new Finding(RequestRules.Conflict, message, Subjects: holding)], ct);

    /// <summary>A document the studio stored will not read back. 422, because it is data rather than a defect
    /// and writing the document again clears it.</summary>
    public static Task StoredUnreadableAsync(HttpContext http, string what, CancellationToken ct) =>
        WriteAsync(http, 422, $"stored {what} is unreadable",
            [new Finding(RequestRules.StoredUnreadable,
                $"the {what} this map has stored will not read back — it was written under a shape no reader "
                + "understands, so save it again from the tool that writes it")], ct);

    /// <summary>The whole gate in one line: <c>if (await Refusals.StopAsync(…)) return;</c>. True when the
    /// findings refuse and the response has been written; false when there was nothing to stop for, complaints
    /// included.
    ///
    /// <para>An endpoint asking <c>findings.Count &gt; 0</c> instead is asking the wrong question, and it is
    /// right only by accident of which gate it happens to be calling: a gate that reports complaints as well as
    /// refusals answers a non-empty list for a perfectly good document, and the endpoint would refuse it. Only
    /// the refusals are written, so a complaint never arrives dressed as one.</para>
    ///
    /// <para><b>The complaints are not the caller's to keep or drop.</b> They are handed to
    /// <see cref="Complaints"/> here, and the pipeline puts them on whatever success the endpoint goes on to
    /// answer — so running the gate is the whole of an endpoint's duty, and one that forgets to think about
    /// complaints reports them anyway.</para></summary>
    public static async Task<bool> StopAsync(
        HttpContext http, int status, string error, Findings findings, CancellationToken ct)
    {
        if (findings.Refuses)
        {
            await WriteAsync(http, status, error, findings.Refusals, ct);
            return true;
        }
        Complaints.Add(http, findings.Complaints);
        return false;
    }

    /// <summary>
    /// The binder's refusals, in the envelope every other refusal already uses. A bound request DTO is
    /// refused before any handler runs — a field the JSON cannot carry into its type, a body that is not an
    /// object — and the framework's default answers <c>{statusCode, message, errors}</c>, a second shape a
    /// caller would need a second parser for.
    ///
    /// <para>Each failure becomes one <c>RQ1</c> finding naming its field, which is what
    /// <see cref="RequiredFields"/> already answers for a field that is missing rather than unreadable: the
    /// two halves of "this body will not read" say the same thing the same way. A failure the binder raises
    /// against no field in particular carries none.</para>
    /// </summary>
    public static void UseRefusalEnvelope(this ErrorOptions errors)
    {
        errors.ProducesMetadataType = typeof(RefusalDto);
        errors.ResponseBuilder = (failures, _, _) => Of("request will not read",
            failures.Select(failure => new Finding(
                RequestRules.Unreadable, failure.ErrorMessage,
                Field: string.IsNullOrWhiteSpace(failure.PropertyName) ? null : Camel(failure.PropertyName))));
    }

    /// <summary>The binder names a property as the DTO declares it; the wire carries it as the serializer
    /// writes it, and a finding's field is the caller's spelling.</summary>
    private static string Camel(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
