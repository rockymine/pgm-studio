using System.Net.Http.Json;
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

    private string CurrentStage => MapStage.IsValid(Stage) ? Stage! : MapStage.Edit;
    private string RowMode => CurrentStage;   // /maps/{slug}/{sketch|configure|edit}
    private MapSummary? JustMap => Just is null ? null : maps?.FirstOrDefault(m => m.Slug == Just);

    private string StageTitle => CurrentStage switch
    {
        MapStage.Sketch => "Sketches",
        MapStage.Configure => "Configuring",
        _ => "Maps",
    };

    private string StageBlurb => CurrentStage switch
    {
        MapStage.Sketch => "Draft layouts you're drawing. Open one to keep sketching, or start a new one.",
        MapStage.Configure => "Worlds with terrain but no finished map.xml — sketched or imported. Open one to keep configuring.",
        _ => "Maps with a finished map.xml. Open one to refine its regions, teams, wools and objectives.",
    };

    private string EmptyMessage => CurrentStage switch
    {
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
