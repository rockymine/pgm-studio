using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.Sketch;

public partial class SketchLayers
{
    [Parameter] public IReadOnlyList<SketchLayerRow> Layers { get; set; } = [];
    [Parameter] public string? ActiveId { get; set; }
    [Parameter] public EventCallback<string> OnSelect { get; set; }
    [Parameter] public EventCallback OnAdd { get; set; }
    [Parameter] public EventCallback<string> OnDelete { get; set; }
    [Parameter] public EventCallback<(string Id, string Name)> OnRename { get; set; }
    [Parameter] public EventCallback<(string Id, double BaseY)> OnSetBaseY { get; set; }

    private SketchLayerRow? ActiveLayer => Layers.FirstOrDefault(l => l.Id == ActiveId);

    private Task RenameActive(ChangeEventArgs e)
        => ActiveLayer is { } L && e.Value?.ToString()?.Trim() is { Length: > 0 } n
            ? OnRename.InvokeAsync((L.Id, n)) : Task.CompletedTask;

    private Task BaseYActive(double v)
        => ActiveLayer is { } L ? OnSetBaseY.InvokeAsync((L.Id, v)) : Task.CompletedTask;
}
