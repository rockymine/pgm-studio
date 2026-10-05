using PgmStudio.Vocabulary;

namespace PgmStudio.Domain;

/// <summary>
/// The layout rules: claims about how a map plays, raised by the plan checks, the evaluator terms and the
/// producibility read. Each states one limit in the glossary's words; the argument behind them is
/// <c>docs/generator/rules.md</c>.
/// </summary>
public static class LayoutRules
{
    /// <summary>A build region is less than 10 blocks across its shorter side.</summary>
    /// <remarks>Widen the <c>rect</c> of the build region in <c>zones</c> until its shorter side is at least 10
    /// blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string CorridorWidth = "G2";

    /// <summary>The gap between two islands a build region links is not between 10 and 20 blocks.</summary>
    /// <remarks>Either move the <c>rect</c> of one of the two pieces in <c>pieces</c> until the gap is between 10
    /// and 20 blocks, or add a piece to <c>pieces</c> in the gap.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string VoidHop = "G5";

    /// <summary>The share of the smallest rectangle around a layout's ground that the ground fills is not between
    /// 20 and 54 percent.</summary>
    /// <remarks>Either add a piece to <c>pieces</c> inside the rectangle, or shrink the <c>rect</c> of a piece in
    /// <c>pieces</c>, until the share is between 20 and 54 percent.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string LayoutFill = "G8";

    /// <summary>The width of the narrowest lane leading to a wool room is not between 10 and 30 blocks.</summary>
    /// <remarks>Change the <c>rect</c> of the pieces in <c>pieces</c> that form the lane until its width is between
    /// 10 and 30 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string LaneWidth = "LN1";

    /// <summary>The longest straight run of same-width pieces joined end to end is not between 25 and 110
    /// blocks.</summary>
    /// <remarks>Either split the run with a turn or a change of width in <c>pieces</c>, or lengthen the <c>rect</c>
    /// of a piece in <c>pieces</c> until the run is at least 25 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string LaneLength = "LN2";

    /// <summary>The share of a layout's ground that lies off every route between waypoints is more than 12
    /// percent.</summary>
    /// <remarks>Either delete a piece that no route crosses from <c>pieces</c>, or add a marker on it to
    /// <c>placements</c>.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string GroundOffRoutes = "LN5";

    /// <summary>A hole that touches no room piece is less than 12 blocks across at its narrowest.</summary>
    /// <remarks>Either move the <c>rect</c> of a piece in <c>pieces</c> until the hole is at least 12 blocks
    /// across, or add a piece to <c>pieces</c> or a build region to <c>zones</c> over the hole.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string HoleWidth = "LN6";

    /// <summary>A layout has more than 3 build regions that join two team sides.</summary>
    /// <remarks>Either merge two build regions between the same team sides into one entry in <c>zones</c>, or
    /// delete one from <c>zones</c>.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string CrossingCount = "CT1";

    /// <summary>A team has more than 4 stepping stones that both teams can reach.</summary>
    /// <remarks>Either delete a stepping stone from <c>pieces</c>, or move its <c>rect</c> until it touches the
    /// ground beside it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string SharedSteppingStones = "CT4";

    /// <summary>A team side has more than 6 isolation cuts.</summary>
    /// <remarks>Move the <c>rect</c> of the piece an isolation cut separates until it touches its team side, then
    /// delete the cut's build region from <c>zones</c>.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string TeamSideCuts = "CT5";

    /// <summary>A layout has more than 15 holes.</summary>
    /// <remarks>Either add a piece to <c>pieces</c> that fills a hole, or delete a piece from <c>pieces</c> or a
    /// build region from <c>zones</c> around it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string RotationHoles = "CT8";

    /// <summary>A hole in the mid has no build region on its edge that joins two team sides or two stepping
    /// stones.</summary>
    /// <remarks>Add a build region to <c>zones</c> along the hole's edge that joins two team sides or two stepping
    /// stones.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string UncrossedMiddleHole = "CT9";

    /// <summary>The gap between the two team sides of a two-team wool layout is not between 15 and 40
    /// blocks.</summary>
    /// <remarks>Move the <c>rect</c> of a piece in <c>pieces</c>, or in a sketch the shape of an island, until the
    /// gap is between 15 and 40 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string TeamGapWidth = "CT12";

    /// <summary>A team has more than 2 stepping stones that only that team can reach.</summary>
    /// <remarks>Either delete a stepping stone from <c>pieces</c>, or move its <c>rect</c> until it touches the
    /// ground beside it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string TeamSteppingStones = "CT13";

    /// <summary>The attackers' route covers more than 87.5 percent of the defenders' route to an objective,
    /// averaged over all objectives.</summary>
    /// <remarks>Add a piece to <c>pieces</c> that gives the attackers a way to the objective off the defenders'
    /// route.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string SharedAttackRoute = "CT14";

    /// <summary>A wool's room piece has no route from the front line that avoids a spawn's room piece.</summary>
    /// <remarks>Either add a piece to <c>pieces</c> that joins the front line to the wool's room piece around the
    /// spawn, or move the spawn in <c>placements.spawns</c> to a piece off the route.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Spawn, RuleConcern.Objective)]
    public const string SpawnOnWoolRoute = "SP1";

    /// <summary>A spawn is not in the half of its room piece farther from the symmetry centre, along the piece's
    /// longer side.</summary>
    /// <remarks>Move the spawn in <c>placements.spawns</c> into the half of its room piece farther from the
    /// symmetry centre.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Spawn)]
    public const string SpawnAtBack = "SP2";

    /// <summary>A spawn's room piece stands 2 or more blocks above or below the piece ahead of its door where they
    /// share an edge.</summary>
    /// <remarks>Set the <c>surface</c> of the spawn's room piece and of the piece ahead of its door within 1 block
    /// of each other.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn, RuleConcern.Terrain)]
    public const string SpawnExitStep = "SP8";

    /// <summary>The ground ahead of a spawn's door is less than 15 blocks long.</summary>
    /// <remarks>Either move the spawn in <c>placements.spawns</c> until 15 blocks of ground lie ahead of its door,
    /// add a piece to <c>pieces</c> between the door and the void, or set its <c>facing</c> toward ground.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn)]
    public const string SpawnDoorGround = "SP9";

    /// <summary>The walking distance from a spawn to the crossing is less than 55 blocks.</summary>
    /// <remarks>Either move the spawn in <c>placements.spawns</c> to a piece farther from the crossing, or lengthen
    /// the <c>rect</c> of a piece in <c>pieces</c> between the spawn and the crossing.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn)]
    public const string SpawnFrontDistance = "SP10";

    /// <summary>A plan has no build region.</summary>
    /// <remarks>Add a build region to <c>zones</c> across the mid.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Spawn, RuleConcern.Objective)]
    public const string NoBuildRegion = "SP11";

    /// <summary>The walking distance from a spawn to its team's nearest wool is less than 20 blocks.</summary>
    /// <remarks>Either move the <c>rect</c> of the wool's room piece in <c>pieces</c> farther from the spawn, or
    /// lengthen the <c>rect</c> of a piece in <c>pieces</c> between them.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string WoolSpawnFloor = "WL2";

    /// <summary>The walking distance between a team's two nearest wools is not between 50 and 227 blocks.</summary>
    /// <remarks>Move the <c>rect</c> of one of the two wools' room pieces in <c>pieces</c> until the walking
    /// distance between them is between 50 and 227 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string WoolWoolDistance = "WL7";

    /// <summary>The walking distance from a spawn to its team's farthest wool is more than 73 blocks longer than to
    /// its nearest wool.</summary>
    /// <remarks>Either move the <c>rect</c> of the farthest wool's room piece in <c>pieces</c> closer to the spawn,
    /// or move the nearest one farther from it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string WoolSpawnSpread = "WL9";

    /// <summary>The walking distance from a team's nearest wool to the crossing is not between 22 and 147
    /// blocks.</summary>
    /// <remarks>Move the <c>rect</c> of the nearest wool's room piece in <c>pieces</c> until its walking distance
    /// to the crossing is between 22 and 147 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string WoolFrontDistance = "WL10";

    /// <summary>A wool's room piece stands 2 or more blocks above or below another piece where they share an
    /// edge.</summary>
    /// <remarks>Set the <c>surface</c> of the wool's room piece and of the other piece within 1 block of each
    /// other.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Terrain)]
    public const string WoolEntryStep = "WL11";

    /// <summary>A gap beside a room piece is less than 16 blocks across toward the front line or another room
    /// piece.</summary>
    /// <remarks>Either move the <c>rect</c> of a piece in <c>pieces</c> until the gap is at least 16 blocks
    /// across, or add a piece to <c>pieces</c> or a build region to <c>zones</c> over the gap.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string RoomFrontGapWidth = "WL12";

    /// <summary>A gap beside a room piece is less than 12 blocks across toward its own team side.</summary>
    /// <remarks>Either move the <c>rect</c> of a piece in <c>pieces</c> until the gap is at least 12 blocks
    /// across, or add a piece to <c>pieces</c> or a build region to <c>zones</c> over the gap.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string RoomHomeGapWidth = "WL20";

    /// <summary>The walking distance from a spawn to its team's nearest wool is not between 29 and 176
    /// blocks.</summary>
    /// <remarks>Move the <c>rect</c> of the nearest wool's room piece in <c>pieces</c> until its walking distance
    /// from the spawn is between 29 and 176 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string WoolSpawnDistance = "WL13";

    /// <summary>A wool's room piece touches the room piece of its team's spawn.</summary>
    /// <remarks>Either move the <c>rect</c> of the wool's room piece in <c>pieces</c> until it no longer touches
    /// the spawn's room piece, or add a piece to <c>pieces</c> between them.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string WoolRoomTouchesSpawn = "WL14";

    /// <summary>The walking distance from a spawn to its team's farthest wool is more than 1.22 times that to its
    /// nearest wool.</summary>
    /// <remarks>Either move the <c>rect</c> of the farthest wool's room piece in <c>pieces</c> closer to the spawn,
    /// or move the nearest one farther from it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string WoolSpawnRatio = "WL15";

    /// <summary>The walking distance from the spawn minus the walking distance to the crossing differs by more than
    /// 116 blocks between two wools of a team.</summary>
    /// <remarks>Move the <c>rect</c> of one wool's room piece in <c>pieces</c> until the two wools differ by at
    /// most 116 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string WoolFrontBalance = "WL16";

    /// <summary>The walking distance from a team's farthest wool to the crossing is more than 1.44 times that from
    /// its nearest wool.</summary>
    /// <remarks>Either move the <c>rect</c> of the farthest wool's room piece in <c>pieces</c> closer to the
    /// crossing, or move the nearest one farther from it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string WoolFrontRatio = "WL17";

    /// <summary>The walking distance from a team's farthest wool to the crossing is not between 25 and 130
    /// blocks.</summary>
    /// <remarks>Move the <c>rect</c> of the farthest wool's room piece in <c>pieces</c> until its walking distance
    /// to the crossing is between 25 and 130 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string WoolRemoteness = "WL18";

    /// <summary>The walking distance from a wool to the crossing is less than 59 blocks.</summary>
    /// <remarks>Either move the <c>rect</c> of the wool's room piece in <c>pieces</c> farther from the crossing, or
    /// lengthen the <c>rect</c> of a piece in <c>pieces</c> between them.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string WoolFrontFloor = "WL19";

    /// <summary>A team has no front line, or more than 8 front lines.</summary>
    /// <remarks>Either merge the pieces behind two neighbouring front lines into one entry in <c>pieces</c>, or
    /// split a front line with a notch in the <c>rect</c> of a piece in <c>pieces</c>.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string FrontlineFaceCount = "FR4";

    /// <summary>A front line is more than 16 cells wide.</summary>
    /// <remarks>Either shorten the <c>rect</c> of a piece in <c>pieces</c> along the front line until it is at most
    /// 16 cells, or split the front line with a notch in the <c>rect</c> of the piece.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string FrontlineFaceWidth = "FR6";

    /// <summary>A piece side with 10 or more blocks of front line has front line along less than a third of its
    /// edge facing void.</summary>
    /// <remarks>Either widen the <c>rect</c> of the build region in <c>zones</c> along the side, or shrink the
    /// <c>rect</c> of the piece in <c>pieces</c>, until the front line covers at least a third of the
    /// side.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string FrontlineShare = "FR8";

    /// <summary>A piece side with any front line has less than 15 blocks of front line.</summary>
    /// <remarks>Either widen the <c>rect</c> of the build region in <c>zones</c> along the side until it gives at
    /// least 15 blocks of front line, or move it until it no longer touches the side.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string MinimumFrontline = "FR9";

    /// <summary>The mid build region is less than 24 blocks wide along the front lines at nano, 32 at micro, 40 at
    /// milli or 48 at centi.</summary>
    /// <remarks>Widen the <c>rect</c> of the <c>mid-band</c> entry in <c>zones</c> until it is at least 24 blocks
    /// at nano, 32 at micro, 40 at milli or 48 at centi.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string ThinMiddle = "MD7";

    /// <summary>The mid build region is more than 2 times as long from front line to front line as it is
    /// wide.</summary>
    /// <remarks>Either widen the <c>rect</c> of the <c>mid-band</c> entry in <c>zones</c>, or shorten its
    /// <c>rect</c> from front line to front line, until it is at most 2 times as long as it is wide.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string LongMiddle = "MD8";

    /// <summary>A piece stands 2 or more blocks above or below another piece where they share an edge.</summary>
    /// <remarks>Set the <c>surface</c> of the two pieces within 1 block of each other.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string UnwalkableStep = "EL1";

    /// <summary>A build region or water lane touches a spawn's room piece.</summary>
    /// <remarks>Either shrink the <c>rect</c> of the build region or water lane in <c>zones</c>, or move it, until
    /// void lies between it and the spawn's room piece.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn)]
    public const string ZoneTouchesSpawn = "BZ5";

    /// <summary>The mid build region is less than 2 cells from a wool's room piece, in any symmetry copy.</summary>
    /// <remarks>Either shrink the <c>rect</c> of the <c>mid-band</c> entry in <c>zones</c>, or move the wool in
    /// <c>placements.wools</c> to another room piece, until the gap is at least 2 cells.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Objective)]
    public const string MidZoneNearWool = "BZ6";

    /// <summary>A build region runs past the last piece it touches, along the side it touches.</summary>
    /// <remarks>Either shrink the <c>rect</c> of the build region in <c>zones</c> to the span of the pieces it
    /// touches, or lengthen the <c>rect</c> of a piece in <c>pieces</c> until it meets the build region's
    /// end.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string ZoneOverhang = "BZ9";

    /// <summary>Two or more build regions touch and together fill one rectangle.</summary>
    /// <remarks>Either merge the build regions into one entry in <c>zones</c> whose <c>rect</c> covers the
    /// rectangle, or move their <c>rect</c> apart until they no longer touch.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan)]
    public const string StitchedZones = "BZ11";

    /// <summary>A water lane overlaps a piece that makes ground.</summary>
    /// <remarks>Shrink the <c>rect</c> of the water lane in <c>zones</c> until it no longer overlaps the
    /// piece.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string WaterLaneOverGround = "BZ12";

    /// <summary>The walking distance from a monument or core to the nearest enemy spawn is not between 3 and 4
    /// times that to its own team's nearest spawn.</summary>
    /// <remarks>Either move the monument or core in <c>placements.destroyables</c> or <c>placements.cores</c>, or
    /// move a spawn in <c>placements.spawns</c>, until the ratio is between 3 and 4 times.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string ObjectiveSpawnRatio = "GO1";

    /// <summary>The walking distance between two monuments or cores of one team is not between 35 and 65
    /// blocks.</summary>
    /// <remarks>Move one of the two monuments or cores in <c>placements.destroyables</c> or <c>placements.cores</c>
    /// until the walking distance between them is between 35 and 65 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective)]
    public const string OwnObjectiveSpacing = "GO2";

    /// <summary>The walking distance from a monument or core to an enemy monument or core is not between 85 and 150
    /// blocks.</summary>
    /// <remarks>Move the monument or core in <c>placements.destroyables</c> or <c>placements.cores</c> until its
    /// walking distance to every enemy monument or core is between 85 and 150 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective)]
    public const string OpposingObjectiveSpacing = "GO3";

    /// <summary>The walking distance from a monument or core to its team's nearest spawn is not between 40 and 90
    /// blocks.</summary>
    /// <remarks>Either move the monument or core in <c>placements.destroyables</c> or <c>placements.cores</c>, or
    /// move the spawn in <c>placements.spawns</c>, until the walking distance between them is between 40 and 90
    /// blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Spawn)]
    public const string ObjectiveSpawnDistance = "GO4";

    /// <summary>A room piece in a box is not the room the generator builds, 2 cells deep at the end of a
    /// lane.</summary>
    /// <remarks>Either change the <c>rect</c> of the room piece in <c>pieces</c> to 2 cells deep at the end of the
    /// lane, or shrink the <c>rect</c> of the box in <c>boxes</c> until it leaves the room out.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string BoxRoomShape = "ST1";

    /// <summary>A 3 by 3 iron cube is not wholly inside a spawn's room piece.</summary>
    /// <remarks>Either move the iron in <c>placements.iron</c> until its cube lies wholly inside a spawn's room
    /// piece, or set the <c>role</c> of the piece it stands on to spawn.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn, RuleConcern.Structure)]
    public const string IronInSpawn = "ST2";

    /// <summary>An approach wall stands more than 4 blocks above the ground at its lowest column.</summary>
    /// <remarks>Either level the ground under the wall with terraform, or change the pair in <c>walls</c> to one
    /// whose shared edge lies on level ground.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Structure, RuleConcern.Terrain, RuleConcern.World)]
    public const string WallHeight = "ST4";

    /// <summary>The shared edge an approach wall bars is not between 10 and 20 blocks long.</summary>
    /// <remarks>Change the pair in <c>walls</c> to one whose shared edge is between 10 and 20 blocks
    /// long.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Objective)]
    public const string ApproachWallEdgeLength = "ST8";

    /// <summary>The footprint of a spawn room or wool room is more than 20 blocks across on either side.</summary>
    /// <remarks>Either set the <c>footprint</c> of the marker to at most 20 by 20 blocks, or shrink the <c>rect</c>
    /// of the room piece in <c>pieces</c> that sets the default footprint.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string BuildingFootprint = "ST9";

    /// <summary>A room piece is more than 20 blocks across its shorter side or more than 30 blocks along its longer
    /// side.</summary>
    /// <remarks>Shrink the <c>rect</c> of the room piece in <c>pieces</c> until it is at most 20 by 30
    /// blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Spawn, RuleConcern.Objective)]
    public const string RoomRegionSize = "ST10";

    /// <summary>The distance from an approach wall to its wool room's nearest parallel entrance is not between 10
    /// and 20 blocks.</summary>
    /// <remarks>Change the pair in <c>walls</c> to one whose shared edge is between 10 and 20 blocks in front of
    /// the wool room's entrance.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Objective)]
    public const string ApproachWallStandoff = "ST11";
}
