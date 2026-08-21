using PgmStudio.Contracts;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Sketch;

public partial class SketchInfoPhase
{
    [Parameter] public string Slug { get; set; } = "";
    /// <summary>Advance to the Draw phase (Continue on the last step) — the rail's Draw button does the same.</summary>
    [Parameter] public EventCallback OnNext { get; set; }

    // Settings step — symmetry, owned by the host (it holds the canvas bridge); this phase only renders
    // the controls and raises the change callbacks so the live (hidden) canvas updates.
    [Parameter] public string Mode { get; set; } = "rot_180";
    [Parameter] public double CenterX { get; set; }
    [Parameter] public double CenterZ { get; set; }
    [Parameter] public EventCallback<ChangeEventArgs> OnModeChange { get; set; }
    [Parameter] public EventCallback<double> OnCenterX { get; set; }
    [Parameter] public EventCallback<double> OnCenterZ { get; set; }

    private int step;   // 0 = Identity, 1 = Settings
    private Task OnNextStep() { if (step < Steps.Length - 1) { step++; return Task.CompletedTask; } return OnNext.InvokeAsync(); }

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
            var doc = await Http.GetFromJsonAsync<MapDocumentDto>($"api/map/{Slug}");
            name = doc?.Name ?? "";
            authors.Clear();
            foreach (var a in (doc?.Authors ?? []).Where(a => a.Role != "contributor"))
                authors.Add(new AuthorRow { Uuid = a.Uuid, Name = a.Name ?? "", Contribution = a.Contribution ?? "" });
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
}
