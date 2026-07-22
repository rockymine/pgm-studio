namespace PgmStudio.Contracts;

/// <summary>One row in the plan store list (GET /api/plans) — the open-from-DB browser's line. <paramref
/// name="Origin"/> is <c>generated</c> | <c>authored</c> | <c>imported</c>. <paramref name="ParentId"/> is the
/// fork this row was split from (null if none); <paramref name="Seed"/>/<paramref name="ComposerVersion"/> are
/// present only for generated rows. <paramref name="Descriptor"/> carries the full reproducible request for a
/// generated row (parsed from its stored descriptor) so the browse hold-tray can identify and re-open it;
/// null for authored/imported rows.</summary>
public sealed record PlanSummary(
    long Id,
    string Name,
    string Origin,
    long? ParentId,
    ulong? Seed,
    string? ComposerVersion,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    ComposeRequestDto? Descriptor = null);

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
