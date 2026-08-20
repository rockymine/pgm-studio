
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
/// already computed (an unplaceable iron, a mid-lane spawn, an odd elevation step) — which used to be derived
/// on every call and then discarded, so the loop an agent drives never saw them. Never affects
/// <see cref="Valid"/> or <see cref="Score"/>.</para>
/// </summary>
public sealed record EvaluationDto(
    double Score, bool Valid, IReadOnlyList<ViolationDto> Violations,
    IReadOnlyList<Finding>? Lint = null);
