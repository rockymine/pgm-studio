using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Vocabulary;

/// <summary>Writes <see cref="Severity"/> as the lowercase word every caller reads it as, so the enum and the
/// wire spell it once.</summary>
public sealed class SeverityConverter() : JsonStringEnumConverter<Severity>(JsonNamingPolicy.CamelCase);

/// <summary>
/// What became of the thing a <see cref="Finding"/> was raised about, in descending order of how much it
/// took: the work did not happen, one piece of it did not, or all of it did and something is worth saying.
/// </summary>
[JsonConverter(typeof(SeverityConverter))]
public enum Severity
{
    /// <summary>The document is not built, stored or exported. What the author asked for cannot be made, and
    /// making something else instead would hand back a map they did not ask for.</summary>
    Refusal,

    /// <summary>The work happened and this one piece of what the author wrote is not in it. A tree, a
    /// boulder or a building the dressing pass could not seat is <b>not in the world</b> — the map built, and
    /// the thing they drew is gone from it. Not a refusal, because the map is there and is playable; not a
    /// complaint, because there is nothing to ignore: the input did not survive, and a caller reading a 2xx
    /// has no other way to learn that.</summary>
    Decline,

    /// <summary>A complaint the author may ignore. The work proceeds, everything they wrote is in it, and the
    /// finding rides along — which goal a map carries, a goal built in a material its size is wrong for, a
    /// goal topping out over the build ceiling: all real remarks, none of them the tool's to overrule.
    /// A finding that says something was <em>dropped</em> is a <see cref="Decline"/>, not this.</summary>
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
/// <param name="Severity">What became of what it is about: the work stopped, this piece of it was dropped,
/// or nothing was lost and there is a remark. Refusal unless stated.</param>
/// <param name="Field">The document field at fault, where one is nameable — <c>doorHead.block</c>, <c>wings</c>.</param>
/// <param name="Subjects">The ids the fault indicts, for an editor to highlight on click.</param>
/// <param name="Cites">What to look up next, where this finding's own id is not it: the layout rule the fault
/// falls under, or the <b>open task</b> that would resolve it. Kept apart from <paramref name="Rule"/> for the
/// reason task ids are kept apart from rule ids everywhere — a rule is stable forever and a task id is a debt
/// with a due date, and one field holding either would make the two indistinguishable to a reader.</param>
/// <param name="Edit">The change that would settle this finding, where the gate can state one in the
/// document's own vocabulary — a ramp mark for a seam that steps, a bench under a house on falling ground, a
/// prop moved out of a road's standoff. Absent where the fix is a decision rather than a mechanical
/// change. A reader applies it rather than re-deriving it from the sentence.</param>
public sealed record Finding(
    string Rule,
    string Message,
    Severity Severity = Severity.Refusal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Field = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Subjects = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Cites = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FindingEdit? Edit = null)
{
    /// <summary>The implicated ids, never null. A reader of the wire takes <see cref="Subjects"/>, which is
    /// absent rather than empty when the gate indicted nothing.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> SubjectIds => Subjects ?? [];

    /// <summary>Whether this one stops the work. A reader of the wire compares <see cref="Severity"/>, so
    /// there is one answer to the question rather than a second field that could disagree with it — and it
    /// stays the refusal test with three severities, since a decline is a success that took something
    /// away.</summary>
    [JsonIgnore]
    public bool Refuses => Severity == Severity.Refusal;

    /// <summary>A complaint rather than a refusal — the same finding, not stopping anything. A
    /// <see cref="Severity.Decline"/> is returned as it is: it already does not stop the work, and rewriting
    /// it as a complaint would lose the one thing it says, which is that the input did not survive.</summary>
    public Finding AsComplaint() =>
        Severity == Severity.Refusal ? this with { Severity = Severity.Complaint } : this;

    /// <summary>One sentence for a whole list, for the <c>message</c> beside the findings on the wire. A caller
    /// with nothing to say gets an empty string rather than a sentence about having nothing to say.</summary>
    public static string Summarize(IEnumerable<Finding> findings) =>
        string.Join("; ", findings.Select(finding => finding.Message));
}

/// <summary>
/// One mechanical change to one of the three documents, stated so a reader applies it rather than
/// re-deriving it from a rule's prose.
///
/// <para><b>Document</b> names which of the three the path is into: <c>plan</c>, <c>layout</c> or
/// <c>intent</c>. <b>Path</b> is the field the change lands on, spelled the way an unread field is
/// (<c>relief.team.marks</c>, <c>dressing.props[erratic-broken]</c>): members joined by dots, an array
/// element by its <c>id</c> in brackets where the element carries one and by its index otherwise. <b>Op</b>
/// is one of three: <c>add</c> appends <see cref="Value"/> to the array at the path; <c>set</c> replaces the
/// value at the path with it; <c>move</c> sets the <c>x</c> and <c>z</c> the value carries on the object at
/// the path. <b>Says</b> is the change in the author's terms, one sentence.</para>
/// </summary>
/// <param name="Document">Which document: <c>plan</c>, <c>layout</c> or <c>intent</c>.</param>
/// <param name="Path">Where in it the change lands.</param>
/// <param name="Op"><c>add</c>, <c>set</c> or <c>move</c>.</param>
/// <param name="Value">What is added, set or moved to, as the document would carry it.</param>
/// <param name="Says">The change in words.</param>
public sealed record FindingEdit(string Document, string Path, string Op, JsonElement Value, string Says)
{
    public const string Plan = "plan", Layout = "layout", Intent = "intent";
    public const string Add = "add", Set = "set", Move = "move";

    /// <summary>An edit whose value is built from an anonymous object or a dictionary, serialized the way
    /// the document states it.</summary>
    public static FindingEdit Of(string document, string path, string op, object value, string says) =>
        new(document, path, op, JsonSerializer.SerializeToElement(value), says);
}
