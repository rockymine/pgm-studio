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

    // "Rotate (°)" is a rotate-by box (rotation bakes into geometry, so there is no absolute angle to hold): its
    // value is always blank, so NumberField snaps back to blank after each entry and the same angle applies again.
    private Task RotateChanged(double degrees) => degrees != 0 ? OnRotate.InvokeAsync(degrees) : Task.CompletedTask;

    // How a path's two long sides are drawn. Its finish — gravel, a cell fabric, any pattern at all — is a
    // theme assigned to the shape, so what is offered here is only the shape of the band.
    private static readonly SelectOption[] StrokeEdges =
    [
        new("solid",   "Solid",   "One width the whole way."),
        new("rough",   "Rough",   "The width varies by up to 45% on each side."),
        new("tapered", "Tapered", "Wide in the middle, narrow at the ends."),
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

    private bool IsLevel => Shape?.HeightMode == HeightModeWords.Level;

    /// <summary>What the height number is measured from under the shape's height mode — the floor for ground,
    /// the middle of the ground under it for raise and sink, the ground at each cell for drape.</summary>
    private string HeightLabel => Shape?.HeightMode switch
    {
        HeightModeWords.Raise => "Height above ground",
        HeightModeWords.Sink => "Depth below ground",
        HeightModeWords.Drape => "Height above ground",
        _ => "Height above floor",
    };

    private string HeightHint => Shape?.HeightMode switch
    {
        HeightModeWords.Raise or HeightModeWords.Sink => "(from the middle height of the ground under it)",
        HeightModeWords.Drape => "(at every cell)",
        _ => $"(top at y {Shape?.Floor + Shape?.BaseHeight})",
    };

    private Task FloorChanged(double v)
        => Shape is null ? Task.CompletedTask : OnSetHeight.InvokeAsync((Shape.Id, Shape.BaseHeight, v));

    /// <summary>How a shape's top is decided against the ground it stands on (docs/tools/sketch.md). The words are
    /// the document's own. Ground is first and is the default: a shape is part of the landmass unless its author
    /// says otherwise. A shape with a mode stands at its own statement, and the modifiers leave it out.</summary>
    private static readonly SelectOption[] HeightModes =
    [
        new("",      "Ground", "Part of the land. The modifiers reshape it with the rest."),
        new("level", "Level",  "A flat top at a fixed height, whatever the ground does. Its sides are cliffs."),
        new("raise", "Raise",  "Stands this far above the ground it covers, wherever it is moved."),
        new("sink",  "Sink",   "Sits this far below the ground it covers."),
        new("drape", "Drape",  "This far above the ground at every cell, so it follows slopes. Suits walls and hedges."),
    ];

    /// <summary>Where this shape's top actually lands, in its own numbers. A mode is a rule and a rule has to
    /// be applied before it says anything; the number it works out to is the thing an author is checking.
    /// Empty for ordinary ground, whose top the Floor and Height rows above already state.</summary>
    private string HeightModeReadout => Shape?.HeightMode switch
    {
        "level" => $"Flat top at {Shape.Floor + Shape.BaseHeight}.",
        "raise" => $"Stands {Shape.BaseHeight} above the middle height of the ground under it.",
        "sink" => $"Cuts {Shape.BaseHeight} below the middle height of the ground under it.",
        "drape" => $"Stands {Shape.BaseHeight} above the ground at every cell.",
        _ => "",
    };

    /// <summary>Whether the modifier stack reshapes a shape's ground (docs/tools/sketch.md, Terraform). Follow is
    /// first and is the default.</summary>
    private static readonly SelectOption[] ReliefScopeOptions =
    [
        new("",        "Follow", "The modifiers reshape it with the land around it."),
        new(Vocabulary.ReliefScopes.Hold, "Hold", "Stays at its own height; the modifiers leave it out."),
        new(Vocabulary.ReliefScopes.Ride, "Ride", "Keeps its form and moves by the median change the modifiers made to the ground under it."),
    ];

    /// <summary>What holding or riding works out to, in the shape's own numbers. Empty for the default.</summary>
    private string ReliefScopeReadout => Shape?.ReliefScope switch
    {
        Vocabulary.ReliefScopes.Hold => $"Stays at {Shape.Floor + Shape.BaseHeight} whatever the modifiers do around it.",
        Vocabulary.ReliefScopes.Ride => $"Keeps its {Shape.BaseHeight}-block form and rises or sinks with the ground the modifiers leave under it.",
        _ => "",
    };

    /// <summary>What the skirt does at the number it is set to. Zero is the one value worth a word, because a
    /// sheer face is right for a built thing and wrong for a landform.</summary>
    private string SkirtReadout => Shape is null || Shape.Skirt <= 0
        ? "Sheer sides."
        : $"Blends into the ground over {Shape.Skirt} block{(Shape.Skirt == 1 ? "" : "s")}.";

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
