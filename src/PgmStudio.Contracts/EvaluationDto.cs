
using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>
/// One drawable evidence primitive, flattened for the wire. The evaluator's four geometric primitives — rect,
/// segment, marker, measure — collapse to one record keyed by <see cref="Kind"/> so a generic canvas overlay
/// draws them with a switch, never per-term code. Every coordinate is in the plan's <b>5×5 cell space</b> (the
/// same frame a piece/zone rect uses); multiply by <c>globals.cell</c> for block coords. <see cref="Tag"/>
/// (<c>offender</c> / <c>bound</c> / <c>measure</c> / <c>context</c>, or a <c>slot:*</c> convention) drives the
/// overlay's styling table. Only the fields a given <see cref="Kind"/> uses are set; the rest stay null.
/// </summary>
/// <param name="Kind"><c>rect</c> · <c>segment</c> · <c>marker</c> · <c>measure</c>.</param>
/// <param name="Rect">A <c>[x, z, w, h]</c> cell rect (<c>rect</c>).</param>
/// <param name="X1">Segment/measure first endpoint (<c>segment</c> / <c>measure</c>).</param>
/// <param name="X">A cell-space point (<c>marker</c>).</param>
/// <param name="Tag">What the primitive is for, which drives the overlay's styling: <c>offender</c>,
/// <c>bound</c>, <c>measure</c>, <c>context</c>, or a <c>slot:*</c> convention.</param>
/// <param name="Z1">Segment/measure first endpoint, north–south.</param>
/// <param name="X2">Segment/measure second endpoint, east–west.</param>
/// <param name="Z2">That endpoint, north–south.</param>
/// <param name="Z">A cell-space point, north–south (<c>marker</c>).</param>
/// <param name="Label">A human label carried by a dimension line (<c>measure</c>, e.g. <c>"17 &lt; 20"</c>).</param>
public sealed record EvidenceDto(
    string Kind, string Tag,
    int[]? Rect = null,
    double? X1 = null, double? Z1 = null, double? X2 = null, double? Z2 = null,
    double? X = null, double? Z = null,
    string? Label = null);

/// <summary>One fired rule: which term measured it, its <see cref="Kind"/> (<c>hard</c> well-formedness vs
/// <c>soft</c> feel), the soft distance outside its authored band (0 for a hard fire), what it
/// <see cref="Finding"/> is — the same shape every other gate answers in, so the canvas highlights an
/// evaluator's subjects exactly as it highlights a validator's — and its drawable <see cref="Evidence"/>.
///
/// <para>The term id stays beside the finding rather than inside it: a rule is what an author broke and a term
/// is which measurement noticed, and a scoring function may well grow a second term citing one rule.</para>
/// </summary>
/// <param name="TermId">Which measurement noticed — the metric id, kept beside the finding rather than
/// inside it, because a rule is what an author broke and a term is what saw it.</param>
/// <param name="Kind"><c>hard</c> for well-formedness, <c>soft</c> for feel.</param>
/// <param name="Distance">How far outside its authored band a soft term landed. 0 for a hard fire, which
/// has no band to be outside of.</param>
/// <param name="Finding">What is wrong, in the one shape every gate answers in — so the canvas highlights
/// an evaluator's subjects exactly as it highlights a validator's.</param>
/// <param name="Evidence">The drawable primitives the overlay paints, so a broken rule is seen rather than
/// only read.</param>
public sealed record ViolationDto(
    string TermId, string Kind, double Distance, Finding Finding,
    IReadOnlyList<EvidenceDto> Evidence);

/// <summary>
/// POST /api/plan/evaluate — the plan editor's live evaluator score + lint. <see cref="Score"/> is the summed
/// cost (lower is better, 0 perfect: Σ hard-penalty + Σ weighted soft-distance); <see cref="Valid"/> is true when
/// no hard term fired; <see cref="Violations"/> is every fired term (hard well-formedness + soft out-of-band),
/// ordered hard-first so the most actionable problems lead. Each violation carries the drawable evidence the
/// canvas overlay paints, so a broken rule is <i>seen</i>, not only read.
/// <para><see cref="Lint"/> is the structural validator's complaints — every non-blocking finding the check
/// already computed (an unplaceable iron, a mid-lane spawn, an odd elevation step). They are derived on every
/// call whether or not they are carried, so carrying them costs nothing and is the only way the loop an agent
/// drives sees them. Never affects <see cref="Valid"/> or <see cref="Score"/>.</para>
/// </summary>
/// <param name="Score">The summed cost, lower being better and 0 perfect: the hard penalties plus the
/// weighted soft distances.</param>
/// <param name="Valid">Whether no hard term fired.</param>
/// <param name="Violations">Every fired term, hard first so the most actionable lead.</param>
/// <param name="Lint">The structural validator's complaints, which never affect
/// <paramref name="Score"/> or <paramref name="Valid"/>. They are derived on every call whether carried or
/// not, so carrying them costs nothing and is the only way a driven loop sees them.</param>
public sealed record EvaluationDto(
    double Score, bool Valid, IReadOnlyList<ViolationDto> Violations,
    IReadOnlyList<Finding>? Lint = null);
