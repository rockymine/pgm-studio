using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>What a finding is to the author reading a check list, worst first: it stops the work, a measured
/// value is outside its usual range, something drawn is not in what was built, or a remark to weigh.</summary>
public enum ProblemKind { Problem, OutOfRange, LeftOut, Warning }

/// <summary>
/// One finding as a check list shows it: its kind to the author, the finding, the caller's <see cref="Index"/>
/// to light it by, and — for a term measured out of range — the term, its value and the band it was held to.
/// </summary>
public sealed record Problem(
    ProblemKind Kind, Finding Finding, int Index,
    string? Term = null, double? Value = null, double[]? Band = null)
{
    /// <summary>A finding by its severity: a refusal is a problem, a decline is left out, a complaint a warning.</summary>
    public static Problem Of(Finding finding, int index) => new(finding.Severity switch
    {
        Severity.Refusal => ProblemKind.Problem,
        Severity.Decline => ProblemKind.LeftOut,
        _ => ProblemKind.Warning,
    }, finding, index);

    /// <summary>A plan evaluation's violation by its kind: a hard term is a problem, a soft one out of range,
    /// carrying the value it measured and its band.</summary>
    public static Problem Of(ViolationDto violation, int index) => violation.Kind == "soft"
        ? new(ProblemKind.OutOfRange, violation.Finding, index, violation.TermId, violation.Value, violation.Band)
        : new(ProblemKind.Problem, violation.Finding, index);

    /// <summary>Several findings by severity, indexed from <paramref name="first"/> in order.</summary>
    public static List<Problem> Of(IEnumerable<Finding> findings, int first = 0) =>
        findings.Select((finding, offset) => Of(finding, first + offset)).ToList();
}
