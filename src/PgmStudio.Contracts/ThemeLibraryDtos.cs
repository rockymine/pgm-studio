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
/// bucket's depth (rim/surface) and toggle.</summary>
public sealed record ThemeBucketDto(string Bucket, long StyleId, int Depth, bool Enabled);

/// <summary>One row in the theme library list (GET /api/themes), with the sample plateau the theme finishes —
/// the same reason <see cref="StyleDto.Preview"/> travels with a style.</summary>
public sealed record ThemeSummary(long Id, string Name, string Preview);

/// <summary>A full theme (GET /api/themes/{id}) — the geometry knobs plus a style binding per bucket. The
/// painter-ready JSON is served separately at GET /api/themes/{id}/json (assembled through the styles).</summary>
public sealed record ThemeDetail(
    long Id, string Name,
    bool BedrockRelative, int BedrockValue, bool Closed, bool WallOnTerrainFaces,
    IReadOnlyList<ThemeBucketDto> Buckets);

/// <summary>Create or replace a theme built from existing styles (POST /api/themes, PUT /api/themes/{id}): the
/// knobs plus the bucket→style bindings.</summary>
public sealed record ThemeSaveRequest(
    string Name,
    bool BedrockRelative, int BedrockValue, bool Closed, bool WallOnTerrainFaces,
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

/// <summary>Why a style could not be forgotten (DELETE /api/styles/{id}, 409): the themes still binding it. A
/// style is shared, so the refusal names what would break instead of surfacing a foreign-key error.</summary>
public sealed record StyleInUseDto(string Error, IReadOnlyList<string> Themes);
