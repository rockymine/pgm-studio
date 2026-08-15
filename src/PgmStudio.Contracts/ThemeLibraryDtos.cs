namespace PgmStudio.Contracts;

/// <summary>One reusable terrain-paint style (GET /api/styles) — a named material recipe. <paramref name="Kind"/>
/// is the material discriminator (<see cref="MaterialKind"/>); <paramref name="Params"/> is the serialized
/// <c>TerrainMaterial</c> the painter reads. <paramref name="Preview"/> is the card picture of that material,
/// rendered through the real painter: a library is browsed by what its entries look like, so the picture travels
/// with the row rather than costing one request per card.</summary>
public sealed record StyleDto(long Id, string Name, string Kind, string Params, string Preview);

/// <summary>Create/update a style (POST /api/styles, PUT /api/styles/{id}).</summary>
public sealed record StyleSaveRequest(string Name, string Kind, string Params);

/// <summary>One bucket binding of a theme (<see cref="ThemeBuckets"/>): the style that fills it, and the
/// bucket's depth (rim/surface) and toggle. <paramref name="StyleId"/> 0 binds no style — the bucket keeps the
/// built-in material and the binding carries only its depth and its toggle, which is how a theme says "no
/// rim" without first being made to choose a rim material.</summary>
public sealed record ThemeBucketDto(string Bucket, long StyleId, int Depth, bool Enabled);

/// <summary>One row in the theme library list (GET /api/themes), with the sample plateau the theme finishes —
/// the same reason <see cref="StyleDto.Preview"/> travels with a style.</summary>
public sealed record ThemeSummary(long Id, string Name, string Preview);

/// <summary>A full theme (GET /api/themes/{id}) — the geometry knobs plus a style binding per bucket. The
/// painter-ready JSON is served separately at GET /api/themes/{id}/json (assembled through the styles).</summary>
public sealed record ThemeDetail(
    long Id, string Name,
    bool BedrockRelative, int BedrockValue, string RimEdges, bool WallOnTerrainFaces,
    IReadOnlyList<ThemeBucketDto> Buckets);

/// <summary>Create or replace a theme built from existing styles (POST /api/themes, PUT /api/themes/{id}): the
/// knobs plus the bucket→style bindings.</summary>
public sealed record ThemeSaveRequest(
    string Name,
    bool BedrockRelative, int BedrockValue, string RimEdges, bool WallOnTerrainFaces,
    IReadOnlyList<ThemeBucketDto> Buckets);

/// <summary>Import a whole theme JSON into the library (POST /api/themes/import): the painter's theme JSON is
/// decomposed into one style per bucket + a composed theme. The response id is the new theme.
///
/// <para><paramref name="Name"/> is optional and the endpoint names an unnamed import "Imported theme"; it is
/// declared nullable because that is what it is. A DTO that claims a field is required when the handler
/// happily defaults it is a claim <see cref="Api.Endpoints.RequiredFields"/> now enforces, and the annotation
/// is the only thing that says which fields those are.</para></summary>
public sealed record ThemeImportRequest(string? Name, string ThemeJson);

/// <summary>Both views of one material (POST /api/terrain/material-preview): <paramref name="Plan"/> is one
/// course seen from above — where a voronoi, a noise field and a wall run vary — and <paramref name="Section"/>
/// is one row of columns cut open downward, the axis a layer stack varies along.</summary>
public sealed record MaterialPreviewDto(string Plan, string Section,
    IReadOnlyList<FindingDto>? Warnings = null);

/// <summary>A theme previewed (POST /api/terrain/theme-preview): <paramref name="Section"/> is a sample plateau
/// painted with the theme and cut open — the buckets in their geometry — and <paramref name="Buckets"/> holds one
/// top-down swatch per themeable bucket, keyed by <see cref="ThemeBuckets"/>.</summary>
public sealed record ThemePreviewDto(string Section, IReadOnlyDictionary<string, string> Buckets,
    IReadOnlyList<FindingDto>? Warnings = null);

/// <summary>Why a style could not be forgotten (DELETE /api/styles/{id}, 409): the themes and room styles still
/// binding it. A style is shared, so the refusal names what would break instead of surfacing a foreign-key
/// error.</summary>
public sealed record StyleInUseDto(string Error, IReadOnlyList<string> Themes);

// ── room styles ───────────────────────────────────────────────────────────────
// A room style is composed the way a theme is — library styles bound to the parts of a thing — but a part takes
// an ordered *stack* of them rather than one, because that is what a shell's floor, wall and roof are.

/// <summary>One course of a room style's part: which <see cref="RoomParts"/> it belongs to, where it sits in
/// that part's stack (0 = the course nearest the part's own base), the style it resolves through, and how many
/// courses it runs.</summary>
public sealed record RoomCourseDto(string Part, int Ordinal, long StyleId, int Height);

/// <summary>One row in the room-style library list (GET /api/room-styles), with the shell it stamps.</summary>
public sealed record RoomStyleSummary(long Id, string Name, string Preview);

/// <summary>The windows a room style cuts through its walls (<see cref="WindowForms"/>). <paramref name="Block"/>
/// is a block id rather than a bound style because the metadata is <b>geometry</b> here — which way a stair
/// climbs, which half a slab fills — and a material resolves its own data from where the cell sits, which would
/// turn every stair in a wall the same way. <paramref name="Data"/> carries only the block's variant (which
/// wood, which dye); the stamper adds the geometry bits.</summary>
/// <param name="HostBlock">The block a window may be cut <em>into</em>, or -1 to cut wherever one fits. On a
/// banded wall a seat chosen by spacing lands half in one band and half in the next; naming the block the wall
/// has to resolve to lets the panel decide instead.</param>
public sealed record RoomWindowDto(
    string Form, int Block, int Data, int Sill, int Width, int Height, int Spacing,
    int HostBlock = -1, int HostData = 0);

/// <summary>The log ends that run out past the corners where two storeys meet — two per corner, one along each
/// axis. <paramref name="Block"/> -1 is a building whose storeys meet without them.</summary>
public sealed record RoomBeamDto(int Block, int Data, int Reach);

/// <summary>The beam over a doorway: stairs in the two corners of its top course, and what spans the middle of
/// a wider one. <paramref name="Form"/> <c>none</c> leaves the opening a plain rectangle.</summary>
public sealed record RoomDoorHeadDto(string Form, int Block, string Fill, int FillBlock, int FillData);

/// <summary>The porch a room style gives a strip of its footprint up for, or absent for a building whose walls
/// stand on the whole of it. <paramref name="RailBlock"/> 0 leaves the deck open to step off anywhere.</summary>
public sealed record RoomPorchDto(int Depth, int Inset, string Edge, string Roof, int RailBlock);

// ── the parts a house is composed from (B71) ─────────────────────────────────────────────────────────
// A roof, a storey and a porch are each their own library row for the reason a style is: the level exists so
// a part can be authored once and reused, which a knob living on the house cannot be. Each carries the knobs
// of its part and — where the part has materials of its own — that part's course stacks, in the same
// RoomCourseDto shape the house uses, so one editor draws all of them.

/// <summary>One row in the roof library, with the roof it stamps.</summary>
public sealed record RoofStyleSummary(long Id, string Name, string Preview);

/// <summary>A roof style: everything above the eave. Its courses are the <c>roof</c>, <c>verge</c> and
/// <c>gable</c> parts.</summary>
public sealed record RoofStyleDetail(
    long Id, string Name, string Form, int Thickness, int Pitch, int Overhang, bool RoofHole, bool RidgeCap,
    IReadOnlyList<RoomCourseDto> Courses);

public sealed record RoofStyleSaveRequest(
    string Name, string Form, int Thickness, int Pitch, int Overhang, bool RoofHole, bool RidgeCap,
    IReadOnlyList<RoomCourseDto> Courses);

/// <summary>One row in the storey library, with the room it stamps. <paramref name="Clear"/> rides along
/// because a house binding a stack of these has to say how tall the stack comes out, and asking the server
/// per keystroke for a number it already sent would be a round trip for an integer.</summary>
public sealed record StoreyStyleSummary(long Id, string Name, int Clear, string Preview);

/// <summary>A storey style: one room. <paramref name="Clear"/> is the air a player stands in — never under
/// three, because a room has to be stood up in — and the courses are the <c>wall</c>, <c>post</c> and the
/// three floor zones.</summary>
public sealed record StoreyStyleDetail(
    long Id, string Name, int Clear, int BorderWidth, int InlayInset, RoomWindowDto Windows,
    IReadOnlyList<RoomCourseDto> Courses);

public sealed record StoreyStyleSaveRequest(
    string Name, int Clear, int BorderWidth, int InlayInset, RoomWindowDto Windows,
    IReadOnlyList<RoomCourseDto> Courses);

/// <summary>One row in the porch library, with the porch it stamps.</summary>
public sealed record PorchStyleSummary(long Id, string Name, string Preview);

/// <summary>A porch style: the strip of footprint the walls give up, and what stands on it. No courses — a
/// porch's deck is the house's floor and its canopy the roof's material, so what is left to it is its
/// shape.</summary>
public sealed record PorchStyleDetail(
    long Id, string Name, int Depth, int Inset, string Edge, string Roof, int RailBlock);

public sealed record PorchStyleSaveRequest(
    string Name, int Depth, int Inset, string Edge, string Roof, int RailBlock);

/// <summary>One storey of a house: which storey style fills it, and the clear it takes <em>here</em> —
/// 0 for the storey style's own. The position in the list is the position in the building, ground first, so
/// there is no ordinal on the wire: reordering the list is reordering the house.</summary>
public sealed record RoomStoreyDto(long StoreyStyleId, int Clear);

/// <summary>A full room style (GET /api/room-styles/{id}) — the per-part extents and knobs plus the courses
/// bound to each part. A part with no courses keeps the built-in finish, the way an unbound theme bucket
/// does.</summary>
public sealed record RoomStyleDetail(
    long Id, string Name,
    int FloorDepth, int WallHeight, int RoofThickness,
    string RoofForm, int Pitch, int Overhang, bool RoofHole, bool RidgeCap,
    int BorderWidth, int InlayInset,
    int Storeys, int StoreyClear,
    RoomWindowDto Windows, RoomPorchDto? Porch,
    string Door, int DoorHeight,
    long? RoofStyleId, long? PorchStyleId, IReadOnlyList<RoomStoreyDto> StoreyStack,
    IReadOnlyList<RoomCourseDto> Courses,
    // Trailing and defaulted so every existing construction site keeps compiling and keeps meaning "this
    // building has none" — which is what every stored style already was.
    RoomBeamDto? Beams = null, int RoofSlab = -1, int RoofSlabData = 0,
    RoomWindowDto? GableWindows = null, RoomDoorHeadDto? DoorHead = null);

/// <summary>Create or replace a room style (POST /api/room-styles, PUT /api/room-styles/{id}).</summary>
public sealed record RoomStyleSaveRequest(
    string Name,
    int FloorDepth, int WallHeight, int RoofThickness,
    string RoofForm, int Pitch, int Overhang, bool RoofHole, bool RidgeCap,
    int BorderWidth, int InlayInset,
    int Storeys, int StoreyClear,
    RoomWindowDto Windows, RoomPorchDto? Porch,
    string Door, int DoorHeight,
    long? RoofStyleId, long? PorchStyleId, IReadOnlyList<RoomStoreyDto> StoreyStack,
    IReadOnlyList<RoomCourseDto> Courses,
    // Trailing and defaulted so every existing construction site keeps compiling and keeps meaning "this
    // building has none" — which is what every stored style already was.
    RoomBeamDto? Beams = null, int RoofSlab = -1, int RoofSlabData = 0,
    RoomWindowDto? GableWindows = null, RoomDoorHeadDto? DoorHead = null);

/// <summary>The four pictures of a room style: from above, projected onto its front, in isometric, and one
/// plane drawn at the scale of the pieces in it. A library <em>card</em> carries the section alone — the
/// isometric is tens of kilobytes, which is nothing for the one style an editor has open and megabytes for a
/// grid of them.</summary>
public sealed record RoomStylePreviewDto(string Plan, string Section, string Iso, string Cutaway,
    IReadOnlyList<FindingDto>? Warnings = null);

/// <summary>One field of a material kind (<c>GET /api/terrain/patterns</c>), as the painter's deserializer
/// will accept it. <paramref name="Type"/> is a wire type word — <c>int</c>, <c>bool</c>, <c>material</c>,
/// <c>material[]</c>, <c>band[]</c>, <c>stripe[]</c>, <c>bandStack</c>, or an enum's name, in which case
/// <paramref name="Choices"/> holds its values. A field with no <paramref name="Default"/> is
/// <paramref name="Required"/> and has to be written.</summary>
public sealed record MaterialFieldDto(
    string Name, string Type, bool Required, object? Default, IReadOnlyList<string>? Choices);

/// <summary>One material kind (<c>GET /api/terrain/patterns</c>): the <c>kind</c> discriminator a theme tags
/// it with, the label a picker offers it under, a sentence on what it draws, and its fields.
/// <para><paramref name="Reads"/> is which facts about a cell it varies with — <c>position</c>, <c>depth</c>,
/// <c>inset</c>, <c>arc</c>, <c>bend</c>, <c>height</c>, <c>team</c> — and therefore where it is legible: a
/// kind reading <c>arc</c> says nothing away from a perimeter, and one reading <c>inset</c> draws rings and
/// falls back off a footprint.</para></summary>
public sealed record MaterialKindDto(
    string Kind, string Name, string Summary,
    IReadOnlyList<string> Reads, IReadOnlyList<MaterialFieldDto> Fields);
