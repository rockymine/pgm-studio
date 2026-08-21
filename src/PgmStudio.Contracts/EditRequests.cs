using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// What an edit to a stored map takes. Each of the sixteen edit routes reads its body as a partial
/// dictionary and hands it to an editor in <c>Pgm/Editing</c>, one project below <c>Contracts</c>, so these
/// are <b>declared</b> at the route rather than bound there — the body is still read key by key, and these
/// say what those keys are. <c>EditRequestShapeTests</c> holds each record to the editor that reads it, by
/// posting the record's own serialization through the editor and asserting the edit landed.
///
/// <para><b>An absent field is not a null one.</b> On a create route an absent field takes the editor's
/// default, named per field below. On an update route it means <i>leave this as it is</i>, which is why every
/// field of an update is optional and why sending <c>null</c> is a different request from sending nothing —
/// on the three that carry a location or a team, an explicit <c>null</c> clears it.</para>
/// </summary>
/// <remarks>The region a caller draws, in the type's own numbers. <see cref="Type"/> chooses which of them
/// are read and the rest are ignored: a <c>rectangle</c> takes the four <c>min</c>/<c>max</c> footprint
/// numbers; a <c>cuboid</c> takes those plus <see cref="MinY"/> and <see cref="MaxY"/>; a <c>point</c> or
/// <c>block</c> takes <see cref="X"/>, <see cref="Y"/> and <see cref="Z"/>; a <c>cylinder</c> takes
/// <see cref="BaseX"/>, <see cref="BaseY"/>, <see cref="BaseZ"/>, <see cref="Radius"/> and
/// <see cref="Height"/>; a <c>circle</c> takes <see cref="CenterX"/>, <see cref="CenterZ"/> and
/// <see cref="Radius"/>. A number the chosen type requires and the body omits is refused as <c>RQ1</c>.</remarks>
/// <param name="Type">One of <c>rectangle</c>, <c>cuboid</c>, <c>point</c>, <c>block</c>, <c>cylinder</c>,
/// <c>circle</c>; anything else is refused. Absent means <c>rectangle</c>.</param>
/// <param name="Id">The name every later route calls this region by. Absent means the editor numbers one
/// from the type — <c>region_1</c>, <c>cylinder_2</c> — and an id already in use is refused as
/// <c>RQ5</c>.</param>
/// <param name="Category">Which of the editor's region groups this one is filed under. Absent means
/// <c>other</c>.</param>
/// <param name="DraftStep">Marks the region as drawn but not yet wired, so the editor's Teams, Objective or
/// Build phase still shows it as a draft. Absent means it is wired.</param>
/// <param name="MinX">The footprint's west edge, in blocks.</param>
/// <param name="MinY">A cuboid's floor. Absent means 0.</param>
/// <param name="MinZ">The footprint's north edge, in blocks.</param>
/// <param name="MaxX">The footprint's east edge, in blocks.</param>
/// <param name="MaxY">A cuboid's ceiling. Absent means 256.</param>
/// <param name="MaxZ">The footprint's south edge, in blocks.</param>
/// <param name="X">A point or block's east–west position. A <c>block</c> is rounded to whole blocks; a
/// <c>point</c> keeps what was written, so a spawn holds its <c>.5</c> block centre.</param>
/// <param name="Y">A point or block's height. Absent means 64.</param>
/// <param name="Z">A point or block's north–south position.</param>
/// <param name="BaseX">A cylinder's centre, east–west.</param>
/// <param name="BaseY">A cylinder's floor. Absent means 64.</param>
/// <param name="BaseZ">A cylinder's centre, north–south.</param>
/// <param name="CenterX">A circle's centre, east–west.</param>
/// <param name="CenterZ">A circle's centre, north–south.</param>
/// <param name="Radius">A cylinder's or circle's radius, in blocks.</param>
/// <param name="Height">A cylinder's height, in blocks. Absent means 10.</param>
public sealed record RegionCreateRequest(
    string? Type = null,
    string? Id = null,
    string? Category = null,
    [property: JsonPropertyName("draft_step")] string? DraftStep = null,
    [property: JsonPropertyName("min_x")] double? MinX = null,
    [property: JsonPropertyName("min_y")] double? MinY = null,
    [property: JsonPropertyName("min_z")] double? MinZ = null,
    [property: JsonPropertyName("max_x")] double? MaxX = null,
    [property: JsonPropertyName("max_y")] double? MaxY = null,
    [property: JsonPropertyName("max_z")] double? MaxZ = null,
    double? X = null,
    double? Y = null,
    double? Z = null,
    [property: JsonPropertyName("base_x")] double? BaseX = null,
    [property: JsonPropertyName("base_y")] double? BaseY = null,
    [property: JsonPropertyName("base_z")] double? BaseZ = null,
    [property: JsonPropertyName("center_x")] double? CenterX = null,
    [property: JsonPropertyName("center_z")] double? CenterZ = null,
    double? Radius = null,
    double? Height = null);

/// <summary>The numbers of a region already created, under the key that sets them. Which are read is the
/// stored region's type, not the request's: a <c>cuboid</c> reads only <see cref="MinY"/> and
/// <see cref="MaxY"/> here, because its footprint is set through <c>bounds</c> instead, and a number the
/// type does not use is accepted and ignored.</summary>
/// <param name="MinX">A rectangle's west edge.</param>
/// <param name="MinY">A cuboid's floor.</param>
/// <param name="MinZ">A rectangle's north edge.</param>
/// <param name="MaxX">A rectangle's east edge.</param>
/// <param name="MaxY">A cuboid's ceiling.</param>
/// <param name="MaxZ">A rectangle's south edge.</param>
/// <param name="X">A point or block's east–west position.</param>
/// <param name="Y">A point or block's height.</param>
/// <param name="Z">A point or block's north–south position.</param>
/// <param name="BaseX">A cylinder's centre, east–west.</param>
/// <param name="BaseY">A cylinder's floor.</param>
/// <param name="BaseZ">A cylinder's centre, north–south.</param>
/// <param name="CenterX">A circle's centre, east–west.</param>
/// <param name="CenterZ">A circle's centre, north–south.</param>
/// <param name="Radius">A cylinder's or circle's radius.</param>
/// <param name="Height">A cylinder's height.</param>
public sealed record RegionCoordsDto(
    [property: JsonPropertyName("min_x")] double? MinX = null,
    [property: JsonPropertyName("min_y")] double? MinY = null,
    [property: JsonPropertyName("min_z")] double? MinZ = null,
    [property: JsonPropertyName("max_x")] double? MaxX = null,
    [property: JsonPropertyName("max_y")] double? MaxY = null,
    [property: JsonPropertyName("max_z")] double? MaxZ = null,
    double? X = null,
    double? Y = null,
    double? Z = null,
    [property: JsonPropertyName("base_x")] double? BaseX = null,
    [property: JsonPropertyName("base_y")] double? BaseY = null,
    [property: JsonPropertyName("base_z")] double? BaseZ = null,
    [property: JsonPropertyName("center_x")] double? CenterX = null,
    [property: JsonPropertyName("center_z")] double? CenterZ = null,
    double? Radius = null,
    double? Height = null);

/// <summary>Rename a region, move its footprint, or set the numbers of its own type — one of the three, and
/// a body carrying none of them is refused as <c>RQ1</c> before the region is even looked up.</summary>
/// <param name="Id">A new name. The rename cascades: the region groups, every compound listing it as a
/// child, and any spawn or wool room pointing at it follow. A name already in use is refused as
/// <c>RQ5</c>.</param>
/// <param name="Bounds">A new 2-D footprint, which any region type accepts and which is answered back as the
/// bounds the region now covers.</param>
/// <param name="Coords">The stored type's own numbers.</param>
public sealed record RegionPatchRequest(
    string? Id = null,
    Bounds2dDto? Bounds = null,
    RegionCoordsDto? Coords = null);

/// <summary>Combine two or more regions into a compound one.</summary>
/// <param name="ChildIds">The regions to combine, in order — which matters for the two types that subtract,
/// where the first is the base. Fewer than two (one, for <c>negative</c>) is refused as <c>ED2</c>, and an
/// id naming no stored region as <c>RQ4</c>.</param>
/// <param name="Type">One of <c>union</c>, <c>complement</c>, <c>intersect</c>, <c>negative</c>. Absent
/// means <c>union</c>.</param>
/// <param name="Id">The name the compound is called by. Absent means the editor numbers one from the type —
/// <c>union_1</c>.</param>
public sealed record RegionGroupRequest(
    [property: JsonPropertyName("child_ids")] IReadOnlyList<string> ChildIds,
    string? Type = null,
    string? Id = null);

/// <summary>Dissolve a compound region, leaving its children standing on their own.</summary>
/// <param name="RegionId">The compound to dissolve. A region that is not compound is refused as
/// <c>ED2</c>.</param>
public sealed record RegionUngroupRequest(
    [property: JsonPropertyName("region_id")] string RegionId);

/// <summary>Mirror a region onto the other side of the map.</summary>
/// <param name="Mode">Which symmetry to mirror through — <c>mirror_x</c>, <c>mirror_z</c>, <c>rot_180</c> or
/// <c>rot_90</c>.</param>
/// <param name="Center">The point to mirror about, the same <c>{cx, cz}</c> the symmetry read answers.
/// Absent falls back to the map's confirmed symmetry centre, and a map that has none is refused as
/// <c>RQ1</c>.</param>
public sealed record RegionCounterpartRequest(
    string? Mode = null,
    SymmetryCenterDto? Center = null);

/// <summary>Fill a region's whole symmetry orbit — every counterpart the map's confirmed symmetry implies,
/// which is three for <c>rot_90</c> and one for the rest. A map with no confirmed symmetry creates
/// nothing.</summary>
/// <param name="Category">Which region group the counterparts are filed under. Absent means
/// <c>other</c>.</param>
/// <param name="DraftStep">Marks the counterparts as drawn but not yet wired, the same as on the region they
/// were fanned from.</param>
public sealed record RegionOrbitRequest(
    string? Category = null,
    [property: JsonPropertyName("draft_step")] string? DraftStep = null);

/// <summary>Where a team enters the map.</summary>
/// <param name="RegionId">The region the team spawns in, which must already exist — this tool draws no
/// regions. A region that already carries a spawn is refused as <c>RQ5</c>.</param>
/// <param name="Team">The team that spawns here, by team id. Absent means none, which is how a spawn is
/// authored before its team is.</param>
/// <param name="Kit">The kit given on spawning, by kit name. Absent means none.</param>
/// <param name="Yaw">The direction the player faces, in degrees. Absent means 0.</param>
public sealed record SpawnCreateRequest(
    [property: JsonPropertyName("region_id")] string RegionId,
    string? Team = null,
    string? Kit = null,
    double? Yaw = null);

/// <summary>Change a spawn already placed. Its region is the path's, and is not moved from here — a spawn
/// moves by being deleted and re-added.</summary>
/// <param name="Team">The team that spawns here, by team id.</param>
/// <param name="Kit">The kit given on spawning.</param>
/// <param name="Yaw">The direction the player faces, in degrees.</param>
public sealed record SpawnUpdateRequest(
    string? Team = null,
    string? Kit = null,
    double? Yaw = null);

/// <summary>Where an observer enters — the map's single <c>&lt;default&gt;</c> spawn, which belongs to no
/// team and is replaced whole by every call.</summary>
/// <param name="RegionId">The region observers spawn in, which must already exist.</param>
/// <param name="Kit">The kit given on spawning. Absent means none.</param>
/// <param name="Yaw">The direction the observer faces, in degrees. Absent means 0.</param>
public sealed record ObserverSpawnRequest(
    [property: JsonPropertyName("region_id")] string RegionId,
    string? Kit = null,
    double? Yaw = null);

/// <summary>Add a team.</summary>
/// <param name="Id">The name every spawn and objective refers to the team by. Required, and an id already in
/// use is refused as <c>RQ5</c>.</param>
/// <param name="Name">What players see. Absent means the id.</param>
/// <param name="Color">The team's colour. Absent means <c>red</c>.</param>
/// <param name="DyeColor">The wool colour the team's kit dyes to, where it differs from
/// <see cref="Color"/>.</param>
/// <param name="MaxPlayers">How many may join. Absent means 20.</param>
/// <param name="MinPlayers">How many are needed to start. Absent means 0.</param>
public sealed record TeamCreateRequest(
    string Id,
    string? Name = null,
    string? Color = null,
    [property: JsonPropertyName("dye_color")] string? DyeColor = null,
    [property: JsonPropertyName("max_players")] int? MaxPlayers = null,
    [property: JsonPropertyName("min_players")] int? MinPlayers = null);

/// <summary>Change a team already added.</summary>
/// <param name="Id">A new id. The rename cascades to every spawn naming the team. An id already in use is
/// refused as <c>RQ5</c>.</param>
/// <param name="Name">What players see.</param>
/// <param name="Color">The team's colour.</param>
/// <param name="DyeColor">The wool colour the team's kit dyes to.</param>
/// <param name="MaxPlayers">How many may join.</param>
/// <param name="MinPlayers">How many are needed to start.</param>
public sealed record TeamUpdateRequest(
    string? Id = null,
    string? Name = null,
    string? Color = null,
    [property: JsonPropertyName("dye_color")] string? DyeColor = null,
    [property: JsonPropertyName("max_players")] int? MaxPlayers = null,
    [property: JsonPropertyName("min_players")] int? MinPlayers = null);

/// <summary>Add a wool objective. A wool is named by its colour, so that is the whole body and a colour
/// already on the map is refused as <c>RQ5</c>.</summary>
/// <param name="Color">One of the sixteen wool colours, as the contract spells them —
/// <c>light_blue</c>, <c>silver</c> and the rest. Absent means <c>white</c>; anything outside the sixteen is
/// refused as <c>RQ1</c>.</param>
public sealed record WoolCreateRequest(string? Color = null);

/// <summary>Change a wool already added.</summary>
/// <param name="Color">A new colour, which renames the wool and every monument on it. One already on the map
/// is refused as <c>RQ5</c>.</param>
/// <param name="Team">The team that must capture this wool, by team id. <c>null</c> or empty clears it, and
/// the studio then infers it where exactly one team is left uncovered.</param>
/// <param name="Location">Where the wool is dispensed from, in block coordinates. <c>null</c> clears
/// it.</param>
/// <param name="WoolRoomRegion">The region holding the wool, by region id. <c>null</c> or empty clears
/// it.</param>
/// <summary>A position in the world, in block coordinates. Fractional values are ordinary and are not
/// rounded here: PGM floors a wool's <c>&lt;location&gt;</c> itself and never a monument's <c>&lt;block&gt;</c>,
/// so a coordinate is carried as written and the contract decides what happens to it.</summary>
/// <param name="X">East–west.</param>
/// <param name="Y">Height.</param>
/// <param name="Z">North–south.</param>
public sealed record LocationDto(double X, double Y, double Z);

public sealed record WoolUpdateRequest(
    string? Color = null,
    string? Team = null,
    LocationDto? Location = null,
    [property: JsonPropertyName("wool_room_region")] string? WoolRoomRegion = null);

/// <summary>A monument — where a wool is placed to score it. The same shape adds one and changes one, and
/// the two readings of an absent field are the general rule: on the add it takes the default, on the change
/// it leaves the monument as it was.</summary>
/// <param name="Team">Whose monument this is, by team id. A second monument for a team already carrying one
/// on this wool is refused as <c>RQ5</c>. Absent, on the add, means none.</param>
/// <param name="Location">Where the wool is placed, in block coordinates. <c>null</c> clears it.</param>
/// <param name="MonumentRegion">The region the monument stands in, by region id. <c>null</c> or empty clears
/// it.</param>
public sealed record MonumentWriteRequest(
    string? Team = null,
    LocationDto? Location = null,
    [property: JsonPropertyName("monument_region")] string? MonumentRegion = null);

/// <summary>What the map is called and who it is credited to. Every field is optional and only the stated
/// ones are written, so a caller changing one column sends one key.</summary>
/// <param name="Name">The map's title.</param>
/// <param name="Version">The map's version string, as the contract carries it. Empty clears it.</param>
/// <param name="Objective">The one-line description of what the map is played for. Empty clears it.</param>
/// <param name="MaxBuildHeight">The height players may build to. <c>null</c> clears it.</param>
/// <param name="Authors">Who the map is credited to — a <b>full replace</b>, so stating this at all states
/// all of them. A person is an account or a pseudonym: an entry carrying a <c>uuid</c>, a <c>name</c>, or a
/// plain string in place of the object is kept, and one carrying neither is skipped.</param>
public sealed record MapMetadataRequest(
    string? Name = null,
    string? Version = null,
    string? Objective = null,
    [property: JsonPropertyName("max_build_height")] double? MaxBuildHeight = null,
    IReadOnlyList<MapAuthorDto>? Authors = null);
