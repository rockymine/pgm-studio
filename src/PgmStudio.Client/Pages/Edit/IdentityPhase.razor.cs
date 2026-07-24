using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Pages.Edit;

// Identity phase state + behaviour (the markup lives in IdentityPhase.razor). The author rows + their
// username resolution are delegated to the shared AuthorsEditor; this phase owns load/save against the
// map metadata endpoint.
public partial class IdentityPhase
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public EventCallback<string?> OnStatus { get; set; }
    [Parameter] public bool IsFirstPhase { get; set; }
    [Parameter] public bool IsLastPhase { get; set; }
    [Parameter] public EventCallback OnPrevPhase { get; set; }
    [Parameter] public EventCallback OnNextPhase { get; set; }

    private string name = "", version = "", objective = "";

    /// <summary>The map's gamemodes, derived server-side from its objective modules. Read-only here:
    /// the author changes them by adding or removing objectives, not by typing.</summary>
    private string[] gamemodes = [];

    private string GamemodeLabel => string.Join(" · ", gamemodes.Select(g => g.ToUpperInvariant()));
    private readonly List<AuthorRow> authors = new();
    private readonly List<AuthorRow> contributors = new();
    private bool dirty;
    private string? saveStatus;

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}");
            name = Str(doc, "name"); version = Str(doc, "version");
            objective = Str(doc, "objective");
            gamemodes = doc.TryGetProperty("gamemodes", out var gm) && gm.ValueKind == JsonValueKind.Array
                ? gm.EnumerateArray().Select(g => g.GetString() ?? "").Where(g => g.Length > 0).ToArray()
                : [];
            authors.Clear(); contributors.Clear();
            if (doc.TryGetProperty("authors", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var a in arr.EnumerateArray())
                {
                    var p = new AuthorRow { Uuid = Str(a, "uuid"), Name = Str(a, "name"), Contribution = Str(a, "contribution") };
                    (Str(a, "role") == "contributor" ? contributors : authors).Add(p);
                }
            dirty = false; saveStatus = null;
            await ReportStatus();
        }
        catch { saveStatus = "Failed to load."; }
    }

    private static string Str(JsonElement e, string key)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private void Dirty() { dirty = true; saveStatus = null; }

    private async Task Save()
    {
        saveStatus = "Saving…"; StateHasChanged();
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["version"] = version,
            ["objective"] = objective,
            ["authors"] = authors.Select(p => Author(p, "author"))
                .Concat(contributors.Select(p => Author(p, "contributor"))).ToList(),
        };
        var resp = await Http.PatchAsJsonAsync($"api/map/{Slug}/metadata", payload);
        if (resp.IsSuccessStatusCode) { dirty = false; saveStatus = "Saved."; await ReportStatus(); }
        else saveStatus = $"Save failed ({(int)resp.StatusCode}).";
        StateHasChanged();
    }

    private static Dictionary<string, object?> Author(AuthorRow p, string role) => new()
    {
        ["uuid"] = p.Uuid, ["name"] = p.Name, ["role"] = role, ["contribution"] = p.Contribution,
    };

    // Required identity fields drive the rail status dot (yellow = incomplete). The gamemode is derived,
    // so it is never something the author can complete here.
    private Task ReportStatus()
    {
        var complete = !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(version);
        return OnStatus.InvokeAsync(complete ? null : "yellow");
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");
}
