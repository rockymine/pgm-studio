namespace PgmStudio.Contracts;

/// <summary>
/// The geometry derived from a posted plan (<c>POST /plan/inspect</c>) — the overlays the plan canvas draws,
/// and the numbers the interface lints quantify over, served raw so a caller sees what is behind a verdict
/// rather than only the verdict.
///
/// <para>Nothing here judges. A rule's band is the author's to state; these are measurements, and a plan
/// mid-edit is routinely incomplete, so the reads that need a compiled board degrade to empty rather than
/// failing the whole feed. A caller reading an empty <see cref="Structures"/> is reading a plan that will not
/// compile yet, not a plan with no structures.</para>
/// </summary>
/// <param name="Interfaces">Where two pieces meet, as a segment, with the surface step across it.</param>
/// <param name="GapLinks">Pieces near enough to be crossed without touching, and what the crossing costs.</param>
/// <param name="Frontline">The piece edges that face the other team.</param>
/// <param name="Frontages">Per authored piece side, how much of it is exposed and how much of that is
/// frontline — the share `FR8` quantifies over.</param>
/// <param name="FrontlineRuns">The frontline as continuous runs with their widths, per team.</param>
/// <param name="IslandGaps">The straits between bridged islands, and how wide each is.</param>
/// <param name="Structures">The boxes the world build will stamp, which the iso view draws.</param>
/// <param name="GoalDistances">Each destroy goal's walk from its own spawn and from the enemy's.</param>
public sealed record PlanInspectDto(
    IReadOnlyList<PlanInterfaceDto> Interfaces,
    IReadOnlyList<PlanGapLinkDto> GapLinks,
    IReadOnlyList<PlanFrontlineEdgeDto> Frontline,
    IReadOnlyList<PlanFrontageDto> Frontages,
    IReadOnlyList<PlanFrontlineRunDto> FrontlineRuns,
    IReadOnlyList<PlanIslandGapDto> IslandGaps,
    IReadOnlyList<PlanStructureBoxDto> Structures,
    IReadOnlyList<PlanGoalWalkDto> GoalDistances);

/// <summary>Where two pieces meet: the seam as a segment, how the surface steps across it, and what the seam
/// carries — a wool room, a wall, the chest piece of one.</summary>
/// <param name="A">One of the two pieces, by id.</param>
/// <param name="B">The other.</param>
/// <param name="Kind">What kind of meeting it is — how the two touch.</param>
/// <param name="X1">One end of the segment, east–west.</param>
/// <param name="Z1">That end, north–south.</param>
/// <param name="X2">The other end, east–west.</param>
/// <param name="Z2">That end, north–south.</param>
/// <param name="Length">How many blocks long the seam is.</param>
/// <param name="Delta">How far the surface steps across it. A step of 2 is one plateau unit; a large one is
/// a cliff or a wall.</param>
/// <param name="WoolRoom">Whether the seam is a wool room's own edge.</param>
/// <param name="Wall">Whether a wall is stamped along it.</param>
/// <param name="WallChest">Which face carries that wall's defence chests, by piece id, or absent where no
/// wall stands here.</param>
public sealed record PlanInterfaceDto(
    string A, string B, string Kind,
    int X1, int Z1, int X2, int Z2, int Length,
    int Delta,
    bool WoolRoom, bool Wall, string? WallChest);

/// <summary>Two pieces close enough to cross between without touching: which zone the hop is in, how far it
/// is, and the nearest segment between them.</summary>
/// <param name="A">One of the two pieces, by id.</param>
/// <param name="B">The other.</param>
/// <param name="Zone">The build zone or water lane the hop is made inside.</param>
/// <param name="Hop">How many blocks of void the crossing spans.</param>
/// <param name="X1">One end of the segment, east–west.</param>
/// <param name="Z1">That end, north–south.</param>
/// <param name="X2">The other end, east–west.</param>
/// <param name="Z2">That end, north–south.</param>
public sealed record PlanGapLinkDto(
    string A, string B, string Zone, int Hop,
    int X1, int Z1, int X2, int Z2);

/// <summary>One piece edge facing the other team.</summary>
/// <param name="Piece">The piece whose edge this is, by id.</param>
/// <param name="X1">One end of the segment, east–west.</param>
/// <param name="Z1">That end, north–south.</param>
/// <param name="X2">The other end, east–west.</param>
/// <param name="Z2">That end, north–south.</param>
public sealed record PlanFrontlineEdgeDto(string Piece, int X1, int Z1, int X2, int Z2);

/// <summary>One side of one authored piece, over the fanned raster board: how many blocks of it are exposed at
/// all, how many of those are frontline, and the share that leaves.</summary>
/// <param name="Piece">The piece, by id.</param>
/// <param name="Side">Which of its four sides.</param>
/// <param name="ExposedBlocks">How many blocks of that side border something other than land.</param>
/// <param name="FrontlineBlocks">How many of those face the other team.</param>
/// <param name="FrontlineShare">The second over the first, 0–1 — the number <c>FR8</c> quantifies
/// over.</param>
public sealed record PlanFrontageDto(
    string Piece, string Side, int ExposedBlocks, int FrontlineBlocks, double FrontlineShare);

/// <summary>A continuous run of frontline, its width and profile, and where it runs.</summary>
/// <param name="Team">Which team's side of the frontline this run is, by index.</param>
/// <param name="WidthBlocks">How wide the run is where it is crossed.</param>
/// <param name="Profile">What the crossing is like along it — the shape a player meets.</param>
/// <param name="X1">One end of the segment, east–west.</param>
/// <param name="Z1">That end, north–south.</param>
/// <param name="X2">The other end, east–west.</param>
/// <param name="Z2">That end, north–south.</param>
public sealed record PlanFrontlineRunDto(
    int Team, int WidthBlocks, string Profile,
    int X1, int Z1, int X2, int Z2);

/// <summary>The strait between two bridged islands: which pieces are on each side, what each side is for, and
/// how many blocks of water lie between.</summary>
/// <param name="PiecesA">The pieces on one side of the strait, by id.</param>
/// <param name="PiecesB">The pieces on the other.</param>
/// <param name="RoleA">What that first side is for, as its pieces' roles read.</param>
/// <param name="RoleB">What the second side is for.</param>
/// <param name="Blocks">How many blocks of water lie between them.</param>
public sealed record PlanIslandGapDto(
    IReadOnlyList<string> PiecesA, IReadOnlyList<string> PiecesB,
    string RoleA, string RoleB, int Blocks);

/// <summary>
/// One box the world build will stamp, in absolute block coordinates and already fanned across the symmetry
/// orbit. Min and floor are inclusive, max and top exclusive — a continuous frame, so a block at <c>x</c>
/// spans <c>[x, x+1)</c>.
/// </summary>
/// <param name="Kind">The structure family: <c>spawn-cube</c>, <c>wool-cage</c>, <c>iron</c>,
/// <c>destroyable</c>, <c>core</c>, <c>wall</c>.</param>
/// <param name="Color">A colour slug the client maps through its dye palette, null where the kind carries its
/// own fixed material colour.</param>
/// <param name="MinX">The box's west edge, inclusive.</param>
/// <param name="MinZ">Its north edge, inclusive.</param>
/// <param name="MaxX">Its east edge, exclusive.</param>
/// <param name="MaxZ">Its south edge, exclusive.</param>
/// <param name="Floor">The height it stands on, inclusive.</param>
/// <param name="Top">The height it stops below, exclusive.</param>
public sealed record PlanStructureBoxDto(
    string Kind, string? Color, int MinX, int MinZ, int MaxX, int MaxZ, int Floor, int Top);

/// <summary>One goal's walks in blocks — from its own team's nearest spawn, from the enemy's, and the
/// enemy÷own ratio. A null walk is no route over the closure, reported as the absence it is rather than
/// invented; judging the ratio is the author's, not this read's.</summary>
/// <param name="Id">The goal marker, by id.</param>
/// <param name="Kind">What kind of goal it is — <c>destroyable</c> or <c>core</c>.</param>
/// <param name="OwnSpawnBlocks">The walk from its own team's nearest spawn, in blocks, or null where no
/// route over the closure exists.</param>
/// <param name="EnemySpawnBlocks">The walk from the enemy's nearest spawn.</param>
/// <param name="Ratio">The second over the first — how much further the attacker walks than the
/// defender.</param>
public sealed record PlanGoalWalkDto(
    string Id, string Kind, double? OwnSpawnBlocks, double? EnemySpawnBlocks, double? Ratio);
