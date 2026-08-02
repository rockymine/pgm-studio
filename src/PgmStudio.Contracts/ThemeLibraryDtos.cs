namespace PgmStudio.Contracts;

/// <summary>One reusable terrain-paint style (GET /api/styles) — a named material recipe. <paramref name="Kind"/>
/// is the material discriminator (solid | layered | teamTint | voronoi | noise | wallRun); <paramref name="Params"/>
/// is the serialized <c>TerrainMaterial</c> the painter reads.</summary>
public sealed record StyleDto(long Id, string Name, string Kind, string Params);

/// <summary>Create/update a style (POST /api/styles, PUT /api/styles/{id}).</summary>
public sealed record StyleSaveRequest(string Name, string Kind, string Params);

/// <summary>One bucket binding of a theme (rim | surface | wall | fill): the style that fills it, and the
/// bucket's depth (rim/surface) and toggle.</summary>
public sealed record ThemeBucketDto(string Bucket, long StyleId, int Depth, bool Enabled);

/// <summary>One row in the theme library list (GET /api/themes).</summary>
public sealed record ThemeSummary(long Id, string Name);

/// <summary>A full theme (GET /api/themes/{id}) — the geometry knobs plus a style binding per bucket. The
/// painter-ready JSON is served separately at GET /api/themes/{id}/json (assembled through the styles).</summary>
public sealed record ThemeDetail(
    long Id, string Name,
    bool BedrockRelative, int BedrockValue, bool Closed, bool WallOnTerrainFaces,
    IReadOnlyList<ThemeBucketDto> Buckets);

/// <summary>Create a theme from existing styles (POST /api/themes): the knobs plus the bucket→style bindings.</summary>
public sealed record ThemeSaveRequest(
    string Name,
    bool BedrockRelative, int BedrockValue, bool Closed, bool WallOnTerrainFaces,
    IReadOnlyList<ThemeBucketDto> Buckets);

/// <summary>Import a whole theme JSON into the library (POST /api/themes/import): the painter's theme JSON is
/// decomposed into one style per bucket + a composed theme. The response id is the new theme.</summary>
public sealed record ThemeImportRequest(string Name, string ThemeJson);
