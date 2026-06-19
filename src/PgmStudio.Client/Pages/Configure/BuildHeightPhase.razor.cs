using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Configure;

// Build · height step: the max-build-height cap. Reuses the shared BuildHeightSideview (identical to the
// Edit Build Regions step) and writes the build slice's maxHeight; BuildGenerator applies it on save.
public partial class BuildHeightPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private string Slug => Wizard.Slug;
    private double? height;

    protected override void OnInitialized()
    {
        if (Wizard.Intent["build"] is JsonObject b && b["maxHeight"] is JsonValue v)
        {
            if (v.TryGetValue(out double d)) height = d;
            else if (v.TryGetValue(out int i)) height = i;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    // Typed into the number input — the side-view picks up the new Height on re-render.
    private void OnHeightInput(ChangeEventArgs e)
    {
        height = double.TryParse(e.Value?.ToString(), out var v) ? v : null;
        WriteHeight();
    }

    // Dragged the side-view line.
    private void OnSideviewHeight(double? y) { height = y; WriteHeight(); }

    // Patch the height into the build slice without disturbing the buildable layer (areas/holes).
    private void WriteHeight()
    {
        var b = Wizard.Intent["build"] as JsonObject;
        if (b is null) { b = new JsonObject(); Wizard.Intent["build"] = b; }
        if (height is { } h) b["maxHeight"] = h; else b.Remove("maxHeight");
        Wizard.MarkDirty();
    }
}
