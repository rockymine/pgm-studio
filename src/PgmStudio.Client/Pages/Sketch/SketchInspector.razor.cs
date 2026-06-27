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

    private static string TypeIcon(string t) => t switch
    {
        "rectangle" => "rectangle-horizontal",
        "circle"    => "circle",
        "polygon"   => "pentagon",
        "lasso"     => "lasso",
        _           => "square",
    };

    private Task HeightChanged(ChangeEventArgs e)
        => Shape is not null && double.TryParse(e.Value?.ToString(), out var v)
            ? OnSetHeight.InvokeAsync((Shape.Id, v, Shape.Floor)) : Task.CompletedTask;

    private Task FloorChanged(ChangeEventArgs e)
        => Shape is not null && double.TryParse(e.Value?.ToString(), out var v)
            ? OnSetHeight.InvokeAsync((Shape.Id, Shape.BaseHeight, v)) : Task.CompletedTask;

    private Task VertexHeightChanged(ChangeEventArgs e)
        => Shape is not null && SelectedVertexIdx >= 0 && double.TryParse(e.Value?.ToString(), out var v)
            ? OnSetVertexHeight.InvokeAsync((Shape.Id, SelectedVertexIdx, v)) : Task.CompletedTask;

    private Task RenameChanged(ChangeEventArgs e)
        => Island is null ? Task.CompletedTask
                          : OnRenameIsland.InvokeAsync((Island.Id, e.Value?.ToString()?.Trim() is { Length: > 0 } n ? n : Island.Name));

    private IEnumerable<SketchShapeRow> IslandShapes()
        => Island is null ? []
                          : Island.ShapeIds.Select(id => Shapes.FirstOrDefault(s => s.Id == id)).OfType<SketchShapeRow>();
}
