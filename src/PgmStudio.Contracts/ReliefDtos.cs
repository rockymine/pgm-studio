using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// The ground a posted layout builds, drawn as contour lines (<c>POST /map/{slug}/sketch/relief</c>) — the
/// preview the sketch canvas strokes while an author shapes the ground. Every group answers the surface the
/// build gives it.
/// </summary>
/// <param name="Interval">The height step between one contour and the next, as asked for.</param>
/// <param name="Groups">One entry per group with ground on the board.</param>
public sealed record ReliefContoursDto(double Interval, IReadOnlyList<ReliefGroupContoursDto> Groups);

/// <summary>One group's surface: how far it ranges, the box it covers, and the lines across it. The bounds are
/// inclusive on both ends, so a one-cell group has equal min and max.</summary>
/// <param name="Group">Which group, by id.</param>
/// <param name="Solved">Always false: every entry is the built surface — the highest top of every column the
/// group forms, across every layer, after the modifier stack — and its lines are read only.</param>
/// <param name="Min">Its lowest height.</param>
/// <param name="Max">Its highest.</param>
/// <param name="MinX">The west edge of the box it covers, inclusive.</param>
/// <param name="MinZ">The north edge, inclusive.</param>
/// <param name="MaxX">The east edge, inclusive.</param>
/// <param name="MaxZ">The south edge, inclusive.</param>
/// <param name="Lines">The contours across it.</param>
/// <param name="Heights">The surface itself, one block height per cell of the box above, row-major
/// from the north-west corner — <c>(z − min_z) · width + (x − min_x)</c>, where the width is
/// <c>max_x − min_x + 1</c>. A cell the group's footprint does not hold is <c>null</c>, since a box is
/// rectangular and a landmass is not.
///
/// <para>It rides with the contours because the field that traces them is already in hand, and the lines alone
/// say where the ground changes height without saying which way. A reader that only strokes contours ignores
/// the key.</para></param>
public sealed record ReliefGroupContoursDto(
    string Group,
    bool Solved,
    int Min,
    int Max,
    [property: JsonPropertyName("min_x")] int MinX,
    [property: JsonPropertyName("min_z")] int MinZ,
    [property: JsonPropertyName("max_x")] int MaxX,
    [property: JsonPropertyName("max_z")] int MaxZ,
    IReadOnlyList<ContourLineDto> Lines,
    IReadOnlyList<int?>? Heights = null);

/// <summary>
/// One contour at one height. <see cref="Points"/> is a flat <c>[x, z, x, z, …]</c> run rather than a list of
/// pairs: a line is hundreds of points and a board carries dozens of lines, so an object per point would
/// multiply the payload by the length of the words <c>x</c> and <c>z</c> — and the client strokes them in
/// pairs either way.
/// </summary>
/// <param name="Closed">Whether the line closes on itself — a group's outline does, a line running off the
/// footprint does not.</param>
/// <param name="Level">The height the contour traces.</param>
/// <param name="Points">A flat <c>[x, z, x, z, …]</c> run, strokable in pairs.</param>
public sealed record ContourLineDto(double Level, bool Closed, IReadOnlyList<double> Points);

/// <summary>What each modifier of a posted layout did to the ground (<c>POST /map/{slug}/sketch/modifiers/changes</c>),
/// one entry per modifier in the order they run.</summary>
/// <param name="Modifiers">One entry per modifier of the layout, in list order.</param>
public sealed record ModifierChangesDto(IReadOnlyList<ModifierChangeDto> Modifiers);

/// <summary>How much of the ground one modifier moved.</summary>
/// <param name="Id">The modifier, by its id.</param>
/// <param name="Columns">The columns whose top, rounded to a block, its own run changed over the ground the
/// modifiers before it left. Zero for one switched off.</param>
public sealed record ModifierChangeDto(string Id, int Columns);

/// <summary>POST /api/map/{slug}/sketch/probe-footprint — what a ring stands on, against the layout's own
/// rasterised footprint. The body is <c>{ "layout": …, "ring": [[x, z], …] }</c>; the ring need not be a shape
/// the layout carries, which is the point — it is asked <em>before</em> a shape is built on it.
///
/// <para>The counts are exclusive: <paramref name="Land"/> + <paramref name="Void"/> + <paramref name="Hole"/>
/// is every cell the ring covers.</para></summary>
/// <param name="Cells">Cells the ring covers.</param>
/// <param name="Land">Of those, cells the footprint has.</param>
/// <param name="Void">Cells past the coast, outside the footprint altogether. A lift with no ground under it
/// reads no terrain and falls back to the shape's own floor, which stands a stub in open void that nothing
/// declines.</param>
/// <param name="Hole">Cells the footprint encloses but does not fill — a hub's slots, a U-shaped room's notch.
/// Those are made by arrangement, so no region marks one and a shape dropped on top fills in the gap the
/// layout was composed to have.</param>
/// <param name="VoidCells">Where the void cells are, up to twenty-four of them.</param>
/// <param name="HoleCells">Where the hole cells are, up to twenty-four of them.</param>
public sealed record FootprintProbeDto(
    int Cells, int Land, int Void, int Hole,
    IReadOnlyList<CellDto> VoidCells, IReadOnlyList<CellDto> HoleCells);

/// <summary>One cell, so a finding can be checked at a coordinate rather than described.</summary>
/// <param name="X">East–west.</param>
/// <param name="Z">North–south.</param>
public sealed record CellDto(int X, int Z);
