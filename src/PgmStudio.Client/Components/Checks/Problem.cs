using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>What a finding is to the author reading a check list, worst first: it stops the work, a measured
/// value is outside its usual range, something drawn is not in what was built, or a remark to weigh.</summary>
public enum ProblemKind { Problem, OutOfRange, LeftOut, Warning }

/// <summary>
/// One finding as a check list shows it: its kind to the author, the finding, and the caller's
/// <see cref="Index"/> to light it by.
/// </summary>
public sealed record Problem(ProblemKind Kind, Finding Finding, int Index)
{
    /// <summary>A finding by its severity: a refusal is a problem, a decline is left out, a finding that
    /// measured a number is out of range, any other complaint a warning.</summary>
    public static Problem Of(Finding finding, int index) => new(finding switch
    {
        { Severity: Severity.Refusal } => ProblemKind.Problem,
        { Severity: Severity.Decline } => ProblemKind.LeftOut,
        { Measured: not null } => ProblemKind.OutOfRange,
        _ => ProblemKind.Warning,
    }, finding, index);

    /// <summary>Several findings by severity, indexed from <paramref name="first"/> in order.</summary>
    public static List<Problem> Of(IEnumerable<Finding> findings, int first = 0) =>
        findings.Select((finding, offset) => Of(finding, first + offset)).ToList();
}
