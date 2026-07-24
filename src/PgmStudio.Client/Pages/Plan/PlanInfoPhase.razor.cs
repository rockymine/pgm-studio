using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Pages.Plan;

// Plan Info phase (map-backed plans only): two steps. Identity — the map's display name (saved to the map
// metadata endpoint, since a plan is a map row) + username-verified authors via the shared AuthorsEditor.
// Settings — the plan globals (symmetry + cell/surface/headroom/max-players), which live on the plan doc:
// the host owns the canvas bridge, so this phase renders them as parameters and raises change callbacks the
// host forwards to the bridge (the same split the Sketch tool uses for its symmetry settings).
public partial class PlanInfoPhase
{
    [Parameter] public string Slug { get; set; } = "";
    /// <summary>Advance to the Draw phase (Continue on the last step) — the rail's Draw button does the same.</summary>
    [Parameter] public EventCallback OnNext { get; set; }

    // Globals owned by the host (it holds the plan-doc bridge); this phase renders them and raises the
    // change callbacks so the live plan document + canvas update.
    [Parameter] public string Name { get; set; } = "Untitled plan";
    [Parameter] public string Symmetry { get; set; } = "rot_180";
    [Parameter] public double Cell { get; set; } = 5;
    [Parameter] public double Surface { get; set; } = 9;
    [Parameter] public double SurfaceStep { get; set; } = 2;
    [Parameter] public double Headroom { get; set; } = 11;
    [Parameter] public double MaxPlayers { get; set; } = 12;
    [Parameter] public EventCallback<string> OnNameChanged { get; set; }
    [Parameter] public EventCallback<string> OnSymmetryChanged { get; set; }
    [Parameter] public EventCallback<double> OnCellChanged { get; set; }
    [Parameter] public EventCallback<double> OnSurfaceChanged { get; set; }
    [Parameter] public EventCallback<double> OnSurfaceStepChanged { get; set; }
    [Parameter] public EventCallback<double> OnHeadroomChanged { get; set; }
    [Parameter] public EventCallback<double> OnMaxPlayersChanged { get; set; }

    private int step;   // 0 = Identity, 1 = Settings
    private Task OnNextStep() { if (step < Steps.Length - 1) { step++; return Task.CompletedTask; } return OnNext.InvokeAsync(); }

    private readonly List<AuthorRow> authors = new();
    private bool dirty;
    private string? saveStatus;

    // Load name + authors once on mount from the map metadata (not OnParametersSet — the host re-renders on
    // canvas callbacks while this phase is up, and re-loading would wipe unsaved edits). Slug is fixed here.
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}");
            var loaded = Str(doc, "name");
            authors.Clear();
            if (doc.TryGetProperty("authors", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var a in arr.EnumerateArray())
                    if (Str(a, "role") != "contributor")
                        authors.Add(new AuthorRow { Uuid = Str(a, "uuid"), Name = Str(a, "name"), Contribution = Str(a, "contribution") });
            // The metadata name is authoritative for the row; push it into the plan doc so the two agree.
            if (loaded.Length > 0 && loaded != Name) await OnNameChanged.InvokeAsync(loaded);
            dirty = false; saveStatus = null;
        }
        catch { saveStatus = "Failed to load."; }
    }

    private async Task OnNameInput(ChangeEventArgs e)
    {
        var v = e.Value?.ToString() ?? "";
        await OnNameChanged.InvokeAsync(v);   // live-sync the plan doc (compile reads doc.meta.name)
        Dirty();
    }

    private void Dirty() { dirty = true; saveStatus = null; }

    private async Task Save()
    {
        saveStatus = "Saving…"; StateHasChanged();
        // Metadata PATCH merges scalars (name) and full-replaces authors; other fields are left untouched.
        var payload = new Dictionary<string, object?>
        {
            ["name"] = Name,
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

    private static readonly string[] Steps = { "Identity", "Settings" };
}
