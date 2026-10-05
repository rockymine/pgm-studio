using PgmStudio.Vocabulary;
namespace PgmStudio.Minecraft.Dressing;

/// <summary>The dressing pass's own rules — where a prop may stand, what it may take from the ground and what
/// it may be made of — served by <c>GET /api/rules</c> from the docstrings here the way every gate family's
/// are.</summary>
public static class DressingRules
{
    /// <summary>A tree stands less than 3 blocks from a road, or a boulder less than 2 blocks.</summary>
    /// <remarks>Move the tree in <c>dressing.props</c> until it is at least 3 blocks from the road. Move the
    /// boulder in <c>dressing.props</c> until it is at least 2 blocks from the road.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string RoadStandoff = "DR-ROAD";

    /// <summary>The passage along a side of a building or group of buildings is less than 8 blocks wide, and the
    /// side is not against the coast.</summary>
    /// <remarks>Either move the building in <c>dressing.props</c> until the passage along each side is at least 8
    /// blocks wide, or add a shape to <c>shapes</c> that widens the ground along the side.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Structure, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string PassAround = "DR-PASS";

    /// <summary>The passage's width in blocks — <see cref="PassAround"/>'s one number.</summary>
    public const int PassAroundWidth = 8;

    /// <summary>A building overlaps a road and leaves the road in more separate runs than before, where cells of
    /// road 2 blocks apart or less are one run.</summary>
    /// <remarks>Either move the building in <c>dressing.props</c> off the road, or change the <c>points</c> of the
    /// road in <c>dressing.props</c> until the road ends at the building.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Structure, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string RouteCrossed = "DR-CROSS";

    /// <summary>How far apart two paved cells may lie and still be one run of road, in blocks. A stroke's own
    /// coverage leaves cells out — a worn road is holes by design — and the question here is whether the road
    /// carries on past the building, not whether its paving is unbroken.</summary>
    public const int RouteRunGap = 2;

    /// <summary>A building leaves no walking route between two waypoints, or makes the walking distance between
    /// them more than 10 blocks longer.</summary>
    /// <remarks>Either move the building in <c>dressing.props</c> off the route the finding names, or add a shape
    /// to <c>shapes</c> that opens another route around it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Structure, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string WayThrough = "DR-WAY";

    /// <summary>A building's footprint is less than 5 blocks across its shorter side.</summary>
    /// <remarks>Change the <c>corners</c> of a wing in <c>wings</c> until the footprint is at least 5 blocks across
    /// its shorter side.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Structure)]
    public const string FootprintFloor = "DR-SIZE";

    /// <summary>The footprint minimum in blocks — <see cref="FootprintFloor"/>'s one number.</summary>
    public const int FootprintMin = 5;

    /// <summary>A prop overlaps kept clear ground.</summary>
    /// <remarks>Either move the prop in <c>dressing.props</c> until it no longer overlaps kept clear ground, or
    /// move the spawn, wool room or structure that keeps the ground clear.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Spawn, RuleConcern.Objective)]
    public const string KeptClear = "DR-KEEP";

    /// <summary>A prop overlaps the claim of a fluid, a road, a building or an earlier prop.</summary>
    /// <remarks>Either move the prop in <c>dressing.props</c> until it no longer overlaps the claim, or move the
    /// fluid, road, building or earlier prop that holds the claim in <c>dressing.props</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string GroundTaken = "DR-CLAIM";

    /// <summary>How far past what it stamps a building holds the ground, in blocks — the author's number, and
    /// the one thing that separates two buildings that merely fail to overlap from two that leave a block of
    /// clear ground between them. What a placement is <em>tested</em> against is the stamped extent, so the
    /// ring is spent once between a pair rather than twice.</summary>
    public const int StructureClearance = 1;

    /// <summary>A prop has no ground under a cell it rests on, in any symmetry copy.</summary>
    /// <remarks>Either move the prop in <c>dressing.props</c> until every cell it rests on has ground under it, or
    /// add a shape to <c>shapes</c> that covers the cell.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string NoGround = "DR-SITE";

    /// <summary>The rise of the ground across a building's footprint is at least the height of its walls plus 2
    /// blocks for each block of pitch.</summary>
    /// <remarks>Either move the building in <c>dressing.props</c> onto ground that rises less than the building's
    /// height, or add an <c>area</c> mark under the footprint to the <c>marks</c> of the group in
    /// <c>relief</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Structure, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string SiteNotLevel = "DR-SLOPE";

    /// <summary>The ground dug out of a building's footprint is more than 3 blocks deep at one column.</summary>
    /// <remarks>Either move the building in <c>dressing.props</c> onto ground beside the lowest column of its
    /// footprint, or add an <c>area</c> mark under the footprint to the <c>marks</c> of the group in
    /// <c>relief</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Structure, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string SiteDug = "DR-DIG";

    /// <summary>The deepest carve at any one column a building's seat may make in silence, in blocks —
    /// <see cref="SiteDug"/>'s one number. Up to it a house settles into a slope; past it the house is
    /// probably submerged (author).</summary>
    public const int SettleDepth = 3;

    /// <summary>A prop names a layer that does not have any ground.</summary>
    /// <remarks>Either set the <c>layer</c> of the prop in <c>dressing.props</c> to the <c>id</c> of an entry in
    /// <c>layers</c> that has ground, or set the <c>layer</c> of the prop to <c>null</c>.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string NoSuchLayer = "DR-LAYER";

    /// <summary>A prop has 8 or more blocks cut off from its feet, or more than 50 percent of its blocks inside
    /// something already standing.</summary>
    /// <remarks>Either move the prop in <c>dressing.props</c> farther from what it reaches into, or change the
    /// <c>style</c> of the prop to one with a smaller <c>height</c> or <c>size</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain)]
    public const string PropCut = "DR-CUT";

    /// <summary>How many of a prop's blocks the clip has to cut off from its own footing before
    /// <see cref="PropCut"/> is raised. Under it what came away is a leaf or two and reads as foliage; at it
    /// and over it a viewer sees a piece of the prop standing in the air.</summary>
    public const int ClipSevered = 8;

    /// <summary>A fluid touches a column of ground that has air at the fluid's level.</summary>
    /// <remarks>Either change the <c>points</c> of the fluid in <c>dressing.props</c> until it covers the ground
    /// dug beside it, or set the <c>level</c> of a basin to the lowest ground along its outline.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain, RuleConcern.World)]
    public const string DryEdge = "DR-DRY";

    /// <summary>A vine in a copied tree has no block behind one of its sides.</summary>
    /// <remarks>Set the data of the vine in the <c>body</c> of the recipe in <c>dressing.styles</c> to the one side
    /// that has a block behind it.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Feature, RuleConcern.Material)]
    public const string UnheldFace = "DR-FACE";

    /// <summary>A request to save a copied tree has no cut record.</summary>
    /// <remarks>Either send the request again with <c>form</c> set to <c>template</c>, or send the request again
    /// with <c>cut</c> set to where and when the tree was cut.</remarks>
    [Rule(RuleCategory.Forbidden, RuleConcern.Request, RuleConcern.Feature)]
    public const string UncutCopy = "DR-COPY";

    /// <summary>The ground a fluid cuts away above its level is more than its stated depth in height.</summary>
    /// <remarks>Either move the fluid in <c>dressing.props</c> onto ground that is already level, or set the
    /// <c>level</c> of the fluid to the height of the ground it should cover.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain, RuleConcern.World)]
    public const string SteepBank = "DR-BANK";

    /// <summary>A fluid overlaps kept clear ground that stands no lower than the fluid level.</summary>
    /// <remarks>Either move the fluid in <c>dressing.props</c> until it no longer overlaps kept clear ground, or
    /// set the <c>level</c> of the fluid to a height above the kept clear ground.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain, RuleConcern.World)]
    public const string HeldDry = "DR-HELD";

    /// <summary>A boulder has no tone family apart from those of the ground under it.</summary>
    /// <remarks>Change the <c>rock</c> of the boulder's recipe in <c>dressing.styles</c> to a material with a tone
    /// family that the ground under it does not have.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Material, RuleConcern.Terrain)]
    public const string RockInTheGroundsTone = "DR-TONE";

    /// <summary>The slope under a boulder is at least the angle at which its palette paints a face, or 30 degrees
    /// where the palette has no band stack by slope.</summary>
    /// <remarks>Move the boulder in <c>dressing.props</c> until the slope under it is less than the angle at which
    /// the palette paints a face.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Feature, RuleConcern.Terrain, RuleConcern.World)]
    public const string RockOnAFace = "DR-STEEP";

    /// <summary>The ground under the trunk of a tree is not soil.</summary>
    /// <remarks>Either move the tree in <c>dressing.props</c> onto soil, or change the <c>surface</c> of the
    /// palette under it in <c>themes</c> to a <c>layered</c> material whose first band is soil.</remarks>
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
/// rule differ by claimant where the rules genuinely differ: a building collides with a fluid and with another
/// building but never with a road (a road is meant to run to its porch), and a prop's standoff is stated
/// against the road rather than against everything.</summary>
public enum ClaimKind
{
    /// <summary>A fluid's bed and beach — carved ground nothing else may take.</summary>
    Fluid,
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

        /// <summary>Record a claim on a cell nothing holds or a fluid holds — a rock standing in the water,
        /// whose cells are its own once it is there. A cell anything else holds keeps its first claimant, as
        /// <see cref="Claim"/> does.</summary>
        public void ClaimThroughFluid(int x, int z, ClaimKind kind, string owner)
        {
            if (At(x, z) is null or { Kind: ClaimKind.Fluid }) Book.cells[(Layer, x, z)] = (kind, owner);
        }

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
