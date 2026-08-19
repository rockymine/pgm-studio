using PgmStudio.Contracts;
using PgmStudio.Domain;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// How every gate in the studio says no.
///
/// <para><b>One envelope, whichever gate refused.</b> A refusal carries the gate's short label, one line for a
/// caller that wants a sentence, and the findings themselves for one that wants to act — and a client reading
/// it never has to know which endpoint it came from before it can render it. Each gate had grown its own
/// shape, six of them across the plan, sketch, library and export endpoints, and a panel showing a refusal had
/// to branch on the route to find the fault in it.</para>
///
/// <para>The status code stays the gate's own: <b>400</b> for a document that is wrong as posted, <b>409</b>
/// for one that is well-formed but conflicts with the map's state, <b>422</b> for one that cannot be
/// processed. That is a fact about the request, not about the fault, so it is the caller's to name.</para>
/// </summary>
internal static class Refusals
{
    /// <summary>The wire form of one finding.</summary>
    public static FindingDto Dto(Finding finding) => new(
        finding.Rule, finding.Message, finding.Refuses ? "refusal" : "complaint",
        finding.Field, finding.SubjectIds.Count > 0 ? finding.SubjectIds : null, finding.Cites);

    /// <summary>A whole list, in order.</summary>
    public static IReadOnlyList<FindingDto> Dtos(IEnumerable<Finding> findings) => [.. findings.Select(Dto)];

    /// <summary>The refusal body: the gate's label, the findings, and their sentences joined into one line.
    ///
    /// <para>This is the same shape one level down as <see cref="Finding.Envelope"/> — the untyped
    /// <c>Dictionary&lt;string, object?&gt;</c> a composer below <c>Api</c> builds by hand because it cannot
    /// see <c>Contracts</c>. Same three keys in the same order (<c>error</c>, <c>message</c>, <c>findings</c>),
    /// rendered here as typed DTOs for the HTTP surface instead; the two must not drift apart.</para></summary>
    public static RefusalDto Of(string error, IEnumerable<Finding> findings)
    {
        var dtos = Dtos(findings);
        return new RefusalDto(error, Finding.Summarize(findings), dtos);
    }

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
    /// <para>Each of these sites used to write a bare <c>{error}</c> and drop the exception, so the caller was
    /// told <i>invalid room style JSON</i> and never which field — while the reader had gone to the trouble of
    /// saying that a part was stated as null and which one. The sentence is the half an author can act on, so
    /// it rides in <c>message</c> and in an <c>RQ1</c> finding like any other refusal.</para>
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
}

/// <summary>
/// The two rules the API boundary itself fires, as opposed to a gate reading a document it understood.
///
/// <para>Every other family in <c>docs/refusals.md</c> is owned by the gate that asks it. These two are owned
/// by the edge, because they are about the <b>request</b> rather than about the map: one document that could
/// not be read at all, and one fault that is the studio's own.</para>
/// </summary>
internal static class RequestRules
{
    /// <summary>A posted document could not be read — absent, empty, malformed, or naming a kind that does not
    /// exist. The caller's to fix, so it answers 400.</summary>
    /// <remarks>Post a body. If one was posted, the finding's <c>field</c> names where the reader stopped — usually a <c>kind</c> that is not one of the names, or a property stated as <c>null</c> where the record cannot hold one.</remarks>
    public const string Unreadable = "RQ1";

    /// <summary><b>Not the caller's fault.</b> Something escaped an endpoint that no gate refused, which is a
    /// defect in the studio; it answers 500 and stays a 500, because dressing a bug as a bad request sends the
    /// author looking for a mistake they did not make. What it buys is that the caller gets the one envelope
    /// every other refusal arrives in rather than a .NET stack trace, and the trace goes to the log where it
    /// belongs.</summary>
    /// <remarks>Nothing an author can do: this is a defect in the studio, and seeing one is a bug report rather than an authoring problem. The stack trace is in the server log.</remarks>
    public const string Unhandled = "RQ2";

    /// <summary><b>A complaint, never a refusal.</b> The document carried a property the reader had nowhere to
    /// keep — a typo, or a field from a document this is not. It rides on the success response under
    /// <c>warnings</c> because the work did succeed and the rest of the document was read; refusing it would
    /// refuse every snapshot written before the last shape change, since a stored document legitimately holds
    /// retired names an upgrade carries forward. See <see cref="Domain.DocumentShape"/>.</summary>
    /// <remarks>Check the spelling of the field the finding names against the document shape the tool's document names. The work succeeded without it, so whatever that field was meant to say was not said.</remarks>
    public const string Unread = "RQ3";
}
