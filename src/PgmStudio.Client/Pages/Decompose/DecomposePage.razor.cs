using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Decompose;

/// <summary>
/// The lane-decomposition surface: mounts the decompose canvas (<c>bridge/decompose-bridge.js</c>) on a
/// map's simplified island outline (<c>GET /map/{slug}/island-sketch</c>), lets the human cut it into lane
/// pieces + tag roles, and on confirm saves to <c>lane_decomposition_json</c> and advances to the next
/// queued two-team CTW map.
/// </summary>
public partial class DecomposePage
{
    [Parameter] public string Slug { get; set; } = "";

    // Lane-piece roles (cut from a team island) + whole-island tags (stepping-stone/mid neutral islands,
    // decorative = excluded non-gameplay). Pre-filled per island from the /island-roles classifier.
    private static readonly string[] LaneRoles = ["spawn", "wool", "frontline", "hub", "other"];
    private static readonly string[] IslandRoles = ["stepping-stone", "mid", "decorative"];

    private ElementReference svgRef, wrapRef, coordsRef, zoomRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<DecomposePage>? selfRef;

    private string tool = "lasso";
    private string focusSel = "";
    private bool blocksOn;          // top-surface block overlay toggle — persists as you browse the queue
    private bool anchorsOn, buildOn;   // /island-roles overlays: objective anchors (G8b) + build region (G8c)
    private JsonElement? rolesCache;   // the map's island-roles response, fetched once per map
    private string? loadedSlug;
    private bool saving;
    private string progressLabel = "";
    private List<PieceRow> pieces = [];
    private List<string> queue = [];
    private string? selectedId;     // the piece selected on the canvas / list → shown in the right inspector
    private PieceRow? Selected => pieces.FirstOrDefault(p => p.Id == selectedId);

    private sealed class PieceRow
    {
        public string Id { get; set; } = "";
        public string Role { get; set; } = "other";
        public int Vertices { get; set; }
        public int Holes { get; set; }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        handle = await JS.InvokeAsync<IJSObjectReference>("studio.mountDecompose", svgRef, wrapRef, coordsRef, zoomRef, selfRef);
        loadedSlug = Slug;
        await LoadMapAsync();
        await RefreshQueueAsync();
    }

    // Navigating to the next map reuses this component (same route, new param) — reload on slug change.
    protected override async Task OnParametersSetAsync()
    {
        if (handle is not null && Slug != loadedSlug)
        {
            loadedSlug = Slug;
            pieces = [];
            await LoadMapAsync();
            await RefreshQueueAsync();
        }
    }

    private async Task LoadMapAsync()
    {
        if (handle is null) return;
        focusSel = "";
        rolesCache = null;   // new map → re-fetch the island-roles overlays on demand
        selectedId = null;
        try
        {
            // resume a saved decomposition (already one-side) if present, else start from the simplified
            // outline, dedup to one side via the map symmetry, and pre-fill whole-island categories from the
            // /island-roles classifier (decorative + neutral islands get tagged; team/objective left to cut).
            var saved = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/lane-decomposition");
            if (HasShapes(saved))
                await handle.InvokeVoidAsync("load", saved, (object?)null, (object?)null);
            else
            {
                var island = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/island-sketch");
                var sym = await FetchSymmetryAsync();
                await handle.InvokeVoidAsync("load", island, sym, await IslandRoleListAsync());
            }
        }
        catch { /* no geometry — leave the canvas empty */ }
        await ApplyBlocksAsync();        // re-apply the block-overlay toggle for this map (load reset the bridge)
        await ApplyRoleOverlaysAsync();  // re-apply the anchor + build-region toggles
    }

    // Objective anchors (G8b) + declared build region (G8c) — both from GET /island-roles, fetched once per map
    // (cached) and re-applied so the toggles persist as the user browses the queue.
    // Per-island gameplay role (island-sketch order) for the pre-fill — reuses the cached island-roles fetch.
    private async Task<List<string?>?> IslandRoleListAsync()
    {
        var r = await RolesAsync();
        if (r is not { } roles) return null;
        return roles.GetProperty("islands").EnumerateArray()
            .Select(i => i.TryGetProperty("role", out var rr) ? rr.GetString() : null).ToList();
    }

    private async Task<JsonElement?> RolesAsync()
    {
        if (rolesCache is null)
        {
            try { rolesCache = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/island-roles"); }
            catch { rolesCache = null; }
        }
        return rolesCache;
    }

    private async Task ApplyRoleOverlaysAsync()
    {
        if (handle is null) return;
        var roles = anchorsOn || buildOn ? await RolesAsync() : null;
        object? anchorList = null, build = null;
        if (roles is { } r)
        {
            anchorList = r.GetProperty("islands").EnumerateArray()
                .SelectMany(i => i.GetProperty("anchors").EnumerateArray())
                .Select(a => new { kind = a.GetProperty("kind").GetString(), x = a.GetProperty("x").GetDouble(), z = a.GetProperty("z").GetDouble() })
                .ToList();
            if (r.TryGetProperty("buildRegion", out var b) && b.ValueKind != JsonValueKind.Null) build = b;
        }
        await handle.InvokeVoidAsync("setAnchors", anchorsOn ? anchorList : null, anchorsOn);
        await handle.InvokeVoidAsync("setBuild", buildOn ? build : null, buildOn);
    }

    private async Task ToggleAnchors() { anchorsOn = !anchorsOn; await ApplyRoleOverlaysAsync(); }
    private async Task ToggleBuild() { buildOn = !buildOn; await ApplyRoleOverlaysAsync(); }

    // The block-colour overlay: lazily fetch this map's top-surface layer when on, else hide. Called on every
    // map load so the toggle preference persists as the user browses the queue.
    private async Task ApplyBlocksAsync()
    {
        if (handle is null) return;
        if (!blocksOn) { await handle.InvokeVoidAsync("setBlocks", (object?)null, false); return; }
        try
        {
            var blocks = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/layers/top-surface");
            await handle.InvokeVoidAsync("setBlocks", blocks, true);
        }
        catch { await handle.InvokeVoidAsync("setBlocks", (object?)null, false); }   // no layer for this map
    }

    private async Task ToggleBlocks() { blocksOn = !blocksOn; await ApplyBlocksAsync(); }

    // The map's primary symmetry + centre, so the canvas keeps one island per orbit. Null → show all.
    private async Task<object?> FetchSymmetryAsync()
    {
        try
        {
            var s = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/symmetry");
            if (s.ValueKind == JsonValueKind.Object
                && s.TryGetProperty("primary", out var pr) && pr.ValueKind == JsonValueKind.Object
                && pr.TryGetProperty("type", out var ty) && ty.GetString() is { Length: > 0 } mode
                && s.TryGetProperty("center", out var ce) && ce.ValueKind == JsonValueKind.Object)
            {
                var cx = ce.TryGetProperty("cx", out var cxv) ? cxv.GetDouble() : 0;
                var cz = ce.TryGetProperty("cz", out var czv) ? czv.GetDouble() : 0;
                return new { mode, cx, cz };
            }
        }
        catch { /* no symmetry — show all islands */ }
        return null;
    }

    private static bool HasShapes(JsonElement e) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty("layout", out var l)
        && l.ValueKind == JsonValueKind.Object && l.TryGetProperty("shapes", out var s)
        && s.ValueKind == JsonValueKind.Array && s.GetArrayLength() > 0;

    private async Task RefreshQueueAsync()
    {
        try
        {
            var q = await Http.GetFromJsonAsync<JsonElement>("api/decompose/queue");
            queue = q.GetProperty("todo").EnumerateArray().Select(m => m.GetProperty("slug").GetString()!).ToList();
            var done = q.GetProperty("done").GetInt32();
            var remaining = q.GetProperty("remaining").GetInt32();
            var i = queue.IndexOf(Slug);
            progressLabel = i >= 0 ? $"{i + 1} of {remaining} to do · {done} done" : $"{remaining} to do · {done} done";
            StateHasChanged();
        }
        catch { /* queue unavailable */ }
    }

    private Task SetTool(string t) { tool = t; return handle?.InvokeVoidAsync("setTool", t).AsTask() ?? Task.CompletedTask; }
    private Task Fit() { focusSel = ""; return handle?.InvokeVoidAsync("fit").AsTask() ?? Task.CompletedTask; }
    private Task ZoomIn() => handle?.InvokeVoidAsync("zoomIn").AsTask() ?? Task.CompletedTask;
    private Task ZoomOut() => handle?.InvokeVoidAsync("zoomOut").AsTask() ?? Task.CompletedTask;
    private async Task OnFocusPiece(ChangeEventArgs e)
    {
        focusSel = e.Value?.ToString() ?? "";
        if (handle is not null && focusSel.Length > 0) await handle.InvokeVoidAsync("fitPiece", focusSel);
    }
    // Browse the to-do queue without decomposing: step to the adjacent queued map (unsaved cuts are dropped —
    // these are for checking maps, not committing them; Confirm & Next is the save path).
    private bool CanPrev => queue.IndexOf(Slug) > 0;
    private bool CanNext { get { var i = queue.IndexOf(Slug); return i >= 0 && i < queue.Count - 1; } }
    private void GoPrev() { var i = queue.IndexOf(Slug); if (i > 0) Nav.NavigateTo($"maps/{queue[i - 1]}/decompose"); }
    private void GoNext() { var i = queue.IndexOf(Slug); if (i >= 0 && i < queue.Count - 1) Nav.NavigateTo($"maps/{queue[i + 1]}/decompose"); }

    // Category swatch colour — mirrors ROLE_COLORS in decompose-bridge.js so the list/inspector match the canvas.
    private static string RoleColor(string role) => role switch
    {
        "spawn" => "var(--color-error)",
        "wool" => "var(--accent)",
        "frontline" => "var(--canvas-result-stroke)",
        "hub" => "var(--text-muted)",
        "stepping-stone" => "var(--color-success)",
        "mid" => "var(--color-warning)",
        "decorative" => "var(--canvas-mirror-stroke)",
        _ => "var(--canvas-add-stroke)",
    };

    private Task Undo() => handle?.InvokeVoidAsync("undo").AsTask() ?? Task.CompletedTask;

    private async Task SelectPiece(string id)
    {
        selectedId = id;
        if (handle is not null) await handle.InvokeVoidAsync("selectPiece", id);
    }

    private async Task SetRole(string id, string role)
    {
        if (pieces.FirstOrDefault(p => p.Id == id) is { } p) p.Role = role;   // immediate inspector feedback
        if (handle is not null) await handle.InvokeVoidAsync("setRole", id, role);
    }

    private async Task ConfirmNext()
    {
        if (handle is null || saving) return;
        saving = true;
        StateHasChanged();
        try
        {
            var state = await handle.InvokeAsync<JsonElement>("getState");
            await Http.PutAsJsonAsync($"api/map/{Slug}/lane-decomposition", state);
        }
        catch { /* save failed — stay put */ saving = false; StateHasChanged(); return; }

        await RefreshQueueAsync();
        var next = queue.FirstOrDefault(s => s != Slug);
        saving = false;
        if (next is not null) Nav.NavigateTo($"maps/{next}/decompose");
        else Nav.NavigateTo("maps");   // queue drained
    }

    /// <summary>The canvas select tool picked a piece (or empty space → null).</summary>
    [JSInvokable]
    public void OnSelect(string? id) { selectedId = id; StateHasChanged(); }

    /// <summary>The bridge pushed the current piece list (after each cut / role change).</summary>
    [JSInvokable]
    public void OnPieces(string json)
    {
        pieces = JsonSerializer.Deserialize<List<PieceRow>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        if (selectedId is not null && pieces.All(p => p.Id != selectedId)) selectedId = null;   // selection split/removed by a cut
        StateHasChanged();
    }

    /// <summary>The decomposition changed (cut / undo / role) — hook for autosave later; no-op for now.</summary>
    [JSInvokable]
    public void OnDirty() { }

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
