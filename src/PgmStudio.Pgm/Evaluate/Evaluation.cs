using PgmStudio.Domain;

namespace PgmStudio.Pgm.Evaluate;

/// <summary>A term is either a <b>hard</b> well-formedness constraint (a violation is a large penalty a valid
/// layout never carries) or a <b>soft</b> feel metric (a distance outside the authored envelope).</summary>
public enum TermKind { Hard, Soft }

/// <summary>One rule violation: the term that fired, what it <see cref="Finding"/> is — the layout-rules id it
/// cites, the message, and the piece/zone ids it indicts, in the one shape every gate in the studio answers in,
/// so the editor highlights an evaluator's subjects exactly as it highlights a validator's — and optional
/// drawable <see cref="Evidence"/>, cell-space primitives a generic renderer paints on the grid (nullable;
/// costs nothing when absent).
///
/// <para>The term id stays out of the finding on purpose. A rule is what an author broke and a term is which
/// measurement noticed, and a scoring function may well grow a second term citing one rule.</para></summary>
public sealed record Violation(
    string TermId, Finding Finding, IReadOnlyList<Evidence>? Evidence = null)
{
    public string RuleId => Finding.Rule;
    public string Message => Finding.Message;
    public IReadOnlyList<string> Subjects => Finding.SubjectIds;
}

/// <summary>One term's contribution to the score: its distance outside its band (0 when inside; always 0 for a
/// hard term) and, when the term fired, the <see cref="Violation"/> describing it.</summary>
public sealed record TermScore(string TermId, TermKind Kind, double Distance, Violation? Violation);

/// <summary>The full result of scoring a plan: the summed <see cref="Score"/> (lower is better; 0 is perfect)
/// and every term's contribution. Valid means no hard term fired.</summary>
public sealed record Evaluation(double Score, IReadOnlyList<TermScore> Terms)
{
    /// <summary>The result for a plan there is nothing to score — no terms fired because none could run.
    /// Distinct from a perfect plan only in that it has no terms at all.</summary>
    public static readonly Evaluation Empty = new(0, []);

    public bool IsValid => Terms.All(t => t.Kind != TermKind.Hard || t.Violation is null);

    public IEnumerable<Violation> Violations => Terms.Where(t => t.Violation is not null).Select(t => t.Violation!);
}
