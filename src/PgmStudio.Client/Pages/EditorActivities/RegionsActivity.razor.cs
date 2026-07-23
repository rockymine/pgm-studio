using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;

using PgmStudio.Client.Components;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class RegionsActivity
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public bool IsFirstActivity { get; set; }
    [Parameter] public bool IsLastActivity { get; set; }
    [Parameter] public EventCallback OnPrevActivity { get; set; }
    [Parameter] public EventCallback OnNextActivity { get; set; }

    private EditorCanvas? canvas;
    private List<RegionGroup>? groups;
    private readonly Dictionary<string, RegionNode> nodeMap = new();

    // R1a multi-selection: the user-picked regions; `primary` is the last-clicked (drives the inspector).
    private readonly HashSet<string> selection = new();
    private string? primaryId;
    private RegionNode? primaryNode;
    private string? error;

    private DotNetObjectReference<RegionsActivity>? selfRef;

    protected override async Task OnParametersSetAsync() => await Load();

    private async Task Load(IEnumerable<string>? select = null)
    {
        groups = null; nodeMap.Clear(); error = null;
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/regions/tree");
            groups = doc.TryGetProperty("groups", out var g) ? RegionGroup.ParseGroups(g) : new();
            foreach (var grp in groups) foreach (var n in grp.Regions) Index(n);
        }
        catch (Exception ex) { groups = new(); error = ex.Message; }

        // keep only still-existing ids; re-select the requested set (e.g. a new group / freed children)
        var keep = (select ?? selection).Where(nodeMap.ContainsKey).ToList();
        selection.Clear();
        foreach (var id in keep) selection.Add(id);
        primaryId = selection.LastOrDefault();
        primaryNode = primaryId is not null ? nodeMap.GetValueOrDefault(primaryId) : null;
        // Re-render canvas geometry first (a group/ungroup changed the region set); SyncCanvas then
        // re-applies the highlight, which a bare render would otherwise clear.
        if (canvas is not null) await canvas.ReloadAsync();
        await SyncCanvas();
        StateHasChanged();
    }

    private void Index(RegionNode n)
    {
        if (!string.IsNullOrEmpty(n.Id)) nodeMap.TryAdd(n.Id, n);
        foreach (var c in n.Children) Index(c);
        if (n.Source is not null) Index(n.Source);
    }

    // ── selection ─────────────────────────────────────────────────────────────────
    private Task OnTreeSelect(RegionNode n) => SelectOne(n.Id);
    private Task OnCanvasSelect(string? id) => id is null ? Clear() : SelectOne(id);

    private async Task SelectOne(string? id)
    {
        error = null;
        selection.Clear();
        if (id is not null && nodeMap.ContainsKey(id)) { selection.Add(id); primaryId = id; }
        else primaryId = null;
        primaryNode = primaryId is not null ? nodeMap.GetValueOrDefault(primaryId) : null;
        await SyncCanvas();
        StateHasChanged();
    }

    private async Task OnTreeSelectCtrl(RegionNode n)
    {
        error = null;
        if (string.IsNullOrEmpty(n.Id)) return;
        if (!selection.Remove(n.Id)) { selection.Add(n.Id); primaryId = n.Id; }
        else if (primaryId == n.Id) primaryId = selection.LastOrDefault();
        primaryNode = primaryId is not null ? nodeMap.GetValueOrDefault(primaryId) : null;
        await SyncCanvas();
        StateHasChanged();
    }

    private async Task Clear()
    {
        selection.Clear(); primaryId = null; primaryNode = null;
        await SyncCanvas();
        StateHasChanged();
    }

    // ── geometry editing (canvas drag-resize + inspector coord fields) ──────────────
    // A canvas resize already updated the shape on screen; persist its footprint and refresh the inspector.
    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        if (!nodeMap.TryGetValue(e.Id, out var node)) return;
        if (await RegionEdits.SetBoundsAsync(Http, Slug, node, e.MinX, e.MinZ, e.MaxX, e.MaxZ) is null && canvas is not null)
            await canvas.ReloadAsync();   // server rejected the edit → reload to revert
        else StateHasChanged();
    }

    // An inspector coord field changed: persist it, then push the recomputed footprint to the canvas.
    private async Task OnSetCoord((string Key, double Value) e)
    {
        if (primaryNode is null) return;
        var nb = await RegionEdits.SetCoordAsync(Http, Slug, primaryNode, e.Key, e.Value);
        if (nb is null) { error = "Edit rejected."; StateHasChanged(); return; }
        if (canvas is not null && nb.Count == 4) await canvas.RefreshRegionBoundsAsync(primaryNode.Id, nb);
        StateHasChanged();
    }

    // canvas highlights every selected region + its descendants (so a compound shows its footprint)
    private async Task SyncCanvas()
    {
        if (canvas is null) return;
        var hi = new HashSet<string>();
        foreach (var id in selection)
            if (nodeMap.TryGetValue(id, out var node)) CollectDescendants(node, hi);
        await canvas.SetSelectionAsync(hi);
    }

    private static void CollectDescendants(RegionNode n, HashSet<string> outSet)
    {
        if (!string.IsNullOrEmpty(n.Id)) outSet.Add(n.Id);
        foreach (var c in n.Children) CollectDescendants(c, outSet);
    }

    // ── Ctrl+G: group ≥2 into a union, or ungroup a single compound ────────────────
    [JSInvokable]
    public async Task OnGroupKey()
    {
        if (selection.Count >= 2) await GroupSelection();
        else if (selection.Count == 1) await UngroupOne(selection.First());
    }

    private async Task GroupSelection()
    {
        var body = new Dictionary<string, object?> { ["child_ids"] = selection.ToList(), ["type"] = "union" };
        var resp = await Http.PostAsJsonAsync($"api/map/{Slug}/regions/group", body);
        if (await Ok(resp) is { } b && b.TryGetProperty("id", out var idEl) && idEl.GetString() is { } newId)
            await Load([newId]);   // select the new group (Ctrl+G again ungroups it)
    }

    private async Task UngroupOne(string id)
    {
        var node = nodeMap.GetValueOrDefault(id);
        if (node is null || !node.HasKids) return;                 // a primitive — nothing to ungroup
        if (node.Wiring.Count > 0)                                 // wired compound — don't orphan the rule
        {
            error = $"\"{id}\" has a rule wired to it — unwire it before ungrouping."; StateHasChanged(); return;
        }
        var resp = await Http.PostAsJsonAsync($"api/map/{Slug}/regions/ungroup", new Dictionary<string, object?> { ["region_id"] = id });
        if (await Ok(resp) is { } b)
        {
            var freed = b.TryGetProperty("child_ids", out var c) && c.ValueKind == JsonValueKind.Array
                ? c.EnumerateArray().Select(x => x.GetString()).Where(x => x is not null).Cast<string>().ToList()
                : new List<string>();
            await Load(freed);
        }
    }

    private async Task<JsonElement?> Ok(HttpResponseMessage resp)
    {
        error = null;
        var el = await resp.Content.ReadFromJsonAsync<JsonElement>();
        if (resp.IsSuccessStatusCode) return el;
        error = el.TryGetProperty("error", out var e) ? e.GetString() : $"error {(int)resp.StatusCode}";
        StateHasChanged();
        return null;
    }

    // ── keyboard registration ───────────────────────────────────────────────────────
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            selfRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("studio.registerShortcuts", selfRef);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await JS.InvokeVoidAsync("studio.clearShortcuts"); } catch { }
        selfRef?.Dispose();
    }
}
