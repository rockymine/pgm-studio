using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Configure;

// Build · height step: the max-build-height cap. Reuses the shared BuildHeightSideview (identical to the
// Edit Build Regions step) and writes the build slice's maxHeight; BuildGenerator applies it on save.
public partial class BuildHeightStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // A new map starts with a sensible vertical cap; the author can raise it (to the max) or clear it.
    private const double DefaultHeight = 20;
    private const double MaxHeight = 100;

    private string Slug => Wizard.Slug;
    private double? height;

    protected override void OnInitialized()
    {
        var build = Wizard.Intent["build"] as JsonObject;
        if (build?["maxHeight"] is JsonValue v)
        {
            if (v.TryGetValue(out double d)) height = d;
            else if (v.TryGetValue(out int i)) height = i;
            if (height is { } h) height = Clamp(h);   // cap a stored/over-range value at the max
        }
        else if (build is null)
        {
            height = DefaultHeight;   // brand-new build slice → seed + persist the default cap
            WriteHeight();
        }
        // else: a build slice exists with no maxHeight → honour the author's "no ceiling" choice
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    // Typed into the number input — the side-view picks up the new Height on re-render (blank = no ceiling).
    private void OnHeightInput(ChangeEventArgs e)
    {
        height = double.TryParse(e.Value?.ToString(), out var v) ? Clamp(v) : null;
        WriteHeight();
    }

    // Dragged the side-view line.
    private void OnSideviewHeight(double? y) { height = y is { } v ? Clamp(v) : null; WriteHeight(); }

    private static double Clamp(double v) => Math.Clamp(v, 0, MaxHeight);

    // Patch the height into the build slice without disturbing the buildable layer (areas/holes).
    private void WriteHeight()
    {
        var b = Wizard.Intent["build"] as JsonObject;
        if (b is null) { b = new JsonObject(); Wizard.Intent["build"] = b; }
        if (height is { } h) b["maxHeight"] = h; else b.Remove("maxHeight");
        Wizard.MarkDirty();
    }
}
