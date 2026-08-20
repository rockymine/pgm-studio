using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// The relief a posted layout solves to, drawn as contour lines (<c>POST /map/{slug}/sketch/relief</c>) —
/// the preview the sketch canvas strokes while an author shapes the ground.
/// </summary>
/// <param name="Interval">The height step between one contour and the next, as asked for.</param>
public sealed record ReliefContoursDto(double Interval, IReadOnlyList<ReliefIslandContoursDto> Islands);

/// <summary>One island's solved surface: how far it ranges, the box it covers, and the lines across it. The
/// bounds are inclusive on both ends, so a one-cell island has equal min and max.</summary>
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
public sealed record ReliefTierDto(
    string Name, int MaxStep, double Share, int Places, double LargestPlace, int Ledges);

/// <summary>One face the terrain presents: which way it looks, how wide it runs, how far it drops, and whether
/// that makes it a cliff. A landform is wider than it is tall; a narrow face at a big drop is a structure.</summary>
public sealed record ReliefFaceDto(string Facing, int Width, int Drop, bool Cliff);

/// <summary>What a barrier costs to cross in one direction: how many rows meet it, how many are passable on
/// foot, how many with a placed block, and how many are simply descended.</summary>
public sealed record ReliefFordsDto(int Rows, int OnFoot, int WithBlock, int Descended);
