using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

// What an edit to a stored map answers. Every one of these is built by an editor in Pgm/Editing, one project
// below Contracts, so each is declared at its route rather than mapped there — a second walk of the same
// dictionary here would be free to disagree with the first. EditAnswerShapeTests holds each record to what
// its editor writes.
//
// The shapes are few because the answers are: most edits hand back nothing (AppliedDto, beside the other
// acknowledgements), a few hand back the id the caller now names the thing by, and three hand back the row
// they just wrote.

/// <summary>A region was created, and this is the id every later route names it by. Region ids are the
/// author's words rather than row numbers, which is why this is not <see cref="CreatedDto"/>.</summary>
/// <param name="Id">The region's name — the author's word where they gave one, else the one the editor
/// numbered from the type.</param>
public sealed record RegionCreatedDto(string Id);

/// <summary>A region was re-coordinated or renamed. <see cref="Bounds"/> is present only where the region
/// has a footprint the edit moved — a rename, or a type whose shape is not a box, answers no bounds at
/// all.</summary>
/// <param name="Bounds">The footprint the region now covers.</param>
public sealed record RegionPatchedDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Bounds2dDto? Bounds = null);

/// <summary>Two or more regions were unioned into a compound. The id is what the caller selects next, and
/// the bounds are the footprint the union covers.</summary>
/// <param name="Id">The compound's name — the author's word, or one numbered from the compound type.</param>
/// <param name="Bounds">The footprint the union covers, which is the union of its children's.</param>
public sealed record RegionGroupedDto(string Id, Bounds2dDto Bounds);

/// <summary>A compound was dissolved, and these are the children it freed — what the caller selects in its
/// place. <see cref="Warning"/> is present only where dissolving lost something the compound carried and
/// its children cannot: an ordered type's base/subtrahend ordering.</summary>
/// <param name="ChildIds">The regions the compound freed, now standing on their own.</param>
/// <param name="Warning">What dissolving lost, where it lost something.</param>
public sealed record RegionUngroupedDto(
    [property: JsonPropertyName("child_ids")] IReadOnlyList<string> ChildIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Warning = null);

/// <summary>A region was fanned across the map's symmetry. <see cref="Created"/> is every counterpart made —
/// one for a mirror or a half turn, three for a quarter turn. <see cref="Counterpart"/> names the single
/// opposite where one counterpart was asked for, and is absent where a whole orbit was.</summary>
/// <param name="Created">Every counterpart made, by id.</param>
/// <param name="Counterpart">The single opposite, where one counterpart was asked for rather than a whole
/// orbit.</param>
public sealed record RegionOrbitDto(
    IReadOnlyList<string> Created,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Counterpart = null);

/// <summary>A wool objective was written, in the same shape <c>GET /map/{slug}</c> carries it — it is the
/// same wool, so it is the same record.</summary>
/// <param name="Wool">The wool as it now stands, including whatever the write defaulted.</param>
public sealed record WoolWrittenDto(MapWoolDto Wool);

/// <summary>A monument was written, in the shape the map document carries it.</summary>
/// <param name="Monument">The monument as it now stands.</param>
public sealed record MonumentWrittenDto(MapMonumentDto Monument);

/// <summary>A team was written, in the shape the map document carries it.</summary>
/// <param name="Team">The team as it now stands, including whatever the write defaulted.</param>
public sealed record TeamWrittenDto(MapTeamDto Team);
