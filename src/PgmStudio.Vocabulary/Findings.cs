using System.Collections;

namespace PgmStudio.Vocabulary;

/// <summary>
/// What a gate answered: every <see cref="Finding"/> it had, and the questions a caller actually asks of them.
///
/// <para><b>A list of findings does not say whether anything was refused, and every caller was working it out
/// again.</b> Some gates report only refusals, so an empty list means clean and a non-empty one means no; others
/// mix refusals with complaints, where a non-empty list may be a perfectly good document carrying a remark. Both
/// were being read with <c>Count &gt; 0</c>, which is right for the first kind and silently wrong for the second
/// — a lint would have blocked a compile. <see cref="Refuses"/> is the question, asked once here.</para>
///
/// <para>Gates return this, always: never null, never a bare list, and never by throwing. The one exception is a
/// document that will not parse, which cannot carry on to collect a second fault and so throws — and the
/// exception carries its <see cref="Finding"/> so the gate above it answers in this shape anyway.</para>
/// </summary>
public sealed class Findings : IReadOnlyList<Finding>
{
    private readonly Finding[] findings;

    /// <summary>Nothing wrong. What a gate returns when it has nothing to say, and what a caller compares
    /// against rather than counting.</summary>
    public static readonly Findings None = new([]);

    public Findings(IEnumerable<Finding> findings) => this.findings = [.. findings];

    /// <summary>The findings a gate names outright, in the order it named them.</summary>
    public static Findings Of(params Finding[] findings) => findings.Length == 0 ? None : new(findings);

    /// <summary>A gate's own list, once it has finished filling it.</summary>
    public static implicit operator Findings(List<Finding> findings) => new(findings);

    /// <summary><b>Whether anything here stops the work.</b> Not <see cref="Count"/>: a gate that reports
    /// complaints as well as refusals answers a non-empty list for a document that is perfectly good.</summary>
    public bool Refuses => findings.Any(finding => finding.Refuses);

    /// <summary>The findings that stop the work.</summary>
    public IEnumerable<Finding> Refusals => findings.Where(finding => finding.Refuses);

    /// <summary>The remarks: findings that stop nothing and took nothing away. A
    /// <see cref="Severity.Decline"/> rides along on the same response and is deliberately not here — it says
    /// a piece of what the author wrote is gone, which is not a remark.</summary>
    public IEnumerable<Finding> Complaints =>
        findings.Where(finding => finding.Severity == Severity.Complaint);

    /// <summary>One sentence for the whole list, for the <c>message</c> beside the findings on the wire.</summary>
    public string Summary => Finding.Summarize(findings);

    /// <summary>The same findings, none of them stopping the work — how a derivation that answers a question
    /// nobody's compile depends on hands its reasons to a caller. A producibility read is the case: inside it a
    /// finding is why the answer is no, and that is the severity <see cref="Refuses"/> must see; on the wire it
    /// rides along with a 200, because a plan the composer could not have made is still a plan that exports.
    /// Downgrading at the boundary keeps both true, where writing every finding as a complaint at its source
    /// left the derivation with no way to ask its own question except by counting.</summary>
    public Findings AsComplaints() =>
        Count == 0 ? this : new(findings.Select(finding => finding.AsComplaint()));

    /// <summary>Both gates' answers as one, for a caller asking two questions of one document.</summary>
    public Findings And(Findings other) =>
        Count == 0 ? other : other.Count == 0 ? this : new([.. findings, .. other.findings]);

    /// <summary>The same findings with a root prefixed onto every <see cref="Finding.Field"/> — how a gate run
    /// over a document's sub-object reports a field an author can actually find, since <c>doorHead.block</c> on
    /// its own does not say which of two bound styles held it.</summary>
    public Findings Under(string root) =>
        Count == 0 ? this : new(findings.Select(finding => finding with
        {
            Field = finding.Field is { } field ? $"{root}.{field}" : root,
        }));

    public int Count => findings.Length;

    public Finding this[int index] => findings[index];

    public IEnumerator<Finding> GetEnumerator() => ((IEnumerable<Finding>)findings).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => findings.GetEnumerator();
}
