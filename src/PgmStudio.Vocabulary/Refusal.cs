namespace PgmStudio.Vocabulary;

/// <summary>
/// A gate's no: the status it is answered under, the gate's short label, and the findings themselves.
///
/// <para><b>An operation returns this rather than writing a response.</b> The gates run below the HTTP
/// surface — some of them below <c>Api</c> altogether — and the layer that speaks HTTP is the one that
/// renders the envelope <c>docs/refusals.md</c> describes, so a caller cannot tell how deep a refusal was
/// raised. What crosses between them is this, which is why it lives in the leaf every one of them reaches.
/// </para>
///
/// <para><b>The status belongs here and not to the route.</b> Which of 400, 404, 409 and 422 a refusal is
/// answered under is decided by what went wrong — a document wrong as posted, a subject that is not stored,
/// a conflict with the map's state, something that cannot be processed — and the route that happens to have
/// asked has no better view of that than the gate did.</para>
///
/// <para>An operation carries one of these <b>beside</b> what it produces rather than declaring the three
/// fields again: a result is its own payload plus, where the work did not happen, this.</para>
/// </summary>
/// <param name="Status">The HTTP status the refusal is answered under.</param>
/// <param name="Error">The gate's short label — <c>objective placement</c>, <c>unknown gamemode</c> — never
/// the fault itself, which is what the findings are for.</param>
/// <param name="Findings">Every finding that refused, in the order the gate raised them.</param>
public sealed record Refusal(int Status, string Error, IReadOnlyList<Finding> Findings)
{
    /// <summary>The refusal a gate raises, in the one form every operation hands back.</summary>
    public static Refusal At(int status, string error, params Finding[] findings) => new(status, error, findings);

    /// <summary>The findings' sentences joined, for a caller that wants one line before it looks at any of
    /// them.</summary>
    public string Message => Finding.Summarize(Findings);
}
