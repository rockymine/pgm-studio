using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Pages.Sketch;

public partial class SketchIdentityPhase
{
    [Parameter] public string Slug { get; set; } = "";
    /// <summary>Advance to the Draw phase (the flow-bar's Continue) — the rail's Draw button does the same.</summary>
    [Parameter] public EventCallback OnNext { get; set; }

    private string name = "";
    private readonly List<AuthorRow> authors = new();
    private bool dirty;
    private string? saveStatus;

    // Load once on mount (not OnParametersSet — the parent re-renders on canvas callbacks while this
    // phase is up, and re-loading would wipe unsaved edits). Slug is fixed for the phase's lifetime.
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}");
            name = Str(doc, "name");
            authors.Clear();
            if (doc.TryGetProperty("authors", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var a in arr.EnumerateArray())
                    if (Str(a, "role") != "contributor")
                        authors.Add(new AuthorRow { Uuid = Str(a, "uuid"), Name = Str(a, "name"), Contribution = Str(a, "contribution") });
            dirty = false; saveStatus = null;
        }
        catch { saveStatus = "Failed to load."; }
    }

    private void Dirty() { dirty = true; saveStatus = null; }

    private async Task Save()
    {
        saveStatus = "Saving…"; StateHasChanged();
        // Metadata PATCH merges scalars (name) and full-replaces authors; version/objective are left
        // untouched by omitting their keys.
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["authors"] = authors.Select(p => new Dictionary<string, object?>
            {
                ["uuid"] = p.Uuid, ["name"] = p.Name, ["role"] = "author", ["contribution"] = p.Contribution,
            }).ToList(),
        };
        try
        {
            var resp = await Http.PatchAsJsonAsync($"api/map/{Slug}/metadata", payload);
            if (resp.IsSuccessStatusCode) { dirty = false; saveStatus = "Saved."; }
            else saveStatus = $"Save failed ({(int)resp.StatusCode}).";
        }
        catch { saveStatus = "Save failed."; }
        StateHasChanged();
    }

    private static string Str(JsonElement e, string key)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
