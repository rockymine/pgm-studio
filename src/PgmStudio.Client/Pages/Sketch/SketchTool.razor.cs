using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Sketch;

public partial class SketchTool
{
    [Parameter] public string Slug { get; set; } = "";

    private ElementReference svgRef, wrapRef, coordsRef, zoomRef, dimRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<SketchTool>? selfRef;

    private string tool = "move";
    private string op = "add";
    private string mode = "rot_180";
    // Working bounds = a width(X)×depth(Z) frame centred at the origin. Default = 2-team landscape.
    private string preset = "landscape";
    private double width = 120;
    private double depth = 80;
    private double centerX = 0;     // symmetry centre
    private double centerZ = 0;

    // Named footprints (W,D). Square is kept for 4-team / D2-symmetry maps; Custom = author-typed W/D.
    private static readonly Dictionary<string, (double W, double D)> Presets = new()
    {
        ["landscape"] = (120, 80),
        ["portrait"]  = (80, 120),
        ["square"]    = (120, 120),
    };
    private bool mirrorOn = true;
    private bool shapesOn = false;
    private bool chunksOn = true;
    private bool snapOn = true;
    private bool threeD = false;
    private bool isoUnavailable = false;   // 3-D preview couldn't initialise (no WebGL / module load failed)
    private string islandLabel = "";

    // ── Phases (rail): Identity (name + authors) · Draw (the canvas). Draw is the default and stays
    //    mounted while Identity is up (hidden, not torn down) so the drawing state + zoom survive. ──
    private string active = "draw";
    private bool IdentityActive => active == "identity";
    private bool DrawActive => active == "draw";
    private Task GoIdentity() => SetPhase("identity");
    private Task GoDraw() => SetPhase("draw");

    private async Task SetPhase(string p)
    {
        active = p;
        if (p == "draw" && handle is not null)
        {
            // The canvas was display:none on Identity; nudge a resize so its <svg> re-reads the viewport
            // size (the sketch canvas has no ResizeObserver). Preserves zoom/pan (only re-measures).
            try { await handle.InvokeVoidAsync("resize"); } catch { }
        }
    }

    // Layout pushed from the bridge (OnLayout) + the current selection (OnShapeSelected/OnIslandSelected).
    private List<SketchIslandRow> islands = [];
    private List<SketchShapeRow> shapes = [];
    private List<LibraryItem> libraryItems = [];
    private List<SketchLayerRow> layerRows = [];
    private string? activeLayerId;
    private string? selectedShapeId;
    private string? selectedIslandId;
    private int selectedVertexIdx = -1;
    private double selectedVertexHeight;

    private SketchShapeRow? SelectedShape => shapes.FirstOrDefault(s => s.Id == selectedShapeId);
    private SketchIslandRow? SelectedIsland => islands.FirstOrDefault(i => i.Id == selectedIslandId);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        handle = await JS.InvokeAsync<IJSObjectReference>("studio.mountSketch", svgRef, wrapRef, coordsRef, zoomRef, dimRef, selfRef);
        try { libraryItems = await handle.InvokeAsync<List<LibraryItem>>("getLibrary"); StateHasChanged(); } catch { /* palette stays empty */ }
        // Restore the saved layout (empty {} for a fresh sketch); the bridge handles an empty state.
        try
        {
            var state = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/sketch");
            await handle.InvokeVoidAsync("load", state);
            // Sync the Setup controls with the loaded setup (the canvas already uses it).
            if (state.ValueKind == JsonValueKind.Object && state.TryGetProperty("setup", out var su)
                && su.ValueKind == JsonValueKind.Object)
            {
                if (su.TryGetProperty("mirror_mode", out var mm) && mm.GetString() is { Length: > 0 } m) mode = m;
                if (su.TryGetProperty("bbox", out var bb) && bb.ValueKind == JsonValueKind.Object
                    && bb.TryGetProperty("min_x", out var mnx) && bb.TryGetProperty("max_x", out var mxx)
                    && bb.TryGetProperty("min_z", out var mnz) && bb.TryGetProperty("max_z", out var mxz))
                {
                    width = mxx.GetDouble() - mnx.GetDouble();
                    depth = mxz.GetDouble() - mnz.GetDouble();
                    preset = InferPreset(width, depth);
                }
                if (su.TryGetProperty("center", out var ce) && ce.ValueKind == JsonValueKind.Object)
                {
                    if (ce.TryGetProperty("cx", out var cxv)) centerX = cxv.GetDouble();
                    if (ce.TryGetProperty("cz", out var czv)) centerZ = czv.GetDouble();
                }
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

    private async Task OnPresetChange(ChangeEventArgs e)
    {
        preset = e.Value?.ToString() ?? "landscape";
        if (Presets.TryGetValue(preset, out var p)) { width = p.W; depth = p.D; await PushBbox(); }
    }

    private async Task OnWidth(double v) { width = v; preset = InferPreset(width, depth); await PushBbox(); }

    private async Task OnDepth(double v) { depth = v; preset = InferPreset(width, depth); await PushBbox(); }

    // The working frame is centred at the origin (the symmetry centre defaults there); the symmetry
    // centre is set separately and does not move the frame.
    private async Task PushBbox()
    {
        double hx = width / 2, hz = depth / 2;
        if (handle is not null)
            await handle.InvokeVoidAsync("setBbox", new { min_x = -hx, max_x = hx, min_z = -hz, max_z = hz });
    }

    private static string InferPreset(double w, double d) =>
        Presets.FirstOrDefault(kv => kv.Value.W == w && kv.Value.D == d).Key ?? "custom";

    private async Task OnCenterX(double v)
    {
        centerX = v;
        if (handle is not null) await handle.InvokeVoidAsync("setCenter", centerX, centerZ);
    }

    private async Task OnCenterZ(double v)
    {
        centerZ = v;
        if (handle is not null) await handle.InvokeVoidAsync("setCenter", centerX, centerZ);
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

    private async Task ToggleSnap()
    {
        snapOn = !snapOn;
        if (handle is not null) await handle.InvokeVoidAsync("setSnap", snapOn);
    }

    private async Task OnFit()
    {
        if (handle is not null) await handle.InvokeVoidAsync("fitToBbox");
    }

    private async Task Toggle3D()
    {
        if (isoUnavailable) return;
        threeD = !threeD;
        if (handle is null) return;
        // The bridge reports an unavailable preview asynchronously via OnIsoUnavailable; this catch only
        // guards a hard interop failure so the toggle can never trip Blazor's unhandled-error boundary.
        try { await handle.InvokeVoidAsync("setView", threeD ? "iso" : "2d"); }
        catch { threeD = false; isoUnavailable = true; StateHasChanged(); }
    }

    private Task RotateIso() => handle?.InvokeVoidAsync("rotateIso").AsTask() ?? Task.CompletedTask;

    private Task SetHeight((string Id, double Base, double Floor) e)
        => handle?.InvokeVoidAsync("setHeight", e.Id, e.Base, e.Floor).AsTask() ?? Task.CompletedTask;

    private Task SetVertexHeight((string Id, int Idx, double Height) e)
    {
        // Keep the inspector's bound value in sync (the bridge doesn't echo it back) so the field shows
        // the committed height rather than reverting to the value from when the vertex was selected.
        selectedVertexHeight = e.Height;
        return handle?.InvokeVoidAsync("setVertexHeight", e.Id, e.Idx, e.Height).AsTask() ?? Task.CompletedTask;
    }

    // ── Panel / inspector actions → the JS bridge ──────────────────────────────

    private Task SelectShape(string id) => handle?.InvokeVoidAsync("selectShape", id).AsTask() ?? Task.CompletedTask;
    private Task SelectIsland(string id) => handle?.InvokeVoidAsync("selectIsland", id).AsTask() ?? Task.CompletedTask;
    private Task Rotate(double deg) => handle?.InvokeVoidAsync("rotateSelected", deg).AsTask() ?? Task.CompletedTask;
    private Task ToggleOp(string id) => handle?.InvokeVoidAsync("toggleOp", id).AsTask() ?? Task.CompletedTask;
    private Task ToggleOverride(string id) => handle?.InvokeVoidAsync("toggleOverride", id).AsTask() ?? Task.CompletedTask;
    private Task DeleteShape(string id) => handle?.InvokeVoidAsync("deleteShape", id).AsTask() ?? Task.CompletedTask;
    private Task PromoteShape(string id) => handle?.InvokeVoidAsync("promoteShape", id).AsTask() ?? Task.CompletedTask;
    private Task ArmPlace(string itemId) => handle?.InvokeVoidAsync("armPlace", itemId).AsTask() ?? Task.CompletedTask;

    // ── Layer panel actions (S7b) ──────────────────────────────────────────────
    private Task SelectLayer(string id) => handle?.InvokeVoidAsync("switchLayer", id).AsTask() ?? Task.CompletedTask;
    private Task AddLayer() => handle?.InvokeVoidAsync("addLayer").AsTask() ?? Task.CompletedTask;
    private Task DeleteLayer(string id) => handle?.InvokeVoidAsync("deleteLayer", id).AsTask() ?? Task.CompletedTask;
    private Task RenameLayer((string Id, string Name) e) => handle?.InvokeVoidAsync("renameLayer", e.Id, e.Name).AsTask() ?? Task.CompletedTask;
    private Task SetLayerBaseY((string Id, double BaseY) e) => handle?.InvokeVoidAsync("setLayerBaseY", e.Id, e.BaseY).AsTask() ?? Task.CompletedTask;
    private Task ToggleMirrors(string islandId) => handle?.InvokeVoidAsync("toggleMirrors", islandId).AsTask() ?? Task.CompletedTask;
    private Task RenameIsland((string Id, string Name) e) => handle?.InvokeVoidAsync("renameIsland", e.Id, e.Name).AsTask() ?? Task.CompletedTask;

    // ── Bridge callbacks ───────────────────────────────────────────────────────

    /// <summary>A shape was selected on the canvas/panel (null = deselected).</summary>
    [JSInvokable]
    public void OnShapeSelected(string? id) { selectedShapeId = id; selectedVertexIdx = -1; StateHasChanged(); }

    /// <summary>A polygon vertex was click-selected on the canvas (null shapeId = cleared).</summary>
    [JSInvokable]
    public void OnVertexSelected(string? shapeId, int idx, double height)
    {
        selectedVertexIdx = shapeId is null ? -1 : idx;
        selectedVertexHeight = height;
        StateHasChanged();
    }

    /// <summary>An island was selected in the panel (null = deselected).</summary>
    [JSInvokable]
    public void OnIslandSelected(string? id) { selectedIslandId = id; StateHasChanged(); }

    /// <summary>The bridge couldn't initialise the read-only 3-D preview (WebGL unavailable, or the
    /// preview module failed to load); fall back to 2-D and disable the toggle.</summary>
    [JSInvokable]
    public void OnIsoUnavailable()
    {
        threeD = false;
        isoUnavailable = true;
        StateHasChanged();
    }

    /// <summary>The bridge pushed the current island→shape tree (on every layout change).</summary>
    [JSInvokable]
    public void OnLayout(string json)
    {
        var dto = JsonSerializer.Deserialize<SketchLayoutDto>(json);
        islands = dto?.Islands ?? [];
        shapes = dto?.Shapes ?? [];
        StateHasChanged();
    }

    /// <summary>The bridge pushed the layer list + active id (on layer add/switch/delete/edit).</summary>
    [JSInvokable]
    public void OnLayers(string json)
    {
        var dto = JsonSerializer.Deserialize<SketchLayersDto>(json);
        layerRows = dto?.Layers ?? [];
        activeLayerId = dto?.Active;
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

    // ── Finish: flush the layout, rasterize server-side, continue to Configure ──

    private bool finishing;
    private string? finishError;

    private async Task Finish()
    {
        if (handle is null) return;
        finishing = true;
        finishError = null;
        StateHasChanged();

        saveCts?.Cancel();          // flush the latest layout before the server rasterizes it
        await SaveAsync(CancellationToken.None);

        try
        {
            var resp = await Http.PostAsync($"api/map/{Slug}/sketch/finish", null);
            if (resp.IsSuccessStatusCode)
            {
                // Land back on the Configure overview (the draft is now a configure-stage map) and offer to
                // continue into the wizard there — rather than force-marching straight into it.
                Nav.NavigateTo($"maps?stage=configure&just={Slug}");
                return;
            }
            var err = await resp.Content.ReadFromJsonAsync<JsonElement>();
            finishError = err.TryGetProperty("error", out var e) ? e.GetString() : "Finish failed.";
        }
        catch { finishError = "Finish failed."; }

        finishing = false;
        StateHasChanged();
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
