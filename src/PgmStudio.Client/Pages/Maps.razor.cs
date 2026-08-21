using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

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
                var created = await resp.Content.ReadFromJsonAsync<OriginatedDto>();
                if (created?.Slug is { Length: > 0 } slug)
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
                var created = await resp.Content.ReadFromJsonAsync<OriginatedDto>();
                if (created?.Slug is { Length: > 0 } slug)
                {
                    Nav.NavigateTo($"maps/{slug}/sketch?phase=info");
                    return;
                }
            }
        }
        catch { /* fall through — button re-enables so the user can retry */ }
        creatingSketch = false;
    }

    // The authoring layers a map holds, in pipeline order — each a direct link into that tool. A map keeps
    // every layer it has ever had (a built plan still has its plan; a configured sketch still has its
    // sketch), so this is the whole of a map's history and all of it is one click away. There is no
    // walking a map back a stage at a time to reach the tool that drew it: opening a layer opens it, and
    // the stage pointer — which only ever says how far the map has got — is left alone.
    private static IEnumerable<(string Id, string Label)> Layers(MapSummary map)
    {
        if (map.HasPlan) yield return (MapStage.Plan, "Plan");
        if (map.HasSketch) yield return (MapStage.Sketch, "Sketch");
        if (map.HasSurface) yield return (MapStage.Configure, "Configure");
    }

    private static string LayerTitle(MapSummary map, string layer) => layer switch
    {
        MapStage.Plan => "Open the plan this map was compiled from. Nothing is rebuilt by looking.",
        MapStage.Sketch => "Open the sketch this map's geometry was drawn in. Nothing is rebuilt by looking.",
        _ => "Open the Configure wizard on this map's world.",
    };

    private string CurrentStage => MapStage.IsValid(Stage) ? Stage! : MapStage.Edit;
    private MapSummary? JustMap => Just is null ? null : maps?.FirstOrDefault(m => m.Slug == Just);

    private string StageTitle => CurrentStage switch
    {
        MapStage.Plan => "Plans",
        MapStage.Sketch => "Sketches",
        MapStage.Configure => "Configuring",
        _ => "Maps",
    };

    // Plans and Sketches list every map holding that layer, whatever it has since become; Configuring and
    // Maps list the maps standing at that stage. The blurbs say which, because "every map with a plan" and
    // "every map at the plan stage" are different collections and the difference is the point.
    private string StageBlurb => CurrentStage switch
    {
        MapStage.Plan => "Every map that holds a plan — including ones already built and configured. Open one to keep planning.",
        MapStage.Sketch => "Every map that holds a drawn sketch — including ones already configured. Open one to keep sketching.",
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
