using PgmStudio.Vocabulary;
namespace PgmStudio.Pgm.Sketch;

/// <summary>The sketch document's own rule ids — what a layout is refused for, and what it is told about a
/// layout that builds but does not build what it says. Served by <c>GET /api/rules</c> from the docstrings
/// here, the way every gate family's are.</summary>
public static class SketchRules
{
    /// <summary>A recompiled layout has no group for the terraform stored on a group.</summary>
    /// <remarks>Either send the request again with <c>force</c> set to <c>true</c>, or change the <c>rect</c> of a
    /// piece in <c>pieces</c> until the same group survives the compile.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string ReliefOrphaned = "SK1";

    /// <summary>The extent of a layout across its symmetry copies is more than the studio builds, counted in
    /// columns.</summary>
    /// <remarks>Either delete a shape from <c>shapes</c>, or move the shape closer to the symmetry centre until the
    /// layout covers fewer columns.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Studio)]
    public const string BoardTooLarge = "SK2";

    /// <summary>The extent, in columns, that <see cref="BoardTooLarge"/> refuses past.
    /// <para><b>Deliberately unpublished.</b> It appears in no message, no rule sentence and no tool
    /// document: a stated ceiling is a target, and an agent told it may draw up to this will draw up to this.
    /// What a refusal says instead is the span it measured, which is the half the author has to act on.
    /// <c>SketchLayoutCheckTests</c> holds the message to that rather than the other way round.</para></summary>
    public const int MaxBoardColumns = 4_000_000;

    /// <summary>A layout names a shape kind, mirror mode, landform, palette, shape or group that does not
    /// exist.</summary>
    /// <remarks>Either change the field the finding names to a name that exists, or add the palette to
    /// <c>themes</c>.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Plan, RuleConcern.Terrain, RuleConcern.Theme)]
    public const string NamesNothing = "SK3";

    /// <summary>A polygon or lasso has fewer than 3 points, a polyline fewer than 2 points, or a circle or polyline
    /// has a radius of 0 blocks or less.</summary>
    /// <remarks>Either set the <c>radius</c> of the shape to more than 0, or add points to <c>vertices</c> until a
    /// polygon has at least 3 and a polyline at least 2, or delete the shape from <c>shapes</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string DrawsNothing = "SK4";

    /// <summary>The floor of a shape is less than 0 blocks, or its top is more than 255 blocks, or its thickness is
    /// less than 0.</summary>
    /// <remarks>Change the <c>floor</c> and the <c>base_height</c> of the shape until the floor is at least 0, the
    /// thickness is at least 0 and the top is at most 255 blocks.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Terrain, RuleConcern.World)]
    public const string UnbuildableHeight = "SK5";

    /// <summary>A map has no stored sketch.</summary>
    /// <remarks>Either send a sketch for the map, or send a plan for the map to compile, then send the request
    /// again.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request, RuleConcern.Plan)]
    public const string NothingStored = "SK6";

    /// <summary>A stored sketch has no shape that draws ground.</summary>
    /// <remarks>Either add a shape to <c>shapes</c> that encloses ground, or change a shape the finding names until
    /// it encloses ground.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string NothingDrawn = "SK7";

    /// <summary>A sketch has no palettes, no terraform and no props.</summary>
    /// <remarks>Either add a palette to <c>themes</c>, or add an entry for a group to <c>relief</c>, or add a prop
    /// to <c>dressing.props</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain, RuleConcern.World)]
    public const string NoFinish = "SK8";

    /// <summary>A shape overlaps another shape on one layer, and its floor is at or above the top of the
    /// other.</summary>
    /// <remarks>Either move the upper shape to a layer of its own in <c>layers</c>, or delete the shape below it
    /// from <c>shapes</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string StackedInOneLayer = "SK9";

    /// <summary>Two layers share more than 1 course in the same column.</summary>
    /// <remarks>Either set the <c>base_y</c> of the upper layer to the top of the layer below it, or change the
    /// <c>base_height</c> of a shape on the layer below until the layers share at most 1 course.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string LayersOverlap = "SK10";

    /// <summary>A layout has more than 1 group with the same id.</summary>
    /// <remarks>Change the <c>id</c> of each duplicate group in <c>groups</c> to one that no other group
    /// has.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string GroupIdTwice = "SK12";

    /// <summary>The band of a polyline overlaps itself.</summary>
    /// <remarks>Either split the polyline at the turn into two polylines in <c>shapes</c>, or change the
    /// <c>vertices</c> of the polyline until the turn is wider than its band.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string StrokeLapsItself = "SK25";

    /// <summary>The first column past an end of a flight stands 2 or more blocks above or below its last tread
    /// where they share an edge.</summary>
    /// <remarks>Add a shape to <c>shapes</c> that forms a landing at the end of the flight, within 1 block of its
    /// last tread.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Terrain)]
    public const string FlightEndsAtADrop = "SK26";

    /// <summary>An island of 16 or more places has open sky over it, stands over other ground and has no route of
    /// steps of at most 2 blocks to the largest island.</summary>
    /// <remarks>Add a shape to <c>shapes</c> that joins the island to the largest island in steps of at most 2
    /// blocks.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain, RuleConcern.World)]
    public const string MassUnreached = "SK11";

    /// <summary>An override add states a top, and its group states terraform that sets the top of the same
    /// columns.</summary>
    /// <remarks>Either set the <c>relief_scope</c> of the shape to <c>hold</c> or <c>exclude</c>, or delete the
    /// group from <c>relief</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string ReliefOverStatedTop = "SK14";

    /// <summary>An override add overlaps a taller override add that covers more columns and states another
    /// palette.</summary>
    /// <remarks>Either set the <c>theme</c> of the smaller shape to the <c>theme</c> of the taller shape, or change
    /// the <c>base_height</c> of the smaller shape until its top is at least the top of the taller shape.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain, RuleConcern.Theme)]
    public const string ThemeHiddenUnderAnother = "SK15";

    /// <summary>An add overlaps a subtract in the courses both cover, where the add is drawn after the subtract or
    /// on another layer.</summary>
    /// <remarks>Either change the <c>floor</c> of the shape that fills the hole until it is at least the top of the
    /// subtract, or change the subtract until it no longer overlaps the shape.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string DrawnOverSubtraction = "SK13";

    /// <summary>A made thing that seats on the ground has no ground under any of its columns.</summary>
    /// <remarks>Either add a shape to <c>shapes</c> that holds ground under the made thing, or set the <c>seat</c>
    /// of its layer to <c>null</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string SeatedOnNothing = "SK16";

    /// <summary>A shape on a layer with groups has no group that lists it, in a layout with symmetry.</summary>
    /// <remarks>Either add the shape to the <c>shapeIds</c> of the group whose ground it is part of, or delete the
    /// shape from <c>shapes</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string ShapeInNoGroup = "SK17";

    /// <summary>A made thing overlaps a structure or a building in at least 1 shared course.</summary>
    /// <remarks>Either change the <c>base_y</c> of the layer of the made thing until it is at least the top of the
    /// structure, or move the marker in <c>placements</c> or the prop in <c>dressing.props</c> it stands
    /// on.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Structure, RuleConcern.Feature)]
    public const string MadeThingInBuilt = "SK18";

    /// <summary>A prop names a recipe that the sketch does not have.</summary>
    /// <remarks>Either add the recipe to <c>dressing.styles</c> under the name the prop gives, or change the
    /// <c>style</c> of the prop to a name in <c>dressing.styles</c>.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Feature)]
    public const string RecipeNotStated = "SK19";

    /// <summary>The height a layer's ground starts at is less than that of the layer listed before it.</summary>
    /// <remarks>Either move the layer before the layer it starts below in <c>layers</c>, or change the
    /// <c>base_y</c> of the layer until it is at least the <c>base_y</c> of the layer before it.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string StackOutOfOrder = "SK20";

    /// <summary>A point cut by a bend has no room on the side the bend asks for.</summary>
    /// <remarks>Either set the <c>wander</c> of the bend to at most the width of the narrowest neck or notch, or
    /// set the <c>step</c> of the bend to more than half the length of the edge.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string BendHeldBack = "SK21";

    /// <summary>The number of anchor heights of a shape is not the number of its points, or a rectangle or circle
    /// states any.</summary>
    /// <remarks>Either set the <c>anchor_heights</c> of the shape to <c>null</c>, or change the shape to a polygon
    /// with as many <c>vertices</c> as it has <c>anchor_heights</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain, RuleConcern.World)]
    public const string PerVertexHeightUnread = "SK22";

    /// <summary>A shape with a palette has no column that has ground on all 8 sides, on its layer.</summary>
    /// <remarks>Either set the <c>theme</c> of the shape to <c>null</c> and its <c>material</c> to a block, or set
    /// the <c>rim.enabled</c> of the palette in <c>themes</c> to <c>false</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain, RuleConcern.World)]
    public const string ThemeShowsOnlyItsEdge = "SK23";

    /// <summary>A shape states a palette, and it also states a material.</summary>
    /// <remarks>Either set the <c>theme</c> of the shape to <c>null</c>, or set the <c>material</c> of the shape to
    /// <c>null</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string PaintStatedTwice = "SK24";

    /// <summary>A plan island that stands at several surfaces has more than 1 palette or material across its
    /// shapes.</summary>
    /// <remarks>Either set the <c>theme</c> of each shape of the island to one palette, or split the island with a
    /// notch in the <c>rect</c> of a piece in <c>pieces</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain, RuleConcern.Theme)]
    public const string PlateausPaintedApart = "SK27";

    /// <summary>A group that the symmetry does not copy touches none of its symmetry copies.</summary>
    /// <remarks>Either set the <c>mirrors</c> of the group to <c>true</c>, or move the group closer to the symmetry
    /// centre until it touches its symmetry copies.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Terrain)]
    public const string BuiltOnOneImage = "SK28";

    /// <summary>A shape drawn in the sketch has no counterpart in the plan the rebuild compiles.</summary>
    /// <remarks>Either add a piece for the shape to <c>pieces</c> in the plan, or add the shape to <c>shapes</c>
    /// again after the rebuild.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Plan, RuleConcern.Terrain)]
    public const string ShapeDropped = "SK29";
}
