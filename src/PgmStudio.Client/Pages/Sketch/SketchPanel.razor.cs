using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.Sketch;

public partial class SketchPanel
{
    [Parameter] public IReadOnlyList<SketchIslandRow> Islands { get; set; } = [];
    [Parameter] public IReadOnlyList<SketchShapeRow> Shapes { get; set; } = [];
    [Parameter] public string? SelectedShapeId { get; set; }
    [Parameter] public string? SelectedIslandId { get; set; }
    [Parameter] public EventCallback<string> OnSelectShape { get; set; }
    [Parameter] public EventCallback<string> OnSelectIsland { get; set; }

    private sealed record Row(bool IsIsland, SketchIslandRow? Island, SketchShapeRow? Shape);

    // Flatten islands + their shapes (then any unassigned shapes) into one ordered render list, so the
    // markup is a single loop — no duplicated row markup, no @code RenderFragment helper.
    private IEnumerable<Row> Rows()
    {
        var byId = Shapes.ToDictionary(s => s.Id);
        var seen = new HashSet<string>();
        foreach (var isl in Islands)
        {
            yield return new Row(true, isl, null);
            foreach (var sid in isl.ShapeIds)
                if (byId.TryGetValue(sid, out var sh) && seen.Add(sid))
                    yield return new Row(false, null, sh);
        }
        foreach (var sh in Shapes)
            if (!seen.Contains(sh.Id))
                yield return new Row(false, null, sh);
    }

    private static string TypeIcon(string t) => t switch
    {
        "rectangle" => "rectangle-horizontal",
        "circle"    => "circle",
        "polygon"   => "pentagon",
        "lasso"     => "lasso",
        _           => "square",
    };
}
