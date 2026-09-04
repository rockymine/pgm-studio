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

    /// <summary>A building stands <b>across</b> a route: the road it covers carries on out the other side, so
    /// what was one way through the board is now two dead ends at a wall. A road that simply <em>ends</em> at
    /// the building is a porch and stands — that is what a road running to a door is — and the two are told
    /// apart by what is left of the stroke once the footprint is out of it: one run of paving is an end, two
    /// or more is a crossing. Only a stroke the author marked a <b>route</b> counts; paint laid to change a
    /// finish is ground rather than a way.</summary>
    /// <remarks>Move the building off the road, or redraw the road to end at its door. A house at the end of a road is a porch and is not this fault — what fires is a road that continues past the far wall, which means players walked that way and now cannot. The whole building is declined and is not in the exported world.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Structure, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string RouteCrossed = "DR-CROSS";

    /// <summary>How far apart two paved cells may lie and still be one run of road, in blocks. A stroke's own
    /// coverage leaves cells out — a worn road is holes by design — and the question here is whether the road
    /// carries on past the building, not whether its paving is unbroken.</summary>
    public const int RouteRunGap = 2;

    /// <summary>A prop closes the way between two of the places the map is played between — its spawns, its
    /// monuments, its goals — or sends that way further round than a player will go. Measured by walking the
    /// terrain between every pair of waypoints and walking it again with the prop's footprint taken out of the
    /// ground: a pair that had a route and now has none is a way closed, and a route surviving more than ten
    /// blocks longer is the same fault at a lesser degree, ten being how far out of their way a player goes.
    /// Props accumulate, so two buildings that each leave a way and together leave none are caught at the
    /// second.</summary>
    /// <remarks>Move the prop off the corridor the finding names, or open another way between those two points. This is the corridor the board was drawn to have rather than a stroke somebody drew on it, so a standoff to a road cannot answer it — a building across a leg with no road on it passes every other test on the board. The whole prop is declined and is not in the exported world.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Structure, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string WayThrough = "DR-WAY";

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

    /// <summary>A building's own footprint is not level enough to stand on: the ground across it rises by at
    /// least the height of the building itself — its wall courses plus the rise of its roof. A building seats on the <b>lowest</b> column of its
    /// plan and the terrain standing over that floor is carved out of it, so a footprint spanning more relief
    /// than the building is tall comes out with its uphill wall entirely below the ground beside it — a house
    /// hidden in a hill rather than one dug into a slope. Sinking into a slope is what the seating rule is
    /// for and stays silent; disappearing into one is this.</summary>
    /// <remarks>Move the building onto a flatter site, or state the plateau it stands on — an `area` relief mark under the footprint gives it one, and is what the ground of an objective is given for the same reason. The threshold is the style's own height, so a two-storey barn may stand where a cottage may not. The whole building is declined and is not in the exported world.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Structure, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string SiteNotLevel = "DR-SLOPE";

    /// <summary>A prop names a layer this board does not have ground on. A stacked board carries a surface
    /// per storey and a prop may say which one it rests on; naming one that is not there is not the same as
    /// naming none, so it is declined rather than quietly seated on the top surface — which is exactly the
    /// storey the author was saying they did not mean.</summary>
    /// <remarks>Name a layer the board draws on, or leave the word off and take the top surface. The layer ids a board has are the `id` of each entry in its `layers[]`.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string NoSuchLayer = "DR-LAYER";

    /// <summary>A prop the world cut down. A prop seats on its <b>feet</b> — the cells of its lowest course
    /// are what the keep-outs, the claims and the ground are asked about — and everything it reaches over is
    /// written only where it meets air. So a prop that stands clear of a building may still have most of its
    /// body inside one, and the blocks that land are whatever the wall left: a rock flattened along a face,
    /// or a crown cut off from its own trunk and standing in the air on the far side.
    ///
    /// <para><b>Being clipped is not the fault; being cut in two is.</b> A rock tucked against a wall is
    /// flattened along it and is still a rock — measured, a boulder loses up to a third of its blocks that way
    /// and severs none of them — and a tree brushing a roof loses a few leaves. What this names is the other
    /// case: the clip took out the blocks that joined a limb to the trunk, so what is left of that limb stands
    /// in the air with nothing under it. The prop seated, nothing declined it, and the world holds a tree with
    /// a piece floating beside it. The finding carries how many blocks are in the world, how many the wall
    /// blocked, how many are cut off from the prop's own footing, and the first cell it was stopped at.</para>
    ///
    /// <para>The threshold is <see cref="ClipSevered"/> blocks cut off, not a share of the prop: a share
    /// tracks how much of the thing is missing, which is the boulder's answer and not this one.</para></summary>
    /// <remarks>Move the prop clear of what it is reaching into, or make it smaller. The ground a building holds is one block past what it stamps (`DR-CLAIM`), and that is a seat rule rather than a size rule — it keeps a stem out of a wall and says nothing about a crown eight blocks wide, so a big prop needs the distance its own reach asks for and not the distance the seat allows. Measured against a wall taller than the tree: a 20-course grown oak severs a limb at every clearance out to 8 blocks, while an 8-course one severs nothing past 2. The prop is left in the world exactly as the clip left it, floating piece included; this is a complaint, not a decline.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string PropCut = "DR-CUT";

    /// <summary>How many of a prop's blocks the clip has to cut off from its own footing before
    /// <see cref="PropCut"/> is raised. Under it what came away is a leaf or two and reads as foliage; at it
    /// and over it a viewer sees a piece of the prop standing in the air.</summary>
    public const int ClipSevered = 8;
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
    /// <summary>A structure the map stated: a room floor, an iron cube, a wall, a redstone line, or a sketch
    /// shape that marked itself kept clear — a town wall, a crop bed, a well's rim.</summary>
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
    /// <summary>The paved cells of a stroke that claims its ground. The one kind a building ignores, and the
    /// one a standoff measures to.</summary>
    Paving,
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
    private readonly Dictionary<(string Storey, int X, int Z), (ClaimKind Kind, string Owner)> cells = [];

    /// <summary>The book as one storey sees it. A stacked board carries a surface per storey and a prop rests
    /// on the one it names, so a claim is a claim <em>of a layer</em>: a channel cut in the ground holds the
    /// columns it carved on its own storey and none of the columns above it. A prop naming no layer rests on
    /// the top surface, which is a storey like any other here, so a board with one layer answers exactly as a
    /// book with no storeys in it does.</summary>
    public Storey On(string? layer) => new(this, layer ?? "");

    /// <summary>One storey's view of the claims — the same verbs, bound to the layer the asking prop rests
    /// on. Every placement takes one of these rather than the book, so a call site cannot forget which storey
    /// it is claiming for.</summary>
    public readonly record struct Storey(GroundClaims Book, string Layer)
    {
        /// <summary>Record that <paramref name="owner"/> — the prop's own id — took this cell as
        /// <paramref name="kind"/>. The owner rides along so a later prop refused here can name what holds
        /// the ground rather than only that something does.</summary>
        public void Claim(int x, int z, ClaimKind kind, string owner) =>
            Book.cells.TryAdd((Layer, x, z), (kind, owner));

        /// <summary>What holds the cell, or null where nothing does — the half a decline needs to be
        /// actionable.</summary>
        public (ClaimKind Kind, string Owner)? At(int x, int z) =>
            Book.cells.TryGetValue((Layer, x, z), out var held) ? held : null;

        /// <summary>Whether anything at all holds the cell — the occupancy question every prop asks before
        /// resting on it, and the gate that keeps cover from growing through a road or a wall.</summary>
        public bool Holds(int x, int z) => Book.cells.ContainsKey((Layer, x, z));

        /// <summary>Whether the cell is held by something other than <paramref name="kind"/> — the building's
        /// question, asked with <see cref="ClaimKind.Paving"/>: pavement never blocks a house.</summary>
        public bool HoldsOtherThan(int x, int z, ClaimKind kind) =>
            Book.cells.TryGetValue((Layer, x, z), out var held) && held.Kind != kind;

        /// <summary>Whether exactly <paramref name="kind"/> holds the cell — the passage check's question,
        /// asked with <see cref="ClaimKind.Structure"/>: only something built blocks a way past, while a road
        /// or a channel alongside a wall is still ground a player crosses.</summary>
        public bool HoldsKind(int x, int z, ClaimKind kind) =>
            Book.cells.TryGetValue((Layer, x, z), out var held) && held.Kind == kind;

        /// <summary>The nearest cell of <paramref name="kind"/> strictly nearer than
        /// <paramref name="standoff"/> blocks (Chebyshev) of the given cell, or null where the standoff is
        /// kept. Walked in growing square rings so the cell named in a refusal is the closest offender,
        /// deterministically.</summary>
        public (int X, int Z)? NearerThan(int x, int z, ClaimKind kind, int standoff)
        {
            for (var ring = 0; ring < standoff; ring++)
                for (var dx = -ring; dx <= ring; dx++)
                    for (var dz = -ring; dz <= ring; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                        if (Book.cells.TryGetValue((Layer, x + dx, z + dz), out var held) && held.Kind == kind)
                            return (x + dx, z + dz);
                    }
            return null;
        }
    }
}
