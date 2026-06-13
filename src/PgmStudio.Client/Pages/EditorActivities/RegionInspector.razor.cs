using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Pages.EditorActivities;

// The Field markup template stays in RegionInspector.razor (it is Razor markup); the inspector's
// state and behaviour live here in the code-behind partial.
public partial class RegionInspector
{
    [Parameter] public RegionNode? Node { get; set; }
    [Parameter] public EventCallback<string> OnSelectChild { get; set; }
    [Parameter] public EventCallback<string> OnDelete { get; set; }
    [Parameter] public EventCallback<string> OnRename { get; set; }  // passes the new id

    private async Task OnIdChanged(ChangeEventArgs e)
    {
        var newId = e.Value?.ToString()?.Trim() ?? "";
        if (newId.Length > 0 && Node is not null && newId != Node.Id) await OnRename.InvokeAsync(newId);
    }

    private object? C(string k) => Node!.Coords.GetValueOrDefault(k);
    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private List<RegionNode> Children()
    {
        var list = new List<RegionNode>(Node!.Children);
        if (Node.Source is not null) list.Add(Node.Source);
        return list.Where(k => !string.IsNullOrEmpty(k.Id)).ToList();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
