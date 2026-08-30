using PgmStudio.Vocabulary;
namespace PgmStudio.Pgm.Sketch;

/// <summary>The sketch document's own rule ids — what a layout is refused for, and what it is told about a
/// layout that builds but does not build what it says. Served by <c>GET /api/rules</c> from the docstrings
/// here, the way every gate family's are.</summary>
public static class SketchRules
{
    /// <summary>A recompile fused the board differently, so a group the author had drawn relief onto no
    /// longer exists to carry it.</summary>
    /// <remarks>Group identity is derived from the geometry, so a recompile that re-fuses the board produces
    /// a different group rather than moving the old one — the relief has nowhere correct to land. Retry with
    /// <c>?force=true</c> to accept the loss and proceed, or redraw the plan so the same landmass survives the
    /// compile.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string ReliefOrphaned = "SK1";

    /// <summary>The board spans more ground than the studio will realize: the extent every shape and its
    /// symmetry images cover, measured in columns, is past what a build can walk. Refused before anything is
    /// built, because the cost is paid per column of the extent whether or not ground is drawn there — a
    /// board that large does not fail, it takes the machine with it.</summary>
    /// <remarks>Draw the board smaller, or move the shapes toward the symmetry centre — the extent is measured across every orbit image, so a shape far out on one side widens the board by twice its distance. The finding carries the span that was measured; a normal board is a few hundred columns a side, which is nowhere near this.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Studio)]
    public const string BoardTooLarge = "SK2";

    /// <summary>The extent, in columns, that <see cref="BoardTooLarge"/> refuses past.
    /// <para><b>Deliberately unpublished.</b> It appears in no message, no rule sentence and no tool
    /// document: a stated ceiling is a target, and an agent told it may draw up to this will draw up to this.
    /// What a refusal says instead is the span it measured, which is the half the author has to act on.
    /// <c>SketchLayoutCheckTests</c> holds the message to that rather than the other way round.</para></summary>
    public const int MaxBoardColumns = 4_000_000;

    /// <summary>The document names something that is not there — a shape kind nobody has, a mirror mode
    /// nobody has, a group listing a shape id the layout does not carry, a relief keyed to a group that
    /// does not exist. A complaint rather than a refusal: the board still builds, and it builds without
    /// whatever the name was for, which is the thing worth saying out loud.</summary>
    /// <remarks>Correct the name the finding's <c>field</c> points at. A shape kind is one of rectangle, circle, polygon, lasso or path; a mirror mode is one of none, mirror_x, mirror_z, mirror_d1, mirror_d2, rot_90 or rot_180 — an unknown one leaves the board unmirrored rather than refusing, so half the map quietly goes missing.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Plan, RuleConcern.Terrain, RuleConcern.Theme)]
    public const string NamesNothing = "SK3";

    /// <summary>A shape draws no ground: a polygon or lasso with fewer than three vertices, a circle or path
    /// of no width, a rectangle with no area. The shape is in the document and contributes nothing to the
    /// board, which reads exactly like a shape that was never drawn.</summary>
    /// <remarks>Give the shape a size, or delete it. A polygon needs three vertices before it encloses anything, and a circle or a path needs a radius above zero.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string DrawsNothing = "SK4";

    /// <summary>A shape states a column the world cannot hold: a floor below bedrock, a top above the world
    /// roof, or a thickness that is negative. The build clamps it into the world rather than refusing, so
    /// what stands is not what the document asked for.</summary>
    /// <remarks>Bring the shape's floor and base_height inside 0..255 — a Minecraft world is 256 blocks tall, and a column stated past either end is silently cut to fit.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Terrain, RuleConcern.World)]
    public const string UnbuildableHeight = "SK5";

    /// <summary>Nothing has been stored to finish. The map is at the sketch stage and no layout has been
    /// written for it, so there is no document to rasterize into world geometry. 422.</summary>
    /// <remarks>Draw the board and store it (<c>PUT /api/map/{slug}/sketch</c>, or the Sketch tool's save) before finishing. A map originated from a plan is written by <c>PUT …/sketch/from-plan</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request, RuleConcern.Plan)]
    public const string NothingStored = "SK6";

    /// <summary>The stored layout rasterizes to no ground at all. Every shape in it draws nothing — an empty
    /// document, or one whose shapes are all of the kinds <c>SK3</c> and <c>SK4</c> report — so finishing it
    /// would write a world with no land in it. 422, and the one place the sketch's complaints become fatal:
    /// finishing is what declares the drawing done.</summary>
    /// <remarks>Draw at least one shape that encloses ground. Where shapes are present, the <c>warnings</c> on this same response name the ones that drew nothing and why.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string NothingDrawn = "SK7";

    /// <summary>A board is finished carrying no finish: no theme registry, no relief and no props. Ground
    /// alone is a legitimate board — a test piece, a shape being tried — so this is a <b>complaint</b> and
    /// never a refusal. It exists because it is the one silence this stage kept: <c>SK3</c> names a shape
    /// citing a theme the layout does not carry, <c>SK4</c> a shape drawing nothing and <c>SK7</c> a layout
    /// rasterizing to no ground, and all three need something stated to disagree with. A board stating none
    /// of it slips between them and exports a world of raw stone with every stage answering 200.</summary>
    /// <remarks>Give the board a theme registry, a relief, or props — whichever it was meant to have. The finding names which of the three are absent, and a board that is deliberately bare may ignore it.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain, RuleConcern.World)]
    public const string NoFinish = "SK8";

    /// <summary>Two shapes on one layer stack over the same ground, and the lower one is not in the world. A
    /// layer is a slab: it holds one span per column, and a taller add replaces a shorter one outright, floor
    /// included. So a floor with a roof drawn over it on the same layer builds as the roof alone, over open
    /// air, and reads as a board the author never drew.</summary>
    /// <remarks>Move the upper shape to its own layer. A stack is a stack of layers — `base_y` is what puts one span above another, and two spans on one layer cannot both survive. Drawing the walls around the lower shape instead of over it is the other way, and is how a roofed gallery is built.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string StackedInOneLayer = "SK9";

    /// <summary>Two layers are driven into each other by more than the one course a stack shares at its
    /// seam. A layer is a slab and the stack is what puts air between two of them, so where their spans meet
    /// they build as one solid mass: the gap the layers were drawn to have is not in the world there, and
    /// nothing under the upper slab can be stood in.</summary>
    /// <remarks>Raise the upper layer's `base_y`, or lower the height of what stands on the layer below. A layer's span is inclusive of its top, so an upper layer sitting exactly at the lower one's top shares that one course and is the ordinary seam — this fires only past it. The world is built either way.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string LayersOverlap = "SK10";

    /// <summary>Two groups of one layer answering to the same id. A group id is the key a relief is
    /// stored under and the handle a placement names, so a board carrying it twice has no single answer to
    /// either: the terrain authored under that name lands on whichever of them is solved first and the rest
    /// of the ground builds flat.</summary>
    /// <remarks>Give each group its own id. A recompile mints one per group, so a duplicate is a document
    /// that was written by hand or by a tool that copied a record onto more than one group.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string GroupIdTwice = "SK12";

    /// <summary>A mass of standable ground under open sky that no route reaches from the rest of the board.
    /// Ground under a roof is a room and says nothing; ground with sky over it and no way onto it is either a
    /// second landmass the author meant or an upper level whose stair was never drawn, and only the author knows
    /// which.</summary>
    /// <remarks>Draw the way onto it — a ramp, a shaft, a shape bridging the gap — or leave it if a detached landmass is what the board is. The map builds either way.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain, RuleConcern.World)]
    public const string MassUnreached = "SK11";

    /// <summary>An override add states a top its group's relief will solve straight through. An override add
    /// says the column is its own, floor and all — it is what a wall, a flight of stairs, a crop bed or a
    /// stepped mound is drawn as — and a relief replaces the top of every column of its group. Only a shape
    /// naming a <c>height_mode</c> stands out of that field, and only a <c>relief_scope</c> keeps its ground
    /// out of the solve, so a made thing carrying neither is built to whatever the relief says and the
    /// author's number is nowhere in the world. Nothing else catches it: the board still builds, every gate
    /// still passes, and a twenty-seven-course wall comes out level with the ground beside it.</summary>
    /// <remarks>Give the shape `"height_mode": "level"` and `"skirt": 0` — level holds it at the absolute top its own floor and height state, and a zero skirt is a sheer face, which is right for a built thing and wrong for a landform. `"relief_scope": "exclude"` is the stronger form, keeping the shape's ground out of the solve entirely. A shape meant to be shaped by the relief wants neither: state no `base_height`, `floor` or `anchor_heights` on it and it is a footprint carrying a theme, which this rule does not read.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string ReliefOverStatedTop = "SK14";

    /// <summary>One shape builds a column and another paints it. Two override adds over one column is not a
    /// fault — the taller wins it, which is what "the tallest add is the height" means — but a theme is scoped
    /// by <b>area</b> and not by height, so where the smaller of the two is also the shorter, the world holds
    /// the taller shape's ground in the smaller one's material. A mound's outer ring crossing a town wall
    /// leaves the wall standing to its own courses and finished in grass over dirt, sides included. It is
    /// visible only in a column read or in the world. A shape in a mirroring group is judged at every image
    /// of its orbit: what a patch contests is as often another patch's reflection as the patch itself.</summary>
    /// <remarks>Cut the smaller shape out of the taller one's footprint — the two are not meant to share ground, and clipping is what states that. Where the overlap is deliberate, give the two the same theme, or scope the paint with a shape at the same height: two shapes at one height are a theme scoped to a patch, which is what scoping is for and is not this.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain, RuleConcern.Theme)]
    public const string PaintedByAnotherShape = "SK15";

    /// <summary>An add covers ground a subtract takes away. A subtract is how a board states its negative
    /// space — the void a plan's buffer pieces compile to, the hole a composed footprint leaves — and a hole
    /// is never scenery: what a body encircles is ground players go round, and a board's walls are drawn to
    /// guard it. An add that <b>fills</b> it is therefore refused — an override add, or any add on another
    /// layer, since a subtract reaches only the layer it is on. An add that draws <b>nothing</b> there is the
    /// other half: on one layer a subtract beats every plain add whatever order the two are written in, so a
    /// shape the author can see on the canvas is simply not in the world, and that only complains.
    ///
    /// <para>A <b>lid</b> is neither. A layer holds one span per column, so an override add resting above the
    /// subtract's own floor moves that span up and records nothing beneath it — a deck over a cut, with the
    /// void still under it — and only an add standing at or below the subtract's floor puts the negative space
    /// back as ground.</para></summary>
    /// <remarks>The negative space is the board's to state and may be redrawn — round the buffer off, narrow it, move it — but never papered over with an add. Move the add off the subtracted ground, or change the subtract to the shape the void is now meant to be. A bridge over the void is written by raising the add's `floor` above the subtract's: the column's one span moves up and the drop stays open under it. A plain add on the subtract's own layer draws nothing and is the complaint rather than the refusal; the board builds, with that shape absent from it.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string DrawnOverSubtraction = "SK13";

    /// <summary>A made thing asks to be seated on the ground and has none under it. A layer that seats takes
    /// its floors from the lowest solid column of its own footprint, so a thing whose footprint covers no
    /// ground at all has nothing to measure against and stays at the height it was drawn. A complaint: a
    /// sculpture hanging in open sky is a legitimate board — a balloon, a ship in the air, a thing on a spire
    /// — and the word for that is simply to state no seat.</summary>
    /// <remarks>Move the thing over ground, or take the layer's `seat` off so its floors are the absolute heights it states. The finding names the made thing and how many of its columns found nothing beneath them.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string SeatedOnNothing = "SK16";

    /// <summary>A shape no group on its layer lists. A group is the unit the symmetry orbit is fanned by —
    /// the build reads each mirroring group's <c>shapeIds</c> and copies those shapes onto their images — so
    /// a shape no list names is drawn once, on the side it was drawn on, and has no image anywhere. Nothing
    /// else catches it: the shape is in the document, it rasterizes where the author put it, the drawn
    /// mirror outline still covers it because a group's outline is the union of the ground it fused and not
    /// the shapes it lists, and half a landmass is missing only in the world. The same list carries the
    /// group's relief and its keep-clear fan, so an unlisted shape takes neither.</summary>
    /// <remarks>List the shape in the group its ground belongs to. The Sketch tool recomputes group membership on every edit, so opening the layout and moving the shape writes it back in; a document written by hand or by a tool names its groups itself and has to name this shape too. A layer stating no groups at all is not this — the whole of it mirrors — and a role-tagged room piece is never listed, by design.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string ShapeInNoGroup = "SK17";
}
