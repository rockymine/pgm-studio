using PgmStudio.Vocabulary;

namespace PgmStudio.Domain;

/// <summary>
/// The layout rules: claims about how a map plays, raised by the plan lint, the evaluator terms and the
/// producibility read. Each carries how it is known beside its meaning and its fix; the argument behind them
/// is <c>docs/generator/rules.md</c>.
/// </summary>
public static class LayoutRules
{
    /// <summary>A build zone is less than 10 blocks across its shorter side, too narrow a corridor to fight
    /// along.</summary>
    /// <remarks>Widen the build zone in <c>zones</c> to at least 10 blocks on both sides.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string CorridorWidth = "G2";

    /// <summary>Two pieces on separate ground that a build zone links stand less than 10 or more than 20 blocks
    /// apart across the void.</summary>
    /// <remarks>Either move one of the two pieces in <c>pieces</c> until the gap is 10 to 20 blocks, or add a piece
    /// between them to split a longer gap.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string VoidHop = "G5";

    /// <summary>The board's ground fills a share of its own frame outside the usual range, or more of its ground
    /// than usual lies off every route between places.</summary>
    /// <remarks>Either add or trim <c>pieces</c> until the ground fills its frame within the usual range, or remove
    /// ground no route crosses.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string BoardFill = "G8";

    /// <summary>The middle has too many or too few crossings between the teams. A crossing is a set of connected
    /// build zones joining two teams' ground, counted over the whole board against the usual range.</summary>
    /// <remarks>Either join crossings into one build zone to have fewer, or add a separate build zone joining the
    /// two teams to have more.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string CrossingCount = "CT1";

    /// <summary>A team has too many or too few stepping stones, islands holding no spawn or wool. Stones both teams
    /// reach and stones only one team reaches are each counted against the usual range.</summary>
    /// <remarks>Either remove stepping stones to have fewer, or add small islands inside the build zones to have
    /// more.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string SteppingStoneCount = "CT4";

    /// <summary>A team's side has too many or too few cuts, build zones joining only its own pieces such as a
    /// bridge to an isolated wool. The count is held to the usual range.</summary>
    /// <remarks>Either join a cut-off piece back to its team's ground to have fewer cuts, or separate a piece from
    /// its team's ground and bridge it with a build zone to have more.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string TeamSideCuts = "CT5";

    /// <summary>The count of holes the board encloses, or the share of the defenders' route to an objective that
    /// the attackers' route also covers, is outside the usual range.</summary>
    /// <remarks>Either add or fill enclosed holes to bring their count into range, or open a route around a hole so
    /// attackers can reach an objective off the defenders' route.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string RotationHoles = "CT8";

    /// <summary>A hole in the contested middle has no crossing build zone on its edge, so players cannot rotate
    /// between the lanes around it. The count is held to the usual range.</summary>
    /// <remarks>Either border the hole with a build zone joining the two teams, or with one joining two mid islands
    /// across it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string UncrossedMiddleHole = "CT9";

    /// <summary>The strait, a build zone joining the two team islands of a two-team wool board with no other island
    /// in it, is under 15 or over 40 blocks across.</summary>
    /// <remarks>Move the team pieces' <c>rect</c> so the gap across the strait is 15 to 40 blocks. On the sketch,
    /// keep the drawn gap between the islands at 15 to 40 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string StraitWidth = "CT12";

    /// <summary>A wool can be reached from the frontline only by walking through a spawn piece.</summary>
    /// <remarks>Either add ground from the frontline to the wool that bypasses the spawn piece, or move the spawn
    /// in <c>placements.spawns</c> to a piece off that route.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Spawn, RuleConcern.Objective)]
    public const string SpawnOnWoolRoute = "SP1";

    /// <summary>A spawn stands in the half of its piece nearer the board's centre, along the piece's longer side,
    /// leaving ground behind it with no use.</summary>
    /// <remarks>Move the spawn's <c>at</c> into the half of its piece farther from the board's centre.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Spawn)]
    public const string SpawnAtBack = "SP2";

    /// <summary>A spawn's ground stands 2 or more blocks above or below a piece it shares an edge with ahead of its
    /// door.</summary>
    /// <remarks>Either set the <c>surface</c> of the spawn and that piece within 1 block of each other, or add a
    /// ramp across the edge with terraform.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn, RuleConcern.Terrain)]
    public const string SpawnExitStep = "SP8";

    /// <summary>A spawn door opens onto void: fewer than 15 blocks of pieces or build zone lie straight ahead of
    /// the spawn piece, in line with the spawn.</summary>
    /// <remarks>Either extend a piece in <c>pieces</c> or a build zone in <c>zones</c> ahead of the door, or turn
    /// the spawn's <c>facing</c> toward ground.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn)]
    public const string SpawnDoorGround = "SP9";

    /// <summary>A spawn is less than 55 blocks by walking distance from the crossing, the build zone between the
    /// two teams.</summary>
    /// <remarks>Either move the spawn further back on its team's ground, or lengthen the ground between the spawn
    /// and the crossing.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn)]
    public const string SpawnFrontDistance = "SP10";

    /// <summary>A team's nearest wool is less than 20 blocks from its spawn by walking distance, or outside the
    /// usual range, or a wool room shares an edge with the spawn piece.</summary>
    /// <remarks>Either move the wool room in <c>pieces</c> further from the spawn by the walk, or put ground
    /// between the wool room and the spawn piece so they share no edge.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string WoolSpawnDistance = "WL2";

    /// <summary>Two of a team's wools are closer together or further apart by walking distance than the usual
    /// range.</summary>
    /// <remarks>Move one of the two wool rooms in <c>pieces</c> until the walk between the wools falls inside the
    /// usual range.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string WoolWoolDistance = "WL7";

    /// <summary>A team's wools sit unevenly far from their spawn: the difference or the ratio between the farthest
    /// and the nearest walking distance is outside the usual range.</summary>
    /// <remarks>Move the nearer or the farther wool room in <c>pieces</c> until each wool is about the same walk
    /// from the spawn.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string WoolSpawnBalance = "WL9";

    /// <summary>A wool's walk to the crossing is outside the usual range: the nearest wool's, the farthest's, their
    /// ratio, or the spread of each wool's spawn walk minus its crossing walk.</summary>
    /// <remarks>Move the wool rooms in <c>pieces</c> until each is a similar walk from the crossing. Where one wool
    /// is nearer the crossing, put it nearer the spawn as well.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string WoolFrontBalance = "WL10";

    /// <summary>A wool room's ground stands 2 or more blocks above or below a piece it shares an edge with, where
    /// an attacker arrives.</summary>
    /// <remarks>Either set the <c>surface</c> of the wool room and that piece within 1 block of each other, or add
    /// a ramp across the edge with terraform.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Terrain)]
    public const string WoolEntryStep = "WL11";

    /// <summary>A gap beside a wool room or spawn is under 16 blocks across toward the front or another goal, or
    /// under 12 toward its own team's ground. A hole touching no goal is under 12.</summary>
    /// <remarks>Either move the pieces in <c>pieces</c> apart until the gap is wide enough, or fill it with ground,
    /// or cover it with a build zone in <c>zones</c>.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string GoalGapWidth = "WL12";

    /// <summary>The narrowest wool lane on the board has a width outside the usual range.</summary>
    /// <remarks>Either widen or narrow the <c>pieces</c> that form that wool lane until its width is within the
    /// usual range.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string LaneWidth = "LN1";

    /// <summary>The longest straight run of same-width pieces joined end to end has a length outside the usual
    /// range.</summary>
    /// <remarks>Either break an overlong run with a turn, a junction or a change of width, or lengthen a run that
    /// is too short.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string LaneLength = "LN2";

    /// <summary>A team presents too many or too few frontline faces, the edges of its ground that face the enemy
    /// across a build zone. The count per team is held to the usual range.</summary>
    /// <remarks>Either merge neighbouring frontline faces into one wider face to have fewer, or split a face with a
    /// gap to have more.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string FrontlineFaceCount = "FR4";

    /// <summary>A team's widest frontline face, its edge facing the enemy across a build zone, is outside the usual
    /// width range for a wool board.</summary>
    /// <remarks>Either shorten or lengthen the frontline piece's <c>rect</c> along the front, or split one wide
    /// face into two tips with a gap between them.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string FrontlineFaceWidth = "FR6";

    /// <summary>A piece side with 10 or more blocks of frontline has it on less than one third of the side's edge
    /// facing void, so the crossing uses only a narrow part of that face.</summary>
    /// <remarks>Either widen the build zone in <c>zones</c> along this side until its frontline covers a third of
    /// the edge facing void, or trim the piece's <c>rect</c> so less of it faces void.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string FrontlineShare = "FR8";

    /// <summary>A piece side carries less than 15 blocks of frontline, too narrow for players to read as a place to
    /// cross.</summary>
    /// <remarks>Either widen the build zone in <c>zones</c> along this side to give at least 15 blocks of
    /// frontline, or move the build zone off this side so it carries none.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string MinimumFrontline = "FR9";

    /// <summary>The middle build zone is narrower than 24 blocks at nano, 32 at micro, 40 at milli or 48 at centi,
    /// or longer front to front than twice its width.</summary>
    /// <remarks>Either widen the middle build zone in <c>zones</c> to the minimum for its <c>maxPlayers</c>, or
    /// shorten it front to front to at most twice its width.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string ThinMiddle = "MD7";

    /// <summary>Two pieces meet edge to edge with surfaces 2 or more blocks apart, a step a player cannot walk
    /// up.</summary>
    /// <remarks>Either add a ramp or a flight of one-block steps where they meet in the terraform, or set the two
    /// pieces' <c>surface</c> within 1 block of each other.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string UnwalkableSeam = "EL1";

    /// <summary>A build zone or water lane touches a spawn piece, sharing an edge with it or overlapping it, so
    /// players bridge from the void straight into the spawn.</summary>
    /// <remarks>Shrink or move the zone's <c>rect</c> so it stops short of the spawn piece's <c>rect</c>. Either
    /// leave a gap of void between them, or end the zone at another piece.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn)]
    public const string ZoneTouchesSpawn = "BZ5";

    /// <summary>The mid build zone comes closer than 2 cells to a piece holding a wool, in any symmetry image, so
    /// players bridge from the middle straight into the wool.</summary>
    /// <remarks>Keep the mid build zone's <c>rect</c> at least 2 cells from every wool piece in every symmetry
    /// image. Either narrow the zone, or place the wool on a piece further back.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string MidZoneNearWool = "BZ6";

    /// <summary>A build zone runs sideways past the last piece it touches, so that stretch connects nothing and
    /// players walk into it and stop.</summary>
    /// <remarks>Trim the zone's <c>rect</c> to the span of the pieces it touches. Either cut the overhanging
    /// stretch, or extend a piece to meet it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string ZoneOverhang = "BZ9";

    /// <summary>Two or more build zones join into one region that fills a plain rectangle, which one zone would
    /// draw as a single crossing.</summary>
    /// <remarks>Either merge them into one zone whose <c>rect</c> covers the region, or separate them into regions
    /// that do not touch.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string StitchedZones = "BZ11";

    /// <summary>A water lane overlaps a terrain piece. The lane opens later in the match, and the part over terrain
    /// is already ground, so it opens nothing.</summary>
    /// <remarks>Trim the water lane's <c>rect</c> back to the void, so it meets the piece at its edge and no longer
    /// covers it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string WaterLaneOverTerrain = "BZ12";

    /// <summary>A destroyable or core stands less than 3 or more than 4 times as far from the enemy's nearest spawn
    /// as from its own team's, by walking distance.</summary>
    /// <remarks>Move the goal in <c>placements</c> or a spawn in <c>placements.spawns</c> until the enemy's walk to
    /// the goal is 3 to 4 times its own team's walk.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string GoalSpawnRatio = "GO1";

    /// <summary>Two destroyables or cores of one team stand less than 35 or more than 65 blocks apart, by walking
    /// distance.</summary>
    /// <remarks>Move one of the two goals in <c>placements</c> until the walk between them is 35 to 65
    /// blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective)]
    public const string OwnGoalSpacing = "GO2";

    /// <summary>A destroyable or core stands less than 85 or more than 150 blocks from an enemy destroyable or
    /// core, by walking distance.</summary>
    /// <remarks>Move the goal in <c>placements</c> until its walk to every enemy goal is 85 to 150
    /// blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective)]
    public const string OpposingGoalSpacing = "GO3";

    /// <summary>A destroyable or core stands less than 40 or more than 90 blocks from its own team's nearest spawn,
    /// by walking distance.</summary>
    /// <remarks>Move the goal in <c>placements</c> or the spawn in <c>placements.spawns</c> until the walk between
    /// them is 40 to 90 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string GoalSpawnDistance = "GO4";

    /// <summary>A box's wool room or spawn piece differs from the room the composer builds, 2 cells deep at the
    /// corridor's end, so the box cannot be reproduced although its corridor can.</summary>
    /// <remarks>Either reshape the room piece in <c>pieces</c> to the 2-cell-deep room at the corridor's end, or
    /// take it out of the box.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string BoxRoomShape = "ST1";

    /// <summary>An iron cube stands partly or wholly outside every spawn piece, so it is mined once instead of
    /// renewing.</summary>
    /// <remarks>Either move the iron in <c>placements.iron</c> so its 3 by 3 cube lies inside a spawn piece, or set
    /// the <c>role</c> of the piece it stands on to spawn.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn, RuleConcern.Structure)]
    public const string IronInSpawn = "ST2";

    /// <summary>An approach wall stands more than 4 blocks above the ground at its lowest column, because its level
    /// top follows the highest ground along its run.</summary>
    /// <remarks>Either level the ground under the wall with terraform, or move the wall in <c>walls</c> to an edge
    /// whose ground is level.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Structure, RuleConcern.Terrain, RuleConcern.World)]
    public const string WallHeight = "ST4";

    /// <summary>An approach wall bars an edge shorter than 10 or longer than 20 blocks, or stands under 10 or over
    /// 20 blocks from the wool room's nearest parallel entrance.</summary>
    /// <remarks>Either move the wall in <c>walls</c> to an edge 10 to 20 blocks long, or move it so it stands 10 to
    /// 20 blocks in front of the entrance.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Objective)]
    public const string ApproachWallPlacement = "ST8";

    /// <summary>A wool room or spawn building is wider or deeper than 20 blocks.</summary>
    /// <remarks>Either state a <c>footprint</c> of at most 20 by 20 on the placement, or shrink the piece its
    /// default footprint is taken from.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string BuildingFootprint = "ST9";

    /// <summary>A wool room or spawn piece is more than 20 blocks across or more than 30 blocks long, in either
    /// orientation.</summary>
    /// <remarks>Shrink the piece's <c>rect</c> to at most 20 by 30 blocks. Give the ground it loses to a plain
    /// piece beside it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn, RuleConcern.Objective)]
    public const string RoomRegionSize = "ST10";

    /// <summary>The mid build region runs more than 2 times its width from one front line to the other.</summary>
    /// <remarks>Either widen the mid build region in <c>zones</c>, or shorten it front to front to at most twice
    /// its width.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string LongMiddle = "MD8";
}
