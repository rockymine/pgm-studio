using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class RegionsActivity
{
    [Parameter] public string Slug { get; set; } = "";

    private EditorCanvas? canvas;
    private List<RegionGroup>? groups;
    private readonly Dictionary<string, RegionNode> nodeMap = new();
    private string? selectedId;
    private HashSet<string> selectedSet = new();
    private RegionNode? selectedNode;

    protected override async Task OnParametersSetAsync()
    {
        groups = null; selectedId = null; selectedSet = new(); selectedNode = null; nodeMap.Clear();
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/regions/tree");
            groups = doc.TryGetProperty("groups", out var g) ? RegionGroup.ParseGroups(g) : new();
            foreach (var grp in groups)
                foreach (var n in grp.Regions) Index(n);
        }
        catch { groups = new(); }
    }

    private void Index(RegionNode n)
    {
        if (!string.IsNullOrEmpty(n.Id)) nodeMap.TryAdd(n.Id, n);
        foreach (var c in n.Children) Index(c);
        if (n.Source is not null) Index(n.Source);
    }

    // Selecting a node also highlights its descendants (children subtree), matching RegionRegistry.
    private async Task Select(string? id)
    {
        if (id is null || !nodeMap.TryGetValue(id, out var node)) { await Deselect(); return; }
        selectedId = id;
        selectedNode = node;
        selectedSet = new();
        CollectDescendants(node, selectedSet);
        if (canvas is not null) await canvas.SetSelectionAsync(selectedSet);
        StateHasChanged();
    }

    private static void CollectDescendants(RegionNode n, HashSet<string> outSet)
    {
        if (!string.IsNullOrEmpty(n.Id)) outSet.Add(n.Id);
        foreach (var c in n.Children) CollectDescendants(c, outSet);
    }

    private async Task Deselect()
    {
        selectedId = null; selectedSet = new(); selectedNode = null;
        if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>());
        StateHasChanged();
    }

    private Task OnTreeSelect(RegionNode n) => Select(n.Id);
    private Task OnCanvasSelect(string? id) => Select(id);
}
