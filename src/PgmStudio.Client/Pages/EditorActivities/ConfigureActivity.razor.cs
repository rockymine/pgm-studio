using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using PgmStudio.Client.Components;

namespace PgmStudio.Client.Pages.EditorActivities;

// 2-step Setup flow (Islands → Symmetry) over the reused EditorCanvas — island-select then symmetry
// overlay, the same canvas the Configure World phase uses. Detection runs on the studio-chosen cleaned
// base: no scan-layer or block-exclusion choice and no world re-scan. Excluding an island recomputes
// symmetry on the backend from the already-detected islands (the exclude-island endpoint invalidates
// the symmetry cache).
public partial class ConfigureActivity
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public bool IsFirstActivity { get; set; }
    [Parameter] public EventCallback OnPrevActivity { get; set; }
    [Parameter] public EventCallback OnNextActivity { get; set; }

    private sealed record Island(int Id, int BlockCount);
    private sealed record SymMode(string Type, bool Detected, double Confidence);

    private int step = 1;
    private readonly HashSet<int> excludedIslands = new();
    private List<Island> islands = new();
    private int? selectedId;
    private List<SymMode> symModes = new();
    private double centerX, centerZ, detCenterX, detCenterZ;
    private string? symChoice;
    private string? error;

    private EditorCanvas? islandCanvas, symCanvas;

    // ── derived views for the markup ──────────────────────────────────────────────
    private List<Island> IncludedIslands => islands.Where(i => !excludedIslands.Contains(i.Id)).ToList();
    private List<Island> ExcludedIslandsList => islands.Where(i => excludedIslands.Contains(i.Id)).ToList();

    protected override async Task OnParametersSetAsync()
    {
        step = 1; symChoice = null; selectedId = null; error = null;
        await LoadState();
        await Task.WhenAll(LoadIslands(), LoadSymmetry());
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    // ── canvas readiness (each step mounts its own EditorCanvas) ──────────────────
    private async Task OnIslandCanvasReady()
    {
        if (islandCanvas is null) return;
        await islandCanvas.SetExcludedIslandsAsync(excludedIslands.ToList());
        await islandCanvas.SetSelectedIslandAsync(selectedId);
    }

    private async Task OnSymCanvasReady()
    {
        if (symCanvas is not null) await symCanvas.SetSymmetryAsync(symChoice == "none" ? null : symChoice, centerX, centerZ);
    }

    // ── data loading ────────────────────────────────────────────────────────────
    private async Task LoadState()
    {
        try
        {
            var s = await Http.GetFromJsonAsync<JsonElement>($"api/configure/{Slug}/state");
            excludedIslands.Clear();
            foreach (var i in IntList(s, "exclude_islands")) excludedIslands.Add(i);
        }
        catch (Exception ex) { error = ex.Message; }
    }

    private async Task LoadIslands()
    {
        try
        {
            var resp = await Http.GetAsync($"api/map/{Slug}/islands");
            if (!resp.IsSuccessStatusCode) { islands = new(); return; }
            var arr = await resp.Content.ReadFromJsonAsync<JsonElement>();
            islands = arr.ValueKind == JsonValueKind.Array
                ? arr.EnumerateArray().Select(i => new Island(i.GetProperty("id").GetInt32(), i.GetProperty("block_count").GetInt32())).ToList()
                : new();
        }
        catch { islands = new(); }
    }

    private async Task LoadSymmetry()
    {
        try
        {
            var resp = await Http.GetAsync($"api/map/{Slug}/symmetry");
            if (!resp.IsSuccessStatusCode) { symModes = new(); return; }
            var s = await resp.Content.ReadFromJsonAsync<JsonElement>();
            symModes = s.TryGetProperty("modes", out var ms) && ms.ValueKind == JsonValueKind.Array
                ? ms.EnumerateArray().Select(m => new SymMode(Str(m, "type"), m.GetProperty("detected").GetBoolean(), m.GetProperty("confidence").GetDouble()))
                    .OrderByDescending(m => m.Confidence).ToList()
                : new();
            if (s.TryGetProperty("center", out var c) && c.ValueKind == JsonValueKind.Object)
            {
                detCenterX = centerX = c.TryGetProperty("cx", out var cx) ? cx.GetDouble() : 0;
                detCenterZ = centerZ = c.TryGetProperty("cz", out var cz) ? cz.GetDouble() : 0;
            }
            // Pre-select the detected primary if the user hasn't chosen yet.
            if (symChoice is null && s.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.Object)
                symChoice = Str(p, "type");
        }
        catch { symModes = new(); }
    }

    // ── step 1: island exclusion ────────────────────────────────────────────────
    private async Task Select(int? id)
    {
        selectedId = id;
        if (islandCanvas is not null) await islandCanvas.SetSelectedIslandAsync(id);
    }

    private async Task ToggleIsland(int id, bool excluded)
    {
        await Http.PatchAsJsonAsync($"api/configure/{Slug}/exclude-island",
            new Dictionary<string, object?> { ["island_id"] = id, ["excluded"] = excluded });
        if (excluded) excludedIslands.Add(id); else excludedIslands.Remove(id);
        if (islandCanvas is not null) await islandCanvas.SetExcludedIslandsAsync(excludedIslands.ToList());
    }

    // ── step 2: symmetry ────────────────────────────────────────────────────────
    private async Task SelectSym(string type)
    {
        symChoice = type;
        if (symCanvas is not null) await symCanvas.SetSymmetryAsync(type == "none" ? null : type, centerX, centerZ);
    }

    private async Task OnCenter(ChangeEventArgs e, bool isX)
    {
        if (double.TryParse(e.Value?.ToString(), out var v)) { if (isX) centerX = v; else centerZ = v; }
        if (symCanvas is not null) await symCanvas.SetSymmetryAsync(symChoice == "none" ? null : symChoice, centerX, centerZ);
    }

    private async Task ResetCenter()
    {
        centerX = detCenterX; centerZ = detCenterZ;
        if (symCanvas is not null) await symCanvas.SetSymmetryAsync(symChoice == "none" ? null : symChoice, centerX, centerZ);
    }

    // ── navigation (flow-bar Back/Next) ────────────────────────────────────────
    // Step 1 → 2 is a plain "Next"; step 2's Next is "confirm and finish" (Finish), same shape as
    // the Configure wizard's own per-phase NextLabel/CanAdvance.
    private string NextLabel => step == 1 ? "Next" : symChoice == "none" ? "Confirm: no symmetry" : "Confirm symmetry";
    private bool NextEnabled => step == 1 || symChoice is not null;
    private bool BackEnabled => step > 1 || !IsFirstActivity;
    private Task OnFlowNext() => step == 1 ? Next() : Finish();
    private Task OnFlowBack() { if (step > 1) { Prev(); return Task.CompletedTask; } return OnPrevActivity.InvokeAsync(); }

    private async Task Next()
    {
        if (step == 1) { await LoadSymmetry(); step = 2; }   // re-detect symmetry minus the excluded islands
    }

    private void Prev() { if (step > 1) step--; }

    /// <summary>Jump straight to a step from the flow-bar pills (e.g. Symmetry) without walking Next.</summary>
    private async Task JumpToStep(int n)
    {
        if (n == step) return;
        if (n == 2) await LoadSymmetry();   // ensure the symmetry panel/canvas are fresh
        step = n;
    }

    private async Task Finish()
    {
        var payload = new Dictionary<string, object?> { ["status"] = symChoice == "none" ? "none" : "confirmed" };
        if (symChoice is not null && symChoice != "none") payload["confirmed_type"] = symChoice;
        payload["cx"] = centerX; payload["cz"] = centerZ;
        var resp = await Http.PatchAsJsonAsync($"api/map/{Slug}/symmetry", payload);
        if (resp.IsSuccessStatusCode) await OnNextActivity.InvokeAsync();
        else error = $"Failed to save symmetry ({(int)resp.StatusCode}).";
    }

    private static string Str(JsonElement e, string key, string def = "")
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : def;

    private static IEnumerable<int> IntList(JsonElement e, string key)
        => e.TryGetProperty(key, out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32())
            : Enumerable.Empty<int>();
}
