using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Configure;

public partial class ConfigureWizard
{
    [Parameter] public string Slug { get; set; } = "";

    private int phaseIndex;
    private int subStep;
    private int furthest;            // highest phase reached — gates the rail (later = locked)
    private string? mapName;

    private ConfigurePhase Phase => ConfigurePhases.All[phaseIndex];
    private int LastStep => Math.Max(0, Phase.SubSteps.Length - 1);
    private bool AtStart => phaseIndex == 0 && subStep == 0;
    private bool AtEnd => phaseIndex == ConfigurePhases.All.Length - 1 && subStep == LastStep;

    private bool BackEnabled => !AtStart;
    private string NextLabel => AtEnd ? "Export" : "Next";
    private string SubLabel => Phase.SubSteps.Length == 0 ? "Map Info" : Phase.SubSteps[subStep];

    protected override async Task OnParametersSetAsync()
    {
        // Best-effort: the shell renders regardless of whether the map has a DB row yet
        // (a freshly-imported/sketched world may not). The name is just for the breadcrumb.
        mapName = null;
        try
        {
            var resp = await Http.GetAsync($"api/map/{Slug}");
            if (resp.IsSuccessStatusCode)
            {
                var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
                mapName = doc.TryGetProperty("name", out var n) ? n.GetString() : null;
            }
        }
        catch { /* shell is independent of map data */ }
    }

    private void JumpPhase(string id)
    {
        var i = ConfigurePhases.IndexOf(id);
        if (i < 0 || i > furthest) return;   // locked
        phaseIndex = i;
        subStep = 0;
    }

    private void JumpStep(int j)
    {
        if (j >= 0 && j <= LastStep) subStep = j;
    }

    private void Back()
    {
        if (subStep > 0) subStep--;
        else if (phaseIndex > 0) { phaseIndex--; subStep = LastStep; }
    }

    private void Next()
    {
        if (subStep < LastStep) { subStep++; return; }
        if (phaseIndex < ConfigurePhases.All.Length - 1)
        {
            phaseIndex++;
            subStep = 0;
            if (phaseIndex > furthest) furthest = phaseIndex;
        }
        // AtEnd: "Export" is a no-op stub here — the gated export lands with N05/N06.
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");
}
