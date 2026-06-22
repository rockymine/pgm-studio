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

    private static readonly string[] Roles = ["spawn", "wool", "frontline", "hub", "other"];

    private ElementReference svgRef, wrapRef, coordsRef, zoomRef;
    private IJSObjectReference? handle;
    private DotNetObjectReference<DecomposePage>? selfRef;

    private string tool = "lasso";
    private string focusSel = "";
    private string? loadedSlug;
    private bool saving;
    private string progressLabel = "";
    private List<PieceRow> pieces = [];
    private List<string> queue = [];

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
        try
        {
            // resume a saved decomposition (already one-side) if present, else start from the simplified
            // outline and dedup to one side via the map symmetry.
            var saved = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/lane-decomposition");
            if (HasShapes(saved)) { await handle.InvokeVoidAsync("load", saved, (object?)null); return; }
            var island = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/island-sketch");
            var sym = await FetchSymmetryAsync();
            await handle.InvokeVoidAsync("load", island, sym);
        }
        catch { /* no geometry — leave the canvas empty */ }
    }

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

    private Task Undo() => handle?.InvokeVoidAsync("undo").AsTask() ?? Task.CompletedTask;
    private Task SelectPiece(string id) => handle?.InvokeVoidAsync("selectPiece", id).AsTask() ?? Task.CompletedTask;
    private Task SetRole(string id, string? role) =>
        role is null ? Task.CompletedTask : (handle?.InvokeVoidAsync("setRole", id, role).AsTask() ?? Task.CompletedTask);

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

    /// <summary>The bridge pushed the current piece list (after each cut / role change).</summary>
    [JSInvokable]
    public void OnPieces(string json)
    {
        pieces = JsonSerializer.Deserialize<List<PieceRow>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
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
