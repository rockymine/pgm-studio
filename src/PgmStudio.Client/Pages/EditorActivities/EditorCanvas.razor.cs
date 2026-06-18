using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class EditorCanvas
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public string? Category { get; set; }
    [Parameter] public EventCallback<string?> OnSelect { get; set; }

    /// <summary>When set, draw tools are enabled and a drawn shape creates a region in this category
    /// (C5). Null = read-only (move/select only), e.g. the Regions browser.</summary>
    [Parameter] public string? DrawCategory { get; set; }
    /// <summary>The editor step (teams/objective/build) freshly drawn regions are tagged with so they show
    /// in this activity until they're wired (E10). Also rendered on the canvas alongside the category set.</summary>
    [Parameter] public string? DraftStep { get; set; }
    /// <summary>Fired after a drawn region is created (and the canvas reloaded) so the host activity
    /// can refresh its sidebar tree/list.</summary>
    [Parameter] public EventCallback OnRegionCreated { get; set; }
    /// <summary>Fired when a region's footprint is changed by a resize drag — the host persists it and
    /// refreshes its inspector. Args: (region id, new min/max x/z).</summary>
    [Parameter] public EventCallback<(string Id, double MinX, double MinZ, double MaxX, double MaxZ)> OnGeometrySaved { get; set; }

    /// <summary>World authoring: clicks pick an island (not a region), the Blocks layer toggle is hidden,
    /// and only the island base layer shows. Fires <see cref="OnIslandSelect"/> on a canvas click.</summary>
    [Parameter] public bool IslandSelect { get; set; }
    /// <summary>Fired when a canvas click selects an island (null = clicked empty space).</summary>
    [Parameter] public EventCallback<int?> OnIslandSelect { get; set; }
    /// <summary>World · Symmetry: base layer only (Blocks toggle hidden); the host drives the axis/centre
    /// overlay via <see cref="SetSymmetryAsync"/>.</summary>
    [Parameter] public bool SymmetryMode { get; set; }
    /// <summary>Teams · Spawn: the point tool reports the clicked world point via <see cref="OnPointPick"/>
    /// (spawn placement); the host renders the placed spawns as point dummy regions
    /// (<see cref="SetAuthorRegionsAsync"/>), picked by the normal select hit-test (<see cref="OnSelect"/>).</summary>
    [Parameter] public bool PointPick { get; set; }
    /// <summary>Fired with the clicked world (x, z) when the point tool places a spawn (point-pick mode).</summary>
    [Parameter] public EventCallback<(double X, double Z)> OnPointPick { get; set; }
    /// <summary>Configure authoring: show a rectangle draw tool whose completed shape is reported via
    /// <see cref="OnRectDrawn"/> as raw geometry (no region is created) — the host writes it to the
    /// intent and renders it back as a dummy region via <see cref="SetAuthorRegionsAsync"/>.</summary>
    [Parameter] public bool RectDraw { get; set; }
    /// <summary>Fired with a drawn rectangle's footprint (RectDraw mode); the host persists it to intent.</summary>
    [Parameter] public EventCallback<(double MinX, double MinZ, double MaxX, double MaxZ)> OnRectDrawn { get; set; }
    /// <summary>Fired once the canvas is mounted + the map is loaded, so a host can apply initial state
    /// (e.g. the excluded-island set) that only takes effect after the islands are rendered.</summary>
    [Parameter] public EventCallback OnReady { get; set; }

    private ElementReference svgRef, wrapRef, coordsRef, zoomRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<EditorCanvas>? selfRef;
    private string tool = "move";

    /// <summary>Island ids (from /islands) offered in the "fit island" dropdown; empty hides it.</summary>
    private List<int> islandIds = new();
    /// <summary>Bound select value; reset to "" after a fit so the same island can be re-picked.</summary>
    private string islandSel = "";

    protected override async Task OnInitializedAsync()
    {
        // Island-select clicks pick an island via the Select tool. Point-pick (spawn placement) leads
        // with the Point tool so the first click drops a spawn; Select is then used to pick markers.
        // Either way, default off Move so the first clicks register instead of panning.
        if (IslandSelect) tool = "select";
        else if (PointPick) tool = "point";
        else if (RectDraw) tool = "rectangle";   // lead with the rectangle tool so the first drag draws

        // Islands power the "fit island" zoom control (any activity, if the map has scan data).
        try
        {
            var isl = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/islands");
            if (isl.ValueKind == JsonValueKind.Array)
                islandIds = isl.EnumerateArray()
                    .Where(e => e.TryGetProperty("id", out var v) && v.ValueKind == JsonValueKind.Number)
                    .Select(e => e.GetProperty("id").GetInt32()).ToList();
        }
        catch { islandIds = new(); }

        // The orbit toggle only applies while drawing, and only on maps with a confirmed symmetry — fetch
        // the primary mode so the toolbar chip can label itself ("Orbit 90" / "Orbit x" …). Default: on.
        if (DrawCategory is null) return;
        try
        {
            var sym = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/symmetry");
            if (sym.TryGetProperty("status", out var st) && st.GetString() == "confirmed"
                && sym.TryGetProperty("primary", out var pr) && pr.ValueKind == JsonValueKind.Object
                && pr.TryGetProperty("type", out var ty))
                orbitMode = ty.GetString();
        }
        catch { orbitMode = null; }   // no symmetry artifact / asymmetric map → no orbit chip
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (firstRender)
        {
            selfRef = DotNetObjectReference.Create(this);
            handle = await JS.InvokeAsync<IJSObjectReference>(
                "studio.mountCanvas", svgRef, wrapRef, coordsRef, zoomRef, selfRef, Slug, Category, DraftStep);
            if (IslandSelect)
            {
                await handle.InvokeVoidAsync("setIslandSelect", true);
                await handle.InvokeVoidAsync("setTool", "select");   // mount defaults to "move"; islands are click-to-pick
            }
            if (PointPick)
            {
                await handle.InvokeVoidAsync("setPointPick", true);
                await handle.InvokeVoidAsync("setTool", "point");   // mount defaults to "move"; lead with the placement tool
            }
            if (RectDraw)
                await handle.InvokeVoidAsync("setTool", "rectangle");   // mount defaults to "move"; lead with the draw tool
            await OnReady.InvokeAsync();
        }
    }

    /// <summary>F3: the map's confirmed symmetry mode (e.g. "rot_90"), or null when no orbit is available.</summary>
    private string? orbitMode;
    /// <summary>F3: whether a drawn region is mirrored into its symmetry orbit. Toggled from the toolbar.</summary>
    private bool orbitOn = true;

    private void ToggleOrbit() => orbitOn = !orbitOn;

    /// <summary>Toolbar label for the orbit chip — "Orbit 90" / "Orbit 180" / "Orbit x" / "Orbit z" …</summary>
    private string OrbitLabel() => orbitMode switch
    {
        "rot_90" => "Orbit 90",
        "rot_180" => "Orbit 180",
        { } m when m.StartsWith("mirror_") => $"Orbit {m["mirror_".Length..]}",
        _ => "Orbit",
    };

    private async Task SetTool(string t)
    {
        tool = t;
        if (handle is not null) await handle.InvokeVoidAsync("setTool", t);
    }

    private bool blocksOn;

    /// <summary>C6: toggle the top-surface block-colour overlay. Stays off if the map has no scan data.</summary>
    private async Task ToggleBlocks()
    {
        if (handle is null) return;
        var ok = await handle.InvokeAsync<bool>("setBlocks", !blocksOn);
        if (ok) blocksOn = !blocksOn;
    }

    /// <summary>Highlight the given region ids on the canvas (called by the activity when the sidebar selects).</summary>
    public async Task SetSelectionAsync(IEnumerable<string> ids)
    {
        // Box the array as a single object — InvokeVoidAsync's `params object?[]` would otherwise
        // spread a string[] into separate JS arguments (the canvas would get one string, not the list).
        if (handle is not null) await handle.InvokeVoidAsync("setSelection", (object)ids.ToArray());
    }

    public async Task ResizeAsync()
    {
        if (handle is not null) await handle.InvokeVoidAsync("resize");
    }

    /// <summary>Pan/zoom the canvas so an island's bounding box fills the viewport (with a little padding).</summary>
    public async Task FitIslandAsync(int islandId)
    {
        if (handle is not null) await handle.InvokeVoidAsync("fitIsland", islandId);
    }

    /// <summary>Reset pan/zoom to the default whole-map view.</summary>
    public async Task ResetViewAsync()
    {
        if (handle is not null) await handle.InvokeVoidAsync("resetView");
    }

    // The dropdown reflects the currently-focused island; the reset button clears it back to the
    // whole-map view (clearing islandSel here makes the bound select actually snap to the placeholder).
    private async Task OnResetClick()
    {
        islandSel = "";
        await ResetViewAsync();
    }

    private async Task OnFitIslandSelect(ChangeEventArgs e)
    {
        islandSel = e.Value?.ToString() ?? "";
        if (int.TryParse(islandSel, out var id)) await FitIslandAsync(id);
    }

    /// <summary>Pan/zoom the canvas so a world bounding box fills the viewport (with a little padding).</summary>
    public async Task FitBoundsAsync(double minX, double minZ, double maxX, double maxZ)
    {
        if (handle is not null) await handle.InvokeVoidAsync("fitBounds", minX, minZ, maxX, maxZ);
    }

    /// <summary>Re-fetch the region tree and re-render the canvas geometry — call after a server-side
    /// mutation (delete / group / ungroup / rename) so the drawn shapes match the data. `setSelection`
    /// only repaints highlights on the cached dataset, so it can't drop a deleted region on its own.
    /// No-op until the canvas is mounted (it self-loads on mount).</summary>
    public async Task ReloadAsync()
    {
        if (handle is not null) await handle.InvokeVoidAsync("load", Slug);
    }

    [JSInvokable] public Task OnCanvasSelect(string? id) => OnSelect.InvokeAsync(id);

    /// <summary>Canvas island pick (World authoring step) → host.</summary>
    [JSInvokable] public Task OnCanvasIslandSelect(int? id) => OnIslandSelect.InvokeAsync(id);

    /// <summary>Highlight the given island with an accent border (null clears it).</summary>
    public async Task SetSelectedIslandAsync(int? id)
    {
        if (handle is not null) await handle.InvokeVoidAsync("setSelectedIsland", id);
    }

    /// <summary>Dim the excluded islands on the canvas.</summary>
    public async Task SetExcludedIslandsAsync(IEnumerable<int> ids)
    {
        if (handle is not null) await handle.InvokeVoidAsync("setExcludedIslands", (object)ids.ToArray());
    }

    /// <summary>Show the symmetry axis/centre overlay (type null clears it).</summary>
    public async Task SetSymmetryAsync(string? type, double cx, double cz)
    {
        if (handle is not null) await handle.InvokeVoidAsync("setSymmetry", type, cx, cz);
    }

    /// <summary>Tint islands by team — a map of island id (string) → team colour hex.</summary>
    public async Task SetIslandTeamsAsync(IReadOnlyDictionary<string, string> idToHex)
    {
        if (handle is not null) await handle.InvokeVoidAsync("setIslandTeams", idToHex);
    }

    /// <summary>Pick the raw clicked world point (point-pick mode, point tool) → host.</summary>
    [JSInvokable] public Task OnCanvasPointPick(double x, double z) => OnPointPick.InvokeAsync((x, z));

    /// <summary>Render intent-backed dummy regions (e.g. spawn-protection rects) — each
    /// { id, type, label, color, bounds:{min_x,min_z,max_x,max_z} }. Selectable + resizable like real regions.</summary>
    public async Task SetAuthorRegionsAsync(IEnumerable<object> nodes)
    {
        if (handle is not null) await handle.InvokeVoidAsync("setAuthorRegions", (object)nodes.ToArray());
    }

    /// <summary>A region's footprint was changed by a resize drag (the canvas already shows it live).
    /// The host persists it — PATCH region bounds on the Edit page, or patch the intent slice in the
    /// Configure wizard — and refreshes its inspector. Args: region id + new {min,max}{x,z}.</summary>
    [JSInvokable]
    public Task OnBoundsSave(string id, JsonElement bounds)
    {
        double N(string k) => bounds.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
        return OnGeometrySaved.InvokeAsync((id, N("min_x"), N("min_z"), N("max_x"), N("max_z")));
    }

    /// <summary>Push a region's new footprint to the canvas after an inspector edit (re-renders just
    /// that shape; no zoom reset).</summary>
    public async Task RefreshRegionBoundsAsync(string id, IReadOnlyDictionary<string, double> bounds)
    {
        if (handle is not null) await handle.InvokeVoidAsync("refreshRegionBounds", id, bounds);
    }

    /// <summary>C5: a draw tool completed a shape → create the region, fill its symmetry orbit (F3),
    /// reload the canvas, notify the host.</summary>
    [JSInvokable]
    public async Task OnRegionDraw(JsonElement draw)
    {
        // RectDraw mode (Configure authoring): report the rectangle's geometry to the host instead of
        // creating a region — the host writes it to intent and renders it back as a dummy region.
        if (RectDraw && OnRectDrawn.HasDelegate)
        {
            double N(string k) => draw.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
            await OnRectDrawn.InvokeAsync((N("min_x"), N("min_z"), N("max_x"), N("max_z")));
            await SetTool("select");   // switch to select so the drawn rect can be picked + resized
            StateHasChanged();
            return;
        }
        if (DrawCategory is null || handle is null) return;
        var resp = await Http.PostAsJsonAsync($"api/map/{Slug}/regions", BuildPayload(draw, DrawCategory, DraftStep));
        if (!resp.IsSuccessStatusCode) return;

        // F3: when the orbit toggle is on, create the source's counterpart(s) so the drawn region appears
        // in every symmetric position. No-op (server-side) on asymmetric maps; skipped entirely when off.
        if (orbitOn)
        {
            var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (created.TryGetProperty("id", out var idEl) && idEl.GetString() is { } newId)
                await Http.PostAsJsonAsync($"api/map/{Slug}/regions/{newId}/orbit", new { category = DrawCategory, draft_step = DraftStep });
        }

        await SetTool("select");
        StateHasChanged();                              // refresh the toolbar highlight (JSInvokable won't auto-render)
        await handle.InvokeVoidAsync("load", Slug);     // re-render the canvas with the new region(s)
        await OnRegionCreated.InvokeAsync();
    }

    // Convert an EditorCanvas drawResult into a createRegion payload (port of drawResultToPayload).
    private static Dictionary<string, object?> BuildPayload(JsonElement d, string category, string? draftStep)
    {
        double N(string k) => d.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
        var type = d.TryGetProperty("type", out var t) ? t.GetString() ?? "rectangle" : "rectangle";
        var p = new Dictionary<string, object?> { ["category"] = category, ["type"] = type };
        if (!string.IsNullOrEmpty(draftStep)) p["draft_step"] = draftStep;
        switch (type)
        {
            case "cylinder": p["base_x"] = N("base_x"); p["base_y"] = 0; p["base_z"] = N("base_z"); p["radius"] = N("radius"); p["height"] = 10; break;
            case "circle": p["center_x"] = N("center_x"); p["center_z"] = N("center_z"); p["radius"] = N("radius"); break;
            case "point" or "block": p["x"] = N("min_x") + 0.5; p["y"] = 0; p["z"] = N("min_z") + 0.5; break;
            default: p["min_x"] = N("min_x"); p["min_z"] = N("min_z"); p["max_x"] = N("max_x"); p["max_z"] = N("max_z"); break;   // rectangle, cuboid
        }
        return p;
    }

    public async ValueTask DisposeAsync()
    {
        if (handle is not null)
        {
            try { await handle.InvokeVoidAsync("dispose"); } catch { }
            try { await handle.DisposeAsync(); } catch { }
        }
        selfRef?.Dispose();
    }
}
