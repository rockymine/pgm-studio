using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Pages.EditorActivities;

namespace PgmStudio.Client.Pages.Configure;

// World · Symmetry sub-step (N01): confirm the detected symmetry (or pick another / none) and its centre.
// This is the World intent slice — the confirmed choice is written to intent.symmetry, which the generator
// uses to orbit-fill teams/wools. The canvas (reused EditorCanvas in symmetry mode — base layer only) shows
// the axis/centre overlay; the detection summary surfaces the suggested team count.
public partial class WorldSymmetryPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed record SymMode(string Type, bool Detected, double Confidence);

    private List<SymMode> modes = new();
    private int islandCount;
    private string? primaryType;
    private double detCenterX, detCenterZ;

    private string? selectedType;   // null = "no symmetry"
    private double centerX, centerZ;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private double? SuggestedTeams => selectedType switch
    {
        "rot_90" => 4,
        "rot_180" or "mirror_x" or "mirror_z" or "mirror_d1" or "mirror_d2" => 2,
        _ => null,
    };

    protected override async Task OnInitializedAsync()
    {
        await LoadIslandCount();
        await LoadSymmetry();
        // Seed from the stored intent if the author already confirmed; otherwise pre-select the detected
        // primary (shown selected, but only written to the intent once the author confirms it).
        if (Wizard.Intent["symmetry"] is JsonObject sym)
        {
            selectedType = sym["mode"]?.GetValue<string>();
            centerX = Num(sym, "centerX") ?? detCenterX;
            centerZ = Num(sym, "centerZ") ?? detCenterZ;
        }
        else
        {
            selectedType = primaryType;
            centerX = detCenterX; centerZ = detCenterZ;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private async Task LoadIslandCount()
    {
        try
        {
            var isl = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/islands");
            if (isl.ValueKind == JsonValueKind.Array) islandCount = isl.GetArrayLength();
        }
        catch { /* leave at 0 */ }
    }

    private async Task LoadSymmetry()
    {
        try
        {
            var s = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/symmetry");
            if (s.TryGetProperty("modes", out var ms) && ms.ValueKind == JsonValueKind.Array)
                modes = ms.EnumerateArray()
                    .Select(m => new SymMode(Str(m, "type"),
                        m.TryGetProperty("detected", out var d) && d.GetBoolean(),
                        m.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0))
                    .OrderByDescending(m => m.Confidence).ToList();
            if (s.TryGetProperty("center", out var ce) && ce.ValueKind == JsonValueKind.Object)
            {
                detCenterX = ce.TryGetProperty("cx", out var cx) && cx.ValueKind == JsonValueKind.Number ? cx.GetDouble() : 0;
                detCenterZ = ce.TryGetProperty("cz", out var cz) && cz.ValueKind == JsonValueKind.Number ? cz.GetDouble() : 0;
            }
            if (s.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.Object)
                primaryType = Str(p, "type");
        }
        catch { modes = new(); }
    }

    // The choices offered: the detected symmetries (high → low confidence) plus an explicit "none".
    private List<SymMode> Choices => modes.Where(m => m.Detected).ToList();

    private async Task OnCanvasReady()
    {
        if (canvas is not null) await canvas.SetSymmetryAsync(selectedType, centerX, centerZ);
    }

    private async Task Select(string? type)
    {
        selectedType = type;
        WriteIntent();
        if (canvas is not null) await canvas.SetSymmetryAsync(selectedType, centerX, centerZ);
    }

    private async Task OnCenter(ChangeEventArgs e, bool isX)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) { if (isX) centerX = v; else centerZ = v; }
        WriteIntent();
        if (canvas is not null) await canvas.SetSymmetryAsync(selectedType, centerX, centerZ);
    }

    // Confirmed symmetry → intent.symmetry (mode + centre); "no symmetry" removes the slice (generator
    // then expects every team stated explicitly). Marks the intent dirty so it persists on phase-advance.
    private void WriteIntent()
    {
        if (selectedType is null)
            Wizard.Intent.Remove("symmetry");
        else
            Wizard.Intent["symmetry"] = new JsonObject { ["mode"] = selectedType, ["centerX"] = centerX, ["centerZ"] = centerZ };
        Wizard.MarkDirty();
    }

    // Friendly label for a symmetry type (matches the wizard's wording elsewhere).
    private static string SymLabel(string? type) => type switch
    {
        "rot_90" => "Rotate 90°",
        "rot_180" => "Rotate 180°",
        "mirror_x" => "Mirror X (left/right)",
        "mirror_z" => "Mirror Z (front/back)",
        "mirror_d1" => "Mirror ╲ (diagonal)",
        "mirror_d2" => "Mirror ╱ (diagonal)",
        _ => "No symmetry",
    };

    private static string Str(JsonElement e, string key)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static double? Num(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue(out double d) ? d : null;
}
