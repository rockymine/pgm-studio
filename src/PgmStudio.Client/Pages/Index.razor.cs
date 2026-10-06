using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Pages;

public partial class Index
{
    private const int RecentMaps = 5;

    private MapStageCounts? counts;

    /// <summary>The maps offered on the page, newest first, each with the tool it opens in and the moment shown.</summary>
    private List<(MapSummary Map, string Opens, DateTime At)> recent = [];

    /// <summary>Whether <see cref="recent"/> holds maps the caller changed themselves rather than the latest overall.</summary>
    private bool mine;

    protected override async Task OnInitializedAsync()
    {
        try { counts = await Http.GetFromJsonAsync<MapStageCounts>("api/maps/stage-counts"); }
        catch { /* the count is decorative — the page still navigates without it */ }
        try
        {
            var maps = await Http.GetFromJsonAsync<List<MapSummary>>("api/maps") ?? [];
            mine = maps.Any(map => map.YouWroteAt is not null);
            recent = maps
                .Where(map => !mine || map.YouWroteAt is not null)
                .Select(map => (Map: map, Opens: MapLayers.Opens(map), At: map.YouWroteAt ?? map.UpdatedAt))
                .Where(entry => entry.Opens is not null)
                .OrderByDescending(entry => entry.At)
                .Take(RecentMaps)
                .Select(entry => (entry.Map, entry.Opens!, entry.At))
                .ToList();
        }
        catch { /* the list is an offer — without it the page still opens every tool */ }
    }

    private static string LayerLabel(string layer) =>
        MapLayers.All.FirstOrDefault(entry => entry.Id == layer).Label ?? layer;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
