using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// What an edit to a stored map answers. Every one of these is built by an editor in <c>Pgm/Editing</c>,
/// one project below <c>Contracts</c>, so each is <b>declared</b> at its route rather than mapped there — a
/// second walk of the same dictionary here would be free to disagree with the first.
/// <c>EditAnswerShapeTests</c> holds each record to what its editor writes.
///
/// <para>The shapes are few because the answers are: most edits hand back nothing — <see cref="AppliedDto"/>,
/// beside the other acknowledgements — a few hand back the id the caller now names the thing by, and three
/// hand back the row they just wrote.</para>
/// </summary>
/// <summary>A region was created, and this is the id every later route names it by. Region ids are the
/// author's words rather than row numbers, which is why this is not <see cref="CreatedDto"/>.</summary>
public sealed record RegionCreatedDto(string Id);

/// <summary>A region was re-coordinated or renamed. <see cref="Bounds"/> is present only where the region
/// has a footprint the edit moved — a rename, or a type whose shape is not a box, answers no bounds at
/// all.</summary>
public sealed record RegionPatchedDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Bounds2dDto? Bounds = null);

/// <summary>Two or more regions were unioned into a compound. The id is what the caller selects next, and
/// the bounds are the footprint the union covers.</summary>
public sealed record RegionGroupedDto(string Id, Bounds2dDto Bounds);

/// <summary>A compound was dissolved, and these are the children it freed — what the caller selects in its
/// place. <see cref="Warning"/> is present only where dissolving lost something the compound carried and
/// its children cannot: an ordered type's base/subtrahend ordering.</summary>
public sealed record RegionUngroupedDto(
    [property: JsonPropertyName("child_ids")] IReadOnlyList<string> ChildIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Warning = null);

/// <summary>A region was fanned across the map's symmetry. <see cref="Created"/> is every counterpart made —
/// one for a mirror or a half turn, three for a quarter turn. <see cref="Counterpart"/> names the single
/// opposite where one counterpart was asked for, and is absent where a whole orbit was.</summary>
public sealed record RegionOrbitDto(
    IReadOnlyList<string> Created,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Counterpart = null);

/// <summary>A wool objective was written, in the same shape <c>GET /map/{slug}</c> carries it — it is the
/// same wool, so it is the same record.</summary>
public sealed record WoolWrittenDto(MapWoolDto Wool);

/// <summary>A monument was written, in the shape the map document carries it.</summary>
public sealed record MonumentWrittenDto(MapMonumentDto Monument);

/// <summary>A team was written, in the shape the map document carries it.</summary>
public sealed record TeamWrittenDto(MapTeamDto Team);
