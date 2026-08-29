using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Sketch;

public partial class SketchPanel
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public IReadOnlyList<SketchGroupRow> Groups { get; set; } = [];
    [Parameter] public IReadOnlyList<SketchShapeRow> Shapes { get; set; } = [];
    [Parameter] public string? SelectedShapeId { get; set; }
    [Parameter] public string? SelectedGroupId { get; set; }
    [Parameter] public EventCallback<string> OnSelectShape { get; set; }
    [Parameter] public EventCallback<string> OnSelectGroup { get; set; }

    private readonly HashSet<string> collapsed = new();

    private void Toggle(string id)
    {
        if (!collapsed.Remove(id)) collapsed.Add(id);
    }

    private SketchShapeRow? ShapeById(string id) => Shapes.FirstOrDefault(s => s.Id == id);

    // Shapes not claimed by any group (defensive — shouldn't normally happen).
    private IEnumerable<SketchShapeRow> Unassigned()
    {
        var assigned = Groups.SelectMany(i => i.ShapeIds).ToHashSet();
        return Shapes.Where(s => !assigned.Contains(s.Id));
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

    // Re-render the lucide icons (chevrons / type glyphs) after the tree changes.
    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
