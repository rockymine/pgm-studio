using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using PgmStudio.Client.Components;
using PgmStudio.Vocabulary;

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

    // "Rotate (°)" field: a relative rotate-by input (rotation bakes into geometry, so there's no absolute
    // angle to hold) — apply the entered degrees about the selection's bbox centre, then clear back to blank.
    // Bumping the @key recreates the input so it resets to "" even when the model value is unchanged (""→""),
    // which also lets you apply the same value repeatedly (a fresh input re-fires change on re-entry).
    private const string rotateInput = "";
    private int rotateNonce = 0;
    private async Task RotateChanged(ChangeEventArgs e)
    {
        rotateNonce++;
        if (double.TryParse(e.Value?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var deg) && deg != 0)
            await OnRotate.InvokeAsync(deg);
    }

    private static string TypeIcon(string t) => t switch
    {
        ShapeKinds.Rectangle => "rectangle-horizontal",
        ShapeKinds.Circle    => "circle",
        ShapeKinds.Polygon   => "pentagon",
        ShapeKinds.Lasso     => "lasso",
        ShapeKinds.Polyline  => "spline",
        _                    => "square",
    };

    // How a path's two long sides are drawn. Its finish — gravel, a cell fabric, any pattern at all — is a
    // theme assigned to the shape, so what is offered here is only the shape of the band.
    private static readonly SelectOption[] StrokeEdges =
    [
        new("solid",   "Solid",   "One width the whole way."),
        new("rough",   "Rough",   "The width wanders up to 45% either side, so the band reads organic."),
        new("tapered", "Tapered", "Fat in the middle, thin at the ends."),
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

    private Task FloorChanged(double v)
        => Shape is null ? Task.CompletedTask : OnSetHeight.InvokeAsync((Shape.Id, Shape.BaseHeight, v));

    /// <summary>How a shape's top is decided once its group carries a relief (docs/world-export/relief.md §7).
    /// The words are the document's own, so what an author picks here and what the layout stores are one
    /// vocabulary. Ground is first and is the default: a shape is part of the landmass unless its author says
    /// otherwise, and a default that made every shape a mesa would turn a drawn board into a staircase of
    /// plates.</summary>
    private static readonly SelectOption[] HeightModes =
    [
        new("",      "Ground", "Part of the landmass — the group's relief is what this shape's ground does."),
        new("level", "Level",  "A mesa: a flat top at an absolute height, whatever the ground under it does, so its faces are cliffs."),
        new("raise", "Raise",  "A monolith: this far above the middle of the ground it covers, so it keeps its prominence wherever it is dragged."),
        new("sink",  "Sink",   "A quarry: this far below the middle of the ground it covers."),
    ];

    /// <summary>Where this shape's top actually lands, in its own numbers. A mode is a rule and a rule has to
    /// be applied before it says anything; the number it works out to is the thing an author is checking.
    /// Empty for ordinary ground, whose top the Floor and Height rows above already state.</summary>
    private string HeightModeReadout => Shape?.HeightMode switch
    {
        "level" => $"Top cut flat at {Shape.Floor + Shape.BaseHeight}, whatever the ground under it does.",
        "raise" => $"Stands {Shape.BaseHeight} above the middle of the ground it covers.",
        "sink" => $"Cuts {Shape.BaseHeight} below the middle of the ground it covers.",
        _ => "",
    };

    /// <summary>Whether a shape's ground joins the relief its group is solved over (docs/world-export/relief.md §11).
    /// Inherit is first and is the default: the group is the unit because a relief solved per shape leaves
    /// a seam wherever two of them meet and disagree about the height they share.</summary>
    private static readonly SelectOption[] ReliefScopes =
    [
        new("",        "Inherit", "Its ground is the group's ground — the relief rolls through it, which is what a shape drawn to make a landmass wants."),
        new("hold",    "Hold",    "Flat at its own floor + height, with the surrounding surface solved knowing where it has to arrive — a walled town the valley runs up to."),
        new("exclude", "Exclude", "Out of the solve entirely, so the land is whatever that outline would have made at any height — a citadel on its own plinth."),
    ];

    /// <summary>What this shape's scope works out to, in its own numbers. Empty for the default, which states
    /// nothing about the shape that the group's own relief does not already say.</summary>
    private string ReliefScopeReadout => Shape?.ReliefScope switch
    {
        "hold" => $"Held flat at {Shape.Floor + Shape.BaseHeight}; the land around it is solved to arrive there.",
        "exclude" => "Out of the solve — the land is whatever the group would have made without it.",
        _ => "",
    };

    /// <summary>What the skirt does at the number it is set to. Zero is the one value worth a word, because a
    /// sheer face is right for a built thing and wrong for a landform.</summary>
    private string SkirtReadout => Shape is null || Shape.Skirt <= 0
        ? "A sheer face — right for a built thing, wrong for a landform."
        : $"Eases into the ground it meets over {Shape.Skirt} block{(Shape.Skirt == 1 ? "" : "s")}.";

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
