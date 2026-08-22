using PgmStudio.Vocabulary;
namespace PgmStudio.Pgm.Sketch;

/// <summary>The sketch document's own rule ids — what a layout is refused for, and what it is told about a
/// layout that builds but does not build what it says. Served by <c>GET /api/rules</c> from the docstrings
/// here, the way every gate family's are.</summary>
public static class SketchRules
{
    /// <summary>A recompile fused the board differently, so an island the author had drawn relief onto no
    /// longer exists to carry it.</summary>
    /// <remarks>Island identity is derived from the geometry, so a recompile that re-fuses the board produces
    /// a different island rather than moving the old one — the relief has nowhere correct to land. Retry with
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
    /// nobody has, an island listing a shape id the layout does not carry, a relief keyed to an island that
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
}
