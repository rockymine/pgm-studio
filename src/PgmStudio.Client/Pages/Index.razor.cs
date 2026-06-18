using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Pages;

public partial class Index
{
    private MapStageCounts? counts;

    protected override async Task OnInitializedAsync()
    {
        try { counts = await Http.GetFromJsonAsync<MapStageCounts>("api/maps/stage-counts"); }
        catch { /* counts are decorative — the cards still navigate without them */ }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    // "4 drafts" / "1 draft" / "—" while loading. Plural == singular for already-plural phrasing.
    private static string CountLabel(int? n, string singular, string plural) =>
        n is null ? "—" : $"{n} {(n == 1 ? singular : plural)}";
}
