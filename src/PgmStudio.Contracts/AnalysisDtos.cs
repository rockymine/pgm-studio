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
