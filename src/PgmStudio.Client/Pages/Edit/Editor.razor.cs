using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.Edit;

public partial class Editor
{
    [Parameter] public string Slug { get; set; } = "";
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private record Phase(string Id, string Icon, string Title);

    // Identity leads (matches the Configure wizard's info/world ordering), then the editing activities.
    private static readonly Phase[] Phases =
    [
        new("overview",      "book-open-text",    "Overview"),
        new("setup",         "settings-2",        "Setup"),
        new("teams",         "users",             "Teams"),
        new("build-regions", "pickaxe",           "Build Regions"),
        new("objective",     "goal",              "Objective"),
        new("regions",       "layout-dashboard",  "Regions"),
    ];

    private string active = "overview";
    private bool loaded;
    private string? mapName;
    private string? mapVersion;
    private string? error;
    private readonly Dictionary<string, string?> status = new();

    private string StatusOf(string id) => status.GetValueOrDefault(id) ?? "";
    private string TitleOf(string id) => Phases.FirstOrDefault(a => a.Id == id)?.Title ?? id;
    private void SetStatus(string id, string? dot) { status[id] = dot; StateHasChanged(); }
    private void OnOverviewStatus(string? dot) => SetStatus("overview", dot);

    // Each phase's own FlowBar offers Back/Next as an alternative to the rail, walking phases in
    // rail order (mirroring the Configure wizard's Back/Next, which does the same across phases). Past
    // the last phase (Regions), Next leaves the editor for the maps list — the editor's equivalent of
    // the wizard's end-of-flow Export.
    private int ActiveIndex => Array.FindIndex(Phases, a => a.Id == active);
    private bool IsFirstPhase => ActiveIndex <= 0;
    private bool IsLastPhase => ActiveIndex == Phases.Length - 1;

    private void GoAdjacent(int delta)
    {
        var i = ActiveIndex + delta;
        if (i < 0) return;
        if (i >= Phases.Length) { Nav.NavigateTo("maps"); return; }
        Switch(Phases[i].Id);
    }

    protected override async Task OnParametersSetAsync()
    {
        loaded = false; error = null; mapName = null; mapVersion = null;
        try
        {
            var resp = await Http.GetAsync($"api/map/{Slug}");
            if (!resp.IsSuccessStatusCode) { error = resp.StatusCode == System.Net.HttpStatusCode.NotFound ? "Map not found" : "Could not load map"; return; }
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
            mapName = doc.TryGetProperty("name", out var n) ? n.GetString() : null;
            mapVersion = doc.TryGetProperty("version", out var v) ? v.GetString() : null;
            loaded = true;
        }
        catch { error = "Could not load map"; }
    }

    private void Switch(string id)
    {
        if (!loaded) return;
        active = id;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
    }
}
