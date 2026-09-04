using System.Reflection;
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
[JsonDerivedType(typeof(StrokeProp), PropKinds.Stroke)]
[JsonDerivedType(typeof(WaterProp), PropKinds.Water)]
[JsonDerivedType(typeof(TreeProp), PropKinds.Tree)]
[JsonDerivedType(typeof(BoulderProp), PropKinds.Boulder)]
[JsonDerivedType(typeof(FloraProp), PropKinds.Flora)]
[JsonDerivedType(typeof(HouseProp), PropKinds.House)]
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

    /// <summary>Where this kind goes in the pass's order, low first — what <see cref="Decorator.Decorate"/>
    /// runs its groups in, and the reason the order matters at all: a prop meets the claims of everything
    /// placed <em>before</em> it and none of the claims of what comes after, so a tree may stand on ground a
    /// bed of flora covers today and a bed of flora may not stand on a tree. <c>DecoratorTests</c> pins each
    /// neighbouring pair against the pass itself, so a resequenced pass fails rather than drifts.</summary>
    public virtual int PlacementOrder => 0;

    /// <summary>Every kind a dressing document names, in the order the codec declares them — read off the
    /// discriminator above rather than listed again, so a kind added there is a kind every caller can ask
    /// for.</summary>
    public static IReadOnlyList<string> Kinds => [.. Prototypes.Keys];

    /// <summary>The word this placement crosses the wire as, or null for a kind the codec does not declare —
    /// read off the same discriminator the serializer writes, so a name minted from it is the name the
    /// document will carry.</summary>
    public static string? KindOf(PlacedProp prop) =>
        Prototypes.FirstOrDefault(pair => pair.Value.GetType() == prop.GetType()).Key;

    /// <summary>The standoff a named kind keeps from claimed paving, or null where no such kind is named. The number
    /// lives on the kind's own type (<see cref="RouteStandoff"/>) and is read off an empty one of it, so
    /// there is no second table of standoffs to disagree with the rule.</summary>
    public static int? PavingStandoffOf(string kind) =>
        Prototypes.TryGetValue(kind, out var prototype) ? prototype.RouteStandoff : null;

    /// <summary>Where a named kind goes in the pass's order, or null where no such kind is named — read the
    /// same way, off the kind's own <see cref="PlacementOrder"/>.</summary>
    public static int? PlacementOrderOf(string kind) =>
        Prototypes.TryGetValue(kind, out var prototype) ? prototype.PlacementOrder : null;

    private static readonly Dictionary<string, PlacedProp> Prototypes =
        typeof(PlacedProp).GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Where(derived => derived.TypeDiscriminator is string)
            .ToDictionary(derived => (string)derived.TypeDiscriminator!,
                          derived => (PlacedProp)Activator.CreateInstance(derived.DerivedType)!);
}

/// <summary>
/// A stroke of surface along a drawn line: the centerline, how far either side of it is covered, and what
/// with. A stroke replaces the surface it crosses rather than adding to it — it is a finish, not terrain —
/// which is why it carries a material rather than a height.
///
/// <para><b>What a stroke <em>is</em> is <see cref="ClaimsGround"/>, and it is not the style.</b>
/// <see cref="StrokeStyle"/> shapes the band — solid, worn, rough, stones, tapered — which is a brush, and a
/// brush says nothing about whether the paving is a thing on the board or a finish on the ground. A gravel
/// tongue over a crag and a road between two spawns can be the same brush at the same radius. Only a stroke
/// that claims its cells holds them against what is placed after it, and that claim is what a tree's and a
/// boulder's standoff is measured to, so paint is the default and the claim is declared: the standoff exists
/// to stop a canopy closing over a road, and asking it of every painted patch leaves a board with nothing
/// plantable on it.</para>
/// </summary>
public sealed record StrokeProp : PlacedProp
{
    /// <summary>a stroke is paved over the ground water left, and everything after it seats on the paving or clear of it (<see cref="PlacementOrder"/>).</summary>
    public override int PlacementOrder => 1;

    /// <summary>The drawn centerline, as <c>[x, z]</c> pairs. Two points or more.</summary>
    public IReadOnlyList<double[]> Points { get; init; } = [];

    /// <summary>How far either side of the centerline is covered, in blocks — half the band.</summary>
    public double Radius { get; init; } = 3;

    /// <summary>The brush: what shape the band takes along the line. Independent of <see cref="ClaimsGround"/>,
    /// since a road and a smear of dirt can be drawn with the same one.</summary>
    public StrokeStyle Style { get; init; } = StrokeStyle.Solid;

    /// <summary>Whether the stroke holds the ground it paves against everything placed after it. A claiming
    /// stroke keeps its cells, so a tree and a boulder stay their stated distance off and a building may end
    /// one but not stand across it; paint claims nothing and is planted over freely. It is not a claim that
    /// players walk here — a protected verge, a crop bed's margin and a road are the same declaration.</summary>
    public bool ClaimsGround { get; init; }

    /// <summary>0–1; what a <see cref="StrokeStyle.Worn"/> stroke keeps. Every other style covers its whole band.</summary>
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
    /// <summary>water is carved first: it is the one prop that changes the ground, and everything after it seats on what it leaves (<see cref="PlacementOrder"/>).</summary>
    public override int PlacementOrder => 0;

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
/// One tree, standing where it was placed. What it is made of is a <see cref="TreeStyle"/> named once in the
/// document's registry: a board carries hundreds of trees over a few dozen recipes, so the recipe is referenced
/// rather than restated, and changing one changes every tree that wears it.
/// </summary>
public sealed record TreeProp : PlacedProp
{
    /// <summary>a tree seats after the rock and before the cover (<see cref="PlacementOrder"/>).</summary>
    public override int PlacementOrder => 4;

    public int X { get; init; }
    public int Z { get; init; }

    /// <summary>The recipe's key in <see cref="DressingDoc.Styles"/>. What the document carries.</summary>
    [JsonPropertyName("style")] public string StyleKey { get; init; } = "";

    /// <summary>The recipe itself, resolved out of the registry when the document was read — never written,
    /// because the document states it once under its key.</summary>
    [JsonIgnore] public TreeStyle Style { get; init; } = new();

    /// <summary>A trunk stands off the road by three blocks (the author's ruling): nearer and the canopy closes
    /// over the route, which stops reading as a road through trees and starts reading as trees in the
    /// road.</summary>
    public override int RouteStandoff => 3;
}

public sealed record BoulderProp : PlacedProp
{
    /// <summary>a boulder is placed before the trees, so a wood grows around the rock rather than the rock landing in the wood (<see cref="PlacementOrder"/>).</summary>
    public override int PlacementOrder => 3;

    public int X { get; init; }
    public int Z { get; init; }

    /// <summary>The recipe's key in <see cref="DressingDoc.Styles"/>.</summary>
    [JsonPropertyName("style")] public string StyleKey { get; init; } = "";

    /// <summary>The recipe itself, resolved out of the registry when the document was read.</summary>
    [JsonIgnore] public BoulderStyle Style { get; init; } = new();

    /// <summary>A rock keeps two blocks off the road (the author's ruling) — less than a tree because a rock
    /// carries no canopy, more than zero because a boulder against the kerb narrows the route it was placed
    /// beside.</summary>
    public override int RouteStandoff => 2;
}

public sealed record FloraProp : PlacedProp
{
    /// <summary>cover goes last, into whatever ground is left (<see cref="PlacementOrder"/>).</summary>
    public override int PlacementOrder => 5;

    /// <summary>The drawn outline, as <c>[x, z]</c> pairs. Three points or more.</summary>
    public IReadOnlyList<double[]> Points { get; init; } = [];

    public FloraSpec Spec { get; init; } = new();
}

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
    /// <remarks>State each wing as two opposite corners, and at least <c>RoomFrames.MinFootprintSpan</c> blocks each way — the same least span a room's footprint takes, since a room's is the single-wing case of a building's. Anything thinner has no inside once its two walls are written.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Structure)]
    public const string WingShape = "HP2";

    /// <summary>The wings cover more ground than a placed building may take.</summary>
    /// <remarks>Shrink the wings, or split the building into two placements. The cap is what one placed building may take.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Structure)]
    public const string PastCap = "HP3";
}

/// <summary>
/// A building standing on the terrain: one or more touching rectangles an author dragged, and the shell to
/// raise on them.
///
/// <para><b>A room's building is the single-wing case of this one.</b> Both are a footprint and a shell; both
/// reach <see cref="HouseStamper"/> through one <see cref="Houses.BuildingPlan"/>, whose single-rectangle
/// constructor is what a room uses; and both are held to one least span
/// (<see cref="RoomFrames.MinFootprintSpan"/>). What a room adds is what being played through asks for — a
/// pad, monuments, chests and an entry contract — and when it is stamped: before the painter rather than
/// after it with the rest of the dressing. A prop is that same building with none of those, standing where
/// someone drew it.</para>
///
/// <para><b>A wing is stored as its two opposite corners</b> rather than as an origin and a size, so the fan
/// mirrors it as the <em>shape</em> it is — the rule an area prop's outline already follows. A rectangle turned
/// through ninety degrees is a rectangle whose width and depth have swapped, and taking two corners round the
/// orbit says that without the stamp having to know it happened. <see cref="HouseProp.Wings"/> is a list of these: one
/// entry is a plain rectangle, and more than one is an L, a T or a U —
/// still one house under one style, the shape <see cref="HouseStamper"/> already takes as a
/// <see cref="Houses.BuildingPlan"/> of touching <see cref="Wing"/> rectangles.</para>
/// </summary>
public sealed record HouseProp : PlacedProp
{
    /// <summary>a building goes down after the roads it fronts and before the props that must stand clear of it (<see cref="PlacementOrder"/>).</summary>
    public override int PlacementOrder => 2;

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

    /// <summary>The shell's key in <see cref="DressingDoc.Styles"/>. What the document carries.</summary>
    [JsonPropertyName("style")] public string StyleKey { get; init; } = "";

    /// <summary>The shell itself, resolved out of the registry when the document was read. A registry entry
    /// rather than a snapshot on every building: a board raises far more buildings than it has shells, and one
    /// entry is what an author edits to change all of them. The registry is still the document's, so editing
    /// the library row a shell was pulled from never rebuilds a shipped map's scenery.</summary>
    [JsonIgnore] public HouseStyle Style { get; init; } = new();

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
    /// <para>The cap is the <b>prop's</b>, and is the one thing about a building that still differs by where
    /// its footprint came from: a room's is bounded by the region it stands on (<c>WX12</c>) and capped at
    /// 20×20 by <c>ST9</c>, which is a map's own geometry rather than a dressing limit. The least span is one
    /// number for both (<see cref="RoomFrames.MinFootprintSpan"/>); the ceiling is two, and whether it should
    /// be is the author's.</para></summary>
    public const int MaxFootprint = 192;

    /// <summary>The plan this prop stamps, or null when it is no building at all — the same answer
    /// <see cref="Check"/> gives the reasons for.
    ///
    /// <para>Every wing is required to hold two walls and an inside on its own, the same least span any
    /// footprint takes (<see cref="RoomFrames.MinFootprintSpan"/>): a wing composes with its neighbours below
    /// the eave, but nothing composes a room out of a sliver with no width of its own.</para></summary>
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
            if (maxX - minX + 1 < RoomFrames.MinFootprintSpan || maxZ - minZ + 1 < RoomFrames.MinFootprintSpan)
                return Findings.Of(new Finding(HousePropRules.WingShape,
                    $"a wing holds two walls and an inside, so it is at least "
                    + $"{RoomFrames.MinFootprintSpan} blocks each way — the least span any building footprint "
                    + $"may be; this one is {maxX - minX + 1} × {maxZ - minZ + 1}",
                    Field: "wings", Subjects: [wing]));
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
