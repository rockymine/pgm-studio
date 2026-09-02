namespace PgmStudio.Contracts;

/// <summary>One station along a walked line (<c>GET /api/map/{slug}/transect</c>) — what stands at one cell of
/// the polyline, and how the ground stepped from the station before it.</summary>
/// <param name="X">The cell's x.</param>
/// <param name="Z">The cell's z.</param>
/// <param name="Ground">The terrain's own recorded height, null over void.</param>
/// <param name="Surface">The top of the highest rasterized span at the cell, whatever layer drew it — the
/// storey a walker actually stands on, equal to <see cref="Ground"/> on a flat board.</param>
/// <param name="Water">The highest liquid course in the column, or null where it holds none.</param>
/// <param name="Top">The highest block of any kind in the column — what stands there reaches this high.</param>
/// <param name="Standing">The claim a walker stands on, as <c>"&lt;kind&gt; &lt;unit&gt;"</c>
/// (<c>"tree tree-2"</c>, <c>"house barn"</c>), or <c>"storey"</c> where the board stacks above the terrain
/// with no claim of its own, else null.</param>
/// <param name="Step">The rise from the previous station's <see cref="Surface"/> — null at the first station
/// and wherever either side is void.</param>
/// <param name="Word">What <see cref="Step"/> reads as: <c>walk</c>, <c>scramble</c>, <c>barrier</c> or
/// <c>drop</c> the way <c>PgmStudio.Geom.Walk.StepWord</c> classes every step in the studio, or <c>void</c>
/// where there is no ground.</param>
public sealed record TransectStationDto(int X, int Z, int? Ground, int? Surface, int? Water, int? Top,
    string? Standing, int? Step, string Word);

/// <summary>One claim found within reach of a walked line — the first cell it was met at.</summary>
/// <param name="Kind">The family the claim belongs to — <c>house</c>, <c>spawn</c>, <c>wool</c>, and the rest
/// <c>StampId.Kind</c> names.</param>
/// <param name="Unit">Which authored thing this is an image of.</param>
/// <param name="Image">Which orbit image this is, 0 for the authored unit itself.</param>
/// <param name="X">Where it was met.</param>
/// <param name="Z">Where it was met.</param>
public sealed record TransectNeighbourDto(string Kind, string Unit, int Image, int X, int Z);

/// <summary>A polyline walked block by block and answered as numbers (<c>GET /api/map/{slug}/transect</c>) —
/// the read for a claim about a shape, since a bank, a wall, a stair or a basin is a profile and never a
/// point.</summary>
/// <param name="Stations">Every station along the line, in the order it was walked.</param>
/// <param name="Rises">How many stations stepped up from the one before, whatever word the step earned.</param>
/// <param name="Falls">How many stepped down.</param>
/// <param name="WorstStep">The largest rise or fall met, in blocks.</param>
/// <param name="Barriers">How many stations crossed a step steeper than a scramble.</param>
/// <param name="Scrambles">How many crossed a step a player scrambles up.</param>
/// <param name="Drops">How many fell further than a player drops for free.</param>
/// <param name="Events">Every non-walk step as a sentence, in the order the line meets them —
/// <c>"BARRIER +8 at (-52, 0)"</c>.</param>
/// <param name="Beside">Every distinct claim within the asked-for reach of any station — empty both where none
/// stood near the line and where nobody asked.</param>
public sealed record TransectDto(IReadOnlyList<TransectStationDto> Stations, int Rises, int Falls,
    int WorstStep, int Barriers, int Scrambles, int Drops, IReadOnlyList<string> Events,
    IReadOnlyList<TransectNeighbourDto> Beside);
