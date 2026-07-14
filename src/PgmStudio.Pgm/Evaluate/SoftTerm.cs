namespace PgmStudio.Pgm.Evaluate;

/// <summary>
/// Base for a soft "feel" term: it exposes a pure <see cref="Value"/> (the metric, in whatever unit) and,
/// separately, draws its <see cref="Evidence"/>. <see cref="Measure"/> ties them together — look the authored
/// band up by the term id, take the <see cref="Band"/> distance, and (when outside) return a scored violation.
/// The split is what keeps the envelope generator and the term honest: <c>envelope-stats</c> learns each band
/// by calling this same <see cref="Value"/> over the seeds, so the number the term scores and the number that
/// defined the band are computed by one method. A term with no band yet (envelope not generated) stays dormant.
/// </summary>
public abstract class SoftTerm : ILayoutTerm
{
    public abstract string Id { get; }
    public abstract string RuleId { get; }
    public TermKind Kind => TermKind.Soft;

    /// <summary>The metric this term measures over a plan, or null when it does not apply (e.g. a wool-spacing
    /// metric on a single-wool plan) — null values are excluded from both scoring and band computation.</summary>
    public abstract double? Value(EvalContext ctx);

    /// <summary>Whether the band generator may widen this term's band with the traced real-map corpus, not the
    /// authored intent seeds alone. Default true (more ground truth, tighter bands). A term whose band is an
    /// authored *cap* we impose — not a distribution we observe — overrides this to false, so real maps that
    /// exceed the cap do not silently license it.</summary>
    public virtual bool LearnsFromTraced => true;

    /// <summary>The piece/zone ids the metric implicates (for editor highlight); empty by default.</summary>
    protected virtual IReadOnlyList<string> Subjects(EvalContext ctx) => [];

    /// <summary>Drawable evidence for a violation — the metric drawn against its band; none by default.</summary>
    protected virtual IReadOnlyList<Evidence> Evidence(EvalContext ctx, double value, Band band) => [];

    public TermScore Measure(EvalContext ctx)
    {
        var value = Value(ctx);
        if (value is null) return TermScores.Clean(this);

        var band = ctx.Envelopes[Id];
        if (band is null) return TermScores.Clean(this);   // no authored band yet → dormant, not a violation

        var distance = band.Value.Distance(value.Value);
        if (distance <= 0.0) return TermScores.Clean(this);

        var message = $"{Id} {value.Value:0.###} outside authored band [{band.Value.Lo:0.###}, {band.Value.Hi:0.###}]";
        return TermScores.Soft(this, distance, message, Subjects(ctx), Evidence(ctx, value.Value, band.Value));
    }
}
