using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.EditorActivities;

// 2-step Setup flow (Islands → Symmetry) over the island/symmetry preview canvas (mounted via
// studio.mountConfigure). Detection runs on the studio-chosen cleaned-base layer — no scan-layer or
// block-exclusion choice and no world re-scan. Excluding an island recomputes symmetry on the backend
// from the already-detected islands (the exclude-island endpoint invalidates the symmetry cache).
public partial class ConfigureActivity : IAsyncDisposable
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public EventCallback OnComplete { get; set; }

    private sealed record Island(int Id, int BlockCount);
    private sealed record SymMode(string Type, bool Detected, double Confidence);

    private int step = 1;
    private readonly HashSet<int> excludedIslands = new();
    private List<Island> islands = new();
    private List<SymMode> symModes = new();
    private double centerX, centerZ, detCenterX, detCenterZ;
    private string? symChoice;
    private string? error;

    private ElementReference svgRef, wrapRef;
    private IJSObjectReference? canvasHandle;

    // ── derived views for the markup ──────────────────────────────────────────────
    private List<Island> IncludedIslands => islands.Where(i => !excludedIslands.Contains(i.Id)).ToList();
    private List<Island> ExcludedIslandsList => islands.Where(i => excludedIslands.Contains(i.Id)).ToList();

    private static string SymLabel(string type) => type switch
    {
        "rot_90" => "Rotate 90°", "rot_180" => "Rotate 180°",
        "mirror_x" => "Mirror X (left/right)", "mirror_z" => "Mirror Z (front/back)",
        "mirror_d1" => "Mirror ╲ (diagonal)", "mirror_d2" => "Mirror ╱ (diagonal)",
        _ => type,
    };

    protected override async Task OnParametersSetAsync()
    {
        step = 1; symChoice = null; error = null;
        await LoadState();
        await Task.WhenAll(LoadIslands(), LoadSymmetry());
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (firstRender)
        {
            canvasHandle = await JS.InvokeAsync<IJSObjectReference>("studio.mountConfigure", svgRef, wrapRef, Slug);
            await SyncCanvas();
        }
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
    private async Task ToggleIsland(int id, bool excluded)
    {
        await Http.PatchAsJsonAsync($"api/configure/{Slug}/exclude-island",
            new Dictionary<string, object?> { ["island_id"] = id, ["excluded"] = excluded });
        if (excluded) excludedIslands.Add(id); else excludedIslands.Remove(id);
        if (canvasHandle is not null) await canvasHandle.InvokeVoidAsync("setExcludedIds", (object)excludedIslands.ToArray());
    }

    // ── step 2: symmetry ────────────────────────────────────────────────────────
    private async Task SelectSym(string type)
    {
        symChoice = type;
        if (canvasHandle is not null)
        {
            await canvasHandle.InvokeVoidAsync("setSymmetryType", type == "none" ? null : type);
            await canvasHandle.InvokeVoidAsync("setCenter", centerX, centerZ);
        }
    }

    private async Task OnCenter(ChangeEventArgs e, bool isX)
    {
        if (double.TryParse(e.Value?.ToString(), out var v)) { if (isX) centerX = v; else centerZ = v; }
        if (canvasHandle is not null) await canvasHandle.InvokeVoidAsync("setCenter", centerX, centerZ);
    }

    private async Task ResetCenter()
    {
        centerX = detCenterX; centerZ = detCenterZ;
        if (canvasHandle is not null) await canvasHandle.InvokeVoidAsync("setCenter", centerX, centerZ);
    }

    // ── navigation ──────────────────────────────────────────────────────────────
    private async Task Next()
    {
        if (step == 1) { await LoadSymmetry(); step = 2; }   // re-detect symmetry minus the excluded islands
        await SyncCanvas();
    }

    private async Task Prev() { if (step > 1) { step--; await SyncCanvas(); } }

    /// <summary>Jump straight to a step from the step-bar tabs (e.g. Symmetry) without walking Next.</summary>
    private async Task JumpToStep(int n)
    {
        if (n == step) return;
        if (n == 2) await LoadSymmetry();   // ensure the symmetry panel/canvas are fresh
        step = n;
        await SyncCanvas();
    }

    private async Task Finish()
    {
        var payload = new Dictionary<string, object?> { ["status"] = symChoice == "none" ? "none" : "confirmed" };
        if (symChoice is not null && symChoice != "none") payload["confirmed_type"] = symChoice;
        payload["cx"] = centerX; payload["cz"] = centerZ;
        var resp = await Http.PatchAsJsonAsync($"api/map/{Slug}/symmetry", payload);
        if (resp.IsSuccessStatusCode) await OnComplete.InvokeAsync();
        else error = $"Failed to save symmetry ({(int)resp.StatusCode}).";
    }

    /// <summary>Drive the preview canvas to match the current step.</summary>
    private async Task SyncCanvas()
    {
        if (canvasHandle is null) return;
        if (step == 1)
        {
            await canvasHandle.InvokeVoidAsync("setMode", "islands");
            await canvasHandle.InvokeVoidAsync("showIslands");
            await canvasHandle.InvokeVoidAsync("setExcludedIds", (object)excludedIslands.ToArray());
        }
        else
        {
            await canvasHandle.InvokeVoidAsync("setMode", "symmetry");
            await canvasHandle.InvokeVoidAsync("showIslands");   // island context (also when jumping straight here)
            await canvasHandle.InvokeVoidAsync("showSymmetry");
            await canvasHandle.InvokeVoidAsync("setSymmetryType", symChoice == "none" ? null : symChoice);
            await canvasHandle.InvokeVoidAsync("setCenter", centerX, centerZ);
        }
    }

    private static string Str(JsonElement e, string key, string def = "")
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : def;

    private static IEnumerable<int> IntList(JsonElement e, string key)
        => e.TryGetProperty(key, out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32())
            : Enumerable.Empty<int>();

    public async ValueTask DisposeAsync()
    {
        if (canvasHandle is not null)
        {
            try { await canvasHandle.InvokeVoidAsync("dispose"); } catch { }
            try { await canvasHandle.DisposeAsync(); } catch { }
        }
    }
}
