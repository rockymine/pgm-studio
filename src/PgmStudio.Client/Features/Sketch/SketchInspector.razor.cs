using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using PgmStudio.Client.Components;
using PgmStudio.Vocabulary;
using HeightModeWords = PgmStudio.Vocabulary.HeightModes;

namespace PgmStudio.Client.Features.Sketch;

public partial class SketchInspector
{
    /// <summary>The bridge. Most of this inspector reports edits up through callbacks the host owns, which is
    /// right for anything the host also has to persist or re-render around; the height mode goes straight to
    /// the canvas because it changes only what the rasterizer emits, and routing it through the host would be
    /// a callback that does nothing but forward.</summary>
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter] public SketchShapeRow? Shape { get; set; }
    [Parameter] public SketchGroupRow? Group { get; set; }
    [Parameter] public IReadOnlyList<SketchShapeRow> Shapes { get; set; } = [];
    [Parameter] public EventCallback<string> OnToggleOp { get; set; }
    [Parameter] public EventCallback<string> OnToggleOverride { get; set; }
    [Parameter] public EventCallback<string> OnDeleteShape { get; set; }
    [Parameter] public EventCallback<string> OnPromoteShape { get; set; }
    [Parameter] public EventCallback<(string Id, double Base, double Floor)> OnSetHeight { get; set; }
    [Parameter] public int SelectedVertexIdx { get; set; } = -1;
    [Parameter] public double SelectedVertexHeight { get; set; }
    [Parameter] public EventCallback<(string Id, int Idx, double Height)> OnSetVertexHeight { get; set; }
    [Parameter] public IReadOnlyList<SketchSlopeControl> SlopeControls { get; set; } = [];
    [Parameter] public EventCallback<(int Idx, double Height)> OnSlopeHeightChanged { get; set; }
    [Parameter] public EventCallback OnApplySlope { get; set; }
    [Parameter] public EventCallback<string> OnToggleMirrors { get; set; }
    [Parameter] public EventCallback<(string Id, string Name)> OnRenameGroup { get; set; }
    [Parameter] public EventCallback<double> OnRotate { get; set; }
    [Parameter] public EventCallback<(string Id, double Radius, string Edge, int Seed)> OnSetStrokeBand { get; set; }

    // "Rotate by" is a rotate-by box (rotation bakes into geometry, so there is no absolute angle to hold): its
    // value is always blank, so NumberField snaps back to blank after each entry and the same angle applies again.
    private Task RotateChanged(double degrees) => degrees != 0 ? OnRotate.InvokeAsync(degrees) : Task.CompletedTask;

    // How a path's two long sides are drawn. Its blocks — gravel, a cell fabric, any pattern at all — are a
    // palette assigned to the shape, so what is offered here is only the shape of the band.
    private static readonly SelectOption[] StrokeEdges =
    [
        new("solid",   "Solid",   "One width the whole way."),
        new("rough",   "Rough",   "The width varies by up to 45% on each side."),
        new("tapered", "Tapered", "Wide at its centre, narrow at the ends."),
    ];

    // The author sets a width; the shape stores the half-width the band is offset by, so the two edges are
    // always that far from the line the author is dragging.
    private Task WidthChanged(double width)
        => Shape is null ? Task.CompletedTask : OnSetStrokeBand.InvokeAsync((Shape.Id, width / 2, Shape.StrokeEdge, Shape.StrokeSeed));

    private Task EdgeChanged(string edge)
        => Shape is null ? Task.CompletedTask
            : OnSetStrokeBand.InvokeAsync((Shape.Id, Shape.Radius, edge.Length == 0 ? "solid" : edge, Shape.StrokeSeed));

    private Task SeedChanged(double seed)
        => Shape is null ? Task.CompletedTask
            : OnSetStrokeBand.InvokeAsync((Shape.Id, Shape.Radius, Shape.StrokeEdge, (int)seed));

    // NumberField clamps to its Min (height >= 1, floor >= 0) and snaps the display back, so these just
    // forward the already-valid value to the bridge.
    private Task HeightChanged(double v)
        => Shape is null ? Task.CompletedTask : OnSetHeight.InvokeAsync((Shape.Id, v, Shape.Floor));

    /// <summary>A level shape is edited by the y its flat top lands at; the document stores that as a
    /// height over the shape's floor.</summary>
    private Task TopChanged(double top)
        => Shape is null ? Task.CompletedTask : OnSetHeight.InvokeAsync((Shape.Id, top - Shape.Floor, Shape.Floor));

    private bool IsLevel => Shape?.HeightMode == HeightModeWords.Level && !Shape.IsSolid;

    /// <summary>Whether the shape's height is the <c>base_height</c> the Height row edits: ground and the
    /// solids that are as tall as they are stated (box, wedge, cylinder). A sphere and a torus are as tall as
    /// their radii make them and an arch is its deck.</summary>
    private bool HasHeight => Shape is not null && (!Shape.IsSolid || Shape.Type is ShapeKinds.Box or ShapeKinds.Wedge or ShapeKinds.Cylinder);

    private static readonly SelectOption[] RectangleForms =
    [
        new("",                  "Ground",   "Ground, shaped by the modifiers."),
        new(ShapeKinds.Box,      "Box",      "A cuboid over the footprint, turned and tilted."),
        new(ShapeKinds.Wedge,    "Wedge",    "A box cut away to nothing at the far end."),
        new(ShapeKinds.Cylinder, "Cylinder", "An upright cylinder in the footprint."),
        new(ShapeKinds.Sphere,   "Sphere",   "A sphere standing on its floor."),
        new(ShapeKinds.Torus,    "Torus",    "A ring, which a tilt stands on its edge."),
    ];

    private static readonly SelectOption[] PathForms =
    [
        new("",              "Ground", "Ground, shaped by the modifiers."),
        new(ShapeKinds.Arch, "Arch",   "A walkable deck arching over a gap along the line."),
    ];

    /// <summary>The forms the selected shape can be read as: a rectangle or a circle any of the five solids, a
    /// path an arch, an outline of points none.</summary>
    private SelectOption[] Forms => Shape?.Footprint switch
    {
        ShapeKinds.Rectangle or ShapeKinds.Circle => RectangleForms,
        ShapeKinds.Polyline => PathForms,
        _ => [],
    };

    private Task FormChanged(string kind)
        => Shape is null || Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("setSolid", Shape.Id, kind).AsTask();

    private Task SolidChanged(string field, double value)
        => Shape is null || Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("setSolidField", Shape.Id, field, value).AsTask();

    private Task CrownChanged(double crown)
        => Shape is null || Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("setArchDeck", Shape.Id, crown, Shape.DeckEnd).AsTask();

    private Task FeetChanged(double feet)
        => Shape is null || Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("setArchDeck", Shape.Id, Shape.DeckCrown, feet).AsTask();

    /// <summary>What the height number is measured from under the shape's height mode: the floor for ground,
    /// the median ground under it for raise and sink, the ground at each cell for drape.</summary>
    private string HeightLabel => Shape?.HeightMode switch
    {
        HeightModeWords.Raise => "Height above ground",
        HeightModeWords.Sink => "Depth below ground",
        HeightModeWords.Drape => "Height above ground",
        _ => "Height above floor",
    };

    /// <summary>Where the height is measured from, where that is not the floor the row above states.</summary>
    private string? HeightNote => Shape?.HeightMode switch
    {
        HeightModeWords.Raise or HeightModeWords.Sink => "Measured from the median ground height under it.",
        HeightModeWords.Drape => "Measured from the ground at every cell.",
        _ => null,
    };

    private Task FloorChanged(double v)
        => Shape is null ? Task.CompletedTask : OnSetHeight.InvokeAsync((Shape.Id, Shape.BaseHeight, v));

    /// <summary>How a shape's top is decided against the ground it stands on (docs/tools/sketch.md). The words are
    /// the document's own. Ground is first and is the default: a shape is part of the landmass unless its author
    /// says otherwise. A shape with a mode stands at its own statement, and the modifiers leave it out.</summary>
    private static readonly SelectOption[] HeightModes =
    [
        new("",      "Ground", "Ground, shaped by the modifiers."),
        new("level", "Level",  "A flat top at one height, whatever the ground does."),
        new("raise", "Raise",  "Stands this far above the ground it covers."),
        new("sink",  "Sink",   "Sits this far below the ground it covers."),
        new("drape", "Drape",  "This far above the ground at every cell, for walls and hedges."),
    ];

    /// <summary>The y the shape's top lands at, worked out from its floor and its height. Empty where the Top row
    /// states it, where the top is measured from the ground under the shape, and on a solid as tall as its radii.</summary>
    private string TopReadout => Shape is null || IsLevel || !HasHeight || !string.IsNullOrEmpty(Shape.HeightMode)
        ? ""
        : $"Top at y {Shape.Floor + Shape.BaseHeight}.";

    /// <summary>What the Surface slope section asks of the canvas, by how many points are marked.</summary>
    private string SlopeDescription => SlopeControls.Count >= 2
        ? "Shift-click a marked point to unmark it."
        : SelectedVertexIdx >= 0
            ? "Shift-click two or three points to tilt the surface. Press Delete to take this point out."
            : "Click a point to set its height, or shift-click two or three points to tilt the surface.";

    private Task ReliefScopeChanged(string word)
        => Shape is null || Handle is null
            ? Task.CompletedTask
            : Handle.InvokeVoidAsync("setReliefScope", Shape.Id, word).AsTask();

    private Task SkirtChanged(double value)
        => Shape is null || Handle is null
            ? Task.CompletedTask
            : Handle.InvokeVoidAsync("setSkirt", Shape.Id, Math.Max(0, (int)Math.Round(value))).AsTask();

    private Task HeightModeChanged(string word)
        => Shape is null || Handle is null
            ? Task.CompletedTask
            : Handle.InvokeVoidAsync("setHeightMode", Shape.Id, word).AsTask();

    private Task VertexHeightChanged(double v)
        => Shape is null || SelectedVertexIdx < 0 ? Task.CompletedTask
            : OnSetVertexHeight.InvokeAsync((Shape.Id, SelectedVertexIdx, v));

    private Task RenameChanged(ChangeEventArgs e)
        => Group is null ? Task.CompletedTask
                          : OnRenameGroup.InvokeAsync((Group.Id, e.Value?.ToString()?.Trim() is { Length: > 0 } n ? n : Group.Name));

    private IEnumerable<SketchShapeRow> GroupShapes()
        => Group is null ? []
                          : Group.ShapeIds.Select(id => Shapes.FirstOrDefault(s => s.Id == id)).OfType<SketchShapeRow>();
}
