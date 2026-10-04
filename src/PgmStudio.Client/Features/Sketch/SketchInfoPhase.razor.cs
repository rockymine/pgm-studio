using PgmStudio.Contracts;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Sketch;

public partial class SketchInfoPhase : IAsyncDisposable
{
    [Parameter] public string Slug { get; set; } = "";
    /// <summary>Advance to the Draw phase (Continue on the last step) — the rail's Draw button does the same.</summary>
    [Parameter] public EventCallback OnNext { get; set; }
    /// <summary>The name the map was saved under, once a save lands.</summary>
    [Parameter] public EventCallback<string> OnRenamed { get; set; }
    /// <summary>Where the identity's save stands — Saved, Saving…, Unsaved, or why it could not — for the
    /// tool bar to show; null once nothing is to say.</summary>
    [Parameter] public EventCallback<string?> OnSaveStatus { get; set; }

    // Settings step — symmetry, owned by the host (it holds the canvas bridge); this phase only renders
    // the controls and raises the change callbacks so the live (hidden) canvas updates.
    [Parameter] public string Mode { get; set; } = "rot_180";
    [Parameter] public double CenterX { get; set; }
    [Parameter] public double CenterZ { get; set; }
    [Parameter] public EventCallback<string> OnModeChange { get; set; }
    [Parameter] public EventCallback<double> OnCenterX { get; set; }
    [Parameter] public EventCallback<double> OnCenterZ { get; set; }

    private static readonly IReadOnlyList<SelectOption> ModeOptions =
    [
        new("mirror_x", "Mirror X"),
        new("mirror_z", "Mirror Z"),
        new("rot_180", "Rotate 180°"),
        new("rot_90", "Rotate 90°"),
    ];

    private int step;   // 0 = Identity, 1 = Settings
    private Task OnNextStep() { if (step < Steps.Length - 1) { step++; return Task.CompletedTask; } return OnNext.InvokeAsync(); }

    /// <summary>How long typing has to pause before the identity is saved.</summary>
    private static readonly TimeSpan SaveAfter = TimeSpan.FromMilliseconds(800);

    private string name = "";
    private readonly List<AuthorRow> authors = new();
    private bool dirty;
    private string? loadError;
    private CancellationTokenSource? pending;

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
            dirty = false;
        }
        catch { loadError = "Couldn't load the map details. Reload the page to try again."; }
    }

    /// <summary>An edit: the identity is saved once typing pauses.</summary>
    private void Dirty()
    {
        dirty = true;
        _ = OnSaveStatus.InvokeAsync("Unsaved");
        pending?.Cancel();
        var wait = pending = new CancellationTokenSource();
        _ = SaveSoonAsync(wait.Token);
    }

    private async Task SaveSoonAsync(CancellationToken cancelled)
    {
        try { await Task.Delay(SaveAfter, cancelled); }
        catch (TaskCanceledException) { return; }
        await Save();
    }

    /// <summary>Leaving the phase saves what is still pending.</summary>
    public async ValueTask DisposeAsync()
    {
        pending?.Cancel();
        if (dirty) await Save();
    }

    private async Task Save()
    {
        if (!dirty) return;
        if (string.IsNullOrWhiteSpace(name))
        {
            await OnSaveStatus.InvokeAsync("Not saved: a map needs a name");
            return;
        }
        dirty = false;
        await OnSaveStatus.InvokeAsync("Saving…");
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
            if (resp.IsSuccessStatusCode)
            {
                await OnSaveStatus.InvokeAsync("Saved");
                await OnRenamed.InvokeAsync(name);
                return;
            }
            dirty = true;
            await OnSaveStatus.InvokeAsync($"Not saved (HTTP {(int)resp.StatusCode})");
        }
        catch
        {
            dirty = true;
            await OnSaveStatus.InvokeAsync("Not saved: the studio did not answer");
        }
    }
}
