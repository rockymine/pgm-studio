using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Vocabulary;

/// <summary>Writes <see cref="Severity"/> as the lowercase word every caller reads it as, so the enum and the
/// wire spell it once.</summary>
public sealed class SeverityConverter() : JsonStringEnumConverter<Severity>(JsonNamingPolicy.CamelCase);

/// <summary>Whether a <see cref="Finding"/> stops the thing it was asked about.</summary>
[JsonConverter(typeof(SeverityConverter))]
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
/// One thing wrong with something an author wrote, in the one shape every gate in the studio answers in —
/// and the one shape it crosses the API in, because this record is what is serialized.
///
/// <para><b>A finding is a rule id, a sentence, and what it is about.</b> The id is the machine-legible half
/// and is stable forever — an agent or a canvas reads it back and acts on it, so it outlives the task that
/// added it and never doubles as a task-tracking id. The sentence is the human half and carries the measured
/// numbers, because "invalid" tells an author nothing they can fix. What it is about is a
/// <see cref="Field"/> where a document field is nameable and <see cref="Subjects"/> where the fault indicts
/// pieces, props or ids the editor can highlight; a finding may carry either, both or neither.</para>
///
/// <para><b>Why one type rather than one per gate.</b> Each gate's own record — a plan finding, a house-style
/// finding, a producibility finding, a joint fault — differed only in field names, so a client rendering a
/// refusal had to know which gate it came from before it could read the fault. What is genuinely not a
/// finding stays out: a <c>TermScore</c> is a distance that <em>carries</em> one, and a distance is not a
/// fault.</para>
///
/// <para><b>Why it lives in a leaf that references nothing.</b> Three parties have to spell a finding
/// identically — the gates below <c>Api</c> that raise one, the HTTP surface that answers it, and the WASM
/// client that renders it — and no project above this one is reachable from all three. The three optional
/// fields are written only when they have a value, and the two computed properties are not written at all,
/// so what a caller reads does not depend on which layer serialized it.</para>
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Field = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Subjects = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Cites = null)
{
    /// <summary>The implicated ids, never null. A reader of the wire takes <see cref="Subjects"/>, which is
    /// absent rather than empty when the gate indicted nothing.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> SubjectIds => Subjects ?? [];

    /// <summary>Whether this one stops the work. A reader of the wire compares <see cref="Severity"/>, so
    /// there is one answer to the question rather than a second field that could disagree with it.</summary>
    [JsonIgnore]
    public bool Refuses => Severity == Severity.Refusal;

    /// <summary>A complaint rather than a refusal — the same finding, not stopping anything.</summary>
    public Finding AsComplaint() => this with { Severity = Severity.Complaint };

    /// <summary>One sentence for a whole list, for the <c>message</c> beside the findings on the wire. A caller
    /// with nothing to say gets an empty string rather than a sentence about having nothing to say.</summary>
    public static string Summarize(IEnumerable<Finding> findings) =>
        string.Join("; ", findings.Select(finding => finding.Message));
}
