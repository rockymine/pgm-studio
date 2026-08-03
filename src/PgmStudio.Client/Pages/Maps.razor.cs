using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Pages;

public partial class Maps
{
    [SupplyParameterFromQuery] public string? Stage { get; set; }
    [SupplyParameterFromQuery] public string? Just { get; set; }   // slug just finished from Sketch → Configure

    private List<MapSummary>? maps;
    private string filter = "";
    private string? loadedStage;   // guards against refetching the same stage on every parameter set
    private bool creatingSketch;
    private bool creatingPlan;
    private string? reopening;   // slug being reopened — one at a time, since it navigates away

    // New plan: create a blank authored plan (a stage=plan map row) and open the plan editor on it. Mirrors
    // NewSketch — the plan editor is the plan's home once it's a map row.
    private async Task NewPlan()
    {
        if (creatingPlan) return;
        creatingPlan = true;
        try
        {
            var resp = await Http.PostAsJsonAsync("api/plan", new { name = "Untitled plan" });
            if (resp.IsSuccessStatusCode)
            {
                var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
                if (created.TryGetProperty("slug", out var s) && s.GetString() is { Length: > 0 } slug)
                {
                    Nav.NavigateTo($"maps/{slug}/plan?phase=info");
                    return;
                }
            }
        }
        catch { /* fall through — button re-enables so the user can retry */ }
        creatingPlan = false;
    }

    // New sketch: create an untitled draft (a map row) and open it on the Info phase to name it — the
    // Sketch tool has no separate creation page; the canvas auto-grows so there's no size to pick first.
    private async Task NewSketch()
    {
        if (creatingSketch) return;
        creatingSketch = true;
        try
        {
            var resp = await Http.PostAsJsonAsync("api/sketch", new { name = "Untitled sketch" });
            if (resp.IsSuccessStatusCode)
            {
                var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
                if (created.TryGetProperty("slug", out var s) && s.GetString() is { Length: > 0 } slug)
                {
                    Nav.NavigateTo($"maps/{slug}/sketch?phase=info");
                    return;
                }
            }
        }
        catch { /* fall through — button re-enables so the user can retry */ }
        creatingSketch = false;
    }

    // Reopen: send a map back to a tool it was drawn in (POST /api/map/{slug}/reopen), then open it
    // there. MapSummary.ReopenStage names the stage the server will move it to and is null when there is
    // none — an imported world kept no drawn source, and a map already sitting in its only authored
    // stage has nowhere to go. A plan built onto its own map keeps both sources, so it alternates: the
    // sketch from Configuring, the plan from the sketch.
    private static bool CanReopen(MapSummary map) => map.ReopenStage is not null;

    private static string ReopenLabel(MapSummary map) =>
        map.ReopenStage == MapStage.Plan ? "Reopen in Plan" : "Reopen in Sketch";

    private static string ReopenTitle(MapSummary map) =>
        map.ReopenStage == MapStage.Plan
            ? "Move this map back to the Plan editor. Its geometry and configuration stay as they are."
            : "Move this map back to the Sketch tool. Its geometry and configuration stay as they are; "
              + "finishing the sketch again brings it back here.";

    private async Task Reopen(MapSummary map)
    {
        if (reopening is not null || map.ReopenStage is null) return;
        reopening = map.Slug;
        try
        {
            var resp = await Http.PostAsync($"api/map/{map.Slug}/reopen", null);
            if (resp.IsSuccessStatusCode)
            {
                Nav.NavigateTo($"maps/{map.Slug}/{map.ReopenStage}");
                return;
            }
        }
        catch { /* fall through — the button re-enables so the user can retry */ }
        reopening = null;
    }

    private string CurrentStage => MapStage.IsValid(Stage) ? Stage! : MapStage.Edit;
    private string RowMode => CurrentStage;   // /maps/{slug}/{sketch|configure|edit}
    private MapSummary? JustMap => Just is null ? null : maps?.FirstOrDefault(m => m.Slug == Just);

    private string StageTitle => CurrentStage switch
    {
        MapStage.Plan => "Plans",
        MapStage.Sketch => "Sketches",
        MapStage.Configure => "Configuring",
        _ => "Maps",
    };

    private string StageBlurb => CurrentStage switch
    {
        MapStage.Plan => "Coarse layout plans you're authoring. Open one to keep planning, or start a new one.",
        MapStage.Sketch => "Draft layouts you're drawing. Open one to keep sketching, or start a new one.",
        MapStage.Configure => "Worlds with terrain but no finished map.xml — sketched or imported. Open one to keep configuring.",
        _ => "Maps with a finished map.xml. Open one to refine its regions, teams, wools and objectives.",
    };

    private string EmptyMessage => CurrentStage switch
    {
        MapStage.Plan => "No plans yet — start one above, or author one from the generator.",
        MapStage.Sketch => "No sketches yet — start one above.",
        MapStage.Configure => "Nothing to configure — import a world, or finish a sketch.",
        _ => "No maps yet.",
    };

    private IEnumerable<MapSummary> Filtered =>
        maps is null ? [] :
        string.IsNullOrWhiteSpace(filter)
            ? maps
            : maps.Where(m => (m.Slug + " " + m.Name).Contains(filter, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnParametersSetAsync()
    {
        if (loadedStage == CurrentStage) return;   // stage unchanged → keep the loaded list
        loadedStage = CurrentStage;
        maps = null;
        maps = await Http.GetFromJsonAsync<List<MapSummary>>($"api/maps?stage={CurrentStage}");
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
