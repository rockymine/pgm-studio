using System.Text.Json.Serialization;
using PgmStudio.Domain;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>
/// One thing an author placed on the map. Dressing is authored, not sprinkled: a tree is cover and a boulder
/// is a wall, so <em>where</em> each one stands is a decision about how the map plays and belongs to the person
/// making the map. A prop therefore carries its own position and its own knobs, and the pass places exactly
/// what is here and nothing else.
///
/// <para>The two kinds of geometry are the two ways a decision can be shaped. A <b>point</b> prop stands
/// somewhere (<see cref="TreeProp"/>, <see cref="BoulderProp"/>); an <b>area</b> prop covers a stretch
/// (<see cref="StrokeProp"/> along a line, <see cref="FloraProp"/> inside a ring). Within an area the placement
/// is still a noise field, because nobody wants to place nine hundred blades of grass — but the area itself
/// was drawn.</para>
///
/// <para>Every prop is fanned across the symmetry orbit. An author draws one half of a map and gets a fair
/// one, which is the same contract the layout itself has had all along.</para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StrokeProp), "stroke")]
[JsonDerivedType(typeof(WaterProp), "water")]
[JsonDerivedType(typeof(TreeProp), "tree")]
[JsonDerivedType(typeof(BoulderProp), "boulder")]
[JsonDerivedType(typeof(FloraProp), "flora")]
[JsonDerivedType(typeof(HouseProp), "house")]
public abstract record PlacedProp
{
    /// <summary>Stable id, so a canvas can select, move and delete one prop among many.</summary>
    public string Id { get; init; } = "";

    /// <summary>Which layer's surface this prop rests on, or null for the top one. A stacked board has a
    /// surface per storey, and a prop stated for a gallery floor lands on the deck over it unless it says
    /// which storey it meant.</summary>
    public string? Layer { get; init; }

    /// <summary>The seed every field this prop rolls is keyed on. Two props of the same kind and knobs at
    /// different seeds differ; the same prop always re-exports identically.</summary>
    public uint Seed { get; init; }

    /// <summary>The clear ground this kind of prop keeps between its resting cells and the nearest cell a
    /// <b>route</b> claims, in blocks — a rule of the kind rather than a knob, which is why it is a property
    /// of the type and not a stored field. Zero for most props: a road is a finish, and cover, water and
    /// buildings may run right up to one. A tree and a boulder state their own (the author's ruling), because
    /// a trunk against the kerb reads as the road growing through the forest rather than the road passing it.
    /// A stroke that is paint rather than a route claims nothing, so nothing stands off it.</summary>
    public virtual int RouteStandoff => 0;
}

/// <summary>
/// A stroke of surface along a drawn line: the centerline, how far either side of it is covered, and what
/// with. A stroke replaces the surface it crosses rather than adding to it — it is a finish, not terrain —
/// which is why it carries a material rather than a height.
///
/// <para><b>What a stroke <em>is</em> is <see cref="Route"/>, and it is not the style.</b>
/// <see cref="PathStyle"/> shapes the band — solid, worn, rough, stones, tapered — which is a brush, and a
/// brush says nothing about whether players read the result as a way through. A gravel tongue over a crag and
/// a road between two spawns can be the same brush at the same radius. Only a route claims its cells as one,
/// and that claim is what a tree's and a boulder's standoff is measured to, so paint is the default and a
/// route is declared: the standoff exists to stop a canopy closing over a road, and asking it of every
/// painted patch leaves a board with nothing plantable on it.</para>
/// </summary>
public sealed record StrokeProp : PlacedProp
{
    /// <summary>The drawn centerline, as <c>[x, z]</c> pairs. Two points or more.</summary>
    public IReadOnlyList<double[]> Points { get; init; } = [];

    /// <summary>How far either side of the centerline is covered, in blocks — half the band.</summary>
    public double Radius { get; init; } = 3;

    /// <summary>The brush: what shape the band takes along the line. Independent of <see cref="Route"/>, since
    /// a road and a smear of dirt can be drawn with the same one.</summary>
    public PathStyle Style { get; init; } = PathStyle.Solid;

    /// <summary>Whether players read this stroke as a way through. A route claims its cells, so a tree and a
    /// boulder keep their stated distance from it; paint claims nothing and is planted over freely.</summary>
    public bool Route { get; init; }

    /// <summary>0–1; what a <see cref="PathStyle.Worn"/> stroke keeps. Every other style covers its whole band.</summary>
    public double Coverage { get; init; } = 0.7;

    /// <summary>What the stroke lays down — a full terrain material, so a road is a solid, a cobbled fabric, a
    /// noise ramp or any pattern the painter offers. The style shapes the <em>band</em>; this decides what
    /// fills it, and the two are independent: a worn cobble and a solid cobble are both sayable.</summary>
    public TerrainMaterial Pave { get; init; } = new SolidMaterial(Palette.Blocks.Gravel);
}

/// <summary>A channel of water: the line the author drew, and how wide and deep a bed is cut under it. Unlike a
/// <see cref="StrokeProp"/>, which repaints the surface and adds no cell, water is the one prop that changes the
/// ground — it takes material <em>out</em> to a carved bed and fills that bed to a level water line, because
/// water laid flat on a surface reads as blue paint rather than water. It only ever cuts existing terrain: the
/// carve stops at the surface it crosses and never fills what was already air.</summary>
public sealed record WaterProp : PlacedProp
{
    /// <summary>The default bank: a cellular voronoi — gravel picking out the cell edges, coarse dirt just
    /// inside them, sand in the middle — so a bed reads like patterned ground the map is finished with rather
    /// than one flat block or a smear of patches.</summary>
    private static readonly TerrainMaterial DefaultBank = new VoronoiMaterial(1, 5,
    [
        new VoronoiBand(new SolidMaterial(Palette.Blocks.Gravel), 2),
        new VoronoiBand(new SolidMaterial(Palette.Blocks.Dirt, 1), 1),
        new VoronoiBand(new SolidMaterial(Palette.Blocks.Sand), 1),
    ]);

    /// <summary>What the water is drawn as, which is what <see cref="Points"/> means: a
    /// <see cref="WaterShape.Channel"/> strokes them as a centerline, a <see cref="WaterShape.Pool"/> closes
    /// them into a ring and fills it. A harbour, a lake or a flooded basin is a pool; a canal, a river or a
    /// moat is a channel.</summary>
    public WaterShape Shape { get; init; } = WaterShape.Channel;

    /// <summary>The drawn points, as <c>[x, z]</c> pairs — a centerline for a channel, an outline for a pool.
    /// Two points or more for a channel, three or more for a pool.</summary>
    public IReadOnlyList<double[]> Points { get; init; } = [];

    /// <summary>Half the channel's water width, in blocks. On a <see cref="WaterShape.Pool"/> it is the
    /// <b>shelf</b> instead: how far in from the shore the bed reaches its full depth, so a harbour shelves
    /// off its quays rather than dropping to a trench at the wall.</summary>
    public double Radius { get; init; } = 3;

    /// <summary>How deep the bed is cut below the water line on the centerline, in blocks. The bed rises to a
    /// single block at the shore, so the fill sits in a bowl rather than a walled trench.</summary>
    public double Depth { get; init; } = 2;

    /// <summary>The world Y the water stands at, where the author states one. Absent, the line is the lowest
    /// surface the channel crosses and the fill never rises past a column's own surface — a channel cut into
    /// ground that was already there.
    ///
    /// <para>Stated, the line is that Y and the fill reaches it whatever the column beneath is doing, which is
    /// what fills a basin: ground dug out in the sketch has no surface up at the line for a derived one to find,
    /// so a lake, a harbour or the water a ship floats on can only be stated. What the author owns then is the
    /// rim — water rises to the line inside the prop's own footprint and nowhere else, so a line above the
    /// surrounding ground stands as a wall of water rather than spilling.</para></summary>
    public double? Level { get; init; }

    public ChannelForm Form { get; init; } = ChannelForm.Canal;

    /// <summary>How far a <see cref="ChannelForm.Natural"/> or <see cref="ChannelForm.Stream"/> edge wobbles its
    /// width off the nominal, in blocks — the roughness of the bank. A canal ignores it.</summary>
    public double Edge { get; init; } = 0.8;

    /// <summary>How wide a beach the water meets the land through, in blocks — the widest the shore band reaches
    /// before a noise field wanders it, dropping it to nothing in places so the water meets the land directly in
    /// some stretches and spreads into a flat in others. 0 gives no beach: the water meets the grass at its edge.</summary>
    public double Shore { get; init; } = 2;

    /// <summary>Whether the beach width opens and closes along the run (a smooth field wandered to nothing in
    /// places), or holds one even width the whole way. Either way it hugs the water; this is only how ragged its
    /// outer edge is.</summary>
    public bool ShoreWander { get; init; } = true;

    /// <summary>The bank the bed floor and the shore beach are laid with — a full terrain material, not one
    /// block, so it can be a solid, a voronoi patchwork or any pattern the painter offers. The shallows show it
    /// through the water, and the beach is the same material meeting the land.</summary>
    public TerrainMaterial Bank { get; init; } = DefaultBank;
}

/// <summary>
/// One tree, standing where it was placed — and one of <b>two</b> trees, which <see cref="Form"/> picks.
///
/// <para>A <see cref="TreeForm.Template"/> tree is vanilla: <see cref="Species"/> names its wood, its canopy
/// profile and its proportions, and <see cref="Height"/> scales the lot. A <see cref="TreeForm.Grown"/> tree
/// is the recursive skeleton: <see cref="Wood"/> names what it is cut from and the knobs below shape it. Each
/// form reads only its own fields, so the ones it does not read are inert rather than wrong — the same way a
/// <see cref="StrokeProp"/> spends <see cref="StrokeProp.Coverage"/> only when it is worn.</para>
/// </summary>
public sealed record TreeProp : PlacedProp
{
    public int X { get; init; }
    public int Z { get; init; }

    public TreeForm Form { get; init; } = TreeForm.Template;

    /// <summary>Template only — a row in <see cref="DressingPalette.Species"/>: the wood, the canopy profile
    /// and the proportions of one vanilla species.</summary>
    public string Species { get; init; } = "oak";

    /// <summary>Grown only — a row in <see cref="DressingPalette.Woods"/>. A grown tree's shape is the
    /// author's, so its wood is all that is left to name.</summary>
    public string Wood { get; init; } = "oak";

    /// <summary>Overall height in blocks. Template: it scales the species' proportions. Grown: not a uniform
    /// scale — a smaller tree also carries a thinner stem and fewer branches, so a sapling reads as a sapling
    /// rather than as a shrunken tree.</summary>
    public double Height { get; init; } = 12;

    /// <summary>Grown only — 1–3 stems at the base.</summary>
    public int Stems { get; init; } = 1;

    /// <summary>Grown only — how far the central axis climbs: low spreads, high spires.</summary>
    public double Leader { get; init; } = 0.55;

    /// <summary>Grown only — how much the trunk wanders on its way up.</summary>
    public double Flow { get; init; } = 0.45;

    /// <summary>Grown only — how far a branch leaves its parent, in radians. A hand-built corpus leaves the
    /// trunk at 59° off vertical and forks its children at 67°, so the default is a radian rather than the
    /// half one a tighter fan wants.</summary>
    public double BranchAngle { get; init; } = 1.1;

    /// <summary>Grown only — branching depth: 2 is a tree, 3 a denser one.</summary>
    public int Levels { get; init; } = 2;

    /// <summary>Grown only — whether the branches are gathered into whorls, a ring every few courses, each
    /// ring shorter than the one below. It is the conifer against the broadleaf, and it is the one shape
    /// choice a picker of six woods cannot make for an author.</summary>
    public bool Whorled { get; init; }

    /// <summary>Grown only — how big each tip's leaf cluster is.</summary>
    public double LeafSize { get; init; } = 0.6;

    /// <summary>How tall this tree is built, held to the range the inspector offers — the bounded reading of
    /// <see cref="Height"/> that every builder and every preview uses.
    ///
    /// <para>The bounds on this and the knobs below are load-bearing rather than tidiness. A tree's cost is
    /// superlinear in its reach — the sample patch a preview cuts is quadratic in it, a grown crown is filled
    /// by testing every cell of its bounding box — while the knobs that set that reach are plain multipliers.
    /// A <see cref="Leader"/> of 55 rather than 0.55 therefore does not draw a strange tree, it asks for a
    /// volume hundreds of blocks on a side and never returns. Holding the values here covers every caller —
    /// the inspector's preview, the picker cards and the world export — instead of each guarding its own
    /// input, and means a stored prop that is out of range still builds something.</para></summary>
    public double Reach => Math.Clamp(Height, 5, 40);

    /// <summary>This tree's growth parameters, as the grower wants them, each bounded like
    /// <see cref="Reach"/>. Read only when it is grown.</summary>
    public TreeShape Shape => new(
        Height: Reach, Stems: Math.Clamp(Stems, 1, 3), Levels: Math.Clamp(Levels, 2, 3),
        BranchAngle: Math.Clamp(BranchAngle, 0.2, 1.5), Flow: Math.Clamp(Flow, 0, 1),
        Leader: Math.Clamp(Leader, 0, 1), Whorled: Whorled);

    /// <summary>How big each tip's leaf cluster is, bounded like <see cref="Reach"/>: it scales the crown, and
    /// the crown is filled cell by cell.</summary>
    public double LeafCluster => Math.Clamp(LeafSize, 0.2, 1);

    /// <summary>The blocks this tree is made of, whichever form it is: a template takes its species' wood, a
    /// grown tree the one it was given.</summary>
    public TreeWood Timber => Form == TreeForm.Template
        ? DressingPalette.SpeciesNamed(Species).Wood
        : DressingPalette.WoodNamed(Wood);

    /// <summary>A trunk stands off the road by three blocks (the author's ruling): nearer and the canopy
    /// closes over the route, which stops reading as a road through trees and starts reading as trees in
    /// the road.</summary>
    public override int RouteStandoff => 3;
}

/// <summary>One boulder, standing bedded into the ground where it was placed.</summary>
public sealed record BoulderProp : PlacedProp
{
    public int X { get; init; }
    public int Z { get; init; }
    public BoulderForm Form { get; init; } = BoulderForm.Round;

    /// <summary>How far the rock reaches from its centre, in blocks. A boulder is an erratic — a mass a
    /// glacier carried and left — so the default is a rock a player takes cover behind rather than a stone
    /// they step over.</summary>
    public double Size { get; init; } = 4;

    /// <summary>That reach held to the range the inspector offers, for the reason
    /// <see cref="TreeProp.Reach"/> holds a tree's: it sizes both the lobes built and the sample patch a
    /// preview cuts, so an out-of-range value asks for a patch thousands of blocks across.</summary>
    public double Reach => Math.Clamp(Size, 2, 10);

    /// <summary>What the rock is cut from — a full terrain material, resolved in the boulder's <em>own</em>
    /// frame rather than the map's, so a mottled rock carries the same mottling to every image of its orbit
    /// instead of sampling whatever the world pattern happens to say where each image landed. Its coordinates
    /// are therefore small (a boulder is a few blocks across), which is what a pattern's patch size has to be
    /// read against here.</summary>
    public TerrainMaterial Rock { get; init; } = new SolidMaterial(Palette.Blocks.Stone);

    /// <summary>Whether moss creeps onto the sky-lit faces — the rock's own micro-flora, laid over whatever
    /// <see cref="Rock"/> resolved.</summary>
    public bool Mossy { get; init; } = true;

    /// <summary>A rock keeps two blocks off the road (the author's ruling) — less than a tree because a rock
    /// carries no canopy, more than zero because a boulder against the kerb narrows the route it was placed
    /// beside.</summary>
    public override int RouteStandoff => 2;
}

/// <summary>A stretch of ground that grows cover. The ring is drawn; what fills it is the density field of
/// <see cref="FloraSpec"/>, because placing every blade by hand is not authoring, it is data entry.</summary>
public sealed record FloraProp : PlacedProp
{
    /// <summary>The drawn outline, as <c>[x, z]</c> pairs. Three points or more.</summary>
    public IReadOnlyList<double[]> Points { get; init; } = [];

    public FloraSpec Spec { get; init; } = new();
}

/// <summary>
/// A building standing on the terrain: one or more touching rectangles an author dragged, and the shell to
/// raise on them.
///
/// <para>It is the same <see cref="HouseStyle"/> a wool cage and a spawn cube are stamped with, and the same
/// <see cref="HouseStamper"/> raises it — but nothing else is shared, and the difference is worth stating.
/// A <b>room</b>'s footprint comes from its plan piece (WX1), it is stamped before the painter, and it carries
/// a pad, monuments, chests and an entry contract because the map is played through it. A <b>prop</b>'s
/// footprint is rectangles someone drew, it is stamped after the painter with the rest of the dressing, and it
/// carries none of those: it is scenery a player can walk into, not a room the objectives live in.</para>
///
/// <para><b>A wing is stored as its two opposite corners</b> rather than as an origin and a size, so the fan
/// mirrors it as the <em>shape</em> it is — the rule an area prop's outline already follows. A rectangle turned
/// through ninety degrees is a rectangle whose width and depth have swapped, and taking two corners round the
/// orbit says that without the stamp having to know it happened. <see cref="Wings"/> is a list of these: one
/// entry is the plain rectangle every board carried until <c>G177</c>, and more than one is an L, a T or a U —
/// still one house under one style, the shape <see cref="HouseStamper"/> already takes as a
/// <see cref="Houses.BuildingPlan"/> of touching <see cref="Wing"/> rectangles.</para>
/// </summary>
/// <summary>
/// One wing as an author drew it: the two opposite corners of its rectangle, and everything it states about
/// itself.
///
/// <para>The corners are <b>doubles in any order</b>, because that is what a drag produces — an author pulling
/// up-left and one pulling down-right placed the same building — and they are floored and normalized on the
/// way to a <see cref="Wing"/>. What the wing states is the very same <see cref="WingSpec"/> the model
/// carries, rather than a second copy of its fields under the same names, so a wing cannot mean one thing in
/// a document and another in the building raised from it.</para>
/// </summary>
/// <param name="Corners">Exactly two opposite <c>[x, z]</c> corners.</param>
/// <param name="Spec">How tall, what roof, which way the ridge runs, and whether it projects. Every field
/// optional: a wing that states nothing is a rectangle wearing the building's own everything, which is what
/// every wing meant before there was anything else to say.</param>
public sealed record AuthoredWing(IReadOnlyList<double[]> Corners, WingSpec Spec = default);

/// <summary>The rule ids a placed building is refused by for its own shape, before the joint model
/// (<see cref="WingJointRules"/>) is asked anything. Stable names, kept apart from any task-tracking id.</summary>
public static class HousePropRules
{
    /// <summary>No rectangles at all — there is no building to place.</summary>
    /// <remarks>Give the building at least one rectangle. A building with none has no footprint to stand on.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Structure)]
    public const string NoWings = "HP1";

    /// <summary>A wing is not two opposite corners, or is too thin to hold two walls and an inside.</summary>
    /// <remarks>State each wing as two opposite corners, and at least as wide as a room. Anything thinner has no inside once its two walls are written.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Structure)]
    public const string WingShape = "HP2";

    /// <summary>The wings cover more ground than a placed building may take.</summary>
    /// <remarks>Shrink the wings, or split the building into two placements. The cap is what one placed building may take.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Structure)]
    public const string PastCap = "HP3";
}

public sealed record HouseProp : PlacedProp
{
    /// <summary>The rectangles the building stands on, each with whatever it states about itself. They abut and
    /// never overlap, and where they touch they share the edge whole — <see cref="Check"/> holds them to it,
    /// since a wing standing clear of the rest is not part of the outline the walls are painted against and a
    /// wing half onto its neighbour is neither one building nor two.</summary>
    public IReadOnlyList<AuthoredWing> Wings { get; init; } = [];

    /// <summary>Which wall the door is cut through, or null to let the building choose the middle of a long
    /// side. A room's door is an entry contract derived from its frame; a prop has no contract to derive one
    /// from, so here it is a choice — and one that has to be <b>turned</b> with the rest of the building at
    /// every image of its orbit, or a mirrored pair both open toward the same side of the map.</summary>
    public RoomEdge? Front { get; init; }

    /// <summary>The shell to raise, snapshotted onto the prop rather than referenced by library id — the rule
    /// a map's bound room styles follow (docs/world-export/structures.md §9). Editing the library row a
    /// building was picked from must never rebuild a shipped map's scenery.</summary>
    public HouseStyle Style { get; init; } = new();

    /// <summary>The largest footprint a placed building may cover, in blocks — three times the 8×8 shell a wool
    /// cage is stamped in, so a 12×16 or a 14×13 house is buildable and a 20×30 one is not.
    ///
    /// <para>The unit is the room the map is actually played through. Scenery that covers more than a few times
    /// one of those stops reading as scenery and starts competing with the objectives for the ground; these are
    /// maps of pieces and lanes, not a landscape with a town in it. A building of several wings is held to the
    /// same number over the ground its wings actually cover, not over the box drawn round them — an L takes no
    /// more of the map for reading larger on the corner it never stood on.</para>
    ///
    /// <para>It is an <b>area</b> rather than a side length so a long low building is as buildable as a square
    /// one. It bounds what a building costs and how much map it takes, and nothing else — <b>height is bounded
    /// separately and by the roof</b>: every form's rise is measured over each wing's own shorter side
    /// (<see cref="RoofField"/>), which is what stops a hall carrying a lean-to as tall as it is long.</para>
    ///
    /// <para>The cap is the <b>prop's</b>, not the stamper's. A wool cage and a spawn cube go through the same
    /// <see cref="HouseStamper"/> and their footprints come from the plan piece they sit on (WX1) — a map's own
    /// geometry, and nothing a dressing limit has any business refusing.</para></summary>
    public const int MaxFootprint = 192;

    /// <summary>The plan this prop stamps, or null when it is no building at all — the same answer
    /// <see cref="Check"/> gives the reasons for.
    ///
    /// <para>Every wing is required to hold two walls and an inside on its own, the same three-block floor a
    /// single rectangle always has: a wing composes with its neighbours below the eave, but nothing composes a
    /// room out of a sliver with no width of its own.</para></summary>
    public BuildingPlan? Plan() => Check().Refuses ? null : Read();

    /// <summary>Why this prop is no building — every reason, not the first. Empty where it is one. Separate
    /// from <see cref="Plan"/> because the two callers want different halves: a build wants the plan or
    /// nothing, and an author wants the sentences, since a plan silently declining to stamp is the failure
    /// this exists to replace.
    ///
    /// <para>Three of the refusals are the prop's own shape (<see cref="HousePropRules"/>): no wings at all,
    /// a wing that is not two corners or is thinner than a room, and a covered area — the cells the union of
    /// wings actually holds, not the box drawn round them — past <see cref="MaxFootprint"/>. Each stops the
    /// read, because nothing further can be asked of corners that do not parse. The rest are the joint model's
    /// (<see cref="WingJoints"/>) and carry its <c>HJ</c> ids, and those are reported together: an author
    /// redrawing one bad joint wants to know about the other.</para></summary>
    public Findings Check()
    {
        if (Wings.Count == 0)
            return Findings.Of(new Finding(HousePropRules.NoWings,
                "a building needs at least one rectangle", Field: "wings"));
        for (var index = 0; index < Wings.Count; index++)
        {
            var corners = Wings[index].Corners;
            var wing = index.ToString();
            if (corners.Count < 2 || corners[0].Length < 2 || corners[1].Length < 2)
                return Findings.Of(new Finding(HousePropRules.WingShape,
                    "every wing is drawn as two opposite corners, each an x and a z",
                    Field: "wings", Subjects: [wing]));
            var (minX, minZ, maxX, maxZ) = Corners(corners);
            if (maxX - minX + 1 < 3 || maxZ - minZ + 1 < 3)
                return Findings.Of(new Finding(HousePropRules.WingShape,
                    $"a wing holds two walls and an inside, so it is at least three blocks each way; this one "
                    + $"is {maxX - minX + 1} × {maxZ - minZ + 1}", Field: "wings", Subjects: [wing]));
        }

        var plan = Read()!;
        var covered = plan.Cells().Count();
        if (covered > MaxFootprint)
            return Findings.Of(new Finding(HousePropRules.PastCap,
                $"the wings cover {covered} blocks, past the {MaxFootprint} a placed building may take",
                Field: "wings"));

        return WingJoints.Check(plan);
    }

    /// <summary>The wings as drawn, with nothing judged — what both of the two above read.</summary>
    private BuildingPlan? Read()
    {
        if (Wings.Count == 0) return null;
        var wings = new List<Wing>(Wings.Count);
        foreach (var authored in Wings)
        {
            var corners = authored.Corners;
            if (corners.Count < 2 || corners[0].Length < 2 || corners[1].Length < 2) return null;
            var (minX, minZ, maxX, maxZ) = Corners(corners);
            wings.Add(new Wing(minX, minZ, maxX, maxZ, authored.Spec));
        }
        return new BuildingPlan(wings);
    }

    private static (int MinX, int MinZ, int MaxX, int MaxZ) Corners(IReadOnlyList<double[]> corners) => (
        (int)Math.Floor(Math.Min(corners[0][0], corners[1][0])),
        (int)Math.Floor(Math.Min(corners[0][1], corners[1][1])),
        (int)Math.Floor(Math.Max(corners[0][0], corners[1][0])),
        (int)Math.Floor(Math.Max(corners[0][1], corners[1][1])));
}
