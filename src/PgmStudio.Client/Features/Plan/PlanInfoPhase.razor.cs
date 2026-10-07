using PgmStudio.Contracts;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Plan;

// Plan Info phase: two steps. Identity — the plan's name; on a map-backed plan that is the map's display
// name (saved to the map metadata endpoint) beside username-verified authors via the shared AuthorsEditor.
// Settings — the plan globals (symmetry + cell/surface/max-build-height/max-players), which live on the plan doc:
// the host owns the canvas bridge, so this phase renders them as parameters and raises change callbacks the
// host forwards to the bridge (the same split the Sketch tool uses for its symmetry settings).
public partial class PlanInfoPhase
{
    /// <summary>The map a map-backed plan belongs to; null on a plan row, which has no metadata of its own.</summary>
    [Parameter] public string? Slug { get; set; }

    private bool MapBacked => Slug is { Length: > 0 };
    /// <summary>The steps of the phase, in order; the host's editor bar lists them.</summary>
    public static readonly IReadOnlyList<string> Steps = ["Identity", "Settings"];
    /// <summary>The step shown, an index into <see cref="Steps"/>.</summary>
    [Parameter] public int Step { get; set; }

    // Globals owned by the host (it holds the plan-doc bridge); this phase renders them and raises the
    // change callbacks so the live plan document + canvas update.
    [Parameter] public string Name { get; set; } = "Untitled plan";
    [Parameter] public string Symmetry { get; set; } = "rot_180";
    [Parameter] public double Cell { get; set; } = 4;
    [Parameter] public double Surface { get; set; } = 9;
    [Parameter] public double SurfaceStep { get; set; } = 2;
    [Parameter] public double MaxPlayers { get; set; } = 12;
    [Parameter] public EventCallback<string> OnNameChanged { get; set; }
    [Parameter] public EventCallback<string> OnSymmetryChanged { get; set; }
    [Parameter] public EventCallback<double> OnCellChanged { get; set; }
    [Parameter] public EventCallback<double> OnSurfaceChanged { get; set; }
    [Parameter] public EventCallback<double> OnSurfaceStepChanged { get; set; }
    [Parameter] public EventCallback<double> OnMaxPlayersChanged { get; set; }

    /// <summary>The symmetries a plan can state, offered in this order wherever a plan's symmetry is picked.</summary>
    internal static readonly IReadOnlyList<SelectOption> SymmetryOptions =
    [
        new("rot_180", "Rotate 180°"),
        new("rot_90", "Rotate 90°"),
        new("mirror_x", "Mirror X"),
        new("mirror_z", "Mirror Z"),
        new("none", "None"),
    ];

    private readonly List<AuthorRow> authors = new();
    private bool dirty;
    private string? saveStatus;

    // Load name + authors once on mount from the map metadata (not OnParametersSet — the host re-renders on
    // canvas callbacks while this phase is up, and re-loading would wipe unsaved edits). The host keys this
    // component by its map, so Slug is fixed for an instance.
    protected override async Task OnInitializedAsync()
    {
        if (!MapBacked) return;
        try
        {
            var doc = await Http.GetFromJsonAsync<MapDocumentDto>($"api/map/{Slug}");
            var loaded = doc?.Name ?? "";
            authors.Clear();
            foreach (var a in (doc?.Authors ?? []).Where(a => a.Role != "contributor"))
                authors.Add(new AuthorRow { Uuid = a.Uuid, Name = a.Name ?? "", Contribution = a.Contribution ?? "" });
            // The metadata name is authoritative for the row; push it into the plan doc so the two agree.
            if (loaded.Length > 0 && loaded != Name) await OnNameChanged.InvokeAsync(loaded);
            dirty = false; saveStatus = null;
        }
        catch { saveStatus = "Couldn't load the plan details. Reload the page to try again."; }
    }

    private async Task OnNameInput(ChangeEventArgs e)
    {
        var v = e.Value?.ToString() ?? "";
        await OnNameChanged.InvokeAsync(v);   // live-sync the plan doc (compile reads doc.meta.name)
        if (MapBacked) Dirty();
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
            else saveStatus = $"Couldn't save (HTTP {(int)resp.StatusCode}). Try again.";
        }
        catch { saveStatus = "Couldn't save. Try again."; }
        StateHasChanged();
    }
}
