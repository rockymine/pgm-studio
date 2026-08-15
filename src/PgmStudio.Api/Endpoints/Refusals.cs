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

    /// <summary>The refusal body: the gate's label, the findings, and their sentences joined into one line.</summary>
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

    /// <summary>The whole gate in one line: <c>if (await Refusals.StopAsync(…)) return;</c>. True when the
    /// findings refuse and the response has been written; false when there was nothing to stop for, complaints
    /// included.
    ///
    /// <para>An endpoint asking <c>findings.Count &gt; 0</c> instead is asking the wrong question, and it is
    /// right only by accident of which gate it happens to be calling: a gate that reports complaints as well as
    /// refusals answers a non-empty list for a perfectly good document, and the endpoint would refuse it. Only
    /// the refusals are written, so a complaint never arrives dressed as one.</para></summary>
    public static async Task<bool> StopAsync(
        HttpContext http, int status, string error, Findings findings, CancellationToken ct)
    {
        if (!findings.Refuses) return false;
        await WriteAsync(http, status, error, findings.Refusals, ct);
        return true;
    }
}
