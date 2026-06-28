using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Concepts;

/// <summary>
/// The Organic-generation demonstration page (<c>/concepts/organic</c>): POSTs a seed to
/// <c>/api/sketch/generate/stages</c>, which runs the real <c>OrganicLane.GrowStages</c>, and hands the
/// per-stage intermediates to <c>studio.renderGenStages</c> (render/gen-stages.js) to paint each stage panel.
/// </summary>
public partial class OrganicGeneration
{
    private int seed = 1;
    private int wools = 2;
    private bool loading;
    private string? error;

    protected override void OnInitialized() => seed = Random.Shared.Next(1, 100000);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (firstRender) await Reload();   // the #gen-stage-* svgs exist after the first render
    }

    private async Task Reload()
    {
        loading = true;
        error = null;
        StateHasChanged();
        try
        {
            var resp = await Http.PostAsJsonAsync("api/sketch/generate/stages", new { seed, wools });
            if (resp.IsSuccessStatusCode)
            {
                var stages = await resp.Content.ReadFromJsonAsync<JsonElement>();
                await JS.InvokeVoidAsync("studio.renderGenStages", stages);
            }
            else { error = "Generation failed."; }
        }
        catch { error = "Generation failed."; }
        loading = false;
        StateHasChanged();
    }

    private async Task Reroll()
    {
        seed = Random.Shared.Next(1, 100000);
        await Reload();
    }

    private async Task OnSeedChange(double v)
    {
        if ((int)v != seed) { seed = (int)v; await Reload(); }
    }

    private async Task OnWoolsChange(double v)
    {
        var w = (int)v;     // already clamped to [1, 6] by NumberField
        if (w != wools) { wools = w; await Reload(); }
    }

    private async Task Scroll(string id) => await JS.InvokeVoidAsync("studio.scrollToId", id);
}
