using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>What a finding is to the author reading a check list, worst first: it stops the work, a measured
/// value is outside its usual range, something drawn is not in what was built, or a remark to weigh.</summary>
public enum ProblemKind { Problem, OutOfRange, LeftOut, Warning }

/// <summary>
/// One finding as a check list shows it: its kind to the author, the finding, the caller's <see cref="Index"/>
/// to light it by, and — for an evaluator's term — the term that measured it.
/// </summary>
public sealed record Problem(ProblemKind Kind, Finding Finding, int Index, string? Term = null)
{
    /// <summary>A finding by its severity: a refusal is a problem, a decline is left out, a finding that
    /// measured a number is out of range, any other complaint a warning.</summary>
    public static Problem Of(Finding finding, int index, string? term = null) => new(finding switch
    {
        { Severity: Severity.Refusal } => ProblemKind.Problem,
        { Severity: Severity.Decline } => ProblemKind.LeftOut,
        { Measured: not null } => ProblemKind.OutOfRange,
        _ => ProblemKind.Warning,
    }, finding, index, term);

    /// <summary>A plan evaluation's violation: its finding as any other, kept beside the term that measured it.</summary>
    public static Problem Of(ViolationDto violation, int index) => Of(violation.Finding, index, violation.TermId);

    /// <summary>Several findings by severity, indexed from <paramref name="first"/> in order.</summary>
    public static List<Problem> Of(IEnumerable<Finding> findings, int first = 0) =>
        findings.Select((finding, offset) => Of(finding, first + offset)).ToList();
}
