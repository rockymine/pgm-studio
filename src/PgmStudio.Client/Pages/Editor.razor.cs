using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages;

public partial class Editor
{
    [Parameter] public string Slug { get; set; } = "";

    private record Activity(string Id, string Icon, string Title);

    // Rail order + icons mirror studio/templates/editor.html.
    private static readonly Activity[] Activities =
    [
        new("configure",     "settings-2",        "Configure"),
        new("overview",      "book-open-text",    "Overview"),
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
    private string TitleOf(string id) => Activities.FirstOrDefault(a => a.Id == id)?.Title ?? id;
    private void SetStatus(string id, string? dot) { status[id] = dot; StateHasChanged(); }
    private void OnOverviewStatus(string? dot) => SetStatus("overview", dot);
    private void GoOverview() => Switch("overview");

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
