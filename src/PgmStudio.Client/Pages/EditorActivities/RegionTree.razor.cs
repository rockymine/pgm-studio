using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class RegionTree
{
    [Parameter] public List<RegionGroup>? Groups { get; set; }
    [Parameter] public string? SelectedId { get; set; }
    [Parameter] public ISet<string>? SelectedSet { get; set; }
    [Parameter] public EventCallback<RegionNode> OnSelect { get; set; }
    /// <summary>Ctrl/⌘-click — add/remove from a multi-selection (R1). Optional; falls back to OnSelect.</summary>
    [Parameter] public EventCallback<RegionNode> OnSelectCtrl { get; set; }

    private readonly HashSet<string> collapsed = new();

    private async Task OnRow(RegionNode n, MouseEventArgs e)
    {
        if ((e.CtrlKey || e.MetaKey) && OnSelectCtrl.HasDelegate) await OnSelectCtrl.InvokeAsync(n);
        else await OnSelect.InvokeAsync(n);
    }

    private sealed record TreeRow(string Kind, string? Text, RegionNode? Node, int Depth, string? ParentType, int Index);

    private List<TreeRow> BuildRows()
    {
        var rows = new List<TreeRow>();
        var visible = Groups!.Where(g => g.Regions.Count > 0).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            var g = visible[i];
            if (!string.IsNullOrEmpty(g.Label)) rows.Add(new("header", g.Label, null, 0, null, 0));
            AddNodes(g.Regions, 0, null, rows);
            if (i < visible.Count - 1) rows.Add(new("divider", null, null, 0, null, 0));
        }
        return rows;
    }

    private void AddNodes(List<RegionNode> nodes, int depth, string? parentType, List<TreeRow> rows)
    {
        for (var idx = 0; idx < nodes.Count; idx++)
        {
            var node = nodes[idx];
            rows.Add(new("node", null, node, depth, parentType, idx));
            if (node.HasKids && !collapsed.Contains(node.Id))
            {
                AddNodes(node.Children, depth + 1, node.Type, rows);
                if (node.Source is not null) AddNodes(new() { node.Source }, depth + 1, node.Type, rows);
            }
        }
    }

    private string? SelClass(string id)
        => id == SelectedId ? "geo-row--selected"
        : SelectedSet is not null && SelectedSet.Contains(id) ? "geo-row--selected-child"
        : null;

    private void Toggle(string id)
    {
        if (!collapsed.Remove(id)) collapsed.Add(id);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
