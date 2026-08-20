using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// What a world scan wrote: the map it wrote for, and one count per kind of feature row it replaced.
///
/// <para>Three routes answer this and they are the same answer — <c>POST /map/{slug}/scan-world</c> re-reads
/// a world already on disk, <c>POST /map/import-folder</c> and <c>POST /map/import-url</c> create the map
/// row first and then scan it. What differs is only how the world was reached, which is what
/// <see cref="RegionDir"/> and <see cref="McaFiles"/> say and why each is absent on the routes that cannot
/// answer it.</para>
///
/// <para>The counts are <c>WorldFeatureWriter</c>'s own, so a caller reads the same eight numbers whichever
/// route it came through — and a scan that wrote nothing is eight zeroes rather than a missing key.</para>
/// </summary>
/// <param name="RegionDir">The folder the world was read out of. Answered by <c>scan-world</c>, which is the
/// route that takes a world where it already sits.</param>
/// <param name="McaFiles">How many region files the downloaded archive held. Answered by <c>import-url</c>,
/// which is the route that has an archive to count.</param>
public sealed record WorldScanDto(
    string Slug,
    [property: JsonPropertyName("wool_blocks")] int WoolBlocks,
    [property: JsonPropertyName("resource_blocks")] int ResourceBlocks,
    [property: JsonPropertyName("chest_items")] int ChestItems,
    [property: JsonPropertyName("spawner_blocks")] int SpawnerBlocks,
    [property: JsonPropertyName("layer_segments")] int LayerSegments,
    int Islands,
    [property: JsonPropertyName("monument_candidates")] int MonumentCandidates,
    [property: JsonPropertyName("core_candidates")] int CoreCandidates,
    [property: JsonPropertyName("region_dir"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RegionDir = null,
    [property: JsonPropertyName("mca_files"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? McaFiles = null);

/// <summary>A world sitting in the imports root that no map has been made from yet
/// (<c>GET /maps/import-candidates</c>): the folder it is in, the slug it would be imported under, and how
/// many region files it holds — which is the only measure of size available before it is read.</summary>
public sealed record ImportCandidateDto(
    string Folder,
    string Slug,
    [property: JsonPropertyName("region_files")] int RegionFiles);

/// <summary>
/// The import brief (<c>GET /map/{slug}/scan-summary</c>): what the scan actually found in the world, grouped
/// so an author can see it at a glance rather than as row counts.
/// </summary>
/// <param name="ChestCount">Distinct chest positions. <paramref name="ChestItems"/> counts the slots inside
/// them, so the two differ and both are worth saying — one chest holding nine stacks is 1 and 9.</param>
public sealed record ScanSummaryDto(
    [property: JsonPropertyName("wool_colors")] IReadOnlyList<WoolColorCountDto> WoolColors,
    [property: JsonPropertyName("resource_types")] IReadOnlyList<ResourceTypeCountDto> ResourceTypes,
    [property: JsonPropertyName("chest_count")] int ChestCount,
    [property: JsonPropertyName("chest_items")] int ChestItems);

/// <summary>One wool colour the world holds, most of it first. <see cref="Hex"/> is the swatch colour the
/// block actually is, so the brief shows the colour rather than naming it.</summary>
public sealed record WoolColorCountDto(string Color, string Name, string Hex, int Count);

/// <summary>One resource the world holds — iron, gold, diamond — most of it first.</summary>
public sealed record ResourceTypeCountDto(string Type, string Name, int Count);
