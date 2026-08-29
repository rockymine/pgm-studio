using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

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
    [Parameter] public EventCallback<(string Id, double Radius, string Edge, int Seed)> OnSetPathBand { get; set; }

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
        "rectangle" => "rectangle-horizontal",
        "circle"    => "circle",
        "polygon"   => "pentagon",
        "lasso"     => "lasso",
        "path"      => "spline",
        _           => "square",
    };

    // How a path's two long sides are drawn. Its finish — gravel, a cell fabric, any pattern at all — is a
    // theme assigned to the shape, so what is offered here is only the shape of the band.
    private static readonly (string Key, string Label)[] PathEdges =
    [
        ("solid",   "Solid — one width the whole way"),
        ("rough",   "Rough — the outline wanders"),
        ("tapered", "Tapered — fat in the middle, thin at the ends"),
    ];

    // The author sets a width; the shape stores the half-width the band is offset by, so the two edges are
    // always that far from the line the author is dragging.
    private Task WidthChanged(double width)
        => Shape is null ? Task.CompletedTask : OnSetPathBand.InvokeAsync((Shape.Id, width / 2, Shape.PathEdge, Shape.PathSeed));

    private Task EdgeChanged(ChangeEventArgs e)
        => Shape is null ? Task.CompletedTask
            : OnSetPathBand.InvokeAsync((Shape.Id, Shape.Radius, e.Value?.ToString() ?? "solid", Shape.PathSeed));

    private Task SeedChanged(double seed)
        => Shape is null ? Task.CompletedTask
            : OnSetPathBand.InvokeAsync((Shape.Id, Shape.Radius, Shape.PathEdge, (int)seed));

    // NumberField clamps to its Min (height >= 1, floor >= 0) and snaps the display back, so these just
    // forward the already-valid value to the bridge.
    private Task HeightChanged(double v)
        => Shape is null ? Task.CompletedTask : OnSetHeight.InvokeAsync((Shape.Id, v, Shape.Floor));

    private Task FloorChanged(double v)
        => Shape is null ? Task.CompletedTask : OnSetHeight.InvokeAsync((Shape.Id, Shape.BaseHeight, v));

    /// <summary>How a shape's top is decided once its group carries a relief (docs/world-export/relief.md §7). The
    /// empty word is ordinary ground and is deliberately first: a shape is part of the landmass unless its
    /// author says otherwise, and a default that made every shape a mesa would turn a drawn board into a
    /// staircase of plates.</summary>
    private static readonly (string Value, string Label)[] HeightModes =
    [
        ("", "ground — the relief decides it"),
        ("level", "a mesa — a flat top at this height"),
        ("raise", "a monolith — this far above the ground"),
        ("sink", "a quarry — this far below the ground"),
    ];

    private string HeightModeBlurb => Shape?.HeightMode switch
    {
        "level" => "Cuts a flat top straight through the field, whatever the ground under it was doing — so its faces are cliffs.",
        "raise" => "Stands proud of the ground it covers, read at the middle of it, so it keeps its prominence wherever it is dragged.",
        "sink" => "Cuts down into the ground it covers by the same reading — a quarry, a sunken arena, a pit.",
        _ => "Part of the landmass: the group's relief is what this shape's ground does.",
    };

    /// <summary>Whether a shape's ground joins the relief its group is solved over (docs/world-export/relief.md §11).
    /// Inheriting is first and is the default: the group is the unit because a relief solved per shape leaves
    /// a seam wherever two of them meet and disagree about the height they share.</summary>
    private static readonly (string Value, string Label)[] ReliefScopes =
    [
        ("", "yes — its ground is the group's ground"),
        ("hold", "holds its own level, and the land meets it"),
        ("exclude", "sits apart — the land ignores it"),
    ];

    private string ReliefScopeBlurb => Shape?.ReliefScope switch
    {
        "hold" => "Flat at its own floor + height, and the surrounding surface is solved knowing where it has to arrive — a walled town the valley runs up to.",
        "exclude" => "Out of the solve entirely, so the land is whatever that outline would have made at any height — a citadel on its own plinth.",
        _ => "The group's relief rolls through it, which is what a shape drawn to make a landmass wants.",
    };

    private Task ReliefScopeChanged(ChangeEventArgs e)
        => Shape is null || Handle is null
            ? Task.CompletedTask
            : Handle.InvokeVoidAsync("setReliefScope", Shape.Id, e.Value?.ToString() ?? "").AsTask();

    private Task SkirtChanged(double value)
        => Shape is null || Handle is null
            ? Task.CompletedTask
            : Handle.InvokeVoidAsync("setSkirt", Shape.Id, Math.Max(0, (int)Math.Round(value))).AsTask();

    private Task HeightModeChanged(ChangeEventArgs e)
        => Shape is null || Handle is null
            ? Task.CompletedTask
            : Handle.InvokeVoidAsync("setHeightMode", Shape.Id, e.Value?.ToString() ?? "").AsTask();

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
