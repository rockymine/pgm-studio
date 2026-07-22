using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class BuildRegionsActivity
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public bool IsFirstActivity { get; set; }
    [Parameter] public EventCallback OnPrevActivity { get; set; }
    [Parameter] public EventCallback OnNextActivity { get; set; }

    private EditorCanvas? canvas;
    private int step = 1;

    // ── navigation (flow-bar Back/Next) — step 2 has no confirm gate (freeform region editing), so
    // Next there just walks on to the next activity, same as a non-stepped activity's Next would. ──
    private bool BackEnabled => step == 2 || !IsFirstActivity;
    private Task OnFlowBack() { if (step == 2) { step = 1; return Task.CompletedTask; } return OnPrevActivity.InvokeAsync(); }
    private Task OnFlowNext() => step == 1 ? GoStep2() : OnNextActivity.InvokeAsync();

    // step 1 — the side-view is the shared BuildHeightSideview component (owns its own JS lifecycle).
    private string? maxHeight;
    private bool heightDirty;
    private string? heightStatus;

    // step 2
    private List<RegionGroup>? groups;
    private readonly Dictionary<string, RegionNode> nodeMap = new();
    private string? selRegion;
    private HashSet<string> selSet = new();
    private string? error;

    private RegionNode? RegionNodeSel => selRegion is not null ? nodeMap.GetValueOrDefault(selRegion) : null;

    protected override async Task OnParametersSetAsync()
    {
        step = 1; heightDirty = false; heightStatus = null;
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}");
            maxHeight = doc.TryGetProperty("max_build_height", out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDouble().ToString() : null;
        }
        catch (Exception ex) { error = ex.Message; }
    }

    private async Task SaveHeight()
    {
        heightStatus = "Saving…"; StateHasChanged();
        var body = new Dictionary<string, object?> { ["max_build_height"] = string.IsNullOrWhiteSpace(maxHeight) ? null : (object?)double.Parse(maxHeight!) };
        if (await Patch("metadata", body)) { heightDirty = false; heightStatus = "Saved."; }
        StateHasChanged();
    }

    private async Task GoStep2()
    {
        step = 2;
        await LoadRegions();
    }

    private async Task LoadRegions(string? selectId = null)
    {
        groups = null; nodeMap.Clear(); selRegion = null; selSet = new();
        try
        {
            var tree = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/regions/tree");
            // Build regions usually nest inside the `not-build-area` negative (a rule-container in the
            // "other" group), so they're not roots of a "build" group — walk EVERY group and collect the
            // top-most build-category node (its carved-out children stay nested under it). Same treatment
            // as the spawn-protection / wool-monument trees.
            var build = new List<RegionNode>();
            var draft = new List<RegionNode>();   // drawn here, not yet wired (E10)
            if (tree.TryGetProperty("groups", out var g))
                foreach (var grp in RegionGroup.ParseGroups(g))
                    foreach (var n in grp.Regions) CollectBuild(n, false, build, draft);
            groups =
            [
                new RegionGroup { Name = "build", Label = "", Regions = build },
                new RegionGroup { Name = "draft", Label = "Draft", Regions = draft },
            ];
        }
        catch (Exception ex) { error = ex.Message; }
        // Re-render canvas geometry: a delete (or rename) changed the regions, and the canvas renders
        // from its own cached dataset — without this the deleted shape lingers until the next action.
        if (canvas is not null) await canvas.ReloadAsync();
        if (selectId is not null) await Select(selectId); else StateHasChanged();
    }

    // Index every node + collect the top-most build-category node on each path (so build-area is added
    // once with its carved-out children nested, not flattened). `claimed` = a build ancestor already taken.
    private void CollectBuild(RegionNode n, bool claimed, List<RegionNode> build, List<RegionNode> draft)
    {
        if (!string.IsNullOrEmpty(n.Id)) nodeMap.TryAdd(n.Id, n);
        var take = n.Category == "build" && !claimed;
        if (take) build.Add(n);
        else if (n.DraftStep == "build" && n.Category == "other") draft.Add(n);
        foreach (var c in n.Children) CollectBuild(c, claimed || take, build, draft);
        if (n.Source is not null) CollectBuild(n.Source, claimed || take, build, draft);
    }

    private async Task Select(string? id)
    {
        if (id is null || !nodeMap.TryGetValue(id, out var node)) { await Deselect(); return; }
        selRegion = id; selSet = new(); CollectDescendants(node, selSet);
        if (canvas is not null) await canvas.SetSelectionAsync(selSet);
        StateHasChanged();
    }

    private static void CollectDescendants(RegionNode n, HashSet<string> outSet)
    {
        if (!string.IsNullOrEmpty(n.Id)) outSet.Add(n.Id);
        foreach (var c in n.Children) CollectDescendants(c, outSet);
    }

    private async Task Deselect() { selRegion = null; selSet = new(); if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>()); StateHasChanged(); }
    private Task OnTreeSelect(RegionNode n) => Select(n.Id);
    private Task OnCanvasSelect(string? id) => Select(id);

    private Task ReloadBuild() => LoadRegions();   // refresh the build tree after a drawn region is created

    private async Task DeleteRegion(string id)
    {
        if (await Delete($"regions/{id}")) await LoadRegions();
    }

    private async Task RenameRegion(string newId)
    {
        if (selRegion is null) return;
        if (await Patch($"regions/{selRegion}", new Dictionary<string, object?> { ["id"] = newId })) await LoadRegions(newId);
    }

    // Side-view slice: set a point/block region's Y (coords patch), then reload keeping it selected.
    private async Task SetRegionY(int y)
    {
        if (selRegion is null) return;
        if (await Patch($"regions/{selRegion}", new Dictionary<string, object?> { ["coords"] = new Dictionary<string, object?> { ["y"] = y } }))
            await LoadRegions(selRegion);
    }

    // Geometry editing (canvas drag-resize + inspector coord fields) — persist + keep canvas/inspector in sync.
    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        if (!nodeMap.TryGetValue(e.Id, out var node)) return;
        if (await RegionEdits.SetBoundsAsync(Http, Slug, node, e.MinX, e.MinZ, e.MaxX, e.MaxZ) is null && canvas is not null)
            await canvas.ReloadAsync();
        else StateHasChanged();
    }

    private async Task OnSetCoord((string Key, double Value) e)
    {
        if (RegionNodeSel is null) return;
        var nb = await RegionEdits.SetCoordAsync(Http, Slug, RegionNodeSel, e.Key, e.Value);
        if (nb is null) { error = "Edit rejected."; StateHasChanged(); return; }
        if (canvas is not null && nb.Count == 4) await canvas.RefreshRegionBoundsAsync(RegionNodeSel.Id, nb);
        StateHasChanged();
    }

    private async Task<bool> Patch(string path, object body) => await Send(Http.PatchAsJsonAsync($"api/map/{Slug}/{path}", body));
    private async Task<bool> Delete(string path) => await Send(Http.DeleteAsync($"api/map/{Slug}/{path}"));
    private async Task<bool> Send(Task<HttpResponseMessage> call)
    {
        error = null;
        var resp = await call;
        if (resp.IsSuccessStatusCode) return true;
        try { var d = await resp.Content.ReadFromJsonAsync<JsonElement>(); error = d.TryGetProperty("error", out var e) ? e.GetString() : $"error {(int)resp.StatusCode}"; }
        catch { error = $"error {(int)resp.StatusCode}"; }
        StateHasChanged();
        return false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    // Typed into the number input — the side-view picks up the new Height on re-render.
    private void OnHeightInput(ChangeEventArgs e)
    {
        maxHeight = e.Value?.ToString();
        heightDirty = true; heightStatus = null;
    }

    // Dragged the side-view line.
    private void OnSideviewHeight(double? y)
    {
        maxHeight = y is { } v ? ((int)Math.Round(v)).ToString() : null;
        heightDirty = true; heightStatus = null;
        StateHasChanged();
    }

    private static double? ParseHeight(string? s) => double.TryParse(s, out var v) ? v : null;
}
