using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// The map's regions as the editor reads them (<c>GET /map/{slug}/regions/tree</c>): the root regions
/// grouped by what they are for, each carrying the regions nested inside it, and the extent everything is
/// drawn against.
///
/// <para>This is the read every editor sidebar and the world canvas make on load, so its shape is what the
/// region UI is written against. A region that is named as another region's child is not a root and appears
/// only inside its parent, which is what makes this a tree rather than the flat registry
/// <c>GET /map/{slug}/regions</c> answers.</para>
/// </summary>
/// <param name="BoundingBox">The extent the canvas fits to — the stored surface box, or the detected
/// islands' AABB where no box was stored. Null on a map with neither.</param>
public sealed record RegionTreeDto(
    IReadOnlyList<RegionGroupDto> Groups,
    [property: JsonPropertyName("bounding_box")] Bounds2dDto? BoundingBox);

/// <summary>One category of root regions, in the order the editor offers them. <see cref="Name"/> is the
/// derived category the group is keyed on (<c>spawn</c>, <c>build</c>, <c>objective</c>, <c>other</c>) and
/// <see cref="Label"/> is what it is called on screen.</summary>
public sealed record RegionGroupDto(string Name, string Label, IReadOnlyList<RegionNodeDto> Regions);

/// <summary>
/// One region in the tree, and every region nested under it.
///
/// <para><see cref="Category"/> is the region's <b>own</b> derived category, which is not always the group it
/// renders under: a build region nested inside a <c>not-build-area</c> rule container renders under
/// <c>other</c> and is still a build region, so a consumer that wants build regions reads this rather than
/// the group. <see cref="Subtype"/> refines it — a spawn is a <c>point</c> or a <c>protection</c>, a wool is a
/// <c>room</c>, <c>monument</c> or <c>spawner</c>.</para>
///
/// <para><see cref="Source"/> is the region a transform derives from — the thing a <c>mirror</c> mirrors or a
/// <c>translate</c> moves — encoded as a node of its own rather than as an id, so a reader can draw it
/// without a second lookup. <see cref="Wiring"/> is the spatial filter events applied here
/// (<c>enter=only-blue</c>, <c>block_break=…</c>), absent on a region nothing is wired to.</para>
/// </summary>
/// <param name="Id">The region's <c>map.xml</c> id, or empty for an anonymous inline region — which is what
/// <see cref="SyntheticId"/> says.</param>
/// <param name="Label">What to show: the id, or <c>(type)</c> for an anonymous one, or <c>→ target</c> for a
/// reference.</param>
/// <param name="Coords">The region's own defining numbers, keyed by what its <see cref="Type"/> is made of —
/// <c>min_x</c>…<c>max_z</c> for a cuboid, <c>center_x</c>/<c>radius</c> for a circle, <c>offset_x</c>… for a
/// translate. An open object rather than a record, because ten region types state their shape ten ways and a
/// fixed set of fields would have to be null for nine of them.</param>
/// <param name="DraftStep">The editor step a freshly drawn, not-yet-wired region was drawn in, so the
/// activity that drew it can still show it. Absent once the region is wired, and on every imported one.</param>
/// <param name="Polygon2d">The region's footprint as rings, for the types that have one. Present only where
/// the geometry could be computed against the map's extent.</param>
public sealed record RegionNodeDto(
    string Id,
    string Type,
    string Label,
    RegionExtentDto? Bounds,
    IReadOnlyDictionary<string, JsonElement>? Coords,
    [property: JsonPropertyName("is_negative")] bool IsNegative,
    [property: JsonPropertyName("synthetic_id")] bool SyntheticId,
    string? Category,
    string? Subtype,
    [property: JsonPropertyName("draft_step")] string? DraftStep,
    IReadOnlyList<string>? Wiring,
    IReadOnlyList<RegionNodeDto> Children,
    RegionNodeDto? Source,
    [property: JsonPropertyName("polygon_2d")] RegionPolygonDto? Polygon2d = null);

/// <summary>
/// One region's extent in plan, which is <b>not</b> always four numbers: a half-space is unbounded on one
/// side and states that side as the string <c>oo</c> or <c>-oo</c>, exactly as the <c>map.xml</c> attribute
/// spells it. The values cross as they are written rather than being coerced, because a reader that clamps an
/// infinity has silently drawn a different region.
/// </summary>
public sealed record RegionExtentDto(
    [property: JsonPropertyName("min_x")] JsonElement MinX,
    [property: JsonPropertyName("min_z")] JsonElement MinZ,
    [property: JsonPropertyName("max_x")] JsonElement MaxX,
    [property: JsonPropertyName("max_z")] JsonElement MaxZ);

/// <summary>A footprint in block coordinates, spelled the way the region surface spells it — the
/// <c>bounds_2d</c> of the contract. Four numbers, always: unlike a region <b>extent</b>, which may be
/// unbounded on a side, this comes from a stored box, a measured island footprint or a region the editor
/// just resolved.</summary>
public sealed record Bounds2dDto(
    [property: JsonPropertyName("min_x")] double MinX,
    [property: JsonPropertyName("min_z")] double MinZ,
    [property: JsonPropertyName("max_x")] double MaxX,
    [property: JsonPropertyName("max_z")] double MaxZ);

/// <summary>
/// A region's footprint as rings of <c>[x, z]</c> pairs. <see cref="Polygons"/> is every part — a union of
/// disjoint shapes has several — and <see cref="Exterior"/> and <see cref="Holes"/> repeat the first part's
/// rings at the top level, for a reader that draws one outline and does not handle the multi-part case.
/// </summary>
public sealed record RegionPolygonDto(
    IReadOnlyList<RegionRingsDto> Polygons,
    IReadOnlyList<IReadOnlyList<double>> Exterior,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> Holes);

/// <summary>One part of a footprint: its outline, and the rings cut out of it.</summary>
public sealed record RegionRingsDto(
    IReadOnlyList<IReadOnlyList<double>> Exterior,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> Holes);
