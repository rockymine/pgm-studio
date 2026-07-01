using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.Sketch;

public partial class SketchInspector
{
    [Parameter] public SketchShapeRow? Shape { get; set; }
    [Parameter] public SketchIslandRow? Island { get; set; }
    [Parameter] public IReadOnlyList<SketchShapeRow> Shapes { get; set; } = [];
    [Parameter] public EventCallback<string> OnToggleOp { get; set; }
    [Parameter] public EventCallback<string> OnToggleOverride { get; set; }
    [Parameter] public EventCallback<string> OnDeleteShape { get; set; }
    [Parameter] public EventCallback<string> OnPromoteShape { get; set; }
    [Parameter] public EventCallback<(string Id, double Base, double Floor)> OnSetHeight { get; set; }
    [Parameter] public int SelectedVertexIdx { get; set; } = -1;
    [Parameter] public double SelectedVertexHeight { get; set; }
    [Parameter] public EventCallback<(string Id, int Idx, double Height)> OnSetVertexHeight { get; set; }
    [Parameter] public EventCallback<string> OnToggleMirrors { get; set; }
    [Parameter] public EventCallback<(string Id, string Name)> OnRenameIsland { get; set; }
    [Parameter] public EventCallback<double> OnRotate { get; set; }

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
        _           => "square",
    };

    // NumberField clamps to its Min (height >= 1, floor >= 0) and snaps the display back, so these just
    // forward the already-valid value to the bridge.
    private Task HeightChanged(double v)
        => Shape is null ? Task.CompletedTask : OnSetHeight.InvokeAsync((Shape.Id, v, Shape.Floor));

    private Task FloorChanged(double v)
        => Shape is null ? Task.CompletedTask : OnSetHeight.InvokeAsync((Shape.Id, Shape.BaseHeight, v));

    private Task VertexHeightChanged(double v)
        => Shape is null || SelectedVertexIdx < 0 ? Task.CompletedTask
            : OnSetVertexHeight.InvokeAsync((Shape.Id, SelectedVertexIdx, v));

    private Task RenameChanged(ChangeEventArgs e)
        => Island is null ? Task.CompletedTask
                          : OnRenameIsland.InvokeAsync((Island.Id, e.Value?.ToString()?.Trim() is { Length: > 0 } n ? n : Island.Name));

    private IEnumerable<SketchShapeRow> IslandShapes()
        => Island is null ? []
                          : Island.ShapeIds.Select(id => Shapes.FirstOrDefault(s => s.Id == id)).OfType<SketchShapeRow>();
}
