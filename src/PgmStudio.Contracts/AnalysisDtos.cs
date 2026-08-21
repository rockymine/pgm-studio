using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>A rectangle in block coordinates, spelled the way the analysis surface spells it. It bounds a
/// raster the studio measured and it selects the ground a search reads, which is why it is both an answer and
/// a request here.</summary>
/// <param name="MinX">The west edge.</param>
/// <param name="MinZ">The north edge.</param>
/// <param name="MaxX">The east edge.</param>
/// <param name="MaxZ">The south edge.</param>
public sealed record BoundsDto(int MinX, int MinZ, int MaxX, int MaxZ);

/// <summary>Which ground to count resource blocks over.</summary>
/// <param name="Bounds">The rectangle to search, nested rather than flat — the four corners under
/// <c>bounds</c>, not beside it. Leaving it out reads the whole map, which is the useful default here; one
/// that is present and short of a corner is refused as <c>RQ1</c>.</param>
public sealed record ResourceSearchRequest(BoundsDto? Bounds = null);

/// <summary>Which ground to count wool sources over.</summary>
/// <param name="Bounds">The rectangle to search, nested rather than flat — the four corners under
/// <c>bounds</c>, not beside it. Required: a wool count over a whole map answers a question nobody asked, so
/// an absent rectangle is refused as <c>RQ1</c> rather than defaulted.</param>
public sealed record WoolSearchRequest(BoundsDto Bounds);

/// <summary>GET /api/map/{slug}/buildability — per-column verdict grid (rows of digit codes).</summary>
public sealed record BuildabilityDto(
    BoundsDto Bbox, int Width, int Height,
    IReadOnlyList<string> Classes, IReadOnlyDictionary<string, string> Colors,
    IReadOnlyDictionary<string, int> Counts, IReadOnlyList<string> Rows, bool HasY0);

/// <summary>
/// One landmass the world scan decomposed the map into, as <c>GET /map/{slug}/islands</c> answers it —
/// the shared decomposition the Configure tool draws, seats spawns against and fits the canvas to.
///
/// <para><see cref="Id"/> is 1-based and is the id the footprint grid stamps into each cell.
/// <see cref="Bounds"/> is <c>[minX, minZ, maxX, maxZ]</c> in whole blocks with the maxima
/// <b>inclusive</b> — the detector takes them off the cells it found, so a one-cell island has
/// <c>minX == maxX</c> and its centre is <c>(minX + maxX + 1) / 2</c>. <see cref="Polygon"/> is GeoJSON — <c>{type: "Polygon", coordinates:
/// [ring, hole…]}</c> over <c>[x, z]</c> pairs, the map's z in the second ordinate — which is what lets the
/// canvas render an island the same way it renders a drawn shape.</para>
///
/// <para>The order is detection order, and it is load-bearing: the island-sketch shapes are 1:1 with it, so
/// an index into this list names the same landmass on both sides.</para>
/// </summary>
public sealed record IslandDto(
    int Id,
    [property: JsonPropertyName("block_count")] int BlockCount,
    IReadOnlyList<int> Bounds,
    GeoPolygonDto Polygon);

/// <summary>A GeoJSON polygon over <c>[x, z]</c> pairs: the outer ring first, then one array per hole.</summary>
public sealed record GeoPolygonDto(string Type, IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> Coordinates);

public sealed record NavPointDto(string Kind, string Name, int X, int Z, int Component);
/// <summary><c>For</c> names the team an entry denial cut the point off from, where that is the cause —
/// null where the whole map's navigability fails to reach it, whoever walks.</summary>
public sealed record IsolatedPointDto(string Kind, string Name, string? For = null);

/// <summary>GET /api/map/{slug}/traversability — spawn↔wool connectivity over the navigability map.</summary>
public sealed record TraversabilityDto(
    bool Connected, int ComponentCount, string Severity, string Message, bool HaveLayers,
    IReadOnlyList<NavPointDto> Points, IReadOnlyList<IsolatedPointDto> Isolated);

/// <summary>GET /api/map/{slug}/coverage — where the ground is lived on and where it is dead: the class of
/// every ground cell as digit rows (indexes into <c>Classes</c>), the shares, and the dead patches worth
/// naming, largest first, each with the coordinates to check it in-game.</summary>
public sealed record CoveragePatchDto(int Area, int CentroidX, int CentroidZ, int NearestReachedBlocks);
/// <summary><c>Traffic</c> is one row per grid row like <c>Rows</c>, each character a base-36 digit giving how
/// many of the <c>Journeys</c> cover that cell (<c>z</c> for 35 or more) — the number the class codes throw
/// away. A cell one journey clips and a cell every journey runs down are both <c>reached</c>, and which of the
/// two a piece of ground is says which way round a hole is preferred. <c>Busiest</c> is the highest count on
/// the board, so a caller can scale without walking the grid.</summary>
public sealed record CoverageDto(
    BoundsDto Bbox, int Width, int Height,
    IReadOnlyList<string> Classes, IReadOnlyDictionary<string, string> Colors, IReadOnlyList<string> Rows,
    int GroundCells, int ReachedCells, int DecoratedCells, int DeadCells, double DeadShare,
    IReadOnlyList<CoveragePatchDto> DeadPatches, int UnnamedDeadPatches, bool HaveRoutes,
    IReadOnlyList<string> Traffic, int Journeys, int Busiest);

/// <summary>One Review pre-flight finding. <c>Status</c> ∈ <c>"pass"</c> | <c>"fail"</c> | <c>"skip"</c>.</summary>
public sealed record PreflightCheckDto(string Key, string Label, string Status, string Detail);

/// <summary>GET /api/map/{slug}/preflight — the Review phase's pre-flight gate: the four generated-map
/// checks (round-trip · mirror-consistency · buildability · traversability), the validate log, and the
/// export verdict. <c>ExportReady</c> mirrors what <c>GET /xml</c> enforces (round-trip must not throw and
/// the spawn↔wool chain must be connected); mirror + buildability are advisory. Scoped to intent-authored
/// maps (<c>IntentMap</c> false ⇒ a corpus map with nothing to pre-flight). Carries the traversability
/// result for the connectivity mini-map.</summary>
public sealed record PreflightDto(
    bool IntentMap, bool ExportReady,
    IReadOnlyList<PreflightCheckDto> Checks, IReadOnlyList<string> Log,
    TraversabilityDto? Traversability);

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

public sealed record ResourceBlockDto(string Type, int X, int Y, int Z);
public sealed record ResourceTypeSummaryDto(
    string Type, int Total, int Renewable, bool AllRenewable, IReadOnlyList<ResourceBlockDto> Sources);

/// <summary>POST /api/map/{slug}/resources — iron/gold/diamond blocks (optionally inside a drawn rect,
/// body <c>{ bounds?: { minX, minZ, maxX, maxZ } }</c>) + how many a <c>&lt;renewable&gt;</c> already
/// covers, for renewable auto-config. HaveLayers is false for an xml-only map.</summary>
public sealed record ResourceSourcesResponseDto(IReadOnlyList<ResourceTypeSummaryDto> Resources, bool HaveLayers);
