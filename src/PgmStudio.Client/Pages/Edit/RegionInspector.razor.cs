using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Pages.Edit;

// The Field markup template stays in RegionInspector.razor (it is Razor markup); the inspector's
// state and behaviour live here in the code-behind partial.
public partial class RegionInspector
{
    [Parameter] public RegionNode? Node { get; set; }
    [Parameter] public string Slug { get; set; } = "";              // enables the side-view slice
    [Parameter] public EventCallback<string> OnSelectChild { get; set; }
    [Parameter] public EventCallback<string> OnDelete { get; set; }
    [Parameter] public EventCallback<string> OnRename { get; set; }  // passes the new id
    [Parameter] public EventCallback<int> OnSetY { get; set; }       // set a point/block region's Y (slice)
    /// <summary>Edit a single geometry field (coord key + new value). When unset the coord inputs stay
    /// read-only; when wired the host persists the change and syncs the canvas.</summary>
    [Parameter] public EventCallback<(string Key, double Value)> OnSetCoord { get; set; }

    private async Task OnIdChanged(ChangeEventArgs e)
    {
        var newId = e.Value?.ToString()?.Trim() ?? "";
        if (newId.Length > 0 && Node is not null && newId != Node.Id) await OnRename.InvokeAsync(newId);
    }

    private async Task SetCoord(string key, ChangeEventArgs e)
    {
        if (key.Length > 0 && double.TryParse(e.Value?.ToString(),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            await OnSetCoord.InvokeAsync((key, Math.Max(v, MinFor(key) ?? double.NegativeInfinity)));
    }

    // PGM rejects a negative radius (CylindricalRegion/SphereRegion assert radius >= 0) and a negative
    // height yields a degenerate empty cylinder; floor both at 0. Other coords are world positions — no floor.
    private static double? MinFor(string key) => key is "radius" or "height" ? 0 : null;

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
