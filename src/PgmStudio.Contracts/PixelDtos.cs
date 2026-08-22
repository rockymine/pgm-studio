using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// A footprint drawn as block pixels, in whichever of three encodings is smallest for it. Every one carries
/// the same bounds, so a caller reads the box first and then whichever arrays are present.
///
/// <para><b>Colours by index, and cells by run, because terrain repeats.</b> A board is tens of thousands of
/// cells drawn from under twenty materials, so the colour goes out once in <see cref="Palette"/> and an index
/// stands in for it; and a painted footprint emits 0.05 to 0.27 runs per cell, because even a voronoi paints
/// in patches and the void between islands is one long run. The producer picks whichever is smaller and the
/// reader decodes all three, so the choice costs nothing and tunes itself.</para>
///
/// <para><b>An absent array is absent, not null.</b> The encodings are alternatives rather than optional
/// extras, so a reader tells them apart by which keys the object has — which is the same test whether it
/// reads this or the raw JSON.</para>
/// </summary>
/// <param name="Xs">With <paramref name="Zs"/>, one entry per painted cell.</param>
/// <param name="Zs">The other half of the per-cell coordinate pair.</param>
/// <param name="Colors">A full hex colour per cell — the encoding with no palette, for a read whose cells
/// carry no repetition worth indexing.</param>
/// <param name="Palette">The distinct colours, once each, indexed by <paramref name="ColorIdx"/> and by the
/// values inside <paramref name="Runs"/>.</param>
/// <param name="ColorIdx">One palette index per cell, parallel to <paramref name="Xs"/>.</param>
/// <param name="Runs">The bounding box walked row by row as <c>[paletteIndex, length, …]</c>, with
/// <c>-1</c> for a cell the footprint does not cover.</param>
/// <param name="MinX">The west edge of the ground it covers, in blocks.</param>
/// <param name="MinZ">The north edge.</param>
/// <param name="MaxX">The east edge.</param>
/// <param name="MaxZ">The south edge.</param>
public sealed record BlockPixelsDto(
    [property: JsonPropertyName("min_x")] int MinX,
    [property: JsonPropertyName("min_z")] int MinZ,
    [property: JsonPropertyName("max_x")] int MaxX,
    [property: JsonPropertyName("max_z")] int MaxZ,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? Xs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? Zs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Colors = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Palette = null,
    [property: JsonPropertyName("color_idx"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? ColorIdx = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? Runs = null);

/// <summary>
/// A built world as the solid runs of every column holding anything, which is what the 3-D preview meshes.
/// The client decides what is visible, because only the client knows which way the camera points; this says
/// what is there.
///
/// <para><see cref="Cols"/> is one flat integer array walked with its own stride —
/// <c>[x, z, runCount, (yTop, yBottom, paletteIndex) × runCount, …]</c> — rather than a record per run,
/// because a board is over a hundred thousand runs and a pair of field names each would be most of the
/// bytes. The bounds are the footprint the columns cover, degenerate when there are none, so a caller
/// decodes one shape either way.</para>
/// </summary>
/// <param name="Palette">The distinct colours, once each, indexed by the values inside
/// <paramref name="Cols"/>.</param>
/// <param name="Cols">One flat array walked with its own stride —
/// <c>[x, z, runCount, (yTop, yBottom, paletteIndex) × runCount, …]</c> — rather than a record per run,
/// because a board is over a hundred thousand runs.</param>
/// <param name="MinX">The west edge of the ground it covers, in blocks.</param>
/// <param name="MinZ">The north edge.</param>
/// <param name="MaxX">The east edge.</param>
/// <param name="MaxZ">The south edge.</param>
public sealed record WorldColumnsDto(
    IReadOnlyList<string> Palette,
    IReadOnlyList<int> Cols,
    [property: JsonPropertyName("min_x")] int MinX,
    [property: JsonPropertyName("min_z")] int MinZ,
    [property: JsonPropertyName("max_x")] int MaxX,
    [property: JsonPropertyName("max_z")] int MaxZ);

/// <summary>
/// The vertical segments of a map projected onto one face, as the side-view canvas draws it: a
/// (primary × y) grid whose cells hold how far back the nearest solid block is, normalised to 0–255 with 0
/// nearest and <c>-1</c> for a cell nothing occupies.
///
/// <para><see cref="Axis"/> is the direction the camera looks from — <c>nz</c>/<c>pz</c> look along Z so the
/// primary axis is x, <c>nx</c>/<c>px</c> look along X so it is z. <see cref="Depth"/> is one flat array of
/// <c>PrimaryCount × YCount</c> cells rather than a row per y, for the reason every other canvas encoding is
/// flat: a face is tens of thousands of cells and a nested shape would be mostly punctuation.</para>
/// </summary>
/// <param name="Axis">Which way the camera looks from: <c>nz</c>/<c>pz</c> look along Z so the primary
/// axis is x, <c>nx</c>/<c>px</c> look along X so it is z.</param>
/// <param name="PrimaryMin">Where the primary axis starts, in blocks.</param>
/// <param name="PrimaryCount">How many columns it runs for, which is the grid's width.</param>
/// <param name="YMin">The lowest y the face covers.</param>
/// <param name="YCount">How many levels it runs for, which is the grid's height.</param>
/// <param name="Depth">One flat array of <c>PrimaryCount × YCount</c> cells, each how far back the nearest
/// solid block is — 0 nearest, 255 furthest, <c>-1</c> where nothing stands.</param>
public sealed record SegmentsDto(
    string Axis,
    [property: JsonPropertyName("primary_min")] int PrimaryMin,
    [property: JsonPropertyName("primary_count")] int PrimaryCount,
    [property: JsonPropertyName("y_min")] int YMin,
    [property: JsonPropertyName("y_count")] int YCount,
    IReadOnlyList<short> Depth);

/// <summary>The terrain floor at one column — the Y a thing dropped there comes to rest on, or null where the
/// column has no segment data at all. Null rather than an absent key, because "this map has nothing there" is
/// the answer rather than the absence of one.</summary>
/// <param name="Y">The height a thing dropped at that column comes to rest on, or null where the column
/// has no segment data at all.</param>
public sealed record ColumnFloorDto(int? Y);
