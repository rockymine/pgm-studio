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
/// decomposed into one style per bucket + a composed theme. The response id is the new theme.</summary>
public sealed record ThemeImportRequest(string Name, string ThemeJson);

/// <summary>Both views of one material (POST /api/terrain/material-preview): <paramref name="Plan"/> is one
/// course seen from above — where a voronoi, a noise field and a wall run vary — and <paramref name="Section"/>
/// is one row of columns cut open downward, the axis a layer stack varies along.</summary>
public sealed record MaterialPreviewDto(string Plan, string Section);

/// <summary>A theme previewed (POST /api/terrain/theme-preview): <paramref name="Section"/> is a sample plateau
/// painted with the theme and cut open — the buckets in their geometry — and <paramref name="Buckets"/> holds one
/// top-down swatch per themeable bucket, keyed by <see cref="ThemeBuckets"/>.</summary>
public sealed record ThemePreviewDto(string Section, IReadOnlyDictionary<string, string> Buckets);

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

/// <summary>A full room style (GET /api/room-styles/{id}) — the per-part extents and knobs plus the courses
/// bound to each part. A part with no courses keeps the built-in finish, the way an unbound theme bucket
/// does.</summary>
public sealed record RoomStyleDetail(
    long Id, string Name,
    int FloorDepth, int WallHeight, int RoofThickness,
    string Eave, bool RoofHole, string Door, int DoorHeight,
    IReadOnlyList<RoomCourseDto> Courses);

/// <summary>Create or replace a room style (POST /api/room-styles, PUT /api/room-styles/{id}).</summary>
public sealed record RoomStyleSaveRequest(
    string Name,
    int FloorDepth, int WallHeight, int RoofThickness,
    string Eave, bool RoofHole, string Door, int DoorHeight,
    IReadOnlyList<RoomCourseDto> Courses);

/// <summary>A room style previewed (POST /api/room-styles/preview): the shell it stamps, from above and cut
/// open. Both are drawn by the real <c>CubeStamper</c> over a sample frame, so a card cannot promise a shell
/// the export would not build.</summary>
public sealed record RoomStylePreviewDto(string Plan, string Section);
