using System.Text.Json;
namespace PgmStudio.Contracts;

/// <summary>One row in the plan store list (GET /api/plans) — the open-from-DB browser's line. <paramref
/// name="Origin"/> is <c>generated</c> | <c>authored</c> | <c>imported</c>. <paramref name="ParentId"/> is the
/// fork this row was split from (null if none); <paramref name="Seed"/>/<paramref name="ComposerVersion"/> are
/// present only for generated rows. <paramref name="Descriptor"/> carries the full reproducible request for a
/// generated row (parsed from its stored descriptor) so the browse hold-tray can identify and re-open it;
/// null for authored/imported rows. <paramref name="StaleComposer"/> is true when a generated row was made by
/// an <b>older composer</b> than the one running: the stored plan is unaffected (its geometry is stored, not
/// recomputed), but its descriptor no longer reproduces it, so re-composing that request now gives a different
/// board. Always false for authored/imported rows, which have no descriptor to go stale.</summary>
public sealed record PlanSummary(
    long Id,
    string Name,
    string Origin,
    long? ParentId,
    ulong? Seed,
    string? ComposerVersion,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    ComposeRequestDto? Descriptor = null,
    bool StaleComposer = false);

/// <summary>A full plan row (GET /api/plans/{id}, and the POST /api/plans save response) — a
/// <see cref="PlanSummary"/> plus the canonical <c>*.plan.json</c> document to load into the editor.</summary>
public sealed record PlanDetail(
    long Id,
    string Name,
    string Origin,
    long? ParentId,
    ulong? Seed,
    string? ComposerVersion,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string PlanJson);

/// <summary>Save the plan open in the editor (POST /api/plans). <paramref name="SourceId"/> is the row the
/// editor loaded, if any: an authored source is updated in place, a generated/imported source is forked into a
/// new authored row. Null saves a fresh authored plan.</summary>
public sealed record PlanSaveRequest(
    string PlanJson,
    long? SourceId);

/// <summary>
/// What a plan compiles to: the pair the draft pipeline consumes, each half serialized with its own
/// consumer's options so both can be posted on verbatim — the layout to <c>PUT …/sketch/from-plan</c>, the
/// intent to <c>PUT …/intent/from-plan</c>.
///
/// <para>Each is the document itself rather than a shape restated here. The layout is a
/// <c>SketchLayout</c> and the intent a <c>MapIntent</c>, and both are described where they are read; naming
/// them again would be a second copy free to disagree with the reader.</para>
/// </summary>
public sealed record CompiledPlanDto(JsonElement Layout, JsonElement Intent);
