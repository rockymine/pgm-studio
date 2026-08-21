using PgmStudio.Vocabulary;
namespace PgmStudio.Minecraft.Dressing;

/// <summary>The dressing pass's own placement rules — the ids its census reasons cite, served by
/// <c>GET /api/rules</c> from the docstrings here the way every gate family's are.</summary>
public static class DressingRules
{
    /// <summary>A prop rests nearer to the road than its kind's standoff allows: a tree 3 blocks, a boulder 2,
    /// measured from its resting cells to the nearest paved cell (Chebyshev; exactly-at-distance stands). The
    /// numbers are each kind's own <see cref="PlacedProp.RouteStandoff"/> — the author's ruling, stated on the
    /// type so there is exactly one place they live.</summary>
    /// <remarks>Move the tree or boulder until its trunk or resting footprint keeps its kind's distance from
    /// the paved edge — measured to the spline the band actually follows, not the drawn polyline. The whole
    /// prop is declined and the census names the offending cell, so the drop can be checked on the canvas.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string RoadStandoff = "DR-ROAD";

    /// <summary>A building leaves no way past itself: none of its four sides has 5 blocks of passable ground
    /// alongside its whole run (the author's number). A house may stand against the map's own edge on one
    /// side — a coast house is a house — but a house with too little ground on every side corks the leg it
    /// stands on, and a route players must dig through a building to walk is not a route.</summary>
    /// <remarks>Move the building so at least one side keeps a five-block passage alongside its whole length — including one step past each corner, which is where the passage turns in from — or widen the ground it stands on. Passable here means terrain with nothing built on it; a road or a channel beside the wall still counts as a way past. The whole building is declined and is not in the exported world.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Structure, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string PassAround = "DR-PASS";

    /// <summary>The passage's width in blocks — <see cref="PassAround"/>'s one number.</summary>
    public const int PassAroundWidth = 5;

    /// <summary>A building's box is smaller than 5×5 blocks: a footprint four blocks deep is a wall with a
    /// roof, not a building anyone enters, and a corpus run produced eight of fourteen that way. Measured on
    /// the plan's bounding box, so a multi-wing building is judged as the one building it is.</summary>
    /// <remarks>Draw the building at least 5×5 blocks — both dimensions. The whole prop is declined and the
    /// census names the footprint, so the drop can be checked against the layout.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Structure)]
    public const string FootprintFloor = "DR-SIZE";

    /// <summary>The footprint minimum in blocks — <see cref="FootprintFloor"/>'s one number.</summary>
    public const int FootprintMin = 5;

    /// <summary>A prop rests on a column the map keeps clear of everything: a spawn point or its room, a wool
    /// room or its monument, a structure the plan stated, a column whose surface is built rather than terrain,
    /// or the lane in front of a spawn or wool-room door. The finding names which of those it was and the cell
    /// it happened at.</summary>
    /// <remarks>Move the prop off the cell the finding names. A door's approach reaches twenty blocks out from a spawn room's face and ten from a wool room's, measured from the stamped building — that lane is what the door is for, so nothing stands in it. The whole prop is declined and is not in the exported world.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Spawn, RuleConcern.Objective)]
    public const string KeptClear = "DR-KEEP";

    /// <summary>A prop rests on ground something already standing has claimed — a channel, a road, a building
    /// or an earlier prop. The pass places in priority order and the first claimant keeps the cell, so this is
    /// the collision itself rather than a near miss; the finding names the cell and what holds it. A building
    /// holds the ground it stamps <em>and</em> <see cref="StructureClearance"/> blocks of ring beyond it, so a
    /// prop seating under an eave is this fault rather than a silent build.</summary>
    /// <remarks>Move the prop off the claimed ground, or move whatever holds it. Two authored things wanting the same cell is the author's to resolve — the pass never shifts a placement to make room, it declines the prop and leaves the ground to whatever already holds it.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string GroundTaken = "DR-CLAIM";

    /// <summary>How far past what it stamps a building holds the ground, in blocks — the author's number, and
    /// the one thing that separates two buildings that merely fail to overlap from two that leave a block of
    /// clear ground between them. What a placement is <em>tested</em> against is the stamped extent, so the
    /// ring is spent once between a pair rather than twice.</summary>
    public const int StructureClearance = 1;

    /// <summary>A prop has no ground to rest on: one of the cells it rests at is off the map's terrain
    /// altogether, so there is no column to seat it in. A building is held to <b>every</b> cell of its
    /// footprint — it seats on its lowest column, so one cell on land and the rest over void builds a house
    /// hanging off a corner — and the finding names the first bare column it stopped at.</summary>
    /// <remarks>Move the prop onto drawn ground. A building needs drawn ground under its whole footprint, not merely under part of it. A prop whose orbit image falls off the board fails this way too — the whole prop is declined at the first image that finds no ground, since a rock standing on one half of a mirrored map and missing from the other is worse than neither.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string NoGround = "DR-SITE";
}

/// <summary>Why a cell is kept clear of everything the dressing pass places — the answer a decline names, so
/// a dropped prop says <em>what</em> stopped it rather than only that something did. The sibling of
/// <see cref="ClaimKind"/>: that says what another prop took, this says what the map itself is holding.</summary>
public enum KeepOut
{
    /// <summary>A spawn point, or the protection area authored around it.</summary>
    Spawn,
    /// <summary>A wool room, its spawn or one of its monuments.</summary>
    WoolRoom,
    /// <summary>A structure the plan stated: a room floor, an iron cube, a wall, a redstone line.</summary>
    Structure,
    /// <summary>A column whose surface block is built rather than terrain — a stamp of some kind, whatever
    /// placed it. Read from the finished world rather than from the intent, which is what makes it catch what
    /// the intent does not name.</summary>
    Built,
    /// <summary>The lane in front of a spawn room's door or a wool room's entry — the ground players walk out
    /// through, which is part of what the door is for.</summary>
    Approach,
}

/// <summary>What kind of thing claimed a cell of ground during the dressing pass. The kind is what lets one
/// rule differ by claimant where the rules genuinely differ: a building collides with water and with another
/// building but never with a road (a road is meant to run to its porch), and a prop's standoff is stated
/// against the road rather than against everything.</summary>
public enum ClaimKind
{
    /// <summary>A water channel's bed and beach — carved ground nothing else may take.</summary>
    Water,
    /// <summary>A path's paved cells. The one kind a building ignores, and the one a standoff measures to.</summary>
    Route,
    /// <summary>A raised building's stamped footprint, plus the <see cref="DressingRules.StructureClearance"/>
    /// ring it holds around it.</summary>
    Structure,
    /// <summary>A seated tree's or boulder's cells — each an exclusion for the props after it.</summary>
    Scatter,
}

/// <summary>
/// The dressing pass's running record of who claimed which cell of ground — the one set every placement
/// checks and joins, carrying <em>what kind of thing</em> claimed each cell rather than the bare fact of a
/// claim. The first claimant of a cell keeps it: the pass places in priority order, so a later claim on a
/// held cell is exactly the collision the placement rules exist to refuse.
/// </summary>
public sealed class GroundClaims
{
    private readonly Dictionary<(int X, int Z), (ClaimKind Kind, string Owner)> cells = [];

    /// <summary>Record that <paramref name="owner"/> — the prop's own id — took this cell as
    /// <paramref name="kind"/>. The owner rides along so a later prop refused here can name what holds the
    /// ground rather than only that something does.</summary>
    public void Claim(int x, int z, ClaimKind kind, string owner) => cells.TryAdd((x, z), (kind, owner));

    /// <summary>What holds the cell, or null where nothing does — the half a decline needs to be actionable.</summary>
    public (ClaimKind Kind, string Owner)? At(int x, int z) =>
        cells.TryGetValue((x, z), out var held) ? held : null;

    /// <summary>Whether anything at all holds the cell — the occupancy question every prop asks before
    /// resting on it, and the gate that keeps cover from growing through a road or a wall.</summary>
    public bool Holds(int x, int z) => cells.ContainsKey((x, z));

    /// <summary>Whether the cell is held by something other than <paramref name="kind"/> — the building's
    /// question, asked with <see cref="ClaimKind.Route"/>: pavement never blocks a house.</summary>
    public bool HoldsOtherThan(int x, int z, ClaimKind kind) =>
        cells.TryGetValue((x, z), out var held) && held.Kind != kind;

    /// <summary>Whether exactly <paramref name="kind"/> holds the cell — the passage check's question, asked
    /// with <see cref="ClaimKind.Structure"/>: only something built blocks a way past, while a road or a
    /// channel alongside a wall is still ground a player crosses.</summary>
    public bool HoldsKind(int x, int z, ClaimKind kind) =>
        cells.TryGetValue((x, z), out var held) && held.Kind == kind;

    /// <summary>The nearest cell of <paramref name="kind"/> strictly nearer than <paramref name="standoff"/>
    /// blocks (Chebyshev) of the given cell, or null where the standoff is kept. Walked in growing square
    /// rings so the cell named in a refusal is the closest offender, deterministically.</summary>
    public (int X, int Z)? NearerThan(int x, int z, ClaimKind kind, int standoff)
    {
        for (var ring = 0; ring < standoff; ring++)
            for (var dx = -ring; dx <= ring; dx++)
                for (var dz = -ring; dz <= ring; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                    var candidate = (x + dx, z + dz);
                    if (cells.TryGetValue(candidate, out var held) && held.Kind == kind) return candidate;
                }
        return null;
    }
}
