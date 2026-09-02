namespace PgmStudio.Contracts;

/// <summary>One station along a walked line (<c>GET /api/map/{slug}/transect</c>) — what stands at one cell of
/// the polyline, and how the ground stepped from the station before it. Every height is stated in one count,
/// the first free course above a block — where a walker's feet are — so two of them subtract to a number of
/// blocks.</summary>
/// <param name="X">The cell's x.</param>
/// <param name="Z">The cell's z.</param>
/// <param name="Ground">The terrain's own recorded height, null over void.</param>
/// <param name="Surface">The top of the highest rasterized span at the cell, whatever layer drew it — the
/// storey a walker actually stands on, equal to <see cref="Ground"/> on a flat board.</param>
/// <param name="Water">The course above the highest liquid in the column, or null where it holds none.</param>
/// <param name="Top">The course above the highest block of any kind in the column — what stands there reaches
/// up to it.</param>
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

/// <summary>One step of a walked route that is not a plain walk — a scramble, a barrier or a drop.</summary>
/// <param name="X">The cell the step lands on, east–west.</param>
/// <param name="Z">The same, north–south.</param>
/// <param name="Rise">The signed rise from the place before it, in blocks.</param>
/// <param name="Word">What that rise reads as — <c>scramble</c>, <c>barrier</c> or <c>drop</c> — the word
/// <c>PgmStudio.Geom.Walk.StepWord</c> gives every step in the studio.</param>
public sealed record WalkStepDto(int X, int Z, int Rise, string Word);

/// <summary>One thing standing within a stated distance of a route: the provenance record's own claim, the
/// first cell it was met at, and how far that cell is from the nearest cell the route passes through.</summary>
/// <param name="Kind">What stands there — a tree, a boulder, a house, water, a spawn, a goal, wool or an
/// iron cube.</param>
/// <param name="Unit">Which one, the claim's own identity.</param>
/// <param name="Image">Which image of that unit's orbit this is.</param>
/// <param name="X">Where the first cell it was met at stands, east–west.</param>
/// <param name="Z">The same, north–south.</param>
/// <param name="Distance">That cell's distance to the nearest cell the route passes through, in
/// cells.</param>
public sealed record WalkNeighbourDto(string Kind, string Unit, int Image, int X, int Z, int Distance);

/// <summary>What one walk over a built board answers, in the units each part is stated in.</summary>
/// <param name="Reachable">Whether there is a way at all.</param>
/// <param name="Distance">How far it is, in blocks — the octile measure a player actually walks.</param>
/// <param name="Blocks">How many blocks the player must place: the climb, and the void bridged.</param>
/// <param name="Drops">How many falls over the free height it takes.</param>
/// <param name="WorstDrop">The deepest of them, in blocks.</param>
/// <param name="Aim">Which question was asked — <c>travel</c> for the short way, <c>reach</c> for the cheap one.</param>
/// <param name="Cells">The route itself, as <c>[x, z]</c> pairs.</param>
/// <param name="Places">The same route with the storey the walk stood on at every cell — <c>[x, z, y]</c> —
/// rather than the ground under whatever roofs it.</param>
/// <param name="Steps">Every step between consecutive places that is not a plain walk, in route order.</param>
/// <param name="Rises">How many of those steps climb.</param>
/// <param name="Falls">How many drop.</param>
/// <param name="WorstStep">The largest of them, in blocks, whichever direction it ran — zero where the
/// route never left a walk.</param>
/// <param name="Beside">Every distinct thing the provenance record names within the asked distance of the
/// route (<c>?beside=N</c>), or empty where none was asked for.</param>
public sealed record WalkReadDto(bool Reachable, int Distance, int Blocks, int Drops, int WorstDrop,
    string Aim, IReadOnlyList<int[]> Cells, IReadOnlyList<int[]> Places, IReadOnlyList<WalkStepDto> Steps,
    int Rises, int Falls, int WorstStep, IReadOnlyList<WalkNeighbourDto> Beside);
