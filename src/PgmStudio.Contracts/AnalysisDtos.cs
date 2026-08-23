using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>Which ground to count resource blocks over.</summary>
/// <param name="Bounds">The rectangle to search, nested rather than flat — the four corners under
/// <c>bounds</c>, not beside it. Leaving it out reads the whole map, which is the useful default here; one
/// that is present and short of a corner is refused as <c>RQ1</c>.</param>
public sealed record ResourceSearchRequest(Bounds2dDto? Bounds = null);

/// <summary>Which ground to count wool sources over.</summary>
/// <param name="Bounds">The rectangle to search, nested rather than flat — the four corners under
/// <c>bounds</c>, not beside it. Required: a wool count over a whole map answers a question nobody asked, so
/// an absent rectangle is refused as <c>RQ1</c> rather than defaulted.</param>
public sealed record WoolSearchRequest(Bounds2dDto Bounds);

/// <summary>GET /api/map/{slug}/buildability — per-column verdict grid (rows of digit codes).</summary>
/// <param name="Bbox">The ground the grid covers, in blocks.</param>
/// <param name="Width">Cells across, which is the length of every row.</param>
/// <param name="Height">Cells down, which is how many rows there are.</param>
/// <param name="Classes">The verdict each digit stands for, in digit order — <c>rows[z][x]</c> minus
/// <c>'0'</c> indexes into this.</param>
/// <param name="Colors">A swatch per class, so a heatmap is drawn without the caller inventing one.</param>
/// <param name="Counts">How many cells each class holds, keyed by class.</param>
/// <param name="Rows">One string per grid row, each character the digit of that cell's verdict.</param>
/// <param name="HasY0">Whether the map has column data at all. False means the grid is empty because
/// nothing was scanned, not because nothing is buildable.</param>
public sealed record BuildabilityDto(
    Bounds2dDto Bbox, int Width, int Height,
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
/// <param name="Id">1-based, and the id the footprint grid stamps into each cell.</param>
/// <param name="BlockCount">How many ground blocks the island holds.</param>
/// <param name="Bounds"><c>[minX, minZ, maxX, maxZ]</c> in whole blocks, maxima <b>inclusive</b>.</param>
/// <param name="Polygon">Its outline as GeoJSON, which is what lets the canvas draw an island the way it
/// draws a shape.</param>
public sealed record IslandDto(
    int Id,
    [property: JsonPropertyName("block_count")] int BlockCount,
    IReadOnlyList<int> Bounds,
    GeoPolygonDto Polygon);

/// <summary>A GeoJSON polygon over <c>[x, z]</c> pairs: the outer ring first, then one array per hole.</summary>
/// <param name="Type">Always <c>Polygon</c> — the GeoJSON discriminator, so a mapping library reads this
/// without translation.</param>
/// <param name="Coordinates">The outer ring first, then one array per hole, each a list of <c>[x, z]</c>
/// pairs.</param>
public sealed record GeoPolygonDto(string Type, IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> Coordinates);

/// <summary>One place the navigability read cares about reaching — a spawn or a wool — and which walkable
/// component it sits in.</summary>
/// <param name="Kind">What the point is: <c>spawn</c> or <c>wool</c>.</param>
/// <param name="Name">The team or the colour, which is how an author knows which one it is.</param>
/// <param name="X">Its east–west position, in blocks.</param>
/// <param name="Z">Its north–south position.</param>
/// <param name="Component">Which connected piece of walkable ground it stands on. Two points sharing a
/// number can walk to each other; two that do not, cannot.</param>
public sealed record NavPointDto(string Kind, string Name, int X, int Z, int Component);
/// <summary><c>For</c> names the team an entry denial cut the point off from, where that is the cause —
/// null where the whole map's navigability fails to reach it, whoever walks.</summary>
/// <param name="Kind">What the point is: <c>spawn</c> or <c>wool</c>.</param>
/// <param name="Name">The team or the colour it belongs to.</param>
/// <param name="For">The team an entry denial cut it off from, where that is the cause — absent where the
/// whole map's navigability fails to reach it, whoever walks.</param>
public sealed record IsolatedPointDto(string Kind, string Name, string? For = null);

/// <summary>GET /api/map/{slug}/traversability — spawn↔wool connectivity over the navigability map.</summary>
/// <param name="Connected">Whether every spawn can walk to every wool it must capture.</param>
/// <param name="ComponentCount">How many separate pieces of walkable ground the points fall into. One is
/// the healthy answer.</param>
/// <param name="Severity">What the verdict is worth — the same word a finding carries.</param>
/// <param name="Message">The verdict in a sentence, with the numbers in it.</param>
/// <param name="HaveLayers">Whether the map has scanned column data. False means nothing was measured, not
/// that nothing connects.</param>
/// <param name="Points">Every spawn and wool, with the component it stands in.</param>
/// <param name="Isolated">The points nothing reaches, and why.</param>
public sealed record TraversabilityDto(
    bool Connected, int ComponentCount, string Severity, string Message, bool HaveLayers,
    IReadOnlyList<NavPointDto> Points, IReadOnlyList<IsolatedPointDto> Isolated);

/// <summary>GET /api/map/{slug}/coverage — where the ground is lived on and where it is dead: the class of
/// every ground cell as digit rows (indexes into <c>Classes</c>), the shares, and the dead patches worth
/// naming, largest first, each with the coordinates to check it in-game.</summary>
/// <param name="Area">How many cells the dead patch covers.</param>
/// <param name="CentroidX">Its middle, east–west — a coordinate to check it at in-game.</param>
/// <param name="CentroidZ">Its middle, north–south.</param>
/// <param name="NearestReachedBlocks">How far it is from the nearest ground a journey does cover. A patch
/// far from everything is dead ground; one right beside a route is a corner nobody turns.</param>
public sealed record CoveragePatchDto(int Area, int CentroidX, int CentroidZ, int NearestReachedBlocks);
/// <summary><c>Traffic</c> is one row per grid row like <c>Rows</c>, each character a base-36 digit giving how
/// many of the <c>Journeys</c> cover that cell (<c>z</c> for 35 or more) — the number the class codes throw
/// away. A cell one journey clips and a cell every journey runs down are both <c>reached</c>, and which of the
/// two a piece of ground is says which way round a hole is preferred. <c>Busiest</c> is the highest count on
/// the board, so a caller can scale without walking the grid.</summary>
/// <param name="Bbox">The ground the grid covers, in blocks.</param>
/// <param name="Width">Cells across, which is the length of every row.</param>
/// <param name="Height">Cells down, which is how many rows there are.</param>
/// <param name="Classes">The verdict each digit stands for, in digit order — <c>rows[z][x]</c> minus
/// <c>'0'</c> indexes into this.</param>
/// <param name="Colors">A swatch per class, so a heatmap is drawn without the caller inventing one.</param>
/// <param name="Rows">One string per grid row, each character the digit of that cell's class.</param>
/// <param name="GroundCells">Ground cells in total.</param>
/// <param name="ReachedCells">Ground cells at least one journey covers.</param>
/// <param name="DecoratedCells">Ground cells the dressing pass put something on.</param>
/// <param name="DeadCells">Ground cells no journey covers and nothing stands on.</param>
/// <param name="DeadShare">Those cells over <paramref name="GroundCells"/>, 0–1.</param>
/// <param name="DeadPatches">The dead patches worth naming, largest first.</param>
/// <param name="UnnamedDeadPatches">How many more there were, below the size worth listing.</param>
/// <param name="HaveRoutes">Whether any journey was walked. False means the shares are about decoration
/// alone.</param>
/// <param name="Traffic">One row per grid row, each character a base-36 digit giving how many journeys
/// cover that cell (<c>z</c> for 35 or more) — the number the class codes throw away.</param>
/// <param name="Journeys">How many journeys were walked, which is what the traffic digits count out
/// of.</param>
/// <param name="Busiest">The highest traffic count on the board, so a caller scales without walking the
/// grid.</param>
/// <param name="Markers">The waypoints the journeys were walked between, each with the kind it is — a class
/// code names what ground is, and which place a cell is is a different question.</param>
public sealed record CoverageDto(
    Bounds2dDto Bbox, int Width, int Height,
    IReadOnlyList<string> Classes, IReadOnlyDictionary<string, string> Colors, IReadOnlyList<string> Rows,
    int GroundCells, int ReachedCells, int DecoratedCells, int DeadCells, double DeadShare,
    IReadOnlyList<CoveragePatchDto> DeadPatches, int UnnamedDeadPatches, bool HaveRoutes,
    IReadOnlyList<string> Traffic, int Journeys, int Busiest,
    IReadOnlyList<CoverageMarkerDto> Markers);

/// <summary>One waypoint a coverage journey started or ended at, at the cell it snapped to.</summary>
/// <param name="Kind">What it is: <c>spawn</c>, <c>wool</c>, <c>destroyable</c>, <c>core</c>, or
/// <c>crossing</c> for a derived seat on a way across the middle.</param>
/// <param name="X">Where it stands, east–west.</param>
/// <param name="Z">Where it stands, north–south.</param>
/// <param name="Color">Its canonical swatch, the same one every picture draws that kind in.</param>
public sealed record CoverageMarkerDto(string Kind, int X, int Z, string Color);

/// <summary>One Review pre-flight finding. <c>Status</c> ∈ <c>"pass"</c> | <c>"fail"</c> | <c>"skip"</c>.</summary>
/// <param name="Key">What the check is, for a caller branching on it.</param>
/// <param name="Label">The check as an author reads it.</param>
/// <param name="Status">Its verdict: <c>pass</c>, <c>fail</c> or <c>skip</c>.</param>
/// <param name="Detail">What it found, with the numbers in it.</param>
public sealed record PreflightCheckDto(string Key, string Label, string Status, string Detail);

/// <summary>GET /api/map/{slug}/preflight — the Review phase's pre-flight gate: the four generated-map
/// checks (round-trip · mirror-consistency · buildability · traversability), the validate log, and the
/// export verdict. <c>ExportReady</c> mirrors what <c>GET /xml</c> enforces (round-trip must not throw and
/// the spawn↔wool chain must be connected); mirror + buildability are advisory. Scoped to intent-authored
/// maps (<c>IntentMap</c> false ⇒ a corpus map with nothing to pre-flight). Carries the traversability
/// result for the connectivity mini-map.</summary>
/// <param name="IntentMap">Whether the map was authored from a stated intent. False is a corpus map, which
/// has nothing to pre-flight.</param>
/// <param name="ExportReady">Whether the export would go through — the round trip must not throw and the
/// spawn↔wool chain must connect. The other two checks are advisory.</param>
/// <param name="Checks">The four checks, each with its verdict.</param>
/// <param name="Log">What the validate pass wrote, line by line.</param>
/// <param name="Traversability">The connectivity read, carried so the mini-map draws without a second
/// call.</param>
public sealed record PreflightDto(
    bool IntentMap, bool ExportReady,
    IReadOnlyList<PreflightCheckDto> Checks, IReadOnlyList<string> Log,
    TraversabilityDto? Traversability);

/// <summary>What one region is <em>for</em>, derived from what points at it rather than declared on it.</summary>
/// <param name="Category">The editor group it is filed under.</param>
/// <param name="Roles">Every part it plays — a region can be a spawn and a build zone at once.</param>
/// <param name="Subtype">The finer reading where the role has one — a spawn point against a spawn
/// area.</param>
public sealed record RegionFacetDto(string Category, IReadOnlyList<string> Roles, string? Subtype = null);

/// <summary>GET /api/map/{slug}/regions — derived region facets + a category count summary.</summary>
/// <param name="Facets">One entry per region, keyed by id.</param>
/// <param name="CategoryCounts">How many regions each category holds.</param>
public sealed record RegionsDto(
    IReadOnlyDictionary<string, RegionFacetDto> Facets,
    IReadOnlyDictionary<string, int> CategoryCounts);

/// <summary>Whether one declared wool can actually be got hold of, and from what.</summary>
/// <param name="WoolId">The objective this is about.</param>
/// <param name="Color">Its colour.</param>
/// <param name="Obtainable">Whether the world holds any source of it at all.</param>
/// <param name="Repeatable">Whether at least one source refills — a dispenser rather than a pile.</param>
/// <param name="OneTime">Whether at least one source is finite, so taking it wrong ends the match.</param>
/// <param name="Severity">What the verdict is worth — the same word a finding carries.</param>
/// <param name="SourceTypes">What the sources are: <c>block</c>, <c>chest</c>, <c>dispenser</c>.</param>
/// <param name="Message">The verdict in a sentence, with the counts in it.</param>
public sealed record WoolAvailabilityDto(
    string WoolId, string Color, bool Obtainable, bool Repeatable, bool OneTime,
    string Severity, IReadOnlyList<string> SourceTypes, string Message);

/// <summary>GET /api/map/{slug}/wool-availability — per declared wool, is it obtainable?</summary>
/// <param name="Wools">One verdict per declared wool.</param>
/// <param name="HaveLayers">Whether the map has scanned world data. False for an xml-only map, where
/// nothing could be counted.</param>
public sealed record WoolAvailabilityResponseDto(IReadOnlyList<WoolAvailabilityDto> Wools, bool HaveLayers);

/// <summary>Whether one monument's block is clear. PGM warns on load where it is not, and the wool cannot
/// be placed.</summary>
/// <param name="WoolColor">The colour the monument takes.</param>
/// <param name="Team">The team that defends it.</param>
/// <param name="MonumentId">The monument this is about.</param>
/// <param name="X">Its block position, east–west — a coordinate to check it at in-game.</param>
/// <param name="Y">Its height.</param>
/// <param name="Z">Its north–south position.</param>
/// <param name="Obstructed">Whether something already stands there.</param>
/// <param name="Severity">What the verdict is worth.</param>
/// <param name="Message">The verdict in a sentence, naming the block in the way.</param>
public sealed record MonumentObstructionDto(
    string WoolColor, string Team, string MonumentId, int X, int Y, int Z,
    bool Obstructed, string Severity, string Message);

/// <summary>GET /api/map/{slug}/monument-obstruction — each wool monument's block must be air; a
/// pre-existing block there blocks wool placement (PGM warns on load).</summary>
/// <param name="Monuments">One verdict per monument.</param>
/// <param name="HaveLayers">Whether the map has scanned world data.</param>
public sealed record MonumentObstructionResponseDto(IReadOnlyList<MonumentObstructionDto> Monuments, bool HaveLayers);

/// <summary>One place a wool colour can be got, with the coordinates to check it at.</summary>
/// <param name="Type">What it is: <c>block</c>, <c>chest</c> or <c>dispenser</c>.</param>
/// <param name="Color">The colour it yields.</param>
/// <param name="X">Its block position, east–west.</param>
/// <param name="Y">Its height.</param>
/// <param name="Z">Its north–south position.</param>
/// <param name="Count">How much of it sits there — blocks, or items in the container.</param>
public sealed record WoolSourceDto(string Type, string Color, int X, int Y, int Z, int Count);
/// <summary>Every source of one colour inside the ground that was searched.</summary>
/// <param name="Color">The colour.</param>
/// <param name="Total">How much of it the sources hold between them.</param>
/// <param name="SourceTypes">What kinds of source they are.</param>
/// <param name="Repeatable">Whether at least one refills.</param>
/// <param name="OneTime">Whether at least one is finite.</param>
/// <param name="Sources">Each source, with its coordinates.</param>
public sealed record WoolColorSummaryDto(
    string Color, int Total, IReadOnlyList<string> SourceTypes, bool Repeatable, bool OneTime,
    IReadOnlyList<WoolSourceDto> Sources);

/// <summary>POST /api/map/{slug}/wool-sources — wool colours found inside a drawn rectangle
/// (body: <c>{ bounds: { minX, minZ, maxX, maxZ } }</c>). HaveLayers is false for an xml-only map.</summary>
/// <param name="Colors">One entry per colour found inside the rectangle.</param>
/// <param name="HaveLayers">Whether the map has scanned world data. False for an xml-only map.</param>
public sealed record WoolSourcesResponseDto(IReadOnlyList<WoolColorSummaryDto> Colors, bool HaveLayers);

/// <summary>A colour the world holds that the intent has not declared as an objective — the gap between
/// what was built and what was stated.</summary>
/// <param name="Color">The colour nobody declared.</param>
/// <param name="Total">How much of it the world holds.</param>
/// <param name="SourceTypes">What kinds of source hold it.</param>
public sealed record WoolSuggestionDto(string Color, int Total, IReadOnlyList<string> SourceTypes);

/// <summary>GET /api/map/{slug}/wool-suggestions — wool colours found in the world but not yet
/// declared as objectives.</summary>
/// <param name="Suggestions">One entry per undeclared colour, most of it first.</param>
/// <param name="HaveLayers">Whether the map has scanned world data.</param>
public sealed record WoolSuggestionsResponseDto(IReadOnlyList<WoolSuggestionDto> Suggestions, bool HaveLayers);

/// <summary>One resource block, with the coordinates to check it at.</summary>
/// <param name="Type">Which resource: <c>iron</c>, <c>gold</c>, <c>diamond</c>.</param>
/// <param name="X">Its block position, east–west.</param>
/// <param name="Y">Its height.</param>
/// <param name="Z">Its north–south position.</param>
public sealed record ResourceBlockDto(string Type, int X, int Y, int Z);
/// <summary>Every block of one resource inside the ground that was searched, and how much of it a declared
/// <c>&lt;renewable&gt;</c> already covers.</summary>
/// <param name="Type">Which resource.</param>
/// <param name="Total">How many blocks of it were found.</param>
/// <param name="Renewable">How many of those a declared renewable region covers.</param>
/// <param name="AllRenewable">Whether every one is covered, which is what an auto-config is aiming at.</param>
/// <param name="Sources">Each block, with its coordinates.</param>
public sealed record ResourceTypeSummaryDto(
    string Type, int Total, int Renewable, bool AllRenewable, IReadOnlyList<ResourceBlockDto> Sources);

/// <summary>POST /api/map/{slug}/resources — iron/gold/diamond blocks (optionally inside a drawn rect,
/// body <c>{ bounds?: { minX, minZ, maxX, maxZ } }</c>) + how many a <c>&lt;renewable&gt;</c> already
/// covers, for renewable auto-config. HaveLayers is false for an xml-only map.</summary>
/// <param name="Resources">One entry per resource found.</param>
/// <param name="HaveLayers">Whether the map has scanned world data. False for an xml-only map.</param>
public sealed record ResourceSourcesResponseDto(IReadOnlyList<ResourceTypeSummaryDto> Resources, bool HaveLayers);
