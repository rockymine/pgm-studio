using System.Text.Json;
namespace PgmStudio.Contracts;

/// <summary>One stored plan row (GET /api/plans), kept read-only as history: the plans the generator's candidates
/// were kept as, and the ones drawn or imported without a map behind them.</summary>
/// <param name="Id">The row number every later route names it by.</param>
/// <param name="Name">What the plan is called.</param>
/// <param name="Origin">How the row came to be: <c>generated</c>, <c>authored</c> or <c>imported</c>.</param>
/// <param name="ParentId">The row this one was forked from, or null where it was not.</param>
/// <param name="Seed">The seed that composed it, present only on a generated row.</param>
/// <param name="ComposerVersion">Which composer composed it, present only on a generated row.</param>
/// <param name="CreatedAt">When the row was made.</param>
/// <param name="UpdatedAt">When it was last written.</param>
/// <param name="Descriptor">The request a generated row was composed from. Absent on an authored or imported row,
/// which has none.</param>
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

/// <summary>A full stored plan row (GET /api/plans/{id}) — a <see cref="PlanSummary"/> plus its canonical
/// <c>*.plan.json</c> document.</summary>
/// <param name="Id">The row number every later route names it by.</param>
/// <param name="Name">What the plan is called.</param>
/// <param name="Origin">How the row came to be: <c>generated</c>, <c>authored</c> or <c>imported</c>.</param>
/// <param name="ParentId">The row this one was forked from, or null where it was not.</param>
/// <param name="Seed">The seed that composed it, present only on a generated row.</param>
/// <param name="ComposerVersion">Which composer composed it, present only on a generated row.</param>
/// <param name="CreatedAt">When the row was made.</param>
/// <param name="UpdatedAt">When it was last written.</param>
/// <param name="PlanJson">The canonical <c>*.plan.json</c> document, as text.</param>
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

/// <summary>
/// What a plan compiles to: the layout and the intent a map is built from, each half serialized with its own
/// consumer's options so both can be posted on verbatim, as the <c>layout</c> and <c>intent</c> of
/// <c>PUT /map/{slug}/source</c>.
///
/// <para>Each is the document itself rather than a shape restated here. The layout is a
/// <c>SketchLayout</c> and the intent a <c>MapIntent</c>, and both are described where they are read; naming
/// them again would be a second copy free to disagree with the reader.</para>
/// </summary>
/// <param name="Layout">The sketch layout the plan compiles to.</param>
/// <param name="Intent">The map intent it compiles to.</param>
public sealed record CompiledPlanDto(JsonElement Layout, JsonElement Intent);

