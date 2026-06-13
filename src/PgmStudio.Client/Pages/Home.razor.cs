using System.Net.Http.Json;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Pages;

public partial class Home
{
    private List<MapSummary>? maps;
    private string filter = "";

    private IEnumerable<MapSummary> Filtered =>
        maps is null ? [] :
        string.IsNullOrWhiteSpace(filter)
            ? maps
            : maps.Where(m => (m.Slug + " " + m.Name).Contains(filter, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
        => maps = await Http.GetFromJsonAsync<List<MapSummary>>("api/maps");

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
