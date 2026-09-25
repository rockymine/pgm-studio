using PgmStudio.Contracts;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Configure;

public partial class WorldCanvas
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public string? Category { get; set; }
    [Parameter] public EventCallback<string?> OnSelect { get; set; }

    /// <summary>Fired when a region's footprint is changed by a resize drag — the host persists it and
    /// refreshes its inspector. Args: (region id, new min/max x/z).</summary>
    [Parameter] public EventCallback<(string Id, double MinX, double MinZ, double MaxX, double MaxZ)> OnGeometrySaved { get; set; }

    /// <summary>World authoring: clicks pick an island (not a region) over the island base layer. Fires
    /// <see cref="OnIslandSelect"/> on a canvas click.</summary>
    [Parameter] public bool IslandSelect { get; set; }
    /// <summary>Fired when a canvas click selects an island (null = clicked empty space).</summary>
    [Parameter] public EventCallback<int?> OnIslandSelect { get; set; }
    /// <summary>World · Symmetry: the host drives the axis/centre overlay via <see cref="SetSymmetryAsync"/>
    /// over the island base layer.</summary>
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
    /// <summary>RectDraw mode: the word naming which kind of rectangle the next drag makes — a
    /// <see cref="DockModeButton"/> the host supplies, since only the host knows the two kinds apart. It
    /// leads the draw group, ahead of the rectangle button.</summary>
    [Parameter] public RenderFragment? DrawMode { get; set; }
    /// <summary>The colour the draw group wears while <see cref="DrawMode"/>'s mode is armed — the same
    /// colour the drawn rectangle takes on the canvas.</summary>
    [Parameter] public string? DrawAccent { get; set; }
    /// <summary>Fired once the canvas is mounted + the map is loaded, so a host can apply initial state
    /// (e.g. the excluded-island set) that only takes effect after the islands are rendered.</summary>
    [Parameter] public EventCallback OnReady { get; set; }

    /// <summary>Show the <b>Editable</b> sub-bar chip — a toggleable overlay of what makes each column
    /// editable (the Build · buildable-layer step; it stays available even in RectDraw mode).</summary>
    [Parameter] public bool ShowEditZones { get; set; }
    /// <summary>Fired with the new on/off state when the Editable overlay is toggled (so the host can,
    /// e.g., show the legend only while it's on).</summary>
    [Parameter] public EventCallback<bool> OnEditZonesToggled { get; set; }

    private ElementReference svgRef, wrapRef;
    /// <summary>The floating top-left readout. Its cursor and zoom elements are handed to the canvas on
    /// mount, which then writes them directly — per mousemove, far too often to render.</summary>
    private CanvasReadout? readout;
    private IJSObjectReference? handle;
    private DotNetObjectReference<WorldCanvas>? selfRef;
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
            islandIds = (await Http.GetFromJsonAsync<List<IslandDto>>($"api/map/{Slug}/islands") ?? [])
                .Select(island => island.Id).ToList();
        }
        catch { islandIds = new(); }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (firstRender)
        {
            selfRef = DotNetObjectReference.Create(this);
            handle = await JS.InvokeAsync<IJSObjectReference>(
                "studio.mountCanvas", svgRef, wrapRef, readout!.Cursor, readout.Zoom, selfRef, Slug, Category);
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

    private bool editZonesOn;

    /// <summary>Toggle the edit-zone overlay. Re-fetches each toggle-on so it reflects the current saved
    /// build slice; stays off when there's no grid (no scan data).</summary>
    private async Task ToggleEditZones()
    {
        if (handle is null) return;
        var ok = await handle.InvokeAsync<bool>("setEditability", !editZonesOn);
        if (ok) { editZonesOn = !editZonesOn; await OnEditZonesToggled.InvokeAsync(editZonesOn); }
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

    /// <summary>Orbit the authored rectangles into non-editable ghost previews by the given symmetry
    /// (null type clears it). The canvas computes them with the shared geometry/symmetry.js.</summary>
    public async Task SetAuthorMirrorAsync(string? type, double cx, double cz)
    {
        if (handle is not null) await handle.InvokeVoidAsync("setAuthorMirror", type, cx, cz);
    }

    /// <summary>A region's footprint was changed by a resize drag (the canvas already shows it live).
    /// The host patches the intent slice it renders and refreshes its inspector. Args: region id + new {min,max}{x,z}.</summary>
    [JSInvokable]
    public Task OnBoundsSave(string id, JsonElement bounds)
    {
        double N(string k) => bounds.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
        return OnGeometrySaved.InvokeAsync((id, N("min_x"), N("min_z"), N("max_x"), N("max_z")));
    }

    /// <summary>A rectangle drawn in <see cref="RectDraw"/> mode → the host, which writes it to the intent and
    /// renders it back as a dummy region. No region is created here.</summary>
    [JSInvokable]
    public async Task OnRegionDraw(JsonElement draw)
    {
        if (!RectDraw || !OnRectDrawn.HasDelegate) return;
        double N(string k) => draw.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
        await OnRectDrawn.InvokeAsync((N("min_x"), N("min_z"), N("max_x"), N("max_z")));
        await SetTool("select");   // switch to select so the drawn rect can be picked + resized
        StateHasChanged();
    }

    /// <summary>Drop the canvas. The field is cleared <b>before</b> the reference is disposed, because every
    /// call on this component guards on <c>handle is not null</c> and a disposed reference is not a live one:
    /// a host that awaits something — a column read, a save — and paints when it returns can resume after its
    /// step has gone, and a non-null handle would answer that paint by throwing out of the renderer.</summary>
    public async ValueTask DisposeAsync()
    {
        if (handle is { } canvas)
        {
            handle = null;
            try { await canvas.InvokeVoidAsync("dispose"); } catch { }
            try { await canvas.DisposeAsync(); } catch { }
        }
        selfRef?.Dispose();
    }
}
