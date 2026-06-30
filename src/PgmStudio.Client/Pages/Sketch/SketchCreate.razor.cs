using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Sketch;

public partial class SketchCreate
{
    private string tab = "blank";        // "blank" | "generate"
    private string sketchName = "";

    // Working frame (blank tab). Default = 2-team landscape (120×80), origin-centred, rotational symmetry —
    // the values POSTed to /api/sketch so the editor opens already framed.
    private string preset = "landscape";
    private double width = 120;
    private double depth = 80;
    private string mode = "rot_180";
    private double centerX = 0;
    private double centerZ = 0;

    // Named footprints (W,D); Custom = author-typed W/D.
    private static readonly Dictionary<string, (double W, double D)> Presets = new()
    {
        ["landscape"] = (120, 80),
        ["portrait"]  = (80, 120),
        ["square"]    = (120, 120),
    };

    // Generate tab.
    private string genArchetype = "H";
    private int genSeed = Random.Shared.Next(1, 100000);

    private bool busy;
    private string? createError;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private void SetTab(string t) { tab = t; createError = null; }

    // Pick a footprint tile: a named preset sets its width/depth; "custom" keeps the current size for typing.
    private void SetPreset(string p)
    {
        preset = p;
        if (Presets.TryGetValue(p, out var d)) { width = d.W; depth = d.D; }
    }

    private void OnWidth(double v) { width = v; preset = InferPreset(width, depth); }
    private void OnDepth(double v) { depth = v; preset = InferPreset(width, depth); }
    private void SetMode(string m) => mode = m;

    private static string InferPreset(double w, double d) =>
        Presets.FirstOrDefault(kv => kv.Value.W == w && kv.Value.D == d).Key ?? "custom";

    private void RerollSeed() => genSeed = Random.Shared.Next(1, 100000);

    private Task Submit() => tab == "generate" ? CreateGenerated() : CreateSketch();

    private async Task CreateSketch()
    {
        if (busy) return;
        busy = true;
        createError = null;
        try
        {
            var name = string.IsNullOrWhiteSpace(sketchName) ? "Untitled sketch" : sketchName.Trim();
            var resp = await Http.PostAsJsonAsync("api/sketch", new { name, width, depth, mode, centerX, centerZ });
            if (await SlugFrom(resp) is { } slug) { Nav.NavigateTo($"maps/{slug}/sketch"); return; }
            createError = "Could not create the sketch.";
        }
        catch { createError = "Could not create the sketch."; }
        busy = false;
    }

    private async Task CreateGenerated()
    {
        if (busy) return;
        busy = true;
        createError = null;
        try
        {
            var name = string.IsNullOrWhiteSpace(sketchName) ? $"{genArchetype} sketch" : sketchName.Trim();
            var resp = await Http.PostAsJsonAsync("api/sketch/generate", new { name, archetype = genArchetype, seed = genSeed });
            if (await SlugFrom(resp) is { } slug) { Nav.NavigateTo($"maps/{slug}/sketch"); return; }
            createError = "Could not generate the sketch.";
        }
        catch { createError = "Could not generate the sketch."; }
        busy = false;
    }

    private static async Task<string?> SlugFrom(HttpResponseMessage resp)
    {
        if (!resp.IsSuccessStatusCode) return null;
        var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return created.TryGetProperty("slug", out var s) ? s.GetString() : null;
    }
}
