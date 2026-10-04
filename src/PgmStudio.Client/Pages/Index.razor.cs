using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Pages;

public partial class Index
{
    private MapStageCounts? counts;

    /// <summary>The map the caller last changed themselves, and the tool it opens in, or null where they have
    /// changed none.</summary>
    private (MapSummary Map, string Opens)? last;

    protected override async Task OnInitializedAsync()
    {
        try { counts = await Http.GetFromJsonAsync<MapStageCounts>("api/maps/stage-counts"); }
        catch { /* counts are decorative — the cards still navigate without them */ }
        try
        {
            var maps = await Http.GetFromJsonAsync<List<MapSummary>>("api/maps") ?? [];
            if (maps.Where(map => map.YouWroteAt is not null).MaxBy(map => map.YouWroteAt) is { } map
                && MapLayers.Opens(map) is { } opens)
                last = (map, opens);
        }
        catch { /* the continue card is an offer — without it the page still opens every tool */ }
    }

    private static string LayerLabel(string layer) =>
        MapLayers.All.FirstOrDefault(entry => entry.Id == layer).Label ?? layer;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    // "4 drafts" / "1 draft" / "—" while loading. Plural == singular for already-plural phrasing.
    private static string CountLabel(int? n, string singular, string plural) =>
        n is null ? "—" : $"{n} {(n == 1 ? singular : plural)}";
}
