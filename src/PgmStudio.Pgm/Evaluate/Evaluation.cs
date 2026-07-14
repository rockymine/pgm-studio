namespace PgmStudio.Pgm.Evaluate;

/// <summary>A term is either a <b>hard</b> well-formedness constraint (a violation is a large penalty a valid
/// layout never carries) or a <b>soft</b> feel metric (a distance outside the authored envelope).</summary>
public enum TermKind { Hard, Soft }

/// <summary>One rule violation, legible and actionable: the term that fired, the layout-rules id it cites, a
/// human message, the piece/zone ids it indicts (the same subject-id shape a <c>PlanFinding</c> carries, so the
/// editor highlights them identically), and optional drawable <see cref="Evidence"/> — cell-space primitives a
/// generic renderer paints on the grid (nullable; costs nothing when absent).</summary>
public sealed record Violation(
    string TermId, string RuleId, string Message, IReadOnlyList<string> Subjects,
    IReadOnlyList<Evidence>? Evidence = null);

/// <summary>One term's contribution to the score: its distance outside its band (0 when inside; always 0 for a
/// hard term) and, when the term fired, the <see cref="Violation"/> describing it.</summary>
public sealed record TermScore(string TermId, TermKind Kind, double Distance, Violation? Violation);

/// <summary>The full result of scoring a plan: the summed <see cref="Score"/> (lower is better; 0 is perfect)
/// and every term's contribution. Valid means no hard term fired.</summary>
public sealed record Evaluation(double Score, IReadOnlyList<TermScore> Terms)
{
    public bool IsValid => Terms.All(t => t.Kind != TermKind.Hard || t.Violation is null);

    public IEnumerable<Violation> Violations => Terms.Where(t => t.Violation is not null).Select(t => t.Violation!);
}
