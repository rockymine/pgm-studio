
using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>One reusable terrain-paint style (GET /api/styles) — a named material recipe, as the save
/// request plus the row's id, which a theme binds it by, and its card picture. The preview is rendered
/// through the real painter: a library is browsed by what its entries look like, so the picture travels with
/// the row rather than costing one request per card.</summary>
public sealed record StyleDto(long Id, string Name, string Kind, string Params, string Preview)
    : StyleSaveRequest(Name, Kind, Params);

/// <summary>Create/update a style (POST /api/styles, PUT /api/styles/{id}).</summary>
/// <param name="Name">What the library lists it under. It is the author's word, not a key — two styles may
/// share one, and a theme binds a style by id.</param>
/// <param name="Kind">Which material this is, from <see cref="MaterialKind"/>. It decides which fields
/// <paramref name="Params"/> has to carry, and <c>GET /api/terrain/patterns</c> answers them per kind.</param>
/// <param name="Params">The serialized <c>TerrainMaterial</c> the painter reads, as JSON text rather than an
/// object: the material hierarchy is fourteen shapes deep and a wire type restating it would be a second
/// copy free to disagree with the deserializer. A body the painter cannot read is refused as
/// <c>HS1</c>.</param>
public record StyleSaveRequest(string Name, [property: WordSet(typeof(MaterialKind))] string Kind, string Params);

/// <summary>One bucket binding of a theme (<see cref="ThemeBuckets"/>): the style that fills it, and the
/// bucket's depth (rim/surface) and toggle. <paramref name="StyleId"/> 0 binds no style — the bucket keeps the
/// built-in material and the binding carries only its depth and its toggle, which is how a theme says "no
/// rim" without first being made to choose a rim material.</summary>
/// <param name="Bucket">Which part of the ground this binding fills.</param>
/// <param name="StyleId">The style that fills it, or <c>0</c> to bind none — which keeps the built-in
/// material and is how a theme says "no rim" without first being made to choose a rim material.</param>
/// <param name="Depth">How many top courses the bucket claims, where it claims a configurable number: the
/// rim and the surface do, the wall's depth is the riser it finds, and the fill takes what is left.</param>
/// <param name="Enabled">Whether the bucket is painted at all.</param>
public sealed record ThemeBucketDto(
    [property: WordSet(typeof(ThemeBuckets))] string Bucket, long StyleId, int Depth, bool Enabled);

/// <summary>One row in the theme library list (GET /api/themes), with the sample plateau the theme finishes —
/// the same reason <see cref="StyleDto.Preview"/> travels with a style.</summary>
/// <param name="Id">The row number every later route names it by.</param>
/// <param name="Name">What the library lists it under.</param>
/// <param name="Preview">The card picture, drawn through the same code the export runs, so a library is
/// browsed by what its entries look like.</param>
public sealed record ThemeSummary(long Id, string Name, string Preview);

/// <summary>A full theme (GET /api/themes/{id}): the save request plus the row's id, since an answer that
/// restated the same fields would be one shape twice. The painter-ready JSON is served separately at
/// GET /api/themes/{id}/json (assembled through the styles). The id is the row number a sketch pulls it in
/// by; every other field is the save request's and is documented there.</summary>
public sealed record ThemeDetail(
    long Id, string Name,
    bool BedrockRelative, int BedrockValue, string RimEdges, bool WallOnTerrainFaces,
    IReadOnlyList<ThemeBucketDto> Buckets)
    : ThemeSaveRequest(Name, BedrockRelative, BedrockValue, RimEdges, WallOnTerrainFaces, Buckets);

/// <summary>Create or replace a theme built from existing styles (POST /api/themes, PUT /api/themes/{id}): the
/// knobs plus the bucket→style bindings.</summary>
/// <param name="Name">What the library lists it under.</param>
/// <param name="BedrockRelative">Whether <paramref name="BedrockValue"/> counts up from the world floor
/// rather than naming an absolute Y.</param>
/// <param name="BedrockValue">Where the bedrock plate sits — the depth the fill runs down to.</param>
/// <param name="RimEdges">Which edges the rim caps, from <see cref="RimEdgeModes"/>: only where the ground
/// borders void, wherever it falls away, or every plateau boundary.</param>
/// <param name="WallOnTerrainFaces">Whether a face between two plateaus is painted with the wall bucket. Off,
/// only a face against a structure is.</param>
/// <param name="Buckets">One binding per bucket the theme fills. A bucket left out keeps the built-in
/// material, and a binding naming style <c>0</c> carries only its depth and its toggle.</param>
public record ThemeSaveRequest(
    string Name,
    bool BedrockRelative, int BedrockValue,
    [property: WordSet(typeof(RimEdgeModes))] string RimEdges, bool WallOnTerrainFaces,
    IReadOnlyList<ThemeBucketDto> Buckets);

/// <summary>Import a whole theme JSON into the library (POST /api/themes/import): the painter's theme JSON is
/// decomposed into one style per bucket + a composed theme. The response id is the new theme.
///
/// <para><paramref name="Name"/> is optional and the endpoint names an unnamed import "Imported theme"; it is
/// declared nullable because that is what it is. A DTO that claims a field is required when the handler
/// happily defaults it is a claim <c>Api.Endpoints.RequiredFields</c> enforces, and the annotation
/// is the only thing that says which fields those are.</para></summary>
/// <param name="Name">What the imported theme is listed under. Absent means <c>Imported theme</c>.</param>
/// <param name="ThemeJson">The painter's own theme JSON, as text — the form <c>GET /api/themes/{id}/json</c>
/// answers, so a theme round-trips between studios through these two routes.</param>
public sealed record ThemeImportRequest(string? Name, string ThemeJson);

/// <summary>Both views of one material (POST /api/terrain/material-preview): <paramref name="Plan"/> is one
/// course seen from above — where a voronoi, a noise field and a wall run vary — and <paramref name="Section"/>
/// is one row of columns cut open downward, the axis a layer stack varies along.</summary>
/// <param name="Plan">One course seen from above — where a voronoi, a noise field and a wall run vary.</param>
/// <param name="Section">One row of columns cut open downward — the axis a layer stack varies along.</param>
public sealed record MaterialPreviewDto(string Plan, string Section);

/// <summary>A theme previewed (POST /api/terrain/theme-preview): <paramref name="Section"/> is a sample plateau
/// painted with the theme and cut open — the buckets in their geometry — and <paramref name="Buckets"/> holds one
/// top-down swatch per themeable bucket, keyed by <see cref="ThemeBuckets"/>.</summary>
/// <param name="Section">The finished plateau, cut open downward.</param>
/// <param name="Buckets">One picture per bucket, keyed by bucket, so a binding is judged on its own as well
/// as in the whole.</param>
public sealed record ThemePreviewDto(string Section, IReadOnlyDictionary<string, string> Buckets);


// ── room styles ───────────────────────────────────────────────────────────────
// A room style is composed the way a theme is — library styles bound to the parts of a thing — but a part takes
// an ordered *stack* of them rather than one, because that is what a shell's floor, wall and roof are.

/// <summary>One course of a room style's part: which <see cref="RoomParts"/> it belongs to, where it sits in
/// that part's stack (0 = the course nearest the part's own base), the style it resolves through, and how many
/// courses it runs.</summary>
/// <param name="Part">Which part of the shell the course belongs to.</param>
/// <param name="Ordinal">Where it sits in that part's stack, 0 being the course nearest the part's own
/// base. Counting up from the base is what pins a band: a stripe written at the fourth course stays at the
/// fourth course when the wall grows.</param>
/// <param name="StyleId">The library style it resolves through.</param>
/// <param name="Height">How many courses it runs.</param>
public sealed record RoomCourseDto(
    [property: WordSet(typeof(RoomParts))] string Part, int Ordinal, long StyleId, int Height);

/// <summary>One row in the room-style library list (GET /api/room-styles), with the shell it stamps.</summary>
/// <param name="Id">The row number every later route names it by.</param>
/// <param name="Name">What the library lists it under.</param>
/// <param name="Preview">The card picture, drawn through the same code the export runs, so a library is
/// browsed by what its entries look like.</param>
public sealed record RoomStyleSummary(long Id, string Name, string Preview);

/// <summary>The windows a room style cuts through its walls (<see cref="WindowForms"/>). <paramref name="Block"/>
/// is a block id rather than a bound style because the metadata is <b>geometry</b> here — which way a stair
/// climbs, which half a slab fills — and a material resolves its own data from where the cell sits, which would
/// turn every stair in a wall the same way. <paramref name="Data"/> carries only the block's variant (which
/// wood, which dye); the stamper adds the geometry bits.</summary>
/// <param name="HostBlock">The block a window may be cut <em>into</em>, or -1 to cut wherever one fits. On a
/// banded wall a seat chosen by spacing lands half in one band and half in the next; naming the block the wall
/// has to resolve to lets the panel decide instead.</param>
/// <param name="Form">What the opening is filled with, or <c>none</c> for a blank wall.</param>
/// <param name="Block">The block the opening is built from.</param>
/// <param name="Data">That block's variant nibble — which wood, which dye. The geometry bits are the
/// stamper's.</param>
/// <param name="Sill">The course above the storey's own floor the opening starts at.</param>
/// <param name="Width">How wide each opening is cut, in blocks.</param>
/// <param name="Height">How tall each opening is cut.</param>
/// <param name="Spacing">Clear blocks of wall between one opening and the next.</param>
/// <param name="HostData">The variant nibble of that host block.</param>
public sealed record RoomWindowDto(
    [property: WordSet(typeof(WindowForms))] string Form,
    int Block, int Data, int Sill, int Width, int Height, int Spacing,
    int HostBlock = -1, int HostData = 0);

/// <summary>The log ends that run out past the corners where two storeys meet — two per corner, one along each
/// axis. <paramref name="Block"/> -1 is a building whose storeys meet without them.</summary>
/// <param name="Block">The block the log ends are cut from, or -1 for a building whose storeys meet without
/// them.</param>
/// <param name="Data">That block's variant nibble — which wood.</param>
/// <param name="Reach">How far each end runs out past the corner, in blocks.</param>
public sealed record RoomBeamDto(int Block, int Data, int Reach);

/// <summary>The beam over a doorway: stairs in the two corners of its top course, and what spans the middle of
/// a wider one. <paramref name="Form"/> <c>none</c> leaves the opening a plain rectangle.</summary>
/// <param name="Form">The beam's shape, or <c>none</c> to leave the opening a plain rectangle.</param>
/// <param name="Block">The block the two corner stairs are cut from.</param>
/// <param name="Fill">What spans the middle of a wider opening.</param>
/// <param name="FillBlock">The block that middle is built from.</param>
/// <param name="FillData">That block's variant nibble.</param>
public sealed record RoomDoorHeadDto(
    [property: WordSet(typeof(DoorHeadForms))] string Form, int Block,
    [property: WordSet(typeof(DoorHeadFills))] string Fill, int FillBlock, int FillData);

/// <summary>The porch a room style gives a strip of its footprint up for, or absent for a building whose walls
/// stand on the whole of it. <paramref name="RailBlock"/> 0 leaves the deck open to step off anywhere.</summary>
/// <param name="Depth">How deep a strip of footprint the walls give up. The room keeps at least three
/// blocks whatever is asked for.</param>
/// <param name="Inset">How far the deck stops short of each end of the wall it stands on. 0 runs it the
/// full width.</param>
/// <param name="Edge">Which wall it stands on. <c>front</c> follows the building's own front — the wall its
/// doorway is cut through — rather than naming a fixed side.</param>
/// <param name="Roof">The canopy's form.</param>
/// <param name="RailBlock">The block the rail is built from, or 0 for a deck left open to step off
/// anywhere.</param>
public sealed record RoomPorchDto(int Depth, int Inset,
    [property: WordSet(typeof(PorchEdges))] string Edge,
    [property: WordSet(typeof(RoofForms))] string Roof, int RailBlock);

// ── the parts a house is composed from (B71) ─────────────────────────────────────────────────────────
// A roof, a storey and a porch are each their own library row for the reason a style is: the level exists so
// a part can be authored once and reused, which a knob living on the house cannot be. Each carries the knobs
// of its part and — where the part has materials of its own — that part's course stacks, in the same
// RoomCourseDto shape the house uses, so one editor draws all of them.

/// <summary>One row in the roof library, with the roof it stamps.</summary>
/// <param name="Id">The row number every later route names it by.</param>
/// <param name="Name">What the library lists it under.</param>
/// <param name="Preview">The card picture, drawn through the same code the export runs, so a library is
/// browsed by what its entries look like.</param>
public sealed record RoofStyleSummary(long Id, string Name, string Preview);

/// <summary>A roof style: everything above the eave — the save request plus the row's id, which a room
/// style binds it by. Its courses are the <c>roof</c>, <c>verge</c> and <c>gable</c> parts.</summary>
public sealed record RoofStyleDetail(
    long Id, string Name, string Form,
    int Pitch, int Overhang, bool RoofHole, bool RidgeCap,
    IReadOnlyList<RoomCourseDto> Courses, int RoofSlab = -1, int RoofSlabData = 0)
    : RoofStyleSaveRequest(Name, Form, Pitch, Overhang, RoofHole, RidgeCap, Courses, RoofSlab, RoofSlabData);

/// <summary>Create or replace a roof style. <paramref name="RoofSlab"/> is the block a half-course rise steps
/// on every odd course, or -1 for a roof laid in whole blocks — the roof's own, since a roof style owns
/// everything above the eave, and the number the slab/pitch pairing is checked against.</summary>
/// <param name="Name">What the library lists it under.</param>
/// <param name="Form">The roof's shape, from <see cref="RoofForms"/>. A <c>shed</c> falls toward the
/// building's front, which is the wall its doorway is cut through.</param>
/// <param name="Pitch">Courses of rise per block travelled inward. 1 is 45°; on a slab roof the same 1 is
/// half as steep, because each course rises half a block.</param>
/// <param name="Overhang">How far the roof reaches past the walls, in blocks. 0 ends it flush and leaves the
/// wall to carry the weather; 1 is an eave.</param>
/// <param name="RoofHole">Whether a flat roof carries a centred hole — the light a windowless room otherwise
/// has none of. A gable has its own volume and never takes one.</param>
/// <param name="RidgeCap">Whether the line the slopes meet on is laid in the <c>verge</c> course rather than
/// the roof's own material. A flat lid has no ridge to cap.</param>
/// <param name="Courses">The material stacks for the parts a roof owns — <c>roof</c>, <c>verge</c> and
/// <c>gable</c>. A part with no courses keeps the built-in finish.</param>
/// <param name="RoofSlab">The block a half-course rise steps on every odd course, or -1 for a roof laid in
/// whole blocks. It is the number the slab/pitch pairing is checked against.</param>
/// <param name="RoofSlabData">That slab's variant nibble — which wood, which stone. Which half of the cube it
/// fills is the stamper's and is not stated here.</param>
public record RoofStyleSaveRequest(
    string Name, [property: WordSet(typeof(RoofForms))] string Form,
    int Pitch, int Overhang, bool RoofHole, bool RidgeCap,
    IReadOnlyList<RoomCourseDto> Courses, int RoofSlab = -1, int RoofSlabData = 0);

/// <summary>One row in the storey library, with the room it stamps. <paramref name="Clear"/> rides along
/// because a house binding a stack of these has to say how tall the stack comes out, and asking the server
/// per keystroke for a number it already sent would be a round trip for an integer.</summary>
/// <param name="Id">The row number every later route names it by.</param>
/// <param name="Name">What the library lists it under.</param>
/// <param name="Clear">The air a player stands in, carried on the row because a house binding a stack of
/// these has to say how tall the stack comes out.</param>
/// <param name="Preview">The card picture of the room it stamps.</param>
public sealed record StoreyStyleSummary(long Id, string Name, int Clear, string Preview);

/// <summary>A storey style: one room, as the save request plus the row's id, which a room style's storey
/// stack names it by. The courses are the <c>wall</c>, <c>post</c> and the three floor zones.</summary>
public sealed record StoreyStyleDetail(
    long Id, string Name, int Clear, int BorderWidth, int InlayInset, RoomWindowDto Windows,
    IReadOnlyList<RoomCourseDto> Courses)
    : StoreyStyleSaveRequest(Name, Clear, BorderWidth, InlayInset, Windows, Courses);

/// <summary>Create or replace a storey style.</summary>
/// <param name="Name">What the library lists it under.</param>
/// <param name="Clear">The air a player stands in, in blocks. Never under three — a room has to be stood up
/// in.</param>
/// <param name="BorderWidth">How many blocks in from the walls the floor's border ring runs.</param>
/// <param name="InlayInset">How far in from the walls the floor's centred plate starts.</param>
/// <param name="Windows">The openings cut through this storey's wall, or <c>none</c> for a blank one.</param>
/// <param name="Courses">The material stacks for the parts a storey owns — <c>wall</c>, <c>post</c> and the
/// three floor zones. A part with no courses keeps the built-in finish.</param>
public record StoreyStyleSaveRequest(
    string Name, int Clear, int BorderWidth, int InlayInset, RoomWindowDto Windows,
    IReadOnlyList<RoomCourseDto> Courses);

/// <summary>One row in the porch library, with the porch it stamps.</summary>
/// <param name="Id">The row number every later route names it by.</param>
/// <param name="Name">What the library lists it under.</param>
/// <param name="Preview">The card picture, drawn through the same code the export runs, so a library is
/// browsed by what its entries look like.</param>
public sealed record PorchStyleSummary(long Id, string Name, string Preview);

/// <summary>A porch style: the strip of footprint the walls give up and what stands on it, as the save
/// request plus the row's id, which a room style binds it by. No courses — a porch's deck is the house's
/// floor and its canopy the roof's material, so what is left to it is its shape.</summary>
public sealed record PorchStyleDetail(
    long Id, string Name, int Depth, int Inset, string Edge, string Roof, int RailBlock)
    : PorchStyleSaveRequest(Name, Depth, Inset, Edge, Roof, RailBlock);

/// <summary>Create or replace a porch style.</summary>
/// <param name="Name">What the library lists it under.</param>
/// <param name="Depth">How deep a strip of footprint the walls give up for it. The room keeps at least three
/// blocks whatever is asked for.</param>
/// <param name="Inset">How far the deck stops short of each end of the wall it stands on. 0 runs it the full
/// width.</param>
/// <param name="Edge">Which wall it stands on, from <see cref="PorchEdges"/>. <c>front</c> follows the
/// building's own front — the wall its doorway is cut through — rather than naming a fixed side.</param>
/// <param name="Roof">The canopy's form, from <see cref="RoofForms"/>.</param>
/// <param name="RailBlock">The block the rail is built from, or 0 for a deck left open to step off
/// anywhere.</param>
public record PorchStyleSaveRequest(
    string Name, int Depth, int Inset,
    [property: WordSet(typeof(PorchEdges))] string Edge,
    [property: WordSet(typeof(RoofForms))] string Roof, int RailBlock);

/// <summary>A tree recipe as the library lists it.</summary>
/// <param name="Id">The row a placement names once the recipe is pulled into a map's registry.</param>
/// <param name="Name">What the library lists it under, and the key a pull files it under.</param>
/// <param name="Preview">The card picture, drawn through the grower the export runs, so a tree is picked by
/// what it looks like rather than by its numbers.</param>
public sealed record TreeStyleSummary(long Id, string Name, string Preview);

/// <summary>A tree recipe (GET /api/tree-styles/{id}): the save request plus the row's id, which a placement
/// names it by once it is pulled into a map's dressing registry.</summary>
public sealed record TreeStyleDetail(
    long Id, string Name, string Form, string Species, string Wood, double Height,
    int Stems, double Leader, double Flow, double BranchAngle, int Levels, bool Whorled, double LeafSize)
    : TreeStyleSaveRequest(Name, Form, Species, Wood, Height, Stems, Leader, Flow, BranchAngle, Levels,
                           Whorled, LeafSize);

/// <summary>Create or replace a tree recipe — one of <b>two</b> trees, which <paramref name="Form"/> picks. A
/// <c>template</c> tree is vanilla and reads its species; a <c>grown</c> tree is the recursive skeleton and
/// reads its wood and the knobs under it. Each form reads only its own fields, so the ones it does not read are
/// inert rather than wrong.</summary>
/// <param name="Name">What the library lists it under.</param>
/// <param name="Form">Which tree this is, from <see cref="TreeForms"/>.</param>
/// <param name="Species">Template only — the vanilla species, whose row carries the wood, the canopy profile
/// and the proportions.</param>
/// <param name="Wood">Grown only — what the tree is cut from. A grown tree's shape is the author's, so its
/// wood is all that is left to name.</param>
/// <param name="Height">Overall height in blocks, held to 5–40. Template: it scales the species' proportions.
/// Grown: not a uniform scale — a smaller tree carries a thinner stem and fewer branches.</param>
/// <param name="Stems">Grown only — 1–3 stems at the base.</param>
/// <param name="Leader">Grown only — how far the central axis climbs, 0–1: low spreads, high spires.</param>
/// <param name="Flow">Grown only — how much the trunk wanders on its way up, 0–1.</param>
/// <param name="BranchAngle">Grown only — how far a branch leaves its parent, in radians, held to 0.2–1.5. A
/// hand-built corpus leaves the trunk at 59° off vertical and forks its children at 67°.</param>
/// <param name="Levels">Grown only — branching depth: 2 is a tree, 3 a denser one.</param>
/// <param name="Whorled">Grown only — whether the branches gather into rings, each shorter than the one below.
/// It is the conifer against the broadleaf, and the one shape choice a picker of six woods cannot make.</param>
/// <param name="LeafSize">Grown only — how big each tip's leaf cluster is, 0.2–1.</param>
public record TreeStyleSaveRequest(
    string Name,
    [property: WordSet(typeof(TreeForms))] string Form,
    [property: WordSet(typeof(TreeSpeciesNames))] string Species,
    [property: WordSet(typeof(TreeWoodNames))] string Wood,
    double Height, int Stems = 1, double Leader = 0.55, double Flow = 0.45,
    double BranchAngle = 1.1, int Levels = 2, bool Whorled = false, double LeafSize = 0.6);

/// <summary>A boulder recipe as the library lists it.</summary>
/// <param name="Id">The row a placement names once the recipe is pulled into a map's registry.</param>
/// <param name="Name">What the library lists it under, and the key a pull files it under.</param>
/// <param name="Preview">The card picture, drawn through the pass that builds it.</param>
public sealed record BoulderStyleSummary(long Id, string Name, string Preview);

/// <summary>A boulder recipe (GET /api/boulder-styles/{id}): the save request plus the row's id.</summary>
public sealed record BoulderStyleDetail(
    long Id, string Name, string Form, double Size, bool Mossy, string Rock)
    : BoulderStyleSaveRequest(Name, Form, Size, Mossy, Rock);

/// <summary>Create or replace a boulder recipe — a glacial erratic's form, its reach, what it is cut from and
/// whether moss takes its sky-lit faces.</summary>
/// <param name="Name">What the library lists it under.</param>
/// <param name="Form">The erratic's shape, from <see cref="BoulderForms"/>.</param>
/// <param name="Size">How far the rock reaches from its centre, held to 2–10 blocks — a rock a player takes
/// cover behind rather than one they step over.</param>
/// <param name="Mossy">Whether moss creeps onto the sky-lit faces.</param>
/// <param name="Rock">What the rock is cut from, as a terrain material's own JSON — any of the fourteen kinds
/// a style may be. Resolved in the boulder's own frame, so a mottled rock carries the same mottling to every
/// image of its orbit.</param>
public record BoulderStyleSaveRequest(
    string Name,
    [property: WordSet(typeof(BoulderForms))] string Form,
    double Size, bool Mossy, string Rock);

/// <summary>One storey of a house: which storey style fills it, and the clear it takes <em>here</em> —
/// 0 for the storey style's own. The position in the list is the position in the building, ground first, so
/// there is no ordinal on the wire: reordering the list is reordering the house.</summary>
/// <param name="StoreyStyleId">The storey style filling this level.</param>
/// <param name="Clear">The air it takes <em>here</em>, or 0 for the storey style's own.</param>
public sealed record RoomStoreyDto(long StoreyStyleId, int Clear);

/// <summary>A full room style (GET /api/room-styles/{id}): the save request plus the row's id, since an
/// answer that restated the same twenty-five fields would be one shape twice and the two would drift. A part
/// with no courses keeps the built-in finish, the way an unbound theme bucket does. The id is the row
/// number a sketch binds it by.</summary>
public sealed record RoomStyleDetail(
    long Id, string Name,
    int FloorDepth, int WallHeight,
    string RoofForm, int Pitch, int Overhang, bool RoofHole, bool RidgeCap,
    int BorderWidth, int InlayInset,
    int Storeys, int StoreyClear,
    RoomWindowDto Windows, RoomPorchDto? Porch,
    string Door, int DoorHeight,
    long? RoofStyleId, long? PorchStyleId, IReadOnlyList<RoomStoreyDto> StoreyStack,
    IReadOnlyList<RoomCourseDto> Courses,
    RoomBeamDto? Beams = null, int RoofSlab = -1, int RoofSlabData = 0,
    RoomWindowDto? GableWindows = null, RoomDoorHeadDto? DoorHead = null, int DoorWidth = 2)
    : RoomStyleSaveRequest(
        Name, FloorDepth, WallHeight, RoofForm, Pitch, Overhang, RoofHole, RidgeCap, BorderWidth, InlayInset,
        Storeys, StoreyClear, Windows, Porch, Door, DoorHeight, RoofStyleId, PorchStyleId, StoreyStack,
        Courses, Beams, RoofSlab, RoofSlabData, GableWindows, DoorHead, DoorWidth)
{
    /// <summary>The style as the request that would store it unchanged — every field, including the ones an
    /// editor draws no control for. An editor loading a row into a draft takes this rather than restating the
    /// field list, because a restated list is one a later field is added outside of, and what is left out of
    /// the draft is written away on the next save.</summary>
    public RoomStyleSaveRequest AsSaveRequest() => new(
        Name, FloorDepth, WallHeight, RoofForm, Pitch, Overhang, RoofHole, RidgeCap, BorderWidth, InlayInset,
        Storeys, StoreyClear, Windows, Porch, Door, DoorHeight, RoofStyleId, PorchStyleId, StoreyStack,
        Courses, Beams, RoofSlab, RoofSlabData, GableWindows, DoorHead, DoorWidth);
}

/// <summary>Create or replace a room style (POST /api/room-styles, PUT /api/room-styles/{id}) — a whole
/// building: the parts it is finished in, the numbers that decide its proportions, and the library rows it
/// binds for its roof, its storeys and its porch.</summary>
/// <param name="Name">What the library lists it under.</param>
/// <param name="FloorDepth">How many courses the floor plate runs down.</param>
/// <param name="WallHeight">How many courses of wall stand between the floor and the eave, where the
/// building is one storey. A stack of storeys spends this height instead.</param>
/// <param name="RoofForm">The roof's shape, from <see cref="RoofForms"/>, where no
/// <paramref name="RoofStyleId"/> is bound.</param>
/// <param name="Pitch">Courses of rise per block travelled inward. 1 is 45°.</param>
/// <param name="Overhang">How far the roof reaches past the walls, in blocks. 0 ends it flush.</param>
/// <param name="RoofHole">Whether a flat roof carries a centred hole — the light a windowless room otherwise
/// has none of. A gable never takes one.</param>
/// <param name="RidgeCap">Whether the line the slopes meet on is laid in the <c>verge</c> course rather than
/// the roof's own material.</param>
/// <param name="BorderWidth">How many blocks in from the walls the floor's border ring runs.</param>
/// <param name="InlayInset">How far in from the walls the floor's centred plate starts.</param>
/// <param name="Storeys">How many floors are stacked inside. Each but the top carries a slab and a ladder,
/// and 1 is the plain single-storey shell. Where <paramref name="StoreyStack"/> names rows, this is a count
/// of them rather than a second answer.</param>
/// <param name="StoreyClear">Blocks of air in each storey. 0 spends <paramref name="WallHeight"/> instead,
/// and the stamper never goes below three — a room has to be stood up in.</param>
/// <param name="Windows">The openings cut through the walls, or <c>none</c>.</param>
/// <param name="Porch">The strip of footprint the walls give up, or absent for a building whose walls stand
/// on the whole of it. A bound <paramref name="PorchStyleId"/> supersedes it.</param>
/// <param name="Door">What fills the doorway, by door slug — a closed set, because only these can be broken
/// by an attacker and so only these open a cage. <c>GET /api/room-styles/doors</c> answers it.</param>
/// <param name="DoorHeight">How many courses tall the opening is cut. What it actually clears depends on
/// <paramref name="DoorHead"/>, which takes the top course.</param>
/// <param name="RoofStyleId">A roof library row to wear instead of the flat roof knobs, or absent to use
/// them.</param>
/// <param name="PorchStyleId">A porch library row to wear instead of <paramref name="Porch"/>, or
/// absent.</param>
/// <param name="StoreyStack">One entry per storey, ground first — which storey style fills it and the clear
/// it takes here. Empty means the building is the single storey the flat knobs describe; the position in the
/// list is the position in the building, so reordering the list is reordering the house.</param>
/// <param name="Courses">The material stacks for the parts the building itself owns. A part with no courses
/// keeps the built-in finish, the way an unbound theme bucket does.</param>
/// <param name="Beams">The log ends that run out past the corners where two storeys meet, or absent for a
/// building whose storeys meet without them.</param>
/// <param name="RoofSlab">The block a half-course rise steps on every odd course, or -1 for a roof laid in
/// whole blocks.</param>
/// <param name="RoofSlabData">That slab's variant nibble — which wood, which stone.</param>
/// <param name="GableWindows">The openings cut through the gable ends, where they differ from the wall's, or
/// absent for a gable left blank.</param>
/// <param name="DoorHead">The beam over the doorway, or absent to leave the opening a plain
/// rectangle.</param>
/// <param name="DoorWidth">How wide the opening is asked for. Never cut under two however it is set: a
/// single-width gap is not a door, and a room an objective is carried out of has to read as somewhere to walk
/// through.</param>
public record RoomStyleSaveRequest(
    string Name,
    int FloorDepth, int WallHeight,
    [property: WordSet(typeof(RoofForms))] string RoofForm, int Pitch, int Overhang, bool RoofHole, bool RidgeCap,
    int BorderWidth, int InlayInset,
    int Storeys, int StoreyClear,
    RoomWindowDto Windows, RoomPorchDto? Porch,
    string Door, int DoorHeight,
    long? RoofStyleId, long? PorchStyleId, IReadOnlyList<RoomStoreyDto> StoreyStack,
    IReadOnlyList<RoomCourseDto> Courses,
    // Trailing and defaulted so every existing construction site keeps compiling and keeps meaning "this
    // building has none" — which is what every stored style already was.
    RoomBeamDto? Beams = null, int RoofSlab = -1, int RoofSlabData = 0,
    RoomWindowDto? GableWindows = null, RoomDoorHeadDto? DoorHead = null, int DoorWidth = 2);

/// <summary>The four pictures of a room style: from above, projected onto its front, in isometric, and one
/// plane drawn at the scale of the pieces in it. A library <em>card</em> carries the section alone — the
/// isometric is tens of kilobytes, which is nothing for the one style an editor has open and megabytes for a
/// grid of them.</summary>
/// <param name="Plan">The style from above.</param>
/// <param name="Section">One plane cut open downward — the view a library card carries, since the isometric
/// is tens of kilobytes.</param>
/// <param name="Iso">The building in isometric.</param>
/// <param name="Cutaway">One plane drawn at the scale of the pieces in it.</param>
public sealed record RoomStylePreviewDto(string Plan, string Section, string Iso, string Cutaway);

/// <summary>One field of a material kind (<c>GET /api/terrain/patterns</c>), as the painter's deserializer
/// will accept it. <paramref name="Type"/> is a wire type word — <c>int</c>, <c>bool</c>, <c>material</c>,
/// <c>material[]</c>, <c>band[]</c>, <c>stripe[]</c>, <c>bandStack</c>, or an enum's name, in which case
/// <paramref name="Choices"/> holds its values. A field with no <paramref name="Default"/> is
/// <paramref name="Required"/> and has to be written.</summary>
/// <param name="Name">The key the field is written under.</param>
/// <param name="Type">What kind of value it takes — <c>int</c>, <c>bool</c>, <c>material</c>,
/// <c>material[]</c>, <c>band[]</c>, <c>stripe[]</c>, <c>bandStack</c>, or an enum's name.</param>
/// <param name="Required">Whether it has to be written. A field with no <paramref name="Default"/> is.</param>
/// <param name="Default">What the deserializer uses when it is left out.</param>
/// <param name="Choices">The values an enum-typed field takes, absent for the rest.</param>
public sealed record MaterialFieldDto(
    string Name, string Type, bool Required, object? Default, IReadOnlyList<string>? Choices);

/// <summary>One material kind (<c>GET /api/terrain/patterns</c>): the <c>kind</c> discriminator a theme tags
/// it with, the label a picker offers it under, a sentence on what it draws, and its fields.
/// <para><paramref name="Reads"/> is which facts about a cell it varies with — <c>position</c>, <c>depth</c>,
/// <c>inset</c>, <c>arc</c>, <c>bend</c>, <c>height</c>, <c>team</c> — and therefore where it is legible: a
/// kind reading <c>arc</c> says nothing away from a perimeter, and one reading <c>inset</c> draws rings and
/// falls back off a footprint.</para></summary>
/// <param name="Kind">The discriminator a theme tags it with.</param>
/// <param name="Name">The label a picker offers it under.</param>
/// <param name="Summary">What it draws, in a sentence.</param>
/// <param name="Reads">Which facts about a cell it varies with — <c>position</c>, <c>depth</c>,
/// <c>inset</c>, <c>arc</c>, <c>bend</c>, <c>height</c>, <c>team</c> — and therefore where it is legible: a
/// kind reading <c>arc</c> says nothing away from a perimeter.</param>
/// <param name="Fields">The knobs it takes, as the painter's deserializer will accept them.</param>
public sealed record MaterialKindDto(
    [property: WordSet(typeof(MaterialKind))] string Kind, string Name, string Summary,
    IReadOnlyList<string> Reads, IReadOnlyList<MaterialFieldDto> Fields);

/// <summary>A room style as the stamper's own JSON — the form the export consumes and the form a map
/// snapshots when it binds one.</summary>
/// <param name="StyleJson">The stamper's own JSON for the style, as text.</param>
public sealed record StyleJsonDto(string StyleJson);

/// <summary>One picture of a draft, for a kind whose card is the whole preview — a prop recipe has no building
/// to cut four ways, so what it answers is the section a browse row carries.</summary>
/// <param name="Card">The SVG a browse row and the editor's stage both draw.</param>
public sealed record StyleCardDto(string Card);

/// <summary>A terrain theme as the painter's own JSON.</summary>
/// <param name="ThemeJson">The painter's own JSON for the theme, as text — the form a sketch snapshots and
/// <c>POST /api/themes/import</c> reads back.</param>
public sealed record ThemeJsonDto(string ThemeJson);
