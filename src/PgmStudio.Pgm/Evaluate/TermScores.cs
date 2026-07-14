namespace PgmStudio.Pgm.Evaluate;

/// <summary>Small factories for the <see cref="TermScore"/> a term returns — so a term states its outcome
/// (clean / hard-violated / soft-distance) without restating its own id and kind each time.</summary>
public static class TermScores
{
    /// <summary>The term is satisfied: zero distance, no violation.</summary>
    public static TermScore Clean(ILayoutTerm term) => new(term.Id, term.Kind, 0.0, null);

    /// <summary>A hard term fired: a violation with a message, the implicated subject ids, and optional
    /// drawable evidence. Distance stays 0 — the evaluator applies the flat hard penalty when it sums.</summary>
    public static TermScore Violated(
        ILayoutTerm term, string message, IReadOnlyList<string> subjects, IReadOnlyList<Evidence>? evidence = null) =>
        new(term.Id, term.Kind, 0.0, new Violation(term.Id, term.RuleId, message, subjects, evidence));

    /// <summary>A soft term's distance outside its band. A nonzero distance also carries a violation (so a plan
    /// far outside the authored envelope is legible), with optional evidence (the band drawn as a measure).</summary>
    public static TermScore Soft(
        ILayoutTerm term, double distance, string message, IReadOnlyList<string> subjects,
        IReadOnlyList<Evidence>? evidence = null) =>
        distance <= 0.0
            ? Clean(term)
            : new(term.Id, term.Kind, distance, new Violation(term.Id, term.RuleId, message, subjects, evidence));
}
