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

    private List<MapSummary>? maps;
    private string filter = "";
    private bool loaded;
    private string? loadedStage;   // guards against refetching the same collection on every parameter set
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

    /// <summary>The tool a row opens: the one at the map's stage, or — for a finished map, which no tool opens
    /// at its stage — the last layer it holds, and nothing when it holds none.</summary>
    private static string? Opens(MapSummary map) =>
        map.Stage == MapStage.Edit ? Layers(map).Select(layer => layer.Id).LastOrDefault() : map.Stage;

    private static string LayerTitle(MapSummary map, string layer) => layer switch
    {
        MapStage.Plan => "Open the plan this map was built from. Opening it changes nothing.",
        MapStage.Sketch => "Open the sketch this map was drawn in. Opening it changes nothing.",
        _ => "Open this map's world in Configure.",
    };

    /// <summary>The collection on show: a stage word, or null for every map in the studio.</summary>
    private string? CurrentStage => MapStage.IsValid(Stage) ? Stage : null;

    private string StageTitle => CurrentStage switch
    {
        MapStage.Plan => "Plans",
        MapStage.Sketch => "Sketches",
        MapStage.Configure => "Configuring",
        MapStage.Edit => "Finished",
        _ => "Maps",
    };

    /// <summary>A row's stage, for the list of every map, where it is the one thing telling the rows apart.</summary>
    private static string StageLabel(string stage) => stage switch
    {
        MapStage.Plan => "Plan",
        MapStage.Sketch => "Sketch",
        MapStage.Configure => "Configuring",
        MapStage.Edit => "Finished",
        _ => stage,
    };

    // Plans and Sketches list every map holding that layer, whatever it has since become; Configuring and
    // Finished list the maps standing at that stage, and Maps lists them all. The blurbs say which, because
    // "every map with a plan" and "every map at the plan stage" are different collections.
    private string StageBlurb => CurrentStage switch
    {
        MapStage.Plan => "Maps that have a plan, including finished ones.",
        MapStage.Sketch => "Maps that have a sketch, including finished ones.",
        MapStage.Configure => "Sketched or imported worlds that do not have a finished map.xml yet.",
        MapStage.Edit => "Maps with a finished map.xml.",
        _ => "Every map in the studio, at whatever stage it has reached.",
    };

    private string EmptyMessage => CurrentStage switch
    {
        MapStage.Plan => "No plans yet. Create one, or open a layout from the generator.",
        MapStage.Sketch => "No sketches yet. Create one to get started.",
        MapStage.Configure => "Nothing to configure yet. Import a world, or build a sketch.",
        MapStage.Edit => "No finished maps yet.",
        _ => "No maps yet. Plan one, draw a sketch, or import a world.",
    };

    private IEnumerable<MapSummary> Filtered =>
        maps is null ? [] :
        string.IsNullOrWhiteSpace(filter)
            ? maps
            : maps.Where(m => (m.Slug + " " + m.Name).Contains(filter, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the caller may originate a map; the start buttons are greyed out until it is known
    /// that they may.</summary>
    private bool mayWrite;

    private string? WriteTitle => mayWrite ? null : "Sign in with a whitelisted account to start a map";

    protected override async Task OnParametersSetAsync()
    {
        mayWrite = await Access.MayWriteAsync();
        if (loaded && loadedStage == CurrentStage) return;   // collection unchanged → keep the loaded list
        loaded = true;
        loadedStage = CurrentStage;
        await LoadAsync();
    }

    private string? loadError;

    private async Task LoadAsync()
    {
        maps = null;
        loadError = null;
        var route = CurrentStage is null ? "api/maps" : $"api/maps?stage={CurrentStage}";
        try { maps = await Http.GetFromJsonAsync<List<MapSummary>>(route); }
        catch (HttpRequestException) { loadError = "Couldn't load the maps. The studio may be restarting; try again in a moment."; }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
