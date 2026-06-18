using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Sketch;

public partial class SketchEditor
{
    [Parameter] public string Slug { get; set; } = "";

    private ElementReference svgRef, wrapRef, coordsRef, zoomRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<SketchEditor>? selfRef;

    private string tool = "move";
    private string op = "add";
    private string mode = "rot_180";
    private bool mirrorOn = true;
    private bool shapesOn = false;
    private bool chunksOn = true;
    private string islandLabel = "";

    // Layout pushed from the bridge (OnLayout) + the current selection (OnShapeSelected/OnIslandSelected).
    private List<SketchIslandRow> islands = [];
    private List<SketchShapeRow> shapes = [];
    private string? selectedShapeId;
    private string? selectedIslandId;

    private SketchShapeRow? SelectedShape => shapes.FirstOrDefault(s => s.Id == selectedShapeId);
    private SketchIslandRow? SelectedIsland => islands.FirstOrDefault(i => i.Id == selectedIslandId);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        handle = await JS.InvokeAsync<IJSObjectReference>("studio.mountSketch", svgRef, wrapRef, coordsRef, zoomRef, selfRef);
        // Restore the saved layout (empty {} for a fresh sketch); the bridge handles an empty state.
        try
        {
            var state = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/sketch");
            await handle.InvokeVoidAsync("load", state);
            // Sync the toolbar mode select with the loaded setup (the canvas already uses it).
            if (state.ValueKind == JsonValueKind.Object && state.TryGetProperty("setup", out var su)
                && su.ValueKind == JsonValueKind.Object && su.TryGetProperty("mirror_mode", out var mm)
                && mm.ValueKind == JsonValueKind.String && mm.GetString() is { Length: > 0 } m)
            {
                mode = m;
                StateHasChanged();
            }
        }
        catch { /* no saved layout / map not found — start blank */ }
    }

    private async Task SetTool(string t)
    {
        tool = t;
        if (handle is not null) await handle.InvokeVoidAsync("setTool", t);
    }

    private async Task SetOperation(string o)
    {
        op = o;
        if (handle is not null) await handle.InvokeVoidAsync("setOperation", o);
    }

    private async Task OnModeChange(ChangeEventArgs e)
    {
        mode = e.Value?.ToString() ?? "rot_180";
        if (handle is not null) await handle.InvokeVoidAsync("setMode", mode);
    }

    private async Task ToggleMirror()
    {
        mirrorOn = !mirrorOn;
        if (handle is not null) await handle.InvokeVoidAsync("setMirrorVisible", mirrorOn);
    }

    private async Task ToggleShapes()
    {
        shapesOn = !shapesOn;
        if (handle is not null) await handle.InvokeVoidAsync("setShapesVisible", shapesOn);
    }

    private async Task ToggleChunks()
    {
        chunksOn = !chunksOn;
        if (handle is not null) await handle.InvokeVoidAsync("setChunkVisible", chunksOn);
    }

    private async Task OnFit()
    {
        if (handle is not null) await handle.InvokeVoidAsync("fitToBbox");
    }

    // ── Panel / inspector actions → the JS bridge ──────────────────────────────

    private Task SelectShape(string id) => handle?.InvokeVoidAsync("selectShape", id).AsTask() ?? Task.CompletedTask;
    private Task SelectIsland(string id) => handle?.InvokeVoidAsync("selectIsland", id).AsTask() ?? Task.CompletedTask;
    private Task ToggleOp(string id) => handle?.InvokeVoidAsync("toggleOp", id).AsTask() ?? Task.CompletedTask;
    private Task ToggleOverride(string id) => handle?.InvokeVoidAsync("toggleOverride", id).AsTask() ?? Task.CompletedTask;
    private Task DeleteShape(string id) => handle?.InvokeVoidAsync("deleteShape", id).AsTask() ?? Task.CompletedTask;
    private Task ToggleMirrors(string islandId) => handle?.InvokeVoidAsync("toggleMirrors", islandId).AsTask() ?? Task.CompletedTask;
    private Task RenameIsland((string Id, string Name) e) => handle?.InvokeVoidAsync("renameIsland", e.Id, e.Name).AsTask() ?? Task.CompletedTask;

    // ── Bridge callbacks ───────────────────────────────────────────────────────

    /// <summary>A shape was selected on the canvas/panel (null = deselected).</summary>
    [JSInvokable]
    public void OnShapeSelected(string? id) { selectedShapeId = id; StateHasChanged(); }

    /// <summary>An island was selected in the panel (null = deselected).</summary>
    [JSInvokable]
    public void OnIslandSelected(string? id) { selectedIslandId = id; StateHasChanged(); }

    /// <summary>The bridge pushed the current island→shape tree (on every layout change).</summary>
    [JSInvokable]
    public void OnLayout(string json)
    {
        var dto = JsonSerializer.Deserialize<SketchLayoutDto>(json);
        islands = dto?.Islands ?? [];
        shapes = dto?.Shapes ?? [];
        StateHasChanged();
    }

    /// <summary>The canvas changed the active tool itself (e.g. auto-switch to select after a draw);
    /// keep the toolbar highlight truthful.</summary>
    [JSInvokable]
    public void OnToolChanged(string t)
    {
        tool = t;
        StateHasChanged();
    }

    /// <summary>The layout changed; update the island-count label and schedule a debounced save.</summary>
    [JSInvokable]
    public void OnDirty(int islandCount)
    {
        islandLabel = islandCount == 1 ? "1 island" : $"{islandCount} islands";
        StateHasChanged();
        ScheduleSave();
    }

    // ── Persistence: debounced PUT of the bridge's getState() ───────────────────

    private CancellationTokenSource? saveCts;

    private void ScheduleSave()
    {
        saveCts?.Cancel();
        saveCts = new CancellationTokenSource();
        _ = SaveDebouncedAsync(saveCts.Token);
    }

    private async Task SaveDebouncedAsync(CancellationToken token)
    {
        try { await Task.Delay(800, token); } catch (TaskCanceledException) { return; }
        await SaveAsync(token);
    }

    private async Task SaveAsync(CancellationToken token)
    {
        if (handle is null) return;
        try
        {
            var state = await handle.InvokeAsync<JsonElement>("getState", token);
            await Http.PutAsJsonAsync($"api/map/{Slug}/sketch", state, token);
        }
        catch { /* save failed (or cancelled) — the next change retries */ }
    }

    public async ValueTask DisposeAsync()
    {
        saveCts?.Cancel();
        // Best-effort final flush of the last (<800 ms) change before tearing the handle down.
        await SaveAsync(CancellationToken.None);
        if (handle is not null)
        {
            try { await handle.InvokeVoidAsync("dispose"); } catch { }
            try { await handle.DisposeAsync(); } catch { }
        }
        selfRef?.Dispose();
    }
}
