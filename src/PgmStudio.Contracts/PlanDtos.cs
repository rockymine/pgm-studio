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
/// built around, the footprint the building stands on, and — on a spawn whose yard has room for one — the iron
/// marker. All piece-relative block offsets, ready to store on the placement. Absent where the piece carries
/// no room or is too small to raise a shell on.</summary>
/// <param name="At">The marker's <c>[x, z]</c> offset in blocks from the piece's minimum corner.</param>
/// <param name="Footprint">The building as <c>[x, z, w, h]</c> in blocks from that same corner.</param>
/// <param name="Iron">The cube's <c>[x, z]</c> offset from that corner — beside the door, on the player's
/// right as they leave — or null on a wool room and on a spawn whose yard has no room for one.</param>
public sealed record DrawnRoomDto(double[] At, double[] Footprint, double[]? Iron);
