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
/// <param name="Fluid">The course above the highest water or lava in the column, or null where it holds none.</param>
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
public sealed record TransectStationDto(int X, int Z, int? Ground, int? Surface, int? Fluid, int? Top,
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

/// <summary>One block of a drawn route, walked down its own centreline
/// (<c>GET /api/map/{slug}/stroke</c>).</summary>
/// <param name="X">The cell's x.</param>
/// <param name="Z">The cell's z.</param>
/// <param name="Ground">The terrain's own recorded height, null over void.</param>
/// <param name="Paved">Whether the pass's own claim reaches this cell — the style, the coverage and the seed
/// decide which cells of a band take surface, so this is read off the claim rather than the document.</param>
/// <param name="Material">The surface block under the station as <c>id:data name</c>, null over void.</param>
/// <param name="Step">The rise from the station before — null at the first and across void.</param>
/// <param name="Word">What that step reads as: <c>walk</c>, <c>scramble</c>, <c>barrier</c>, <c>drop</c>, or
/// <c>void</c> where there is no ground.</param>
public sealed record StrokeStationDto(
    int X, int Z, int? Ground, bool Paved, string? Material, int? Step, string Word);

/// <summary>One surface block a road is paved with.</summary>
/// <param name="Material">The block, as <c>id:data name</c>.</param>
/// <param name="Cells">How many of its paved stations carry it.</param>
public sealed record StrokeRunDto(string Material, int Cells);

/// <summary>A stretch of a road the paving does not reach — ground kept clear, coverage the style left out,
/// or the line running off the board.</summary>
/// <param name="FromX">Where the gap starts.</param>
/// <param name="FromZ">Where the gap starts.</param>
/// <param name="ToX">Where it ends.</param>
/// <param name="ToZ">Where it ends.</param>
/// <param name="Cells">How many stations long it is.</param>
public sealed record StrokeGapDto(int FromX, int FromZ, int ToX, int ToZ, int Cells);

/// <summary>A drawn stroke walked end to end (<c>GET /api/map/{slug}/stroke?id=</c>) — the paving as it was
/// laid, which <c>walk</c> cannot answer because that walks the way a player would choose.</summary>
/// <param name="Id">The stroke's own id.</param>
/// <param name="Image">Which orbit image was walked.</param>
/// <param name="Images">How many the orbit has.</param>
/// <param name="ClaimsGround">Whether the stroke holds the ground it paves against what is placed after it —
/// paint claims nothing and nothing stands off it.</param>
/// <param name="Stations">Every block of the centreline, in order.</param>
/// <param name="Paved">How many of them the paving reaches.</param>
/// <param name="Rises">How many stations step up from the one before.</param>
/// <param name="Falls">How many step down.</param>
/// <param name="WorstStep">The largest step either way, in blocks.</param>
/// <param name="Events">Every step that is not a plain walk, in order.</param>
/// <param name="Materials">What the road is paved with, most of it first.</param>
/// <param name="MaterialRuns">How many unbroken runs of one material the paving falls into — one is a road of
/// a single block, and a count near its own length is a road speckled over a patterned ground.</param>
/// <param name="Gaps">Where the paving does not reach.</param>
public sealed record StrokeReadDto(
    string Id, int Image, int Images, bool ClaimsGround, IReadOnlyList<StrokeStationDto> Stations, int Paved,
    int Rises, int Falls, int WorstStep, IReadOnlyList<string> Events,
    IReadOnlyList<StrokeRunDto> Materials, int MaterialRuns, IReadOnlyList<StrokeGapDto> Gaps);

/// <summary>One step of a walked route that is not a plain walk — a scramble, a barrier, a drop, or the climb
/// onto or off a stated wall.</summary>
/// <param name="X">The cell the step lands on, east–west.</param>
/// <param name="Z">The same, north–south.</param>
/// <param name="Rise">The signed rise from the place before it, in blocks.</param>
/// <param name="Word">What that rise reads as — <c>scramble</c>, <c>barrier</c> or <c>drop</c>, the word
/// <c>PgmStudio.Geom.Walk.StepWord</c> gives every step in the studio, or <c>wall</c> where the step lands on
/// or leaves a plan's wall.</param>
public sealed record WalkStepDto(int X, int Z, int Rise, string Word);

/// <summary>One thing standing within a stated distance of a route: the provenance record's own claim, the
/// first cell it was met at, and how far that cell is from the nearest cell the route passes through.</summary>
/// <param name="Kind">What stands there — a tree, a boulder, a house, a fluid, a spawn, a goal, wool or
/// an iron cube.</param>
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
/// <param name="WorstStep">The largest of them that is not a <c>wall</c> step, in blocks, whichever
/// direction it ran — zero where the route never left a walk.</param>
/// <param name="Beside">Every distinct thing the provenance record names within the asked distance of the
/// route (<c>?beside=N</c>), or empty where none was asked for.</param>
public sealed record WalkReadDto(bool Reachable, int Distance, int Blocks, int Drops, int WorstDrop,
    string Aim, IReadOnlyList<int[]> Cells, IReadOnlyList<int[]> Places, IReadOnlyList<WalkStepDto> Steps,
    int Rises, int Falls, int WorstStep, IReadOnlyList<WalkNeighbourDto> Beside);

/// <summary>One picture of the board from a player's eye (<c>GET /api/map/{slug}/views</c>): what it looks at,
/// where the eye stands where the view says so, and the <c>render/eye</c> query words that draw it.</summary>
/// <param name="Id">The view's id — <c>view-N</c> for one an author kept, a word naming the thing for one
/// the studio suggests.</param>
/// <param name="Name">What the gallery calls it.</param>
/// <param name="Kept">Whether an author kept it; false for one the studio suggests from the board.</param>
/// <param name="LookX">The thing looked at, east–west.</param>
/// <param name="LookZ">The same, north–south.</param>
/// <param name="FromX">Where the eye stands, east–west, or null for a view that leaves the eye to find its
/// own place.</param>
/// <param name="FromZ">The same, north–south.</param>
/// <param name="Y">The eye's height, or null for a player's eye over the ground it stands on.</param>
/// <param name="Pitch">How far the eye tips down, in degrees, or null to tip it to the middle of what it
/// looks at.</param>
/// <param name="Query">The query words <c>GET …/render/eye</c> draws this view with; a caller adds its own
/// <c>width</c> and <c>height</c>.</param>
/// <param name="Eye">The camera the view resolves to on the board as it is built — where the eye ends up
/// standing when the view leaves it to find its own place — or null where the server has no block textures to
/// resolve it with, or no place sees what the view looks at.</param>
/// <param name="Own">Whether this is the board's own straight-down view, which every board keeps: it can be
/// changed, and it is never let go.</param>
/// <param name="Yaw">Which way the eye turns, in the game's degrees, where the view states it whole; null for a
/// view that faces what it looks at.</param>
/// <param name="Picture">Whether the map's picture — the <c>map.png</c> an export carries — is drawn from this
/// view: the kept view marked as it, else the board's overview.</param>
public sealed record MapViewDto(string Id, string Name, bool Kept, int LookX, int LookZ,
    int? FromX, int? FromZ, double? Y, double? Pitch, string Query, EyeCameraDto? Eye = null, bool Own = false,
    double? Yaw = null, bool Picture = false);

/// <summary>Every view a map has: the studio's suggestions first, then the ones kept.</summary>
/// <param name="Views">The views, suggestions first.</param>
/// <param name="Undrawable">Why none of them can be drawn on this server — the block textures are not
/// there — or null where they can.</param>
public sealed record MapViewsDto(IReadOnlyList<MapViewDto> Views, string? Undrawable);

/// <summary>A view to keep (<c>POST /api/map/{slug}/views</c>), or what a kept view becomes
/// (<c>PUT /api/map/{slug}/views/{viewId}</c>).</summary>
/// <param name="Name">What to call it. Blank takes <c>View N</c> for a new view and keeps the name of one
/// being changed.</param>
/// <param name="LookX">The thing to look at, east–west.</param>
/// <param name="LookZ">The same, north–south.</param>
/// <param name="FromX">Where to stand, east–west; null leaves the eye to find its own place.</param>
/// <param name="FromZ">The same, north–south, stated together with <paramref name="FromX"/>.</param>
/// <param name="Y">The eye's height, 0 to 320; null stands it a player's eye over the ground.</param>
/// <param name="Pitch">How far the eye tips down, −90 to 90 degrees, 90 straight down — an aerial shot is an eye
/// raised with <paramref name="Y"/> and tipped down with this; null tips it to the middle of what it looks
/// at.</param>
/// <param name="Yaw">Which way the eye turns, in the game's degrees — 0 south, 90 west, 180 north, −90 east, from −180 to 180.
/// Stated with <paramref name="FromX"/>, <paramref name="FromZ"/> and <paramref name="Y"/>, it states the camera
/// whole and <paramref name="LookX"/>, <paramref name="LookZ"/> is only where its middle lands; null faces what
/// it looks at.</param>
/// <param name="Picture">True draws the map's picture from this view, and from no other; false stops drawing it
/// from this one, which leaves the overview. Null leaves a changed view as it was and a new one not.</param>
public sealed record MapViewKeepRequest(string? Name, int LookX, int LookZ,
    int? FromX = null, int? FromZ = null, double? Y = null, double? Pitch = null, double? Yaw = null,
    bool? Picture = null);

/// <summary>One reading in a map's report (<c>GET /api/map/{slug}/report</c>): what it is, the route that
/// answers the same reading on its own, and the reading itself — or why there is none.</summary>
/// <param name="Name">What the reading is, as the report names it — <c>slopes</c>,
/// <c>transect spawn-0 along x</c>.</param>
/// <param name="Route">The route, relative to the map, that answers the same reading alone, with the words that
/// pick it. A posted read is prefixed <c>POST</c> and takes the stored layout as its body.</param>
/// <param name="Text">The reading as text, or null where there is none.</param>
/// <param name="Missing">Why there is no reading — a map with no plan has no grid — or null where there is
/// one.</param>
public sealed record MapReportReadDto(string Name, string Route, string? Text, string? Missing);

/// <summary>One picture a map's report names: what it shows, the route that draws it, and — where the report
/// was asked for its pictures — the picture itself.</summary>
/// <param name="Name">What the picture shows — <c>isometric</c>, <c>heightmap</c>, a view's own id.</param>
/// <param name="Route">The <c>GET</c> route, relative to the map, that draws it.</param>
/// <param name="Png">The picture, base64 PNG, drawn only on <c>?pictures=true</c>.</param>
/// <param name="Missing">Why it could not be drawn, or null.</param>
public sealed record MapReportPictureDto(string Name, string Route, string? Png, string? Missing);

/// <summary>The three numbers a board is wrong or right by: how much of its ground steps further than a player
/// walks, whether every prop its document names is in the world, and the worst step between a spawn and a
/// goal.</summary>
/// <param name="Walked">Cells of ground whose worst step to a neighbour is a walk.</param>
/// <param name="Scrambled">Cells whose worst step wants a placed block.</param>
/// <param name="Barrier">Cells whose worst step is a face.</param>
/// <param name="Placed">Props the dressing pass placed.</param>
/// <param name="Declined">Props it declined: each one named by the document and not in the world.</param>
/// <param name="WorstStep">The worst step on any route walked from a spawn to a goal, or null where none
/// was.</param>
/// <param name="WorstRoute">The route that step is on, as the report names it.</param>
/// <param name="Says">The three numbers as the three lines a reader takes first — the words the text report
/// opens on and the Sketch tool shows.</param>
public sealed record MapReportHeadlineDto(int Walked, int Scrambled, int Barrier, int Placed, int Declined,
    int? WorstStep, string? WorstRoute, IReadOnlyList<string> Says);

/// <summary>Everything a drive reads back about a stored map, off one build (<c>GET /api/map/{slug}/report</c>):
/// the three numbers first, then every reading in the order a reader meets them, then the pictures by the
/// routes that draw them.</summary>
/// <param name="Slug">The map.</param>
/// <param name="Change">The change of the map the report reads — its latest.</param>
/// <param name="Headline">The three numbers.</param>
/// <param name="Reads">Every reading, each with the route that answers it alone.</param>
/// <param name="Pictures">Every picture, by the route that draws it.</param>
public sealed record MapReportDto(string Slug, long Change, MapReportHeadlineDto Headline,
    IReadOnlyList<MapReportReadDto> Reads, IReadOnlyList<MapReportPictureDto> Pictures);
