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
/// <param name="Id">The row number every later route names it by.</param>
/// <param name="Name">What the plan is called.</param>
/// <param name="Origin">How the row came to be: <c>generated</c>, <c>authored</c> or <c>imported</c>.</param>
/// <param name="ParentId">The row this one was forked from, or null where it was not.</param>
/// <param name="Seed">The seed that composed it, present only on a generated row.</param>
/// <param name="ComposerVersion">Which composer composed it, present only on a generated row.</param>
/// <param name="CreatedAt">When the row was made.</param>
/// <param name="UpdatedAt">When it was last written.</param>
/// <param name="Descriptor">The full reproducible request behind a generated row, so the browse hold-tray
/// can identify and re-open it. Absent on an authored or imported row, which has none.</param>
/// <param name="StaleComposer">Whether a generated row was made by an <b>older composer</b> than the one
/// running. The stored plan is unaffected — its geometry is stored, not recomputed — but its descriptor no
/// longer reproduces it. Always false for a row with no descriptor to go stale.</param>
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
/// <param name="Id">The row number every later route names it by.</param>
/// <param name="Name">What the plan is called.</param>
/// <param name="Origin">How the row came to be: <c>generated</c>, <c>authored</c> or <c>imported</c>.</param>
/// <param name="ParentId">The row this one was forked from, or null where it was not.</param>
/// <param name="Seed">The seed that composed it, present only on a generated row.</param>
/// <param name="ComposerVersion">Which composer composed it, present only on a generated row.</param>
/// <param name="CreatedAt">When the row was made.</param>
/// <param name="UpdatedAt">When it was last written.</param>
/// <param name="PlanJson">The canonical <c>*.plan.json</c> document, as text — what the editor loads.</param>
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
/// <param name="PlanJson">The plan document itself, as text — the same shape <c>PUT …/plan</c> takes and
/// <c>GET /api/plans/{id}</c> answers.</param>
/// <param name="SourceId">The row the editor loaded, if any.</param>
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
/// <param name="Layout">The sketch layout the plan compiles to, ready to post to
/// <c>PUT …/sketch/from-plan</c> verbatim.</param>
/// <param name="Intent">The map intent it compiles to, ready to post to <c>PUT …/intent/from-plan</c>.</param>
public sealed record CompiledPlanDto(JsonElement Layout, JsonElement Intent);

/// <summary>What a freshly drawn <c>spawn</c> or <c>wool-room</c> piece is seeded with: the marker the room is
/// built around, and the footprint the building stands on — both piece-relative block offsets, ready to store
/// on the placement. Absent where the piece carries no room or is too small to raise a shell on.</summary>
/// <param name="At">The marker's <c>[x, z]</c> offset in blocks from the piece's minimum corner.</param>
/// <param name="Footprint">The building as <c>[x, z, w, h]</c> in blocks from that same corner.</param>
public sealed record DrawnRoomDto(double[] At, double[] Footprint);
