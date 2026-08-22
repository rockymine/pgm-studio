using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// The relief a posted layout solves to, drawn as contour lines (<c>POST /map/{slug}/sketch/relief</c>) —
/// the preview the sketch canvas strokes while an author shapes the ground.
/// </summary>
/// <param name="Interval">The height step between one contour and the next, as asked for.</param>
/// <param name="Islands">One entry per island the layout carries.</param>
public sealed record ReliefContoursDto(double Interval, IReadOnlyList<ReliefIslandContoursDto> Islands);

/// <summary>One island's solved surface: how far it ranges, the box it covers, and the lines across it. The
/// bounds are inclusive on both ends, so a one-cell island has equal min and max.</summary>
/// <param name="Island">Which island, by id.</param>
/// <param name="Min">Its lowest solved height.</param>
/// <param name="Max">Its highest.</param>
/// <param name="MinX">The west edge of the box it covers, inclusive.</param>
/// <param name="MinZ">The north edge, inclusive.</param>
/// <param name="MaxX">The east edge, inclusive.</param>
/// <param name="MaxZ">The south edge, inclusive.</param>
/// <param name="Lines">The contours across it.</param>
public sealed record ReliefIslandContoursDto(
    string Island,
    int Min,
    int Max,
    [property: JsonPropertyName("min_x")] int MinX,
    [property: JsonPropertyName("min_z")] int MinZ,
    [property: JsonPropertyName("max_x")] int MaxX,
    [property: JsonPropertyName("max_z")] int MaxZ,
    IReadOnlyList<ContourLineDto> Lines);

/// <summary>
/// One contour at one height. <see cref="Points"/> is a flat <c>[x, z, x, z, …]</c> run rather than a list of
/// pairs: a line is hundreds of points and a board carries dozens of lines, so an object per point would
/// multiply the payload by the length of the words <c>x</c> and <c>z</c> — and the client strokes them in
/// pairs either way.
/// </summary>
/// <param name="Closed">Whether the line closes on itself — an island's outline does, a line running off the
/// footprint does not.</param>
/// <param name="Level">The height the contour traces.</param>
/// <param name="Points">A flat <c>[x, z, x, z, …]</c> run, strokable in pairs.</param>
public sealed record ContourLineDto(double Level, bool Closed, IReadOnlyList<double> Points);

/// <summary>
/// What the relief a posted layout carries actually <b>charges</b>, per island
/// (<c>POST /map/{slug}/sketch/relief/read</c>).
///
/// <para>Not a walkability score. A relief walkable everywhere is a field rather than a map, and one number
/// ranks every deliberate barrier as a defect — so this states reachability at each of the game's three
/// thresholds, separates places from ledges, qualifies faces as cliffs by the corpus rule, measures crossings
/// in both directions, and reports the symmetry error, which nothing else would show.</para>
/// </summary>
/// <param name="Islands">One reading per island the layout carries.</param>
public sealed record ReliefReadDto(IReadOnlyList<ReliefIslandReadDto> Islands);

/// <summary>
/// One island's relief, read.
/// </summary>
/// <param name="Cells">How much ground there is to read.</param>
/// <param name="Relief">The whole range, <paramref name="High"/> less <paramref name="Low"/>.</param>
/// <param name="Steps">How far the ground moves between neighbouring cells, counted per step size — the
/// histogram every tier below is a cut of, reported raw as well.</param>
/// <param name="Tiers">How the surface reads at a jump, at a placed block, and at building in earnest.</param>
/// <param name="Faces">The first twelve faces the terrain presents. The tail is all banks, which is why the
/// list is cut and <paramref name="FaceCount"/> says how many there were.</param>
/// <param name="Cliffs">How many of the faces qualify as cliffs by the corpus rule rather than by intent.</param>
/// <param name="AcrossX">What crossing the island costs along x, measured in both directions — a drop is free
/// the way it falls, so a face that stops a crossing one way lets it through the other.</param>
/// <param name="SymmetryError">How far the two halves disagree. A relief that is unfair is unfair invisibly,
/// since nothing else about it looks wrong.</param>
/// <param name="Island">Which island, by id.</param>
/// <param name="Low">Its lowest solved height.</param>
/// <param name="High">Its highest.</param>
/// <param name="FaceCount">How many faces the terrain presents in total, since
/// <paramref name="Faces"/> is cut at twelve.</param>
/// <param name="AcrossZ">What crossing the island costs along z, measured in both directions.</param>
public sealed record ReliefIslandReadDto(
    string Island,
    int Cells, int Low, int High, int Relief,
    IReadOnlyDictionary<string, int> Steps,
    IReadOnlyList<ReliefTierDto> Tiers,
    IReadOnlyList<ReliefFaceDto> Faces,
    int FaceCount,
    int Cliffs,
    ReliefFordsDto AcrossX,
    ReliefFordsDto AcrossZ,
    int SymmetryError);

/// <summary>How a surface reads at one passability threshold: what share a player can cross, how many
/// <b>places</b> that leaves, how much of the ground the largest holds, and how many <b>ledges</b> are
/// stranded off it. Counting places and ledges together is what turns one connected map with twenty cliff-top
/// ledges into a meaningless twenty-one pieces.</summary>
/// <param name="Name">The threshold this reads at — a jump, a placed block, building in earnest.</param>
/// <param name="MaxStep">The height difference a player can cross at it, in blocks.</param>
/// <param name="Share">How much of the island is crossable at it, 0–1.</param>
/// <param name="Places">How many separate pieces of ground that leaves.</param>
/// <param name="LargestPlace">How much of the ground the largest of them holds, 0–1.</param>
/// <param name="Ledges">How many pieces are stranded off it — counted apart from
/// <paramref name="Places"/>, since one connected map with twenty cliff-top ledges is not twenty-one
/// places.</param>
public sealed record ReliefTierDto(
    string Name, int MaxStep, double Share, int Places, double LargestPlace, int Ledges);

/// <summary>One face the terrain presents: which way it looks, how wide it runs, how far it drops, and whether
/// that makes it a cliff. A landform is wider than it is tall; a narrow face at a big drop is a structure.</summary>
/// <param name="Facing">Which way the face looks.</param>
/// <param name="Width">How wide it runs, in blocks.</param>
/// <param name="Drop">How far it falls.</param>
/// <param name="Cliff">Whether that makes it a cliff by the corpus rule — a landform is wider than it is
/// tall, so a narrow face at a big drop is a structure.</param>
public sealed record ReliefFaceDto(string Facing, int Width, int Drop, bool Cliff);

/// <summary>What a barrier costs to cross in one direction: how many rows meet it, how many are passable on
/// foot, how many with a placed block, and how many are simply descended.</summary>
/// <param name="Rows">How many rows meet the barrier.</param>
/// <param name="OnFoot">How many of those are passable on foot.</param>
/// <param name="WithBlock">How many with a placed block.</param>
/// <param name="Descended">How many are simply descended — free the way they fall.</param>
public sealed record ReliefFordsDto(int Rows, int OnFoot, int WithBlock, int Descended);
