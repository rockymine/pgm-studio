namespace PgmStudio.Domain;

/// <summary>Whether a <see cref="Finding"/> stops the thing it was asked about.</summary>
public enum Severity
{
    /// <summary>The document is not built, stored or exported. What the author asked for cannot be made, and
    /// making something else instead would hand back a map they did not ask for.</summary>
    Refusal,

    /// <summary>A complaint the author may ignore. The work proceeds and the finding rides along with it —
    /// which goal a map carries, whether a corner touch is meant, whether a lane is narrow: all real remarks,
    /// none of them the tool's to overrule.</summary>
    Complaint,
}

/// <summary>
/// One thing wrong with something an author wrote, in the one shape every gate in the studio answers in.
///
/// <para><b>A finding is a rule id, a sentence, and what it is about.</b> The id is the machine-legible half
/// and is stable forever — an agent or a canvas reads it back and acts on it, so it outlives the task that
/// added it and never doubles as a task-tracking id. The sentence is the human half and carries the measured
/// numbers, because "invalid" tells an author nothing they can fix. What it is about is a
/// <see cref="Field"/> where a document field is nameable and <see cref="Subjects"/> where the fault indicts
/// pieces, props or ids the editor can highlight; a finding may carry either, both or neither.</para>
///
/// <para><b>Why one type rather than one per gate.</b> Every gate had grown its own record — a plan finding, a
/// house-style finding, a producibility finding, a joint fault, three dictionaries built inline at the export
/// gate — and with them six wire shapes, so a client rendering a refusal had to know which gate it came from
/// before it could read the fault. The differences were never real: each was a rule, a sentence and a subject
/// under different field names. What is genuinely not a finding stays out — a <c>TermScore</c> is a distance
/// that <em>carries</em> one, and a distance is not a fault.</para>
/// </summary>
/// <param name="Rule">The stable id: <c>HS1</c>, <c>HJ2</c>, <c>PL7</c>, <c>OB20</c>, <c>WX4</c>, <c>DR-DOC</c>.
/// Every gate names one, so a refusal is never a sentence an author has to parse to act on.</param>
/// <param name="Message">What is wrong, in the terms the author wrote it in, with the numbers in it.</param>
/// <param name="Severity">Whether it stops the work. Refusal unless stated.</param>
/// <param name="Field">The document field at fault, where one is nameable — <c>doorHead.block</c>, <c>wings</c>.</param>
/// <param name="Subjects">The ids the fault indicts, for an editor to highlight on click.</param>
/// <param name="Cites">What to look up next, where this finding's own id is not it: the layout rule the fault
/// falls under, or the <b>open task</b> that would resolve it. Kept apart from <paramref name="Rule"/> for the
/// reason task ids are kept apart from rule ids everywhere — a rule is stable forever and a task id is a debt
/// with a due date, and one field holding either would make the two indistinguishable to a reader.</param>
public sealed record Finding(
    string Rule,
    string Message,
    Severity Severity = Severity.Refusal,
    string? Field = null,
    IReadOnlyList<string>? Subjects = null,
    string? Cites = null)
{
    /// <summary>The implicated ids, never null.</summary>
    public IReadOnlyList<string> SubjectIds => Subjects ?? [];

    /// <summary>Whether this one stops the work.</summary>
    public bool Refuses => Severity == Severity.Refusal;

    /// <summary>A complaint rather than a refusal — the same finding, not stopping anything.</summary>
    public Finding AsComplaint() => this with { Severity = Severity.Complaint };

    /// <summary>The keys this finding goes onto the wire under, for a caller composing an untyped response.
    /// Defined here rather than at each gate so the shape a client parses cannot differ by which gate refused;
    /// a caller serializing the record itself gets the same names from camelCase naming. <c>field</c> and
    /// <c>subjects</c> are written only when there is something to say, so a reader never has to tell an
    /// absent subject from an empty one.</summary>
    public Dictionary<string, object?> Wire()
    {
        var wire = new Dictionary<string, object?>
        {
            ["rule"] = Rule,
            ["message"] = Message,
            ["severity"] = Refuses ? "refusal" : "complaint",
        };
        if (Field is { } field) wire["field"] = field;
        if (Cites is { } cites) wire["cites"] = cites;
        if (SubjectIds.Count > 0) wire["subjects"] = SubjectIds;
        return wire;
    }

    /// <summary>One sentence for a whole list, for the <c>message</c> beside the findings on the wire. A caller
    /// with nothing to say gets an empty string rather than a sentence about having nothing to say.</summary>
    public static string Summarize(IEnumerable<Finding> findings) =>
        string.Join("; ", findings.Select(finding => finding.Message));

    /// <summary>
    /// The refusal envelope, for a composer below the API layer that cannot see <c>Contracts</c> and so
    /// cannot render <c>RefusalDto</c> — the gate's short label, the findings' wire form, and their sentences
    /// joined into the same <c>message</c> a typed caller gets. Three keys, in this order: <c>error</c>,
    /// <c>message</c>, <c>findings</c>.
    ///
    /// <para>This is the one wire shape a refusal takes below <c>Api</c>. <c>Api.Endpoints.Refusals.Of</c>
    /// renders the identical shape one layer up, as typed DTOs for the HTTP surface, and the two must not
    /// drift apart — a client reading a refusal should never be able to tell which of the two produced it.</para>
    /// </summary>
    public static Dictionary<string, object?> Envelope(string error, IEnumerable<Finding> findings)
    {
        var findingList = findings as IReadOnlyList<Finding> ?? findings.ToList();
        return new Dictionary<string, object?>
        {
            ["error"] = error,
            ["message"] = Summarize(findingList),
            ["findings"] = findingList.Select(finding => finding.Wire()).ToList(),
        };
    }
}
