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
        return new RefusalDto(error, string.Join("; ", dtos.Select(finding => finding.Message)), dtos);
    }

    /// <summary>Write the refusal directly to the response, for an endpoint whose success body is a different
    /// type — <c>Send.ResponseAsync</c> is typed to the endpoint's own response and cannot carry this.</summary>
    public static Task WriteAsync(
        HttpContext http, int status, string error, IEnumerable<Finding> findings, CancellationToken ct)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(Of(error, findings), ct);
    }
}
