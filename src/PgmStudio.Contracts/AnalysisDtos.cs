namespace PgmStudio.Contracts;

public sealed record BoundsDto(int MinX, int MinZ, int MaxX, int MaxZ);

/// <summary>GET /api/map/{slug}/buildability — per-column verdict grid (rows of digit codes).</summary>
public sealed record BuildabilityDto(
    BoundsDto Bbox, int Width, int Height,
    IReadOnlyList<string> Classes, IReadOnlyDictionary<string, string> Colors,
    IReadOnlyDictionary<string, int> Counts, IReadOnlyList<string> Rows, bool HasY0);

public sealed record NavPointDto(string Kind, string Name, int X, int Z, int Component);
public sealed record IsolatedPointDto(string Kind, string Name);

/// <summary>GET /api/map/{slug}/traversability — spawn↔wool connectivity over the navigability map.</summary>
public sealed record TraversabilityDto(
    bool Connected, int ComponentCount, string Severity, string Message, bool HaveLayers,
    IReadOnlyList<NavPointDto> Points, IReadOnlyList<IsolatedPointDto> Isolated);

public sealed record RegionFacetDto(string Category, IReadOnlyList<string> Roles, string? Subtype = null);

/// <summary>GET /api/map/{slug}/regions — derived region facets + a category count summary.</summary>
public sealed record RegionsDto(
    IReadOnlyDictionary<string, RegionFacetDto> Facets,
    IReadOnlyDictionary<string, int> CategoryCounts);

public sealed record WoolAvailabilityDto(
    string WoolId, string Color, bool Obtainable, bool Repeatable, bool OneTime,
    string Severity, IReadOnlyList<string> SourceTypes, string Message);

/// <summary>GET /api/map/{slug}/wool-availability — per declared wool, is it obtainable?</summary>
public sealed record WoolAvailabilityResponseDto(IReadOnlyList<WoolAvailabilityDto> Wools, bool HaveLayers);

public sealed record MonumentObstructionDto(
    string WoolColor, string Team, string MonumentId, int X, int Y, int Z,
    bool Obstructed, string Severity, string Message);

/// <summary>GET /api/map/{slug}/monument-obstruction — each wool monument's block must be air; a
/// pre-existing block there blocks wool placement (PGM warns on load).</summary>
public sealed record MonumentObstructionResponseDto(IReadOnlyList<MonumentObstructionDto> Monuments, bool HaveLayers);

public sealed record WoolSourceDto(string Type, string Color, int X, int Y, int Z, int Count);
public sealed record WoolColorSummaryDto(
    string Color, int Total, IReadOnlyList<string> SourceTypes, bool Repeatable, bool OneTime,
    IReadOnlyList<WoolSourceDto> Sources);

/// <summary>POST /api/map/{slug}/wool-sources — wool colours found inside a drawn rectangle
/// (body: <c>{ bounds: { minX, minZ, maxX, maxZ } }</c>). HaveLayers is false for an xml-only map.</summary>
public sealed record WoolSourcesResponseDto(IReadOnlyList<WoolColorSummaryDto> Colors, bool HaveLayers);

public sealed record WoolSuggestionDto(string Color, int Total, IReadOnlyList<string> SourceTypes);

/// <summary>GET /api/map/{slug}/wool-suggestions — wool colours found in the world but not yet
/// declared as objectives.</summary>
public sealed record WoolSuggestionsResponseDto(IReadOnlyList<WoolSuggestionDto> Suggestions, bool HaveLayers);
