using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Sketch;

public partial class SketchTool
{
    [Parameter] public string Slug { get; set; } = "";

    private ElementReference svgRef, wrapRef;
    /// <summary>The floating top-left readout. Its three elements are handed to the canvas on mount, which
    /// then writes cursor / size / zoom into them directly — per mousemove, far too often to render.</summary>
    private CanvasReadout? readout;
    private IJSObjectReference? handle;
    private DotNetObjectReference<SketchTool>? selfRef;

    private string tool = "move";
    private string op = "add";
    private string mode = "rot_180";
    private double centerX = 0;     // symmetry centre
    private double centerZ = 0;
    private bool mirrorOn = true;
    private bool shapesOn = false;
    private bool chunksOn = true;
    private bool blocksOn = false;   // S23: the rasterized block-footprint preview
    private bool snapOn = true;
    private bool threeD = false;
    private bool isoUnavailable = false;   // 3-D preview couldn't initialise (no WebGL / module load failed)
    private string islandLabel = "";

    // ── Phases (rail): Info (Identity + Settings steps) · Draw (the canvas). Draw stays mounted while
    //    Info is up (hidden, not torn down) so the drawing state + zoom survive the trip. ──
    [SupplyParameterFromQuery] public string? Phase { get; set; }
    private string active = "draw";
    private bool InfoActive => active == "info";
    private bool DrawActive => active == "draw";
    private Task GoInfo() => SetPhase("info");
    private Task GoDraw() => SetPhase("draw");

    // ── Theme phase: the map's whole finish, in three steps. Create (author terrain themes) and Apply (assign
    //    them on the island tree) are the paint; Rooms binds the shells the stamped wool cages and spawn cubes
    //    take (structures.md §9). Apply reuses the live canvas, so its body stays mounted like Draw; the other
    //    two are their own bodies. ──
    private string themeStep = "create";
    private bool ThemeCreateActive => active == "theme" && themeStep == "create";
    private bool ThemeApplyActive => active == "theme" && themeStep == "apply";
    private bool ThemeRoomsActive => active == "theme" && themeStep == "rooms";
    private static readonly string[] ThemeSteps = ["Create", "Apply", "Rooms"];
    private Task GoTheme() { themeStep = "create"; return SetPhase("theme"); }
    private Task GoThemeRooms() { themeStep = "rooms"; StateHasChanged(); return Task.CompletedTask; }
    /// <summary>A step click from a body other than the one it names: 1 is the canvas mode, 2 the rooms body,
    /// anything else Create.</summary>
    private Task GoThemeStep(int step) => step switch
    {
        1 => EnterThemeApply(),
        2 => GoThemeRooms(),
        _ => BackToThemeCreate(),
    };
    private async Task EnterThemeApply()
    {
        themeStep = "apply";
        tool = "select";
        StateHasChanged();
        // Redundant with SetPhase's own call — kept because this step is the one that shows the canvas, and
        // it must be in the right mode by the time it does, however it was reached.
        await PushCanvasMode("theme");
    }
    private Task BackToThemeCreate() { themeStep = "create"; StateHasChanged(); return Task.CompletedTask; }

    // ── Dressing phase (decoration.md) ──
    // One step, not two. Dressing has nothing to define up front: every part of it is a thing put somewhere,
    // so the phase is the canvas with its own placing tools and an inspector for whatever is under the cursor.
    // The Theme phase keeps its create/apply split because a theme genuinely is a recipe authored once.
    private bool DressingActive => active == "dressing";
    private string dressingJson = "";
    private Task GoDressing() { tool = DressingTools.Tree; return SetPhase("dressing"); }

    /// <summary>Whether the canvas is being used to place a scope rather than to draw — today only Theme's
    /// apply step, which selects shapes it does not edit.</summary>
    private bool ScopeApplyActive => ThemeApplyActive;

    // The select tool edits as well as selects in Draw; in the Theme phase it only selects, so it says so.
    private string SelectToolTitle => ScopeApplyActive ? "Select" : "Select / edit";

    /// <summary>The tools that make a shape, and so the only ones the operation decides anything for. Measure
    /// reads, split cuts what is already there, and move/select do not draw at all — with one of those armed
    /// the operation is set but idle, which is what the pill dims to say.</summary>
    private static readonly string[] DrawTools = ["rectangle", "polygon", "lasso"];

    private bool DrawToolActive => DrawTools.Contains(tool);

    private bool Carving => op == "subtract";

    // The operation is a property of what is about to be drawn, so the control names what the next shape will
    // do rather than naming the set operation — and it is one control showing one state, not a pair of them
    // showing the same state twice.
    private string OpTitle =>
        (Carving ? "Carving — the next shape cuts land away. Click to build instead."
                 : "Building — the next shape adds land. Click to carve instead.")
        + (DrawToolActive ? "" : " Applies once a rectangle, polygon or lasso is armed.");

    private Task ToggleOperation() => SetOperation(Carving ? "add" : "subtract");

    /// <summary>The colour the draw group wears — the same fill the canvas gives a shape of that operation, so
    /// the palette states what the drawing will look like. Dimmed while the armed tool draws nothing: the
    /// operation is set but decides nothing until a rectangle, polygon or lasso is in hand.</summary>
    private string OpAccent => DrawToolActive
        ? (Carving ? "var(--canvas-sub-fill)" : "var(--canvas-add-fill)")
        : $"color-mix(in oklch, var(--canvas-{(Carving ? "sub" : "add")}-fill) 45%, var(--text-muted))";

    // The same toggle shows the bare voxelization while drawing and the paint on top of it once theming.
    private string BlocksChipTitle => ScopeApplyActive
        ? "Show the blocks the export places — the rasterized footprint painted by its themes"
        : "Show the rasterized block footprint — the exact cells the shapes voxelize into";

    // The shapes the current selection themes: an island's members, else the single selected shape, else none.
    private IReadOnlyList<string> ScopeTargetShapeIds =>
        selectedIslandId is not null ? (SelectedIsland?.ShapeIds ?? (IReadOnlyList<string>)[])
        : selectedShapeId is not null ? new[] { selectedShapeId }
        : [];

    // A freshly-created sketch lands on Info (?phase=info) to name it; opening an existing one goes
    // straight to Draw.
    protected override void OnInitialized() { if (Phase == "info") active = "info"; }

    // Switching phases only flips which body renders: the canvas observes its own wrap and re-measures
    // (and runs a deferred fit) when Draw un-hides. Nudging it from here would run against the still-
    // hidden DOM — this method returns before the phase div is re-rendered.
    private async Task SetPhase(string p)
    {
        active = p;
        await PushCanvasMode(p);
    }

    // What the canvas is in a given phase, pushed from the one place a phase changes so no route in or out can
    // leave the wrong mode behind. Editing geometry belongs to Draw alone, so every other phase gets the canvas
    // as a selection surface; and only Theme previews the finished paint, because while the shapes are still
    // being drawn the Blocks overlay should show the voxelization it is there to show, not a finishing pass.
    private async Task PushCanvasMode(string phase)
    {
        if (handle is null) return;
        // Geometry is Draw's alone. Dressing places props rather than shapes, so it is not select-only in the
        // theme sense — its own tools are armed instead, and the shape tools are simply not offered.
        await handle.InvokeVoidAsync("setSelectOnly", phase == "theme");
        // Both finishing phases show the paint: Theme is authoring it, and Dressing is placing things on it,
        // which is a judgement about the finish as much as about the planting.
        await handle.InvokeVoidAsync("setPaintPreview", phase is "theme" or "dressing");
        await handle.InvokeVoidAsync("setDressingMode", phase == "dressing");
    }

    // Layout pushed from the bridge (OnLayout) + the current selection (OnShapeSelected/OnIslandSelected).
    private List<SketchIslandRow> islands = [];
    private List<SketchShapeRow> shapes = [];
    private List<SketchLayerRow> layerRows = [];
    private string? activeLayerId;
    private string? selectedShapeId;
    private string? selectedIslandId;
    private int selectedVertexIdx = -1;
    private double selectedVertexHeight;
    private List<SketchSlopeControl> slopeControls = [];   // shift-marked surface-slope controls (2–3)

    private SketchShapeRow? SelectedShape => shapes.FirstOrDefault(s => s.Id == selectedShapeId);
    private SketchIslandRow? SelectedIsland => islands.FirstOrDefault(i => i.Id == selectedIslandId);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        handle = await JS.InvokeAsync<IJSObjectReference>(
            "studio.mountSketch", svgRef, wrapRef, readout!.Cursor, readout.Zoom, readout.Size, selfRef, Slug);
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

    private async Task ToggleBlocks()
    {
        blocksOn = !blocksOn;
        if (handle is not null) await handle.InvokeVoidAsync("setBlocksVisible", blocksOn);
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

    private Task SetPathBand((string Id, double Radius, string Edge, int Seed) e)
        => handle?.InvokeVoidAsync("setPathBand", e.Id, e.Radius, e.Edge, e.Seed).AsTask() ?? Task.CompletedTask;

    private Task SetVertexHeight((string Id, int Idx, double Height) e)
    {
        // Keep the inspector's bound value in sync (the bridge doesn't echo it back) so the field shows
        // the committed height rather than reverting to the value from when the vertex was selected.
        selectedVertexHeight = e.Height;
        return handle?.InvokeVoidAsync("setVertexHeight", e.Id, e.Idx, e.Height).AsTask() ?? Task.CompletedTask;
    }

    // Edit one slope control's height in place (before Apply) — no bridge call, just the local model the
    // inspector's per-control input binds to.
    private void SetSlopeHeight((int Idx, double Height) e)
    {
        var control = slopeControls.FirstOrDefault(c => c.Idx == e.Idx);
        if (control is not null) control.Height = e.Height;
    }

    // Fit the surface plane through the marked controls (with their edited heights) and fill every vertex.
    private Task ApplySlope()
        => handle is null || selectedShapeId is null || slopeControls.Count < 2
            ? Task.CompletedTask
            : handle.InvokeVoidAsync("applySlope", selectedShapeId, JsonSerializer.Serialize(slopeControls)).AsTask();

    // ── Panel / inspector actions → the JS bridge ──────────────────────────────

    private Task SelectShape(string id) => handle?.InvokeVoidAsync("selectShape", id).AsTask() ?? Task.CompletedTask;
    private Task SelectIsland(string id) => handle?.InvokeVoidAsync("selectIsland", id).AsTask() ?? Task.CompletedTask;
    private Task Rotate(double deg) => handle?.InvokeVoidAsync("rotateSelected", deg).AsTask() ?? Task.CompletedTask;
    private Task ToggleOp(string id) => handle?.InvokeVoidAsync("toggleOp", id).AsTask() ?? Task.CompletedTask;
    private Task ToggleOverride(string id) => handle?.InvokeVoidAsync("toggleOverride", id).AsTask() ?? Task.CompletedTask;
    private Task DeleteShape(string id) => handle?.InvokeVoidAsync("deleteShape", id).AsTask() ?? Task.CompletedTask;
    private Task PromoteShape(string id) => handle?.InvokeVoidAsync("promoteShape", id).AsTask() ?? Task.CompletedTask;

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
    public void OnShapeSelected(string? id) { selectedShapeId = id; selectedVertexIdx = -1; slopeControls = []; StateHasChanged(); }

    /// <summary>The shift-marked surface-slope control set changed on the canvas — each entry is a vertex index
    /// + its current height, which the inspector lets the author edit before fitting the plane.</summary>
    [JSInvokable]
    public void OnSlopeControls(string? shapeId, string json)
    {
        slopeControls = shapeId is null ? [] : (JsonSerializer.Deserialize<List<SketchSlopeControl>>(json) ?? []);
        StateHasChanged();
    }

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

    /// <summary>The theme registry / assignments changed on the bridge — re-render so the Apply rail refreshes.</summary>
    [JSInvokable]
    public void OnThemes(string json) => StateHasChanged();

    /// <summary>The placed dressing or its selection changed on the canvas — the props, the selected id, and
    /// the selected prop itself, which is what the inspector and the list both read.</summary>
    [JSInvokable]
    public void OnDressing(string json) { dressingJson = json; StateHasChanged(); }

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
        // A draft left with nothing drawn is discarded so an abandoned "New sketch" click doesn't linger on
        // the dashboard. The client gates on empty geometry to skip the call for real work; the server
        // re-checks the full pristine condition (default name, no authors, no shapes) before deleting.
        if (shapes.Count == 0 && islands.Count == 0)
        {
            try { await Http.DeleteAsync($"api/map/{Slug}/sketch/discard-if-empty"); } catch { }
        }
        if (handle is not null)
        {
            try { await handle.InvokeVoidAsync("dispose"); } catch { }
            try { await handle.DisposeAsync(); } catch { }
        }
        selfRef?.Dispose();
    }
}
