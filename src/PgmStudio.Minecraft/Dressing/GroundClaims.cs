using PgmStudio.Vocabulary;
namespace PgmStudio.Minecraft.Dressing;

/// <summary>The dressing pass's own rules — where a prop may stand, what it may take from the ground and what
/// it may be made of — served by <c>GET /api/rules</c> from the docstrings here the way every gate family's
/// are.</summary>
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

    /// <summary>A building leaves no way past itself: a side of it carries fewer than 8 blocks of passable
    /// ground alongside its whole run (the author's number). <b>Every</b> side is asked, because the lane a
    /// building stands in is the ground players arrive on rather than a way round it — a house in the middle
    /// of a route corks it however far the route runs on ahead. A side the ground stops flush against is the
    /// map's own edge or a hole in it, and a building may stand against one — a coast house is a house — but
    /// not against two facing each other, which is a building spanning the land rather than seated at its
    /// edge. Measured from what the building <b>stamps</b> rather than from its walls: a roof oversails its
    /// wall by at least one block, and the blocks a player walks under are the ones that were written.
    ///
    /// <para><b>Asked of a group of buildings, not of each one.</b> Buildings standing within a passage of
    /// each other are one block of buildings, and the eight is owed round what they make together — a player
    /// walks round a village rather than between every pair of its houses, so the ground inside it is the
    /// claim ring's to keep. Grouping is transitive, and at exactly the passage two buildings answer for
    /// themselves, so no gap between them is one the rule has no reading of.</para></summary>
    /// <remarks>Move the building against the edge of the ground it stands on and keep an eight-block passage along every other side, or widen that ground, or bring it close enough to its neighbours to be one block of buildings with them. The eight are counted from the roof's edge, not the wall's, so a lane 15 blocks across takes a building 7 blocks across including its eaves. Passable here means terrain with nothing built on it; a road or a channel beside the wall still counts as a way past, a building outside this one's group does not. A complaint: the building is in the exported world, standing where it was put, because where a building stands is something an author moves. `POST /api/map/{slug}/sketch/seats` answers this same rule forwards over every cell of the board, so a placement is found rather than guessed at.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Structure, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string PassAround = "DR-PASS";

    /// <summary>The passage's width in blocks — <see cref="PassAround"/>'s one number.</summary>
    public const int PassAroundWidth = 8;

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
    /// <para><b>And being buried is the same fault from underneath.</b> A prop seats on the <em>lowest</em>
    /// column its feet stand over, which is what lets it sit into a slope — and on a stepped landform that
    /// column is the bottom of a step, so the body is written into ground that stands over it and almost none
    /// of it lands. What survives is whatever cleared the surface, and that remnant rests on real ground, so
    /// nothing is severed and the count above stays nought however much was lost. The share is the reading
    /// that sees it: past <see cref="ClipBlockedShare"/> of the prop inside something already standing, the
    /// thing in the world is not the thing that was asked for.</para></summary>
    /// <remarks>Move the prop clear of what it is reaching into, or make it smaller. The ground a building holds is one block past what it stamps (`DR-CLAIM`), and that is a seat rule rather than a size rule — it keeps a stem out of a wall and says nothing about a crown eight blocks wide, so a big prop needs the distance its own reach asks for and not the distance the seat allows. Measured against a wall taller than the tree: a 20-course grown oak severs a limb at every clearance out to 8 blocks, while an 8-course one severs nothing past 2. A prop reported as buried rather than severed is standing on ground that steps under it — move it onto one step or the other, since a seat is taken on the lowest column its feet cover. The prop is left in the world exactly as the clip left it, floating piece included; this is a complaint, not a decline.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string PropCut = "DR-CUT";

    /// <summary>How many of a prop's blocks the clip has to cut off from its own footing before
    /// <see cref="PropCut"/> is raised. Under it what came away is a leaf or two and reads as foliage; at it
    /// and over it a viewer sees a piece of the prop standing in the air.</summary>
    public const int ClipSevered = 8;

    /// <summary>Water standing against a hole in its own basin. A pool fills the bed it carves, and the hollow
    /// it sits in is very often dug by something else — a relief mark, a shape's own floor — so where that
    /// hollow reaches further than the bed does, the extra is excavated and never filled: a trench as deep as
    /// the water is, running alongside it, with the water standing against open air and nothing said.
    ///
    /// <para>Air is a fault only where there is ground to hold water back. A pool reaching the board's own
    /// edge meets the void, and a wall of water at the world's rim is what a coast is — so a neighbour with no
    /// terrain column at all is passed over, and only a column the board <b>drew</b> and then left open
    /// counts. That is the author's ruling and the whole of the test.</para></summary>
    /// <remarks>Widen the pool onto the ground that was dug for it, or stop digging it there. The finding names the first open column, so the two shapes — the hollow and the water that fills it — can be compared where they part company. A complaint: the world is built and the water is in it.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain, RuleConcern.World)]
    public const string DryEdge = "DR-DRY";

    /// <summary>A block of a copied body naming a face it has nothing to cling to. A copied body is written
    /// block for block with the data it was cut with — that is what makes it a copy — so a block whose data
    /// <b>is</b> a direction has to point at something the body actually holds. A vine states every side it
    /// clings to at once; a side naming air is a curtain hanging on nothing, and a pair of <em>opposite</em>
    /// sides is that same fault seen from the front, as a vine with two faces in one block. A vine under
    /// another naming the same side is held by it, which is how a curtain hangs past the leaf it started on.
    ///
    /// <para>Asked once per body rather than once per placement: a board draws the same tree thirty times and
    /// the fault is in the recipe.</para></summary>
    /// <remarks>State the one side the leaf is on. A vine's sides are turned with the body it belongs to when a prop is fanned round the orbit (`BlockGeometry.Turned`), so a single face survives a mirror or a quarter-turn and does not need a second bit to protect it.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Feature, RuleConcern.Material)]
    public const string UnheldFace = "DR-FACE";

    /// <summary>A body of water that dug a shaft rather than filled a hollow. Its line is one plane across the
    /// whole run — by default the lowest surface it crosses — and every bed column standing above that line is
    /// emptied down to it. <c>depth</c> bounds how far <b>below</b> the line the bed goes and nothing bounds
    /// how far above it the carve reaches, so a pond drawn across a slope comes out as a straight-sided pit as
    /// deep as the ground falls, whatever depth was asked for. Measured against the author's own stated depth,
    /// because that is the number they said: a bank taller than the water is deep is ground taken out rather
    /// than water put in.</summary>
    /// <remarks>Draw the body inside ground that is already level — the finding names the wall's own cell and its two courses, which is where to read the fall — or state a `level` and let the water fill the hollow that is there instead of making one. A complaint: the world is built and the water is in it.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain, RuleConcern.World)]
    public const string SteepBank = "DR-BANK";

    /// <summary>A boulder built out of nothing but the tones of the ground it stands on. A rock is an
    /// erratic — a mass carried here and left — so it reads as a rock by not being made of the field it sits
    /// in, and one whose every tone family the ground already states has no silhouette at any size: it is a
    /// patch of the same ground standing up. Measured against the ground where something can <b>rest</b>, so a
    /// meadow whose steep faces are bare stone is a meadow and a stone rock on it stands out as intended.
    ///
    /// <para>Tone families are <c>TerrainPalette</c>'s — the unit a pattern is filled from, which is what an
    /// author reaches for and what a player reads at a distance. A rock keeping one family the ground does not
    /// have is a rock, however much else it shares: what disappears is the one built wholly from the
    /// field.</para></summary>
    /// <remarks>Cut the rock from stone, andesite and cobblestone, which is what a placement naming no recipe already gets: it reads against sand, grass, dirt and red sand, and against any single clay, since no two clay colours are close. Where the ground is itself grey stone, take the rock the other way — a clay, a dark block or a sand — rather than deepening the grey. A complaint: the world is built and the rock is in it.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Material, RuleConcern.Terrain)]
    public const string RockInTheGroundsTone = "DR-TONE";

    /// <summary>A boulder standing on a face rather than on ground. An erratic is a mass left where the ice
    /// dropped it, which is ground flat enough to hold one; a rock pinned to a steep hillside reads as neither
    /// — the slope is already the feature there, and the rock sitting on it only interrupts a line that was
    /// doing the work. Measured as the terrain's own inclination under the placement
    /// (<c>SurfaceGradient.Degrees</c>, the same reading the paint is banded by) against the angle at which the
    /// theme painting that cell stops calling the ground a meadow.
    ///
    /// <para><b>The board states the angle, not this rule.</b> A surface graded by slope has already said
    /// where its cliff begins — the band that paints the steepest ground there is — and that boundary is the
    /// one an author drew. Ground the middle band paints is still ground, so a rock on the coarse dirt of a
    /// gentle hillside stands; only the band that means <em>bare rock face</em> is refused. A theme grading by
    /// nothing is read at <c>Materials.DefaultCliffAngle</c>.</para></summary>
    /// <remarks>Move the rock onto ground the board does not paint as a face — the flat, or the graded band under it. The finding names the cell, the angle measured there and the angle the theme calls a cliff, so the three can be compared against the incline read. A complaint: the world is built and the rock is in it.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain, RuleConcern.World)]
    public const string RockOnAFace = "DR-STEEP";

    /// <summary>A tree standing on ground nothing grows out of. A tree is a thing that grew where it is, so
    /// the block under its trunk is the one that says so; on stone, gravel, clay or a path's paving it reads
    /// as a model of a tree set down rather than as a wood, and no canopy over it repairs that.
    ///
    /// <para>The board has to carry the soil before a tree can stand in it. A theme painting a surface that
    /// is rock all the way up gives the pass nowhere to put one, so the fix is usually the <em>paint</em> —
    /// a soil band under the wood — rather than the position, and the read that answers where soil is on the
    /// ground is the themes census.</para>
    ///
    /// <para>Grass and the three dirts, by <c>DressingPalette.RootsInto</c>. Sand and gravel grow a tuft and
    /// not a trunk, so they are ground here and not soil (author). Asked of a tree that landed: one the pass
    /// turned away is standing nowhere and has nothing to be rooted in.</para></summary>
    /// <remarks>Paint soil where the wood stands — a band of grass or dirt under the canopy — or move the tree onto ground that already has it. <c>POST …/sketch/seats?kind=tree</c> answers where that ground is, and refuses every cell without it under this id. A complaint: the world is built and the tree is in it.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Material, RuleConcern.Terrain)]
    public const string TreeOnBareGround = "DR-ROOT";

    /// <summary>How much of a prop the clip has to block before <see cref="PropCut"/> is raised on the share
    /// alone. A rock tucked against a wall is flattened along it and measures about a third, which is a rock;
    /// over half of the body inside something already standing is not the prop the author placed, whether or
    /// not anything came away from its feet.</summary>
    public const double ClipBlockedShare = 0.5;
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
