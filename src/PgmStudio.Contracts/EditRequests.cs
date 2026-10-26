using System.Text.Json.Serialization;
using PgmStudio.Vocabulary;

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
/// <remarks>The region a caller draws. Its numbers travel nested under <c>coords</c>, the same envelope a
/// patch sends, so a caller that has learned one route can write the other.</remarks>
/// <param name="Type">One of <c>rectangle</c>, <c>cuboid</c>, <c>point</c>, <c>block</c>, <c>cylinder</c>,
/// <c>circle</c>; anything else is refused. Absent means <c>rectangle</c>.</param>
/// <param name="Id">The name every later route calls this region by. Absent means the editor numbers one
/// from the type — <c>region_1</c>, <c>cylinder_2</c> — and an id already in use is refused as
/// <c>RQ5</c>.</param>
/// <param name="Category">Which of the editor's region groups this one is filed under. Absent means
/// <c>other</c>.</param>
/// <param name="DraftStep">Marks the region as drawn but not yet wired; <c>/regions/tree</c> echoes it back
/// while the region's derived category is still <c>other</c>. Absent means it is wired.</param>
/// <param name="Coords">The type's own numbers. Which are read is <see cref="Type"/>, and a number the
/// chosen type requires and <c>coords</c> omits is refused as <c>RQ1</c>; an absent <c>coords</c> is the
/// same refusal.</param>
public sealed record RegionCreateRequest(
    string? Type = null,
    string? Id = null,
    string? Category = null,
    [property: JsonPropertyName("draft_step")] string? DraftStep = null,
    RegionCoordsDto? Coords = null);

/// <summary>The numbers a region is drawn from, under the key that sets each one. Which are read is the
/// region's type — a <c>rectangle</c> takes the four <c>min</c>/<c>max</c> footprint numbers, a
/// <c>cuboid</c> those plus <see cref="MinY"/> and <see cref="MaxY"/>, a <c>point</c> or <c>block</c>
/// <see cref="X"/>, <see cref="Y"/> and <see cref="Z"/>, a <c>cylinder</c> <see cref="BaseX"/>,
/// <see cref="BaseY"/>, <see cref="BaseZ"/>, <see cref="Radius"/> and <see cref="Height"/>, a <c>circle</c>
/// <see cref="CenterX"/>, <see cref="CenterZ"/> and <see cref="Radius"/> — and a number the type does not
/// use is accepted and ignored. On a create the type is the request's; on a patch it is the stored
/// region's, and every field is optional because a patch sets only what it names.</summary>
/// <param name="MinX">A rectangle's or cuboid's west edge. On a create, required for both.</param>
/// <param name="MinY">A cuboid's floor. On a create, absent means 0.</param>
/// <param name="MinZ">A rectangle's or cuboid's north edge. On a create, required for both.</param>
/// <param name="MaxX">A rectangle's or cuboid's east edge. On a create, required for both.</param>
/// <param name="MaxY">A cuboid's ceiling. On a create, absent means 256.</param>
/// <param name="MaxZ">A rectangle's or cuboid's south edge. On a create, required for both.</param>
/// <param name="X">A point's or block's east–west position. A <c>block</c> is rounded to whole blocks; a
/// <c>point</c> keeps what was written, so a spawn holds its <c>.5</c> block centre.</param>
/// <param name="Y">A point's or block's height. On a create, absent means 64.</param>
/// <param name="Z">A point's or block's north–south position.</param>
/// <param name="BaseX">A cylinder's centre, east–west.</param>
/// <param name="BaseY">A cylinder's floor. On a create, absent means 64.</param>
/// <param name="BaseZ">A cylinder's centre, north–south.</param>
/// <param name="CenterX">A circle's centre, east–west.</param>
/// <param name="CenterZ">A circle's centre, north–south.</param>
/// <param name="Radius">A cylinder's or circle's radius, in blocks.</param>
/// <param name="Height">A cylinder's height, in blocks. On a create, absent means 10.</param>
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

/// <summary>Rename a region or move it, by naming the numbers that change — one of the two, and a body
/// carrying neither is refused as <c>RQ1</c> before the region is even looked up.</summary>
/// <param name="Id">A new name. The rename cascades: the region groups, every compound listing it as a
/// child, and any spawn or wool room pointing at it follow. A name already in use is refused as
/// <c>RQ5</c>.</param>
/// <param name="Coords">The numbers to set, under the same keys a create sends. The stored region's type
/// chooses which are read; the footprint the region now covers is answered back.</param>
public sealed record RegionPatchRequest(
    string? Id = null,
    RegionCoordsDto? Coords = null);

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

/// <summary>A position in the world, in block coordinates. Fractional values are ordinary and are not
/// rounded here: PGM floors a wool's <c>&lt;location&gt;</c> itself and never a monument's <c>&lt;block&gt;</c>,
/// so a coordinate is carried as written and the contract decides what happens to it.</summary>
/// <param name="X">East–west.</param>
/// <param name="Y">Height.</param>
/// <param name="Z">North–south.</param>
public sealed record LocationDto(double X, double Y, double Z);

/// <summary>Change a wool already added.</summary>
/// <param name="Color">A new colour, which renames the wool and every monument on it. One already on the map
/// is refused as <c>RQ5</c>.</param>
/// <param name="Team">The team that must capture this wool, by team id. <c>null</c> or empty clears it, and
/// the studio then infers it where exactly one team is left uncovered.</param>
/// <param name="Location">Where the wool is dispensed from, in block coordinates. <c>null</c> clears
/// it.</param>
/// <param name="WoolRoomRegion">The region holding the wool, by region id. <c>null</c> or empty clears
/// it.</param>
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

/// <summary>Move or turn one play piece — a spawn, a wool, a destroyable or a core, named by the path's
/// <c>id</c>. A body states the four bounds of a placed rectangle, which moves the piece there, or a
/// <c>turn</c>, which turns a room in place; never both. Every orbit image of the piece moves or turns with it
/// unless the body states <c>alone</c>.</summary>
/// <param name="Part">Absent for the piece itself — a room's region, which carries the room seated on it, or
/// an objective's structure — the room's own region word (<c>spawn</c>, <c>woolRoom</c>) for the same, or
/// <c>building</c> for the house raised on a room's region, which moves and turns whole; or <c>start</c> or
/// <c>end</c> for one end of an approach wall, which moves alone and leaves the other where it stands.</param>
/// <param name="MinX">The placed rectangle's west edge, in world blocks. The four are the same bounds the
/// piece's own rectangle states — a room's <c>protection</c>, the wing of its house <c>wing</c> names, a goal's structure from its
/// first block's edge to past its last, the rectangle <c>GET …/intent/pieces</c> answers for any piece — and together they <b>move and do not resize</b>: a span differing
/// from the one they replace is refused as <c>RQ1</c>. A wall's end is one block corner, all four bounds on it.</param>
/// <param name="MinZ">Its north edge.</param>
/// <param name="MaxX">Its east edge.</param>
/// <param name="MaxZ">Its south edge.</param>
/// <param name="Turn">Quarter turns clockwise seen from above, about the middle of the room's region — or of its
/// building, with <c>part</c> <c>building</c> — or of a build area or water lane. Negative turns the other way.
/// Any other piece states no facing and is refused one.</param>
/// <param name="Alone">Move or turn the named image without the other images of its authored unit.</param>
/// <param name="Wing">With <c>part</c> <c>building</c>, which wing of the house the placed rectangle is, counted
/// from 0 — the room; the whole house moves with it.</param>
public sealed record PieceMoveRequest(
    [property: WordSet(typeof(PieceParts))] string? Part = null,
    [property: JsonPropertyName("minX")] double? MinX = null,
    [property: JsonPropertyName("minZ")] double? MinZ = null,
    [property: JsonPropertyName("maxX")] double? MaxX = null,
    [property: JsonPropertyName("maxZ")] double? MaxZ = null,
    int? Turn = null,
    bool Alone = false,
    int Wing = 0);

/// <summary>One play piece where the sketch draws it — the answer of <c>GET /map/{slug}/intent/pieces</c>, one
/// per spawn, wool room, destroyable, core, control point, build area, water lane, iron cube, approach wall and the
/// observer, the images the map's symmetry draws included.</summary>
/// <param name="Id">The piece's id, which <c>PATCH …/intent/pieces/{id}</c> moves it by.</param>
/// <param name="Kind">What the piece is.</param>
/// <param name="Color">The team or dye colour it is drawn in, or empty for a piece that belongs to no team.</param>
/// <param name="MinX">Its west edge, in world blocks: the rectangle a move of it states.</param>
/// <param name="MinZ">Its north edge.</param>
/// <param name="MaxX">Its east edge.</param>
/// <param name="MaxZ">Its south edge.</param>
/// <param name="Carried">True where the sketch does not pick the piece on its own: an image the symmetry draws
/// that the intent does not store, an image of an orbit other than its authored one, or an iron cube seated in a
/// spawn room. A move of an image still names its id and moves the whole orbit.</param>
/// <param name="Line">A wall's two ends, <c>[x1, z1, x2, z2]</c> on block corners; absent for every other piece.</param>
/// <param name="Shape">A control point's pad, a square or a disc; absent for every other piece.</param>
public sealed record PlayPieceDto(
    string Id,
    [property: WordSet(typeof(PlayPieceKinds))] string Kind,
    string Color,
    [property: JsonPropertyName("minX")] double MinX,
    [property: JsonPropertyName("minZ")] double MinZ,
    [property: JsonPropertyName("maxX")] double MaxX,
    [property: JsonPropertyName("maxZ")] double MaxZ,
    bool Carried,
    int[]? Line = null,
    [property: WordSet(typeof(ControlPointShapes))] string? Shape = null);

/// <summary>A play piece to add — the body of <c>POST /map/{slug}/intent/pieces</c>. The piece arrives with every
/// image the map's symmetry draws of it.</summary>
/// <param name="Kind">What to add.</param>
/// <param name="MinX">The placed rectangle's west edge, in world blocks: a room's region or an area for a spawn, a
/// wool room, a build area and a water lane; the block clicked for a destroyable, a core, a control point, an iron
/// cube, a defence chest and the observer, which stand centred on it.</param>
/// <param name="MinZ">Its north edge.</param>
/// <param name="MaxX">Its east edge.</param>
/// <param name="MaxZ">Its south edge.</param>
/// <param name="Team">The team the authored image belongs to; absent takes the first in orbit order.</param>
/// <param name="Ends">A wall's two ends, <c>[x1, z1, x2, z2]</c> on block corners, which a wall states in place
/// of the rectangle.</param>
public sealed record PieceAddRequest(
    [property: WordSet(typeof(PlayPieceKinds))] string Kind,
    [property: JsonPropertyName("minX")] double? MinX = null,
    [property: JsonPropertyName("minZ")] double? MinZ = null,
    [property: JsonPropertyName("maxX")] double? MaxX = null,
    [property: JsonPropertyName("maxZ")] double? MaxZ = null,
    string? Team = null,
    int[]? Ends = null);

/// <summary>A room's house, whole — the body of <c>PUT /map/{slug}/intent/pieces/{id}/house</c>.</summary>
/// <param name="House">The house: its wings, the room first, and the style it states, absent for the map's.</param>
/// <param name="Alone">Set it on the named image only, rather than on every image of its unit.</param>
public sealed record PieceHouseRequest(System.Text.Json.JsonElement House, bool Alone = false);

/// <summary>A room's doors, whole — the body of <c>PUT /map/{slug}/intent/pieces/{id}/doors</c>.</summary>
/// <param name="Doors">The doors, in the order they are cut — the first is the one a spawn's team walks out of.</param>
/// <param name="Alone">Set them on the named image only, rather than on every image of its unit.</param>
public sealed record PieceDoorsRequest(List<PieceDoor> Doors, bool Alone = false);

/// <summary>One door of a room: the way the wall it is cut in faces, and the point on that wall it is centred on,
/// or no point for a door centred on its wall.</summary>
/// <param name="Edge">The way the wall faces.</param>
/// <param name="X">The point's x, on the wall's outer edge, or absent for a door centred on its wall.</param>
/// <param name="Z">The point's z, stated with <paramref name="X"/>.</param>
public sealed record PieceDoor(
    [property: WordSet(typeof(EdgeWords))] string Edge,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? X = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Z = null);

/// <summary>The doors a room is built with — the answer of <c>GET /map/{slug}/intent/pieces/{id}/doors</c>.</summary>
/// <param name="Doors">The doors, in the order they are cut.</param>
/// <param name="Stated">Whether the room states them; a room stating none is built with the one its ground gives
/// it, which these are.</param>
public sealed record RoomDoorsDto(List<PieceDoor> Doors, bool Stated);

/// <summary>One setting of a play piece — the body of <c>PATCH /map/{slug}/intent/pieces/{id}/settings</c>: a
/// goal's name, design, material, lava, float or leak, a wool's colour, or a spawn's facing.</summary>
/// <param name="Key">Which setting.</param>
/// <param name="Text">What a name, a colour, a design or a material becomes.</param>
/// <param name="Number">What a float, a leak or a lava size becomes, or the quarter turns clockwise a facing
/// turns.</param>
/// <param name="Flag">Whether a core's lava is open to its rim, for <c>openTop</c>.</param>
/// <param name="Alone">Set it on the named image only, where it is one every image of the unit shares.</param>
/// <remarks>A body stating none of <c>text</c>, <c>number</c> and <c>flag</c> puts the setting back to its
/// default.</remarks>
public sealed record PieceSettingRequest(
    [property: WordSet(typeof(PieceSettings))] string Key,
    string? Text = null,
    int? Number = null,
    bool? Flag = null,
    bool Alone = false);
